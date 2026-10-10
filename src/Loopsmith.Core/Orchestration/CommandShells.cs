using System.Collections.Immutable;
using Loopsmith.Core.BuildExplanation;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.LoopGraphing;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.Simulation;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Core.Orchestration;

/// <summary>
/// One shell per command: read everything first (impure), compute on a railway (pure), and return
/// the effects for the host to write. Read top to bottom to see every pure ↔ impure boundary.
/// </summary>
public static class CommandShells
{
    public static ImmutableArray<Effect> RunExplain(ExplainRequest request)
    {
        var build = BuildLoading.LoadValidatedBuild(request.Load);                                   // impure

        var effects = build.Match(                                                                   // pure
            ok => PlanExplain(ok.Value, request.Style),
            error => [new Effect.ShowFailure(error.Failure)]);
        return effects;
    }

    public static ImmutableArray<Effect> RunValidate(LoadRequest request)
    {
        var build = BuildLoading.LoadValidatedBuild(request);                                       // impure

        var effects = build.Match(                                                                   // pure
            ok => ImmutableArray.Create<Effect>(
                new Effect.WriteLines(BuildExplaining.RenderBuildSummary(ok.Value)),
                new Effect.WriteLines([StyledText.ToLine(0, $"✓ valid against catalog {ok.Value.Catalog.Version}".ToSpan(Tone.Strong))])),
            error => [new Effect.ShowFailure(error.Failure)]);
        return effects;
    }

    public static ImmutableArray<Effect> RunTrace(TraceRequest request)
    {
        var build = BuildLoading.LoadValidatedBuild(request.Load);                                   // impure
        var scenario = FileSourceFetching.ReadOptionalTextFile(request.ScenarioPath);               // impure

        var effects = build                                                                          // pure
            .Bind(validated => scenario
                .Bind(file => ParseActions(request.ActionTokens, file.Map(f => f.Text)))
                .Map(actions => PlanTrace(validated, actions, request.Options)))
            .Match(ok => ok.Value, error => [new Effect.ShowFailure(error.Failure)]);
        return effects;
    }

    public static ImmutableArray<Effect> RunGraph(GraphRequest request)
    {
        var build = BuildLoading.LoadValidatedBuild(request.Load);                                   // impure

        var effects = build.Match(                                                                   // pure
            ok => PlanGraph(ok.Value, request),
            error => [new Effect.ShowFailure(error.Failure)]);
        return effects;
    }

    public static ImmutableArray<Effect> RunLoop(LoopRequest request)
    {
        var ruleFiles = FileSourceFetching.ReadRuleFilesFor(request.RulesDirectory, request.LoopPath); // impure
        var loopFile = FileSourceFetching.ReadTextFile(request.LoopPath);                             // impure

        var effects = ruleFiles                                                                      // pure
            .Bind(CatalogLoading.ParseCatalog)
            .Bind(catalog => loopFile.Bind(file => LoopDesigning.ImportLoop(catalog, file)))
            .Match(ok => PlanLoopReport(ok.Value, request.Trace), error => [new Effect.ShowFailure(error.Failure)]);
        return effects;
    }

    public static ImmutableArray<Effect> RunCompare(CompareRequest request)
    {
        var ruleFiles = FileSourceFetching.ReadRuleFilesFor(request.RulesDirectory, request.LeftPath); // impure
        var left = FileSourceFetching.ReadTextFile(request.LeftPath);                                 // impure
        var right = FileSourceFetching.ReadTextFile(request.RightPath);                               // impure

        var effects = ruleFiles                                                                      // pure
            .Bind(CatalogLoading.ParseCatalog)
            .Bind(catalog => ImportBoth(catalog, left, right))
            .Match(ok => PlanComparison(ok.Value.Left, ok.Value.Right), error => [new Effect.ShowFailure(error.Failure)]);
        return effects;
    }

    // ── pure planning ───────────────────────────────────────────────────────────

    /// <summary>
    /// Build summary, the report (the order, the verdict, each step's needs, what it sets off, what is wasted), the
    /// steps and, optionally, the full trace of the first pass and of the repeating pass when it differs.
    /// </summary>
    public static ImmutableArray<Effect> PlanLoopReport(DesignSession session, Optional<TraceOptions> trace)
    {
        var report = LoopDesigning.AnalyzeDesign(session);
        var blank = new Effect.WriteLines([StyledText.ToLine(0, "".ToSpan())]);
        var repeats = report.RepeatingPass != report.FirstPass;
        var traceEffects = trace.Match(
            options => repeats
                ? [.. PlanPassTrace(session, "First pass, step by step", report.FirstPass, options.Value), .. PlanPassTrace(session, "Repeating pass, step by step", report.RepeatingPass, options.Value)]
                : PlanPassTrace(session, "First pass, step by step", report.FirstPass, options.Value),
            _ => []);
        return
        [
            new Effect.WriteLines(BuildExplaining.RenderBuildSummary(session.Build)),
            .. PlanCatalogNotes([session]),
            blank,
            new Effect.WriteLines(LoopDesigning.RenderLoopReport(report)),
            blank,
            new Effect.WriteLines(LoopReportRendering.RenderLoopDesign(session.Design)),
            .. traceEffects,
        ];
    }

    private static ImmutableArray<Effect> PlanPassTrace(DesignSession session, string title, LoopPass pass, TraceOptions options) =>
    [
        new Effect.WriteLines([StyledText.ToLine(0, "".ToSpan())]),
        new Effect.WriteLines([StyledText.ToLine(0, title.ToSpan(Tone.Strong))]),
        new Effect.WriteLines(TraceRenderer.RenderSequence(session.Build, pass.Start, pass.Resolutions, options)),
    ];

    public static ImmutableArray<Effect> PlanComparison(DesignSession left, DesignSession right)
    {
        var comparison = LoopDesigning.CompareLoops(LoopDesigning.AnalyzeDesign(left), LoopDesigning.AnalyzeDesign(right));
        return [.. PlanCatalogNotes([left, right]), new Effect.WriteLines(LoopDesigning.RenderLoopComparison(comparison))];
    }

    private static ImmutableArray<Effect> PlanCatalogNotes(ImmutableArray<DesignSession> sessions) =>
        sessions
            .SelectMany(session => LoopDesigning.FindCatalogMismatch(session).Match(
                issue => new Effect[] { new Effect.WriteLines([StyledText.ToLine(1, $"i {session.Design.Name}: {issue.Value.Message}".ToSpan(Tone.Muted))]) },
                _ => []))
            .ToImmutableArray();

    private static Result<(DesignSession Left, DesignSession Right), string> ImportBoth(
        RuleCatalog catalog, Result<SourceText, string> left, Result<SourceText, string> right) =>
        new[] { left, right }
            .Select(file => file.Bind(f => LoopDesigning.ImportLoop(catalog, f)))
            .CombineAll()
            .Map(sessions => (sessions[0], sessions[1]))
            .MapError(errors => string.Join(Environment.NewLine, errors));

    public static ImmutableArray<Effect> PlanExplain(ValidatedBuild build, ExplanationStyle style)
    {
        var summary = BuildExplaining.RenderBuildSummary(build);
        var explanation = LoopDesigning.ExplainBuild(build, style);
        return
        [
            new Effect.WriteLines(summary),
            new Effect.WriteLines([StyledText.ToLine(0, "".ToSpan())]),
            new Effect.WriteLines(explanation),
        ];
    }

    public static ImmutableArray<Effect> PlanTrace(ValidatedBuild build, ImmutableArray<PlayerAction> actions, TraceOptions options)
    {
        var initial = ActionResolution.CreateInitialState();
        var resolutions = ActionResolution.ResolveSequence(build, initial, actions);
        var trace = TraceRenderer.RenderSequence(build, initial, resolutions, options);
        return [new Effect.WriteLines(BuildExplaining.RenderBuildSummary(build)), new Effect.WriteLines(trace)];
    }

    public static ImmutableArray<Effect> PlanGraph(ValidatedBuild build, GraphRequest request)
    {
        var graph = LoopGraphBuilding.BuildLoopGraph(build);
        var loops = LoopFinding.FindLoops(graph);
        return request.Format == GraphFormat.Mermaid
            ? [new Effect.WriteText(LoopRendering.RenderMermaid(graph, loops.Take(request.Limit).ToImmutableArray(), request.LoopsOnly))]
            : [new Effect.WriteLines(LoopRendering.RenderLoops(graph, loops, request.Limit))];
    }

    /// <summary>The <c>--actions</c> tokens, then those of a scenario file (one token per line or comma separated, # comments); none is an error.</summary>
    public static Result<ImmutableArray<PlayerAction>, string> ParseActions(ImmutableArray<string> tokens, Optional<string> scenario)
    {
        var fromScenario = scenario.Match(
            text => text.Value
                .Split('\n')
                .Select(line => line.Split('#')[0])
                .SelectMany(line => line.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
            _ => []);
        var all = tokens.Concat(fromScenario).Select(t => t.Trim()).Where(t => t.Length > 0).ToImmutableArray();
        if (all.IsEmpty)
        {
            return new Result<ImmutableArray<PlayerAction>, string>.Error("No actions given. Use --actions \"grenade:kill,melee:kill\" or --scenario <file>.");
        }

        return all
            .Select(ActionTokenParsing.ParseActionToken)
            .CombineAll()
            .MapError(errors => string.Join(Environment.NewLine, errors));
    }
}

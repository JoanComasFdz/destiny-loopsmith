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

    public static ImmutableArray<Effect> RunSimulate(SimulateRequest request)
    {
        var build = BuildLoading.LoadValidatedBuild(request.Load);                                   // impure
        var scenario = request.ScenarioPath.Match(
            path => FileSourceFetching.ReadTextFile(path.Value).Map(file => Optional.Some(file.Text)), // impure
            _ => new Result<Optional<string>, string>.Ok(Optional.None<string>()));

        var effects = build                                                                          // pure
            .Bind(validated => scenario
                .Bind(text => ParseActions(request.ActionTokens, text))
                .Map(actions => PlanSimulation(validated, actions, request.Options)))
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

    // ── pure planning ───────────────────────────────────────────────────────────

    public static ImmutableArray<Effect> PlanExplain(ValidatedBuild build, ExplanationStyle style)
    {
        var groups = BuildExplaining.ExplainBuild(build);
        var summary = BuildExplaining.RenderBuildSummary(build);
        var explanation = BuildExplaining.RenderExplanation(groups, style);
        return
        [
            new Effect.WriteLines(summary),
            new Effect.WriteLines([StyledText.ToLine(0, "".ToSpan())]),
            new Effect.WriteLines(explanation),
        ];
    }

    public static ImmutableArray<Effect> PlanSimulation(ValidatedBuild build, ImmutableArray<PlayerAction> actions, TraceOptions options)
    {
        var initial = ActionResolution.CreateInitialState(build);
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

    /// <summary>Actions from <c>--actions</c> tokens, else from a scenario file (one token per line or comma separated, # comments).</summary>
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
            .Select(DomainPhrasing.ParseActionToken)
            .CombineAll()
            .MapError(errors => string.Join(Environment.NewLine, errors));
    }
}

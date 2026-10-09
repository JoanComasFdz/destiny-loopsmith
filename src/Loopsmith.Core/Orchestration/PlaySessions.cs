using System.Collections.Immutable;
using Loopsmith.Core.BuildExplanation;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.Simulation;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Core.Orchestration;

/// <summary>
/// Interactive design of a loop: the host reads a line, <see cref="PlaySessions.StepPlay"/> answers. Every action
/// played becomes a step of <see cref="Design"/>, which is written to <see cref="SavePath"/> on quit when it has steps,
/// and on <c>w</c>.
/// </summary>
public sealed record PlaySession(
    DesignSession Design,
    ImmutableArray<PlayerAction> Available,
    TraceOptions Options,
    Optional<string> SavePath,
    bool IsOver);

/// <summary>One line of input answered: the next session, what to show, and where to save the design now (if anywhere).</summary>
public sealed record PlayTurn(PlaySession Session, ImmutableArray<Effect> Effects, Optional<string> SaveTo);

public static class PlaySessions
{
    private const string Commands =
        "number or action token (grenade:kill:3 = kill 3) · u undo · n <note> · d <description> · a analyse · w save · e explain · s state · r reset · q quit";

    public static Result<PlaySession, string> StartPlay(PlayRequest request)
    {
        var ruleFiles = FileSourceFetching.ReadRuleFilesFor(request.Load.RulesDirectory, request.Load.BuildPath); // impure
        var buildFile = FileSourceFetching.ReadTextFile(request.Load.BuildPath);                                 // impure

        var session = ComposePlay(ruleFiles, buildFile, request);                                               // pure
        return session;
    }

    public static Result<PlaySession, string> ComposePlay(
        Result<ImmutableArray<SourceText>, string> ruleFiles,
        Result<SourceText, string> buildFile,
        PlayRequest request) =>
        ruleFiles
            .Bind(CatalogLoading.ParseCatalog)
            .Bind(catalog => buildFile.Bind(file => LoopDesigning.StartDesign(catalog, file, request.LoopName.UnwrapOr(""))))
            .Map(design => request.LoopName.IsSome()
                ? design
                : LoopDesigning.RenameDesign(design, $"{design.Build.Build.Name} loop", design.Design.Author, design.Design.Description))
            .Map(design => CreatePlay(design, request.Options, request.SavePath));

    public static PlaySession CreatePlay(DesignSession design, TraceOptions options, Optional<string> savePath) =>
        new(design, ActionResolution.ListAvailableActions(design.Build, design.Current), options, savePath, false);

    public static ImmutableArray<Effect> PlanOpening(PlaySession session) =>
    [
        new Effect.WriteLines(BuildExplaining.RenderBuildSummary(session.Design.Build)),
        new Effect.WriteLines([DescribeDesign(session)]),
        new Effect.WriteLines([StyledText.ToLine(0,
            "Fresh spawn".ToSpan(Tone.Strong),
            " — abilities are always available (ability energy isn't simulated).".ToSpan(Tone.Muted))]),
        new Effect.WriteLines(TraceRenderer.RenderState(session.Design.Build, session.Design.Current, [])),
        .. PlanPrompt(session),
    ];

    /// <summary>Impure shell: answer one line of input and, when it asks for it, write the loop file.</summary>
    public static (PlaySession Session, ImmutableArray<Effect> Effects) StepPlay(PlaySession session, string input)
    {
        var turn = PlanTurn(session, input);                                                                   // pure
        var saved = turn.SaveTo.Match(path => SaveDesign(turn.Session.Design, path.Value), _ => []);          // impure

        var effects = turn.Effects.AddRange(saved);                                                          // pure
        return (turn.Session, effects);
    }

    private static ImmutableArray<Effect> SaveDesign(DesignSession design, string path)
    {
        var text = LoopDesigning.ExportLoop(design);                                                         // pure
        var written = FileSourceFetching.WriteTextFile(path, text);                                         // impure

        var effects = written.Match(                                                                         // pure
            _ => ImmutableArray.Create<Effect>(new Effect.WriteLines([StyledText.ToLine(0,
                $"✓ Saved '{design.Design.Name}' ({design.Design.Steps.Length} steps) to {path}".ToSpan(Tone.Strong))])),
            error => [new Effect.ShowFailure(error.Failure)]);
        return effects;
    }

    /// <summary>Pure: one line of input → next session, what to show, and whether to save.</summary>
    public static PlayTurn PlanTurn(PlaySession session, string input)
    {
        var line = input.Trim();
        var (command, argument) = SplitCommand(line);
        return command switch
        {
            "q" or "quit" or "exit" => QuitPlay(session),
            "w" or "write" or "save" => session.SavePath.Match(
                path => new PlayTurn(session, [], Optional.Some(path.Value)),
                _ => Answer(session, [new Effect.ShowFailure("No file to save to: start play with --save <file.loop.yaml>.")])),
            "r" or "reset" => ResetDesign(session),
            "u" or "undo" => UndoStep(session),
            "n" or "note" => AnnotateLastStep(session, argument),
            "d" or "describe" => Answer(
                session with { Design = LoopDesigning.RenameDesign(session.Design, session.Design.Design.Name, session.Design.Design.Author, ToDescription(argument)) },
                [new Effect.WriteLines([StyledText.ToLine(0, "Description set.".ToSpan(Tone.Muted))])]),
            "a" or "analyse" or "analyze" => Answer(session,
            [
                new Effect.WriteLines(LoopDesigning.RenderLoopReport(LoopDesigning.AnalyzeDesign(session.Design, LoopDesigning.DefaultMaxCycles))),
                new Effect.WriteLines(LoopReportRendering.RenderLoopDesign(session.Design.Design)),
            ]),
            "e" or "explain" => Answer(session, CommandShells.PlanExplain(session.Design.Build, ExplanationStyle.Note)),
            "s" or "state" => Answer(session, [new Effect.WriteLines(TraceRenderer.RenderState(session.Design.Build, session.Design.Current, []))]),
            "" or "?" or "h" or "help" => Answer(session, []),
            _ => PlayAction(session, line),
        };
    }

    private static (string Command, string Argument) SplitCommand(string line)
    {
        var space = line.IndexOf(' ', StringComparison.Ordinal);
        return space < 0
            ? (line.ToLowerInvariant(), "")
            : (line[..space].ToLowerInvariant(), line[(space + 1)..].Trim());
    }

    private static PlayTurn PlayAction(PlaySession session, string line)
    {
        var action = int.TryParse(line, out var number) && number >= 1 && number <= session.Available.Length
            ? new Result<PlayerAction, string>.Ok(session.Available[number - 1])
            : ActionTokenParsing.ParseActionToken(line);
        return action.Match(
            ok =>
            {
                var design = LoopDesigning.AppendStep(session.Design, ok.Value, Optional.None<string>());
                var next = CreatePlay(design, session.Options, session.SavePath);
                var trace = TraceRenderer.RenderResolution(design.Build, design.Resolutions[^1], session.Options with { ShowState = true });
                return Answer(next, [new Effect.WriteLines(trace)]);
            },
            error => Answer(session, [new Effect.ShowFailure(error.Failure)]));
    }

    private static PlayTurn ResetDesign(PlaySession session)
    {
        var fresh = CreatePlay(LoopDesigning.TruncateSteps(session.Design, 0), session.Options, session.SavePath);
        return new PlayTurn(fresh, PlanOpening(fresh), Optional.None<string>());
    }

    private static PlayTurn UndoStep(PlaySession session)
    {
        if (session.Design.Design.Steps.IsEmpty)
        {
            return Answer(session, [new Effect.ShowFailure("Nothing to undo.")]);
        }

        var undone = session.Design.Design.Steps[^1].Action.ToActionToken();
        var next = CreatePlay(LoopDesigning.RemoveLastStep(session.Design), session.Options, session.SavePath);
        return Answer(next,
        [
            new Effect.WriteLines([StyledText.ToLine(0, $"Undid step {session.Design.Design.Steps.Length} ({undone}).".ToSpan(Tone.Muted))]),
            new Effect.WriteLines(TraceRenderer.RenderState(next.Design.Build, next.Design.Current, [])),
        ]);
    }

    private static PlayTurn AnnotateLastStep(PlaySession session, string note)
    {
        var steps = session.Design.Design.Steps.Length;
        if (steps == 0)
        {
            return Answer(session, [new Effect.ShowFailure("No step to annotate yet.")]);
        }

        var design = LoopDesigning.AnnotateStep(session.Design, steps - 1, ToOptionalText(note));
        return Answer(session with { Design = design },
            [new Effect.WriteLines([StyledText.ToLine(0, $"Note set on step {steps}.".ToSpan(Tone.Muted))])]);
    }

    private static Optional<string> ToOptionalText(string text) =>
        text.Length == 0 ? Optional.None<string>() : Optional.Some(text);

    /// <summary>A description typed on one line: <c>\n</c> starts a new line.</summary>
    private static Optional<string> ToDescription(string text) =>
        ToOptionalText(text).Map(description => description.Replace("\\n", "\n", StringComparison.Ordinal));

    /// <summary>Quitting saves the design when there is one: an empty design never overwrites a loop file.</summary>
    private static PlayTurn QuitPlay(PlaySession session)
    {
        var over = session with { IsOver = true };
        var hasSteps = !session.Design.Design.Steps.IsEmpty;
        var notSaved = session.SavePath.IsSome() && !hasSteps
            ? ImmutableArray.Create<Effect>(new Effect.WriteLines([StyledText.ToLine(0, "No steps designed — nothing saved.".ToSpan(Tone.Muted))]))
            : [];
        return new PlayTurn(over, notSaved, hasSteps ? session.SavePath : Optional.None<string>());
    }

    private static PlayTurn Answer(PlaySession session, ImmutableArray<Effect> effects) =>
        new(session, [.. effects, .. PlanPrompt(session)], Optional.None<string>());

    private static StyledLine DescribeDesign(PlaySession session)
    {
        var saving = session.SavePath.Match(path => $" — saved to {path.Value} on quit (w saves now)", _ => "");
        return StyledText.ToLine(0, "Designing ".ToSpan(Tone.Muted), session.Design.Design.Name.ToSpan(Tone.Strong), saving.ToSpan(Tone.Muted));
    }

    private static ImmutableArray<Effect> PlanPrompt(PlaySession session) =>
    [
        new Effect.WriteLines([StyledText.ToLine(0, "Next:".ToSpan(Tone.Strong))]),
        new Effect.WriteLines(TraceRenderer.RenderAvailableActions(session.Design.Build, session.Available)),
        new Effect.WriteLines([StyledText.ToLine(0, Commands.ToSpan(Tone.Muted))]),
    ];
}

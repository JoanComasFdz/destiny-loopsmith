using System.Collections.Immutable;
using Loopsmith.Core.BuildExplanation;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.Simulation;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Core.Orchestration;

/// <summary>Interactive step-through: the host reads a line, <see cref="PlaySessions.AdvancePlay"/> answers.</summary>
public sealed record PlaySession(ValidatedBuild Build, GameState State, ImmutableArray<PlayerAction> Available, TraceOptions Options, bool IsOver);

public static class PlaySessions
{
    public static Result<PlaySession, string> StartPlay(LoadRequest request, TraceOptions options)
    {
        var build = BuildLoading.LoadValidatedBuild(request);                                       // impure

        var session = build.Map(validated => CreateSession(validated, options));                    // pure
        return session;
    }

    public static PlaySession CreateSession(ValidatedBuild build, TraceOptions options)
    {
        var state = ActionResolution.CreateInitialState(build);
        var available = ActionResolution.ListAvailableActions(build, state);
        return new PlaySession(build, state, available, options, false);
    }

    public static ImmutableArray<Effect> PlanOpening(PlaySession session) =>
    [
        new Effect.WriteLines(BuildExplaining.RenderBuildSummary(session.Build)),
        new Effect.WriteLines([StyledText.ToLine(0, "Fresh spawn — all abilities charged.".ToSpan(Tone.Strong))]),
        new Effect.WriteLines(TraceRenderer.RenderState(session.Build, session.State, [])),
        .. PlanPrompt(session),
    ];

    /// <summary>Pure: one line of input → next session + what to show.</summary>
    public static (PlaySession Session, ImmutableArray<Effect> Effects) AdvancePlay(PlaySession session, string input)
    {
        var command = input.Trim().ToLowerInvariant();
        switch (command)
        {
            case "q" or "quit" or "exit":
                return (session with { IsOver = true }, []);
            case "r" or "reset":
                var fresh = CreateSession(session.Build, session.Options);
                return (fresh, PlanOpening(fresh));
            case "e" or "explain":
                return (session, [.. CommandShells.PlanExplain(session.Build, ExplanationStyle.Note), .. PlanPrompt(session)]);
            case "s" or "state":
                return (session, [new Effect.WriteLines(TraceRenderer.RenderState(session.Build, session.State, [])), .. PlanPrompt(session)]);
            case "" or "?" or "h" or "help":
                return (session, PlanPrompt(session));
        }

        var action = int.TryParse(command, out var number) && number >= 1 && number <= session.Available.Length
            ? new Result<PlayerAction, string>.Ok(session.Available[number - 1])
            : ActionTokenParsing.ParseActionToken(command);
        return action.Match<(PlaySession, ImmutableArray<Effect>)>(
            ok =>
            {
                var resolution = ActionResolution.ResolveAction(session.Build, session.State, ok.Value);
                var next = session with { State = resolution.State, Available = resolution.NowAvailable };
                var trace = TraceRenderer.RenderResolution(session.Build, resolution, session.Options with { ShowState = true });
                return (next, [new Effect.WriteLines(trace), .. PlanPrompt(next)]);
            },
            error => (session, [new Effect.ShowFailure(error.Failure), .. PlanPrompt(session)]));
    }

    private static ImmutableArray<Effect> PlanPrompt(PlaySession session) =>
    [
        new Effect.WriteLines([StyledText.ToLine(0, "Next:".ToSpan(Tone.Strong))]),
        new Effect.WriteLines(TraceRenderer.RenderAvailableActions(session.Build, session.Available)),
        new Effect.WriteLines([StyledText.ToLine(0, "number or action token · e explain · s state · r reset · q quit".ToSpan(Tone.Muted))]),
    ];
}

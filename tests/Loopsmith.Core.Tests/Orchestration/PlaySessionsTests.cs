using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.LoopFiles;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary><c>loopsmith play</c> designs a loop: every action is a step; u/n/d edit it; q and w save it.</summary>
public sealed class PlaySessionsTests
{
    private const string BuildPath = "builds/skip-grenade-hunter/build.yaml";

    private static readonly TraceOptions Options = new(false, false, false);

    private static PlaySession StartPlay(Optional<string> savePath) => StartPlay(savePath, Optional.None<string>());

    private static PlaySession StartPlay(Optional<string> savePath, Optional<string> name) =>
        PlaySessions.ComposePlay(
                FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules")),
                FileSourceFetching.ReadTextFile(RepoFiles.ToPath(BuildPath)),
                new PlayRequest(new LoadRequest(BuildPath, Optional.None<string>()), Options, savePath, name))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static PlaySession PlayAll(PlaySession session, params string[] inputs) =>
        inputs.Aggregate(session, (current, input) => PlaySessions.PlanTurn(current, input).Session);

    [Fact]
    public void Every_action_played_becomes_a_step_and_the_loop_is_named_after_the_build_by_default()
    {
        var session = PlayAll(StartPlay(Optional.None<string>()), "class", "grenade:kill", "1");

        Assert.Equal("Skip Grenade Hunter loop", session.Design.Design.Name);
        Assert.Equal(3, session.Design.Design.Steps.Length);
        Assert.Equal(new PlayerAction.UseClassAbility(), session.Design.Design.Steps[0].Action);
        Assert.Equal(session.Design.Resolutions[^1].NowAvailable, session.Available);
    }

    [Fact]
    public void Abilities_never_run_out_and_tokens_may_aim_at_several_enemies()
    {
        var session = PlayAll(StartPlay(Optional.None<string>()), "grenade:kill", "grenade:kill:3", "grenade:kill", "kinetic:hit:5");

        Assert.Equal(
            ["grenade:kill", "grenade:kill:3", "grenade:kill", "kinetic:hit:5"],
            session.Design.Design.Steps.Select(step => step.Action.ToActionToken()));
        Assert.All(session.Design.Resolutions, resolution => Assert.False(resolution.Blocked.IsSome()));
        Assert.All(session.Design.Resolutions, resolution => Assert.NotEmpty(resolution.Fired));
    }

    [Fact]
    public void Undo_note_and_describe_edit_the_design()
    {
        var session = PlayAll(StartPlay(Optional.None<string>(), Optional.Some("Mine")),
            "class", "grenade:kill", "u", "N Arm Slice: + Reaper #1", "d First line\\nSecond line");

        var design = session.Design.Design;
        Assert.Equal("Mine", design.Name);
        var step = Assert.Single(design.Steps);
        Assert.Equal(Optional.Some("Arm Slice: + Reaper #1"), step.Note);
        Assert.Equal(Optional.Some("First line\nSecond line"), design.Description);
    }

    [Fact]
    public void Unknown_input_changes_nothing_and_says_why()
    {
        var turn = PlaySessions.PlanTurn(StartPlay(Optional.None<string>()), "dance");

        Assert.Empty(turn.Session.Design.Design.Steps);
        Assert.Contains(turn.Effects, effect => effect is Effect.ShowFailure);
    }

    [Fact]
    public void Saving_needs_a_file()
    {
        var turn = PlaySessions.PlanTurn(StartPlay(Optional.None<string>()), "w");

        Assert.False(turn.SaveTo.IsSome());
        Assert.Contains(turn.Effects, effect => effect is Effect.ShowFailure failure && failure.Message.Contains("--save"));
    }

    [Fact]
    public void Quitting_saves_a_designed_loop_but_never_an_empty_one()
    {
        var empty = PlaySessions.PlanTurn(StartPlay(Optional.Some("x.loop.yaml")), "q");
        var designed = PlaySessions.PlanTurn(PlayAll(StartPlay(Optional.Some("x.loop.yaml")), "class"), "quit");

        Assert.True(empty.Session.IsOver);
        Assert.False(empty.SaveTo.IsSome());
        Assert.True(designed.Session.IsOver);
        Assert.Equal(Optional.Some("x.loop.yaml"), designed.SaveTo);
    }

    [Fact]
    public void Write_saves_the_loop_file_and_it_imports_back()
    {
        var path = Path.Combine(Path.GetTempPath(), $"loopsmith-{Guid.NewGuid():N}", "loops", "saved.loop.yaml");
        var session = PlayAll(StartPlay(Optional.Some(path), Optional.Some("Saved: loop")), "class", "n dodge", "grenade:kill");

        var (_, effects) = PlaySessions.StepPlay(session, "w");

        Assert.DoesNotContain(effects, effect => effect is Effect.ShowFailure);
        var text = File.ReadAllText(path);
        var design = LoopFileParsing.ParseLoopFile(new SourceText(path, text)).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        Assert.Equal("Saved: loop", design.Name);
        Assert.Equal(["class", "grenade:kill"], design.Steps.Select(step => step.Action.ToActionToken()));
        Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(path)!)!, recursive: true);
    }

    [Fact]
    public void Analyse_shows_the_report_of_the_loop_so_far()
    {
        var turn = PlaySessions.PlanTurn(PlayAll(StartPlay(Optional.None<string>()), "class", "grenade:kill"), "a");

        var text = string.Join("\n", turn.Effects.OfType<Effect.WriteLines>().Select(lines => lines.Lines.ToPlainText()));
        Assert.Contains("2 steps per cycle", text);
        Assert.Contains("Steps", text);
    }
}

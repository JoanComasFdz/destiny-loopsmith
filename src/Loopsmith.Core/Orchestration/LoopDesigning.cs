using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.BuildExplanation;
using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.LoopFiles;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.ReportComparison;
using Loopsmith.Core.RuleParsing;
using Loopsmith.Core.Simulation;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Core.Orchestration;

public enum TriggerGroup { Ability, Weapon, Pickup, Time }

/// <summary>
/// A trigger the designer can pick next — every option listed can be played (abilities are always available, ADRs D21).
/// <see cref="IsNew"/> marks what the last step unlocked (a pickup that just landed). Abilities and weapons are listed
/// against one enemy; <see cref="LoopDesigning.SetTargetCount"/> aims them at more.
/// </summary>
public sealed record TriggerOption(
    PlayerAction Action,
    string Token,
    string Label,
    TriggerGroup Group,
    bool IsNew);

/// <summary>
/// A loop being designed: the validated build, the design so far, one resolution per step (same order as
/// <c>Design.Steps</c>) and the state after the last step. Immutable — every edit returns a new session,
/// so undo is "keep the previous session".
/// </summary>
public sealed record DesignSession(
    ValidatedBuild Build,
    LoopDesign Design,
    GameState Initial,
    ImmutableArray<Resolution> Resolutions,
    GameState Current);

/// <summary>Pure API the hosts (CLI, web) use to design, export, import, analyse and compare loops.</summary>
public static class LoopDesigning
{
    public const int DefaultMaxCycles = 10;

    public static Result<DesignSession, string> StartDesign(RuleCatalog catalog, SourceText buildFile, string name) =>
        BuildFileParsing.ParseBuildFile(buildFile)
            .Bind(build => BuildValidation.ValidateBuild(build, catalog))
            .Map(validated => CreateSession(validated, new LoopDesign(
                name,
                Optional.None<string>(),
                Optional.None<string>(),
                Optional.Some(catalog.Version),
                buildFile,
                [])));

    public static DesignSession CreateSession(ValidatedBuild build, LoopDesign design)
    {
        var initial = ActionResolution.CreateInitialState();
        var actions = design.Steps.Select(step => step.Action).ToImmutableArray();
        var resolutions = ActionResolution.ResolveSequence(build, initial, actions);
        var current = resolutions.IsEmpty ? initial : resolutions[^1].State;
        return new DesignSession(build, design, initial, resolutions, current);
    }

    public static DesignSession AppendStep(DesignSession session, PlayerAction action, Optional<string> note)
    {
        var resolution = ActionResolution.ResolveAction(session.Build, session.Current, action);
        var design = session.Design with { Steps = session.Design.Steps.Add(new LoopStep(action, note)) };
        return session with { Design = design, Resolutions = session.Resolutions.Add(resolution), Current = resolution.State };
    }

    public static DesignSession RemoveLastStep(DesignSession session) =>
        TruncateSteps(session, session.Design.Steps.Length - 1);

    /// <summary>Keep the first <paramref name="keep"/> steps — branch from there with a different trigger.</summary>
    public static DesignSession TruncateSteps(DesignSession session, int keep)
    {
        var count = Math.Clamp(keep, 0, session.Design.Steps.Length);
        var resolutions = session.Resolutions.Take(count).ToImmutableArray();
        var design = session.Design with { Steps = session.Design.Steps.Take(count).ToImmutableArray() };
        var current = resolutions.IsEmpty ? session.Initial : resolutions[^1].State;
        return session with { Design = design, Resolutions = resolutions, Current = current };
    }

    public static DesignSession RenameDesign(DesignSession session, string name, Optional<string> author, Optional<string> description) =>
        session with { Design = session.Design with { Name = name, Author = author, Description = description } };

    public static DesignSession AnnotateStep(DesignSession session, int index, Optional<string> note) =>
        index < 0 || index >= session.Design.Steps.Length
            ? session
            : session with { Design = session.Design with { Steps = session.Design.Steps.SetItem(index, session.Design.Steps[index] with { Note = note }) } };

    /// <summary>
    /// Every trigger the build offers right now, grouped: every ability (always available — ADRs D21), every weapon,
    /// the pickups on the ground, a wait. <see cref="TriggerOption.IsNew"/> marks what the last step made available.
    /// </summary>
    public static ImmutableArray<TriggerOption> ListTriggerOptions(DesignSession session)
    {
        var build = session.Build;
        var glossary = build.Catalog.Glossary;
        var previous = session.Resolutions.Length switch
        {
            0 => [],
            1 => ActionResolution.ListAvailableActions(build, session.Initial),
            _ => session.Resolutions[^2].NowAvailable,
        };
        var candidates = ListCandidateActions(build, session.Current);
        return candidates
            .Select(candidate => new TriggerOption(
                candidate.Action,
                candidate.Action.ToActionToken(),
                glossary.DescribeAction(candidate.Action, build.Build),
                candidate.Group,
                !previous.Contains(candidate.Action) && !session.Resolutions.IsEmpty))
            .ToImmutableArray();
    }

    private static ImmutableArray<(PlayerAction Action, TriggerGroup Group)> ListCandidateActions(ValidatedBuild build, GameState state)
    {
        var hits = new[] { HitOutcome.Kill, HitOutcome.Damage };
        var abilities = new[] { OffensiveAbility.Grenade, OffensiveAbility.Melee }
            .SelectMany(kind => hits.Select(hit => ((PlayerAction)new PlayerAction.CastAbility(kind, hit, TargetCount.One), TriggerGroup.Ability)))
            .Append(((PlayerAction)new PlayerAction.UseClassAbility(), TriggerGroup.Ability))
            .Concat(hits.Select(hit => ((PlayerAction)new PlayerAction.CastAbility(OffensiveAbility.Super, hit, TargetCount.One), TriggerGroup.Ability)));
        var weapons = build.Build.Weapons
            .SelectMany(weapon => hits.Select(hit => ((PlayerAction)new PlayerAction.FireWeapon(weapon.Slot, hit, TargetCount.One), TriggerGroup.Weapon)));
        var pickups = state.Pickups
            .Where(p => p.Count > 0)
            .Select(p => ((PlayerAction)new PlayerAction.CollectPickups(p.Pickup), TriggerGroup.Pickup));
        var wait = new[] { ((PlayerAction)new PlayerAction.Wait(ActionResolution.DefaultWait), TriggerGroup.Time) };
        return [.. abilities, .. weapons, .. pickups, .. wait];
    }

    /// <summary>How many enemies an ability or weapon action hits (or kills); none for the class ability, pickups and waits.</summary>
    public static Optional<TargetCount> ReadTargetCount(PlayerAction action) =>
        action switch
        {
            PlayerAction.CastAbility cast => Optional.Some(cast.Targets),
            PlayerAction.FireWeapon fire => Optional.Some(fire.Targets),
            _ => Optional.None<TargetCount>(),
        };

    /// <summary>
    /// The same ability or weapon action against <paramref name="targets"/> enemies ("kill 3 with the grenade"); the
    /// class ability, pickups and waits have no targets and come back unchanged. Hosts validate a typed count with
    /// <c>TargetCount.TryFrom</c> (1..20).
    /// </summary>
    public static PlayerAction SetTargetCount(PlayerAction action, TargetCount targets) =>
        action switch
        {
            PlayerAction.CastAbility cast => cast with { Targets = targets },
            PlayerAction.FireWeapon fire => fire with { Targets = targets },
            _ => action,
        };

    // ── export, import, analyse, compare (LoopFiles, Simulation.LoopRunning, ReportComparison) ──

    /// <summary>The design as a <c>.loop.yaml</c> file (docs/loop-format.md).</summary>
    public static string ExportLoop(DesignSession session) =>
        LoopFileWriting.WriteLoopFile(session.Design);

    /// <summary>
    /// Parse a <c>.loop.yaml</c>, validate its embedded build against the catalog, replay its steps. A loop designed
    /// against another catalog still imports (see <see cref="ListDesignIssues"/>); an invalid embedded build or an
    /// unknown action token is an error.
    /// </summary>
    public static Result<DesignSession, string> ImportLoop(RuleCatalog catalog, SourceText loopFile) =>
        LoopFileParsing.ParseLoopFile(loopFile)
            .Bind(design => BuildFileParsing.ParseBuildFile(design.Build)
                .Bind(build => BuildValidation.ValidateBuild(build, catalog))
                .Map(validated => CreateSession(validated, design)));

    /// <summary>Run the design up to <paramref name="maxCycles"/> times back to back and measure it.</summary>
    public static LoopReport AnalyzeDesign(DesignSession session, int maxCycles) =>
        LoopRunning.RunLoop(session.Build, session.Design, maxCycles);

    public static LoopComparison CompareLoops(LoopReport left, LoopReport right) =>
        LoopComparing.CompareLoops(left, right);

    /// <summary>
    /// The build's own validation issues (warnings, info) plus, as Info, a loop designed against a catalog other
    /// than the one it is replayed with.
    /// </summary>
    public static ImmutableArray<BuildIssue> ListDesignIssues(DesignSession session) =>
        [.. session.Build.Issues, .. FindCatalogMismatch(session).Match(issue => [issue.Value], _ => ImmutableArray<BuildIssue>.Empty)];

    /// <summary>Info when the loop was designed against another catalog version than the one replaying it.</summary>
    public static Optional<BuildIssue> FindCatalogMismatch(DesignSession session)
    {
        var current = session.Build.Catalog.Version;
        return session.Design.Catalog.Bind(designed => designed == current
            ? Optional.None<BuildIssue>()
            : Optional.Some(new BuildIssue(Severity.Info, $"Loop designed against catalog {designed}; replaying with {current}.")));
    }

    /// <summary>What one step fired, worded like the CLI's trace, without its "#n action" header (the host draws its own).</summary>
    public static ImmutableArray<StyledLine> RenderStepOutcomes(DesignSession session, Resolution resolution, TraceOptions options) =>
        [.. TraceRenderer.RenderResolution(session.Build, resolution, options).Skip(1)];

    /// <summary>The static view of a build: every trigger and what it sets off, as a build note or an aligned tree.</summary>
    public static ImmutableArray<StyledLine> ExplainBuild(ValidatedBuild build, ExplanationStyle style) =>
        BuildExplaining.RenderExplanation(BuildExplaining.ExplainBuild(build), style);

    /// <summary>The report as styled lines (verdict, steady state: kills, pickups, what fired, what was wasted, uptime).</summary>
    public static ImmutableArray<StyledLine> RenderLoopReport(LoopReport report) =>
        LoopReportRendering.RenderLoopReport(report);

    /// <summary>The comparison as a two-column table, the better value of each row marked ✓.</summary>
    public static ImmutableArray<StyledLine> RenderLoopComparison(LoopComparison comparison) =>
        ComparisonRendering.RenderComparison(comparison);
}

/// <summary>Parse the rule catalog once (the web host reads the rule files from embedded resources).</summary>
public static class CatalogLoading
{
    public static Result<RuleCatalog, string> ParseCatalog(ImmutableArray<SourceText> ruleFiles) =>
        RuleCatalogParsing.ParseCatalog(ruleFiles);
}

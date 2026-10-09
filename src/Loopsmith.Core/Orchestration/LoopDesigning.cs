using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.RuleParsing;
using Loopsmith.Core.Simulation;

namespace Loopsmith.Core.Orchestration;

public enum TriggerGroup { Ability, Weapon, Pickup, Time }

/// <summary>A trigger the designer can pick next. Unavailable ones say why; <see cref="IsNew"/> marks what the last step unlocked.</summary>
public sealed record TriggerOption(
    PlayerAction Action,
    string Token,
    string Label,
    TriggerGroup Group,
    bool IsAvailable,
    Optional<string> Unavailable,
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
        var initial = ActionResolution.CreateInitialState(build);
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

    /// <summary>Every trigger the build offers right now, grouped; unavailable ones carry the reason.</summary>
    public static ImmutableArray<TriggerOption> ListTriggerOptions(DesignSession session)
    {
        var build = session.Build;
        var glossary = build.Catalog.Glossary;
        var available = ActionResolution.ListAvailableActions(build, session.Current);
        var previous = session.Resolutions.Length switch
        {
            0 => available,
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
                available.Contains(candidate.Action),
                available.Contains(candidate.Action) ? Optional.None<string>() : Optional.Some(DescribeUnavailable(session.Current, candidate.Action)),
                available.Contains(candidate.Action) && !previous.Contains(candidate.Action) && !session.Resolutions.IsEmpty))
            .ToImmutableArray();
    }

    private static ImmutableArray<(PlayerAction Action, TriggerGroup Group)> ListCandidateActions(ValidatedBuild build, GameState state)
    {
        var hits = new[] { HitOutcome.Kill, HitOutcome.Damage };
        var abilities = new[] { OffensiveAbility.Grenade, OffensiveAbility.Melee }
            .SelectMany(kind => hits.Select(hit => ((PlayerAction)new PlayerAction.CastAbility(kind, hit), TriggerGroup.Ability)))
            .Append(((PlayerAction)new PlayerAction.UseClassAbility(), TriggerGroup.Ability))
            .Concat(hits.Select(hit => ((PlayerAction)new PlayerAction.CastAbility(OffensiveAbility.Super, hit), TriggerGroup.Ability)));
        var weapons = build.Build.Weapons
            .SelectMany(weapon => hits.Select(hit => ((PlayerAction)new PlayerAction.FireWeapon(weapon.Slot, hit), TriggerGroup.Weapon)));
        var pickups = state.Pickups
            .Where(p => p.Count > 0)
            .Select(p => ((PlayerAction)new PlayerAction.CollectPickups(p.Pickup), TriggerGroup.Pickup));
        var wait = new[] { ((PlayerAction)new PlayerAction.Wait(ActionResolution.DefaultWait), TriggerGroup.Time) };
        return [.. abilities, .. weapons, .. pickups, .. wait];
    }

    private static string DescribeUnavailable(GameState state, PlayerAction action)
    {
        var kind = action switch
        {
            PlayerAction.CastAbility cast => Optional.Some(cast.Kind.ToAbilityKind()),
            PlayerAction.UseClassAbility => Optional.Some(AbilityKind.ClassAbility),
            _ => Optional.None<AbilityKind>(),
        };
        return kind
            .Map(k => $"needs a full charge ({state.ReadGauge(k).Energy.Value:0.##}/1)")
            .UnwrapOr("not available now");
    }

    // ── implemented by the loop slices (LoopFiles, Simulation.LoopRunning, LoopComparison) ──

    /// <summary>The design as a <c>.loop.yaml</c> file (docs/loop-format.md).</summary>
    public static string ExportLoop(DesignSession session) =>
        $"# TODO(loop slices): export not implemented yet\nloop: {session.Design.Name}\n";

    /// <summary>Parse a <c>.loop.yaml</c>, validate its embedded build against the catalog, replay its steps.</summary>
    public static Result<DesignSession, string> ImportLoop(RuleCatalog catalog, SourceText loopFile) =>
        new Result<DesignSession, string>.Error($"Importing loops is not implemented yet ({loopFile.Path}, catalog {catalog.Version}).");

    /// <summary>Run the design up to <paramref name="maxCycles"/> times back to back and measure it.</summary>
    public static LoopReport AnalyzeDesign(DesignSession session, int maxCycles) =>
        new(session.Design.Name, session.Build.Build.Name, session.Design.Steps.Length,
            new EnergySnapshot(EnergyAmount.From(0m), EnergyAmount.From(0m), EnergyAmount.From(0m), EnergyAmount.From(0m)),
            [], 0, maxCycles, [], [], [], 0, 0);

    public static LoopComparison CompareLoops(LoopReport left, LoopReport right) =>
        new(left, right, []);
}

/// <summary>Parse the rule catalog once (the web host reads the rule files from embedded resources).</summary>
public static class CatalogLoading
{
    public static Result<RuleCatalog, string> ParseCatalog(ImmutableArray<SourceText> ruleFiles) =>
        RuleCatalogParsing.ParseCatalog(ruleFiles);
}

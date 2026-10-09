using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Simulation;

/// <summary>Pure entry point: no I/O, no clock, no randomness. Same build + state + action ⇒ same resolution.</summary>
public static class ActionResolution
{
    public static readonly Seconds DefaultWait = Seconds.From(5m);

    /// <summary>
    /// Fresh spawn: no buffs, an undebuffed pack, nothing on the ground. There is no ability energy to fill —
    /// abilities are always available (ADRs D21).
    /// </summary>
    public static GameState CreateInitialState() =>
        new(0, Seconds.From(0m), [], new TargetState(EnemyTier.Minor, []), []);

    public static Resolution ResolveAction(ValidatedBuild build, GameState state, PlayerAction action)
    {
        var stepped = state with { Step = state.Step + 1 };
        var opening = OpenAction(build, stepped, action);
        var seed = new Cascade(opening.State, [], opening.Notes, 0);
        var finished = opening.Events.Aggregate(seed, (acc, pending) => EventCascading.CascadeEvent(build, acc, pending, 0));
        var passives = ListActivePassives(build, finished.State);
        var available = ListAvailableActions(build, finished.State);
        return new Resolution(action, finished.State, finished.Fired, passives, available, finished.Notes, opening.Blocked);
    }

    /// <summary>Resolves a whole loop (FR-4): each action starts from the previous resolution's state.</summary>
    public static ImmutableArray<Resolution> ResolveSequence(ValidatedBuild build, GameState state, ImmutableArray<PlayerAction> actions)
    {
        var seed = (State: state, Resolutions: ImmutableArray<Resolution>.Empty);
        var result = actions.Aggregate(seed, (acc, action) =>
        {
            var resolution = ResolveAction(build, acc.State, action);
            return (resolution.State, acc.Resolutions.Add(resolution));
        });
        return result.Resolutions;
    }

    /// <summary>
    /// Every ability (always — no energy model, ADRs D21), every equipped weapon, the pickups on the ground and a wait.
    /// Abilities and weapons are listed against one enemy; a host may set any <see cref="TargetCount"/>.
    /// </summary>
    public static ImmutableArray<PlayerAction> ListAvailableActions(ValidatedBuild build, GameState state)
    {
        var offensive = new[] { OffensiveAbility.Grenade, OffensiveAbility.Melee, OffensiveAbility.Super }
            .SelectMany(kind => new PlayerAction[]
            {
                new PlayerAction.CastAbility(kind, HitOutcome.Kill, TargetCount.One),
                new PlayerAction.CastAbility(kind, HitOutcome.Damage, TargetCount.One),
            });
        var classAbility = new PlayerAction[] { new PlayerAction.UseClassAbility() };
        var weapons = build.Build.Weapons.SelectMany(weapon => new PlayerAction[]
        {
            new PlayerAction.FireWeapon(weapon.Slot, HitOutcome.Kill, TargetCount.One),
            new PlayerAction.FireWeapon(weapon.Slot, HitOutcome.Damage, TargetCount.One),
        });
        var pickups = state.Pickups.Where(p => p.Count > 0).Select(p => (PlayerAction)new PlayerAction.CollectPickups(p.Pickup));
        var wait = new PlayerAction[] { new PlayerAction.Wait(DefaultWait) };
        return [.. offensive, .. classAbility, .. weapons, .. pickups, .. wait];
    }

    public static ImmutableArray<ActivePassive> ListActivePassives(ValidatedBuild build, GameState state) =>
        build.Equipped
            .SelectMany(e => e.Element.Passives.Select(p => (e.Element, Passive: p)))
            .Where(x => x.Passive.When.IsSatisfiedBy(state))
            .Select(x => new ActivePassive(x.Element.Id, x.Element.Name, x.Element.Affinity, x.Passive))
            .ToImmutableArray();

    private sealed record Opening(GameState State, ImmutableArray<PendingEvent> Events, ImmutableArray<string> Notes, Optional<string> Blocked)
    {
        public Opening(GameState state, ImmutableArray<PendingEvent> events, ImmutableArray<string> notes)
            : this(state, events, notes, Optional.None<string>())
        {
        }
    }

    private static Opening BlockAction(GameState state, string reason) =>
        new(state, [], [reason], Optional.Some(reason));

    private static Opening OpenAction(ValidatedBuild build, GameState state, PlayerAction action) =>
        action.Match(
            cast => CastAbility(build, state, cast),
            _ => UseClassAbility(state),
            fire => FireWeapon(build, state, fire),
            collect => CollectPickups(state, collect),
            wait => Wait(state, wait));

    /// <summary>Never blocked: casting, then the strike on every target (ADRs D21, D22).</summary>
    private static Opening CastAbility(ValidatedBuild build, GameState state, PlayerAction.CastAbility cast)
    {
        var kind = cast.Kind.ToAbilityKind();
        var origin = new DamageOrigin.Ability(kind, build.Build.Subclass.ToDamageType());
        var strike = ListStrikeEvents(origin, cast.Hit, cast.Targets);
        return new Opening(state, [new PendingEvent.Ready(new GameEvent.AbilityCast(kind)), .. strike], []);
    }

    private static Opening UseClassAbility(GameState state) =>
        new(state, [new PendingEvent.Ready(new GameEvent.AbilityCast(AbilityKind.ClassAbility))], []);

    private static Opening FireWeapon(ValidatedBuild build, GameState state, PlayerAction.FireWeapon fire)
    {
        var weapon = build.Build.Weapons.Select(w => w.Slot == fire.Slot ? Optional.Some(w) : Optional.None<WeaponLoadout>()).FindFirstSome();
        return weapon.Match(
            some => new Opening(state, ListStrikeEvents(new DamageOrigin.Weapon(fire.Slot, some.Value.Type), fire.Hit, fire.Targets), []),
            _ => BlockAction(state, $"No weapon in the {fire.Slot} slot — nothing happens."));
    }

    /// <summary>
    /// One action against N enemies (ADRs D22): N per-enemy hits, then N kills for a kill — each cascades fully, so
    /// later hits see the debuffs earlier ones applied — then one <see cref="GameEvent.TargetsHit"/> for the
    /// multi-target triggers ("hit 3+ enemies").
    /// </summary>
    private static ImmutableArray<PendingEvent> ListStrikeEvents(DamageOrigin origin, HitOutcome hit, TargetCount targets)
    {
        var hits = Enumerable.Repeat<PendingEvent>(new PendingEvent.HitTarget(origin), targets.Value);
        var kills = hit == HitOutcome.Kill
            ? Enumerable.Repeat<PendingEvent>(new PendingEvent.KillTarget(origin), targets.Value)
            : [];
        return [.. hits, .. kills, new PendingEvent.Ready(new GameEvent.TargetsHit(origin, targets, hit))];
    }

    private static Opening CollectPickups(GameState state, PlayerAction.CollectPickups collect)
    {
        var count = state.CountPickups(collect.Pickup);
        if (count == 0)
        {
            return BlockAction(state, $"No {collect.Pickup} on the ground — nothing happens.");
        }

        var events = Enumerable.Repeat<PendingEvent>(new PendingEvent.Ready(new GameEvent.PickedUp(collect.Pickup)), count);
        return new Opening(state.ClearPickups(collect.Pickup), events.ToImmutableArray(), []);
    }

    /// <summary>Time passes: timed buffs and debuffs expire. Nothing recharges — ability energy isn't simulated (ADRs D21).</summary>
    private static Opening Wait(GameState state, PlayerAction.Wait wait)
    {
        var elapsed = wait.Duration.Value;
        var aged = state with
        {
            Clock = Seconds.From(state.Clock.Value + elapsed),
            Buffs = AgeStatuses(state.Buffs, elapsed),
            Target = state.Target with { Debuffs = AgeStatuses(state.Target.Debuffs, elapsed) },
        };
        return new Opening(aged, [], []);
    }

    private static ImmutableArray<ActiveStatus> AgeStatuses(ImmutableArray<ActiveStatus> statuses, decimal elapsed) =>
        statuses
            .Select(status => status.Remaining.Match(
                remaining => remaining.Value.Value > elapsed
                    ? Optional.Some(status with { Remaining = Optional.Some(Seconds.From(remaining.Value.Value - elapsed)) })
                    : Optional.None<ActiveStatus>(),
                _ => Optional.Some(status)))
            .SelectMany(kept => kept.Match(some => new[] { some.Value }, _ => []))
            .ToImmutableArray();
}

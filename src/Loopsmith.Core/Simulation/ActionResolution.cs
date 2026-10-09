using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Simulation;

/// <summary>Pure entry point: no I/O, no clock, no randomness. Same build + state + action ⇒ same resolution.</summary>
public static class ActionResolution
{
    public static readonly Seconds DefaultWait = Seconds.From(5m);

    private static readonly ImmutableArray<AbilityKind> AbilityOrder =
        [AbilityKind.Grenade, AbilityKind.Melee, AbilityKind.ClassAbility, AbilityKind.Super];

    /// <summary>Fresh spawn: grenade, melee and class ability charged; super empty.</summary>
    public static GameState CreateInitialState(ValidatedBuild build)
    {
        AbilityGauge CreateGauge(AbilityKind kind)
        {
            var max = CountMaxCharges(build, kind);
            return new AbilityGauge(kind, EnergyAmount.From(kind == AbilityKind.Super ? 0m : max), max);
        }

        var gauges = new AbilityGauges(
            CreateGauge(AbilityKind.Grenade), CreateGauge(AbilityKind.Melee), CreateGauge(AbilityKind.ClassAbility), CreateGauge(AbilityKind.Super));
        return new GameState(0, Seconds.From(0m), gauges, [], new TargetState(EnemyTier.Minor, []), []);
    }

    public static Resolution ResolveAction(ValidatedBuild build, GameState state, PlayerAction action)
    {
        var stepped = state with { Step = state.Step + 1 };
        var opening = OpenAction(build, stepped, action);
        var seed = new Cascade(opening.State, [], opening.Notes, 0);
        var finished = opening.Events.Aggregate(seed, (acc, pending) => EventCascading.CascadeEvent(build, acc, pending, 0));
        var passives = ListActivePassives(build, finished.State);
        var available = ListAvailableActions(build, finished.State);
        return new Resolution(action, finished.State, finished.Fired, passives, available, finished.Notes);
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

    public static ImmutableArray<PlayerAction> ListAvailableActions(ValidatedBuild build, GameState state)
    {
        var offensive = new[] { OffensiveAbility.Grenade, OffensiveAbility.Melee, OffensiveAbility.Super }
            .Where(kind => HasCharge(state, kind.ToAbilityKind()))
            .SelectMany(kind => new PlayerAction[]
            {
                new PlayerAction.CastAbility(kind, HitOutcome.Kill),
                new PlayerAction.CastAbility(kind, HitOutcome.Damage),
            });
        var classAbility = HasCharge(state, AbilityKind.ClassAbility)
            ? new PlayerAction[] { new PlayerAction.UseClassAbility() }
            : [];
        var weapons = build.Build.Weapons.SelectMany(weapon => new PlayerAction[]
        {
            new PlayerAction.FireWeapon(weapon.Slot, HitOutcome.Kill),
            new PlayerAction.FireWeapon(weapon.Slot, HitOutcome.Damage),
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

    private sealed record Opening(GameState State, ImmutableArray<PendingEvent> Events, ImmutableArray<string> Notes);

    private static Opening OpenAction(ValidatedBuild build, GameState state, PlayerAction action) =>
        action.Match(
            cast => CastAbility(build, state, cast),
            _ => UseClassAbility(state),
            fire => FireWeapon(build, state, fire),
            collect => CollectPickups(state, collect),
            wait => Wait(build, state, wait));

    private static Opening CastAbility(ValidatedBuild build, GameState state, PlayerAction.CastAbility cast)
    {
        var kind = cast.Kind.ToAbilityKind();
        if (!HasCharge(state, kind))
        {
            return new Opening(state, [], [$"Not enough {kind} energy — nothing happens."]);
        }

        var paid = SpendCharge(state, kind);
        var origin = new DamageOrigin.Ability(kind, build.Build.Subclass.ToDamageType());
        var events = ImmutableArray.Create<PendingEvent>(
            new PendingEvent.Ready(new GameEvent.AbilityCast(kind)),
            new PendingEvent.HitTarget(origin));
        return new Opening(paid, cast.Hit == HitOutcome.Kill ? events.Add(new PendingEvent.KillTarget(origin)) : events, []);
    }

    private static Opening UseClassAbility(GameState state)
    {
        if (!HasCharge(state, AbilityKind.ClassAbility))
        {
            return new Opening(state, [], ["Not enough ClassAbility energy — nothing happens."]);
        }

        var paid = SpendCharge(state, AbilityKind.ClassAbility);
        return new Opening(paid, [new PendingEvent.Ready(new GameEvent.AbilityCast(AbilityKind.ClassAbility))], []);
    }

    private static Opening FireWeapon(ValidatedBuild build, GameState state, PlayerAction.FireWeapon fire)
    {
        var weapon = build.Build.Weapons.Select(w => w.Slot == fire.Slot ? Optional.Some(w) : Optional.None<WeaponLoadout>()).FindFirstSome();
        return weapon.Match(
            some =>
            {
                var origin = new DamageOrigin.Weapon(fire.Slot, some.Value.Type);
                var hit = ImmutableArray.Create<PendingEvent>(new PendingEvent.HitTarget(origin));
                return new Opening(state, fire.Hit == HitOutcome.Kill ? hit.Add(new PendingEvent.KillTarget(origin)) : hit, []);
            },
            _ => new Opening(state, [], [$"No weapon in the {fire.Slot} slot — nothing happens."]));
    }

    private static Opening CollectPickups(GameState state, PlayerAction.CollectPickups collect)
    {
        var count = state.CountPickups(collect.Pickup);
        if (count == 0)
        {
            return new Opening(state, [], [$"No {collect.Pickup} on the ground — nothing happens."]);
        }

        var events = Enumerable.Repeat<PendingEvent>(new PendingEvent.Ready(new GameEvent.PickedUp(collect.Pickup)), count);
        return new Opening(state.ClearPickups(collect.Pickup), events.ToImmutableArray(), []);
    }

    private static Opening Wait(ValidatedBuild build, GameState state, PlayerAction.Wait wait)
    {
        var elapsed = wait.Duration.Value;
        var aged = state with
        {
            Clock = Seconds.From(state.Clock.Value + elapsed),
            Buffs = AgeStatuses(state.Buffs, elapsed),
            Target = state.Target with { Debuffs = AgeStatuses(state.Target.Debuffs, elapsed) },
        };
        var regen = AbilityOrder
            .Where(kind => kind != AbilityKind.Super)
            .Select(kind => (Kind: kind, Cooldown: ReadBaseCooldown(build, kind)))
            .ToImmutableArray();
        var regenerated = regen
            .Where(x => x.Cooldown.Value.IsSome())
            .Aggregate(aged, (acc, x) => acc.SetEnergy(
                x.Kind,
                acc.ReadGauge(x.Kind).Energy.Value + (elapsed / x.Cooldown.Value.UnwrapOr(1m))));
        var unknown = regen.Where(x => !x.Cooldown.Value.IsSome()).Select(x => x.Kind.ToString()).ToImmutableArray();
        var notes = unknown.IsEmpty
            ? ImmutableArray<string>.Empty
            : [$"Base cooldown unknown for {string.Join(", ", unknown)} — no passive regen applied (import the Compendium)."];
        return new Opening(regenerated, [], notes);
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

    private static ResolvedValue ReadBaseCooldown(ValidatedBuild build, AbilityKind kind)
    {
        var resolved = build.FindAbilityProfile(kind)
            .Map(p => p.BaseCooldownSeconds.ResolveForCopies(1))
            .UnwrapOr(new ResolvedValue(Optional.None<decimal>(), Certainty.Unknown));
        return resolved.Value.Match(some => some.Value > 0m, _ => false)
            ? resolved
            : new ResolvedValue(Optional.None<decimal>(), Certainty.Unknown);
    }

    private static int CountMaxCharges(ValidatedBuild build, AbilityKind kind)
    {
        var baseCharges = build.Equipped
            .Select(e => e.Element.Ability)
            .Select(ability => ability.Match(some => some.Value.Kind == kind ? some.Value.Charges : 0, _ => 0))
            .DefaultIfEmpty(0)
            .Max();
        var extra = build.Equipped
            .SelectMany(e => e.Element.Passives)
            .Where(p => p.When.IsEmpty)
            .Sum(p => p.Effect is Passive.ExtraCharges charges && charges.Ability == kind ? charges.Extra : 0);
        return Math.Max(1, baseCharges) + extra;
    }

    private static bool HasCharge(GameState state, AbilityKind kind) =>
        state.ReadGauge(kind).Energy.Value >= 1m;

    private static GameState SpendCharge(GameState state, AbilityKind kind) =>
        state.SetEnergy(kind, state.ReadGauge(kind).Energy.Value - 1m);
}

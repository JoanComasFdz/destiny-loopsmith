using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Simulation;

/// <summary>Pure entry point: no I/O, no randomness. Same build + state + step ⇒ same resolution.</summary>
public static class ActionResolution
{
    /// <summary>Fresh spawn: no buffs, a clean pack, nothing on the ground (abilities are always available, ADRs D5).</summary>
    public static GameState CreateInitialState() =>
        new(0, [], new TargetState(EnemyTier.Minor, []), []);

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

    /// <summary>Resolves a sequence of steps: each starts from the previous resolution's state.</summary>
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
    /// Every ability (always, ADRs D5), every equipped weapon, the pickups on the ground and the states the player can
    /// declare now (<see cref="ListDeclarations"/>). Abilities and weapons are listed against one enemy; a host may set
    /// any <see cref="TargetCount"/>.
    /// </summary>
    public static ImmutableArray<PlayerAction> ListAvailableActions(ValidatedBuild build, GameState state)
    {
        var offensive = new[] { OffensiveAbility.Grenade, OffensiveAbility.Melee, OffensiveAbility.Super }
            .SelectMany(kind => new PlayerAction[]
            {
                new PlayerAction.CastAbility(kind, HitOutcome.Kill, TargetCount.One),
                new PlayerAction.CastAbility(kind, HitOutcome.Damage, TargetCount.One),
            });
        var classAbility = ListClassAbilityActions(build);
        var weapons = build.Build.Weapons.SelectMany(weapon => new PlayerAction[]
        {
            new PlayerAction.FireWeapon(weapon.Slot, HitOutcome.Kill, TargetCount.One),
            new PlayerAction.FireWeapon(weapon.Slot, HitOutcome.Damage, TargetCount.One),
        });
        var pickups = state.Pickups.Select(pickup => (PlayerAction)new PlayerAction.CollectPickups(pickup));
        var declarations = ListDeclarations(build, state).Select(declaration => (PlayerAction)new PlayerAction.Declare(declaration));
        return [.. offensive, .. classAbility, .. weapons, .. pickups, .. declarations];
    }

    /// <summary>
    /// The declarations that hold now (ADRs D3): a new pack (always, ADRs D7), <c>max:</c> for each active buff that
    /// stacks and isn't at max yet, <c>end:</c> for each active buff and each debuff on the pack.
    /// </summary>
    public static ImmutableArray<StateDeclaration> ListDeclarations(ValidatedBuild build, GameState state)
    {
        var glossary = build.Catalog.Glossary;
        var maxes = state.Buffs
            .Where(buff => glossary.IsStacking(buff.Status) && !buff.AtMax)
            .Select(buff => (StateDeclaration)new StateDeclaration.ReachMax(buff.Status));
        var ends = state.Buffs.Select(buff => buff.Status).Concat(state.Target.Debuffs)
            .Select(status => (StateDeclaration)new StateDeclaration.EndStatus(status));
        return [new StateDeclaration.NewPack(), .. maxes, .. ends];
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
            use => UseClassAbility(state, use.Airborne),
            fire => FireWeapon(build, state, fire),
            collect => CollectPickups(state, collect),
            declare => Declare(build, state, declare.Declaration));

    /// <summary>Never blocked: casting, then the strike on every target (ADRs D4, D5).</summary>
    private static Opening CastAbility(ValidatedBuild build, GameState state, PlayerAction.CastAbility cast)
    {
        var kind = cast.Kind.ToAbilityKind();
        var origin = new DamageOrigin.Ability(kind, build.Build.Subclass.ToDamageType());
        var strike = ListStrikeEvents(origin, cast.Hit, cast.Targets);
        return new Opening(state, [new PendingEvent.Ready(new GameEvent.AbilityCast(kind)), .. strike], []);
    }

    /// <summary>On the ground or in the air, the class ability is cast: every class ability rule fires either way.</summary>
    private static Opening UseClassAbility(GameState state, bool airborne) =>
        new(state, [new PendingEvent.Ready(new GameEvent.AbilityCast(AbilityKind.ClassAbility, airborne))], []);

    /// <summary>The class ability, plus its airborne use when an equipped rule reacts only to that (Ascension).</summary>
    public static ImmutableArray<PlayerAction> ListClassAbilityActions(ValidatedBuild build) =>
        build.Equipped.Any(equipped => equipped.Element.Rules.Any(rule => rule.On is Trigger.AbilityCast { Airborne: true }))
            ? [new PlayerAction.UseClassAbility(), new PlayerAction.UseClassAbility(Airborne: true)]
            : [new PlayerAction.UseClassAbility()];

    private static Opening FireWeapon(ValidatedBuild build, GameState state, PlayerAction.FireWeapon fire)
    {
        var weapon = build.Build.Weapons.Select(w => w.Slot == fire.Slot ? Optional.Some(w) : Optional.None<WeaponLoadout>()).FindFirstSome();
        return weapon.Match(
            some => new Opening(state, ListStrikeEvents(new DamageOrigin.Weapon(fire.Slot, some.Value.Type), fire.Hit, fire.Targets), []),
            _ => BlockAction(state, $"No weapon in the {fire.Slot} slot — nothing happens."));
    }

    /// <summary>
    /// One action against N enemies (ADRs D4): N per-enemy hits, then N kills for a kill — each cascades fully, so
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

    /// <summary>Picks the pickup up: one <see cref="GameEvent.PickedUp"/>, and it is no longer on the ground.</summary>
    private static Opening CollectPickups(GameState state, PlayerAction.CollectPickups collect) =>
        state.HasPickup(collect.Pickup)
            ? new Opening(state.DropPickup(collect.Pickup), [new PendingEvent.Ready(new GameEvent.PickedUp(collect.Pickup))], [])
            : BlockAction(state, $"No {collect.Pickup} on the ground — nothing happens.");

    private static Opening Declare(ValidatedBuild build, GameState state, StateDeclaration declaration) =>
        declaration.Match(
            max => DeclareMax(build.Catalog.Glossary, state, max.Status),
            end => DeclareEnd(build.Catalog.Glossary, state, end.Status),
            _ => new Opening(state.ReplacePack(), [], []));   // always holds: the next enemies are a new pack

    /// <summary>
    /// "Bolt Charge at max" (ADRs D3): the buff is declared at its maximum and <see cref="GameEvent.StacksMaxed"/>
    /// cascades. Blocked unless the buff stacks, is active and isn't declared at max already — checked in that order.
    /// </summary>
    private static Opening DeclareMax(KeywordGlossary glossary, GameState state, StatusId status)
    {
        var blocker = glossary.FindStatus(status).Match(
            some => FindMaxBlocker(glossary, state, some.Value),
            _ => Optional.Some($"No buff '{status}' in the rules — nothing to declare."));
        return blocker.Match(
            reason => BlockAction(state, reason.Value),
            _ => new Opening(state.DeclareAtMax(status), [new PendingEvent.Ready(new GameEvent.StacksMaxed(status))], []));
    }

    private static Optional<string> FindMaxBlocker(KeywordGlossary glossary, GameState state, StatusDefinition definition) =>
        definition switch
        {
            { Kind: KeywordKind.Debuff } => Optional.Some($"{definition.Name} is a debuff — only a buff on you can be at max."),
            _ when !glossary.IsStacking(definition.Id) => Optional.Some($"{definition.Name} doesn't stack — end it with end:{definition.Id}."),
            _ when !state.HasBuff(definition.Id) => Optional.Some($"{definition.Name} isn't active — nothing to declare at max."),
            _ when state.IsAtMax(definition.Id) => Optional.Some($"{definition.Name} is already at max."),
            _ => Optional.None<string>(),
        };

    /// <summary>"Amplified ends", "Jolt ends": the buff (with its declaration) or the debuff is removed; no event.</summary>
    private static Opening DeclareEnd(KeywordGlossary glossary, GameState state, StatusId status) =>
        glossary.FindStatus(status).Match(
            some => EndStatus(state, some.Value),
            _ => BlockAction(state, $"No buff or debuff '{status}' in the rules — nothing ends."));

    private static Opening EndStatus(GameState state, StatusDefinition definition) =>
        (definition.Kind, state.HasBuff(definition.Id), state.TargetHas(definition.Id)) switch
        {
            (KeywordKind.Buff, true, _) => new Opening(state.DropBuff(definition.Id), [], []),
            (KeywordKind.Debuff, _, true) => new Opening(state.DropDebuff(definition.Id), [], []),
            _ => BlockAction(state, $"{definition.Name} isn't active — nothing ends."),
        };
}

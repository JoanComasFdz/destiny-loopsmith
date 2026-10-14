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
        new(0, [], new TargetState(EnemyTier.Minor, []), [], Optional.None<AbilityKind>(), []);

    public static Resolution ResolveAction(ValidatedBuild build, GameState state, PlayerAction action)
    {
        var stepped = state with { Step = state.Step + 1, Casting = ReadCast(action) };
        var opening = OpenAction(build, stepped, action);
        var seed = new Cascade(opening.State, [], opening.Notes, 0);
        var finished = opening.Events.Aggregate(seed, (acc, pending) => EventCascading.CascadeEvent(build, acc, pending, 0, opening.ByChance));
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
    /// Every ability (always, ADRs D5) and each of its secondary uses a rule reacts to (<see cref="ListSecondaryInputs"/>:
    /// Tempest Strike's sliding melee, Ascension's air move), a finisher (always, <see cref="ListFinisherActions"/>), every
    /// equipped weapon, the pickups on the ground and the states the player can declare now (<see cref="ListDeclarations"/>).
    /// Abilities and weapons are listed against one enemy; a host may set any <see cref="TargetCount"/>.
    /// </summary>
    public static ImmutableArray<PlayerAction> ListAvailableActions(ValidatedBuild build, GameState state)
    {
        var offensive = new[] { OffensiveAbility.Grenade, OffensiveAbility.Melee, OffensiveAbility.Super }
            .SelectMany(kind => ListCastActions(build, kind));
        var classAbility = ListClassAbilityActions(build);
        var finisher = ListFinisherActions();
        var weapons = build.Build.Weapons.SelectMany(weapon => new PlayerAction[]
        {
            new PlayerAction.FireWeapon(weapon.Slot, HitOutcome.Kill, TargetCount.One),
            new PlayerAction.FireWeapon(weapon.Slot, HitOutcome.Damage, TargetCount.One),
        });
        var pickups = state.Pickups.Select(pickup => (PlayerAction)new PlayerAction.CollectPickups(pickup));
        var declarations = ListDeclarations(build, state).Select(declaration => (PlayerAction)new PlayerAction.Declare(declaration));
        return [.. offensive, .. classAbility, .. finisher, .. weapons, .. pickups, .. declarations];
    }

    /// <summary>
    /// The declarations that hold now (ADRs D3): a new pack (always, ADRs D7), critical health when an equipped rule reacts
    /// to it (<see cref="ListCriticalHealthDeclarations"/>), <c>max:</c> for each active buff that stacks and isn't at max
    /// yet, <c>end:</c> for each active buff and each debuff on the pack.
    /// </summary>
    public static ImmutableArray<StateDeclaration> ListDeclarations(ValidatedBuild build, GameState state)
    {
        var criticalHealth = ListCriticalHealthDeclarations(build);
        var glossary = build.Catalog.Glossary;
        var maxes = state.Buffs
            .Where(buff => glossary.IsStacking(buff.Status) && !buff.AtMax)
            .Select(buff => (StateDeclaration)new StateDeclaration.ReachMax(buff.Status));
        var ends = state.Buffs.Select(buff => buff.Status).Concat(state.Target.Debuffs)
            .Select(status => (StateDeclaration)new StateDeclaration.EndStatus(status));
        return [new StateDeclaration.NewPack(), .. criticalHealth, .. maxes, .. ends];
    }

    /// <summary>
    /// "Critical health" (<c>health:critical</c>), when an equipped rule reacts to it (<c>on: { criticalHealth: {} }</c>):
    /// it always holds, but in a build where nothing reacts to it, it sets nothing off, so it isn't offered.
    /// </summary>
    public static ImmutableArray<StateDeclaration> ListCriticalHealthDeclarations(ValidatedBuild build) =>
        build.Equipped.Any(equipped => equipped.Element.Rules.Any(rule => rule.On is Trigger.CriticalHealth))
            ? [new StateDeclaration.CriticalHealth()]
            : [];

    /// <summary>The ability a step casts (an offensive ability, the class ability), under way for the whole step; none for any other step.</summary>
    public static Optional<AbilityKind> ReadCast(PlayerAction action) =>
        action.Match(
            cast => Optional.Some(cast.Kind.ToAbilityKind()),
            _ => Optional.Some(AbilityKind.ClassAbility),
            _ => Optional.None<AbilityKind>(),
            _ => Optional.None<AbilityKind>(),
            _ => Optional.None<AbilityKind>(),
            _ => Optional.None<AbilityKind>());

    /// <summary>The passives whose guards hold now; a chance when they hold only by a counted buff's chance stacks (ADRs D33).</summary>
    public static ImmutableArray<ActivePassive> ListActivePassives(ValidatedBuild build, GameState state) =>
        build.Equipped
            .SelectMany(e => e.Element.Passives.Select(p => (e.Element, Passive: p, Guard: p.When.ReadGuard(state))))
            .Where(x => x.Guard != GuardResult.Fails)
            .Select(x => new ActivePassive(x.Element.Id, x.Element.Name, x.Element.Affinity, x.Passive,
                x.Guard == GuardResult.ByChance ? Likelihood.Chance : Likelihood.Always))
            .ToImmutableArray();

    /// <summary>A step's state, its own events, notes and why it is blocked; <see cref="ByChance"/>: its events happen only by chance (a pickup only chance dropped).</summary>
    private sealed record Opening(GameState State, ImmutableArray<PendingEvent> Events, ImmutableArray<string> Notes, Optional<string> Blocked, bool ByChance = false)
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
            use => UseClassAbility(state, use.Input),
            fire => FireWeapon(build, state, fire),
            collect => CollectPickups(state, collect),
            declare => Declare(build, state, declare.Declaration),
            _ => PerformFinisher(state));

    /// <summary>
    /// Never blocked: the finisher, then its hit and kill on one enemy of the pack. A finisher has no damage type and is
    /// no weapon or ability, so only "any source" kills and hits react to it.
    /// </summary>
    private static Opening PerformFinisher(GameState state)
    {
        var origin = new DamageOrigin.Finisher();
        return new Opening(state, [new PendingEvent.Ready(new GameEvent.Finisher()), new PendingEvent.HitTarget(origin), new PendingEvent.KillTarget(origin)], []);
    }

    /// <summary>Never blocked: casting, then the strike on every target (ADRs D4, D5).</summary>
    private static Opening CastAbility(ValidatedBuild build, GameState state, PlayerAction.CastAbility cast)
    {
        var kind = cast.Kind.ToAbilityKind();
        var origin = new DamageOrigin.Ability(kind, build.Build.Subclass.ToDamageType(), cast.Input);
        var strike = ListStrikeEvents(origin, cast.Hit, cast.Targets);
        return new Opening(state, [new PendingEvent.Ready(new GameEvent.AbilityCast(kind, cast.Input)), .. strike], []);
    }

    /// <summary>On the ground or with its air move, the class ability is cast: every class ability rule fires either way.</summary>
    private static Opening UseClassAbility(GameState state, AbilityInput input) =>
        new(state, [new PendingEvent.Ready(new GameEvent.AbilityCast(AbilityKind.ClassAbility, input))], []);

    /// <summary>The class ability, plus each secondary use an equipped rule reacts to (Ascension's air move).</summary>
    public static ImmutableArray<PlayerAction> ListClassAbilityActions(ValidatedBuild build) =>
    [
        new PlayerAction.UseClassAbility(),
        .. ListSecondaryInputs(build, AbilityKind.ClassAbility).Select(input => new PlayerAction.UseClassAbility(input)),
    ];

    /// <summary>
    /// The secondary inputs of an ability that an equipped rule reacts to — a cast trigger that names the input
    /// (<c>abilityCast: { ability: classAbility, input: air }</c>) or a hit or kill by that use (<c>via: melee:slide</c>) —
    /// in the order the game lists them. A secondary nothing reacts to isn't offered: it does what the ability does.
    /// </summary>
    public static ImmutableArray<AbilityInput> ListSecondaryInputs(ValidatedBuild build, AbilityKind kind) =>
        [.. ListSecondaryUses(build, kind).Select(use => use.Input)];

    /// <summary>As <see cref="ListSecondaryInputs"/>, each with the equipped elements whose rules name it (Tempest Strike, Ascension).</summary>
    public static ImmutableArray<SecondaryUse> ListSecondaryUses(ValidatedBuild build, AbilityKind kind)
    {
        var named = build.Equipped
            .SelectMany(equipped => equipped.Element.Rules
                .SelectMany(rule => rule.On.FindNamedInput().Match(use => new[] { use.Value }, _ => []))
                .Where(use => use.Kind == kind)
                .Select(use => (use.Input, equipped.Element.Id)))
            .ToImmutableArray();
        return
        [
            .. kind.ListSecondaryInputs()
                .Select(input => new SecondaryUse(kind, input, [.. named.Where(use => use.Input == input).Select(use => use.Id).Distinct()]))
                .Where(use => !use.NamedBy.IsEmpty),
        ];
    }

    /// <summary>An offensive ability's hit and kill against one enemy: its press, then each secondary use an equipped rule reacts to.</summary>
    public static ImmutableArray<PlayerAction> ListCastActions(ValidatedBuild build, OffensiveAbility kind) =>
    [
        .. ListSecondaryInputs(build, kind.ToAbilityKind()).Prepend(AbilityInput.Press).SelectMany(input => new PlayerAction[]
        {
            new PlayerAction.CastAbility(kind, HitOutcome.Kill, TargetCount.One, input),
            new PlayerAction.CastAbility(kind, HitOutcome.Damage, TargetCount.One, input),
        }),
    ];

    /// <summary>
    /// A finisher: always possible in the game, as an ability is (ADRs D5), so every build is offered one. It fires the
    /// rules that react to a finisher (<c>on: { finisher: {} }</c>), and its kill fires the <c>kill</c> rules from any
    /// source; in a build with neither, it sets nothing off.
    /// </summary>
    public static ImmutableArray<PlayerAction> ListFinisherActions() =>
        [new PlayerAction.PerformFinisher()];

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

    /// <summary>
    /// Picks the pickup up: one <see cref="GameEvent.PickedUp"/>, and it is no longer on the ground. One only chance rules
    /// dropped gives what it gives only by chance (ADRs D8).
    /// </summary>
    private static Opening CollectPickups(GameState state, PlayerAction.CollectPickups collect) =>
        state.HasPickup(collect.Pickup)
            ? new Opening(state.DropPickup(collect.Pickup), [new PendingEvent.Ready(new GameEvent.PickedUp(collect.Pickup))], [], Optional.None<string>(), state.HasOnlyByChance(collect.Pickup))
            : BlockAction(state, $"No {collect.Pickup} on the ground — nothing happens.");

    private static Opening Declare(ValidatedBuild build, GameState state, StateDeclaration declaration) =>
        declaration.Match(
            max => DeclareMax(build, state, max.Status),
            end => DeclareEnd(build.Catalog.Glossary, state, end.Status),
            _ => new Opening(state.ReplacePack(), [], []),   // always holds: the next enemies are a new pack
            _ => new Opening(state, [new PendingEvent.Ready(new GameEvent.CriticalHealth())], []));   // always holds: no damage is modelled (ADRs D1)

    /// <summary>
    /// "Bolt Charge at max" (ADRs D3): the buff is declared at its maximum and <see cref="GameEvent.StacksMaxed"/>
    /// cascades. Blocked unless the buff stacks, is active and isn't at max already — checked in that order. A counted
    /// buff (ADRs D33) then holds its cap in sure stacks: the player says the chance ones came, or more than the loop shows.
    /// </summary>
    private static Opening DeclareMax(ValidatedBuild build, GameState state, StatusId status)
    {
        var glossary = build.Catalog.Glossary;
        var blocker = glossary.FindStatus(status).Match(
            some => FindMaxBlocker(glossary, state, some.Value),
            _ => Optional.Some($"No buff '{status}' in the rules — nothing to declare."));
        return blocker.Match(
            reason => BlockAction(state, reason.Value),
            _ => new Opening(DeclareCountedMax(build, state.DeclareAtMax(status), status), [new PendingEvent.Ready(new GameEvent.StacksMaxed(status))], []));
    }

    /// <summary>A counted buff declared at max holds its cap in sure stacks, when the cap is known; any other buff is unchanged.</summary>
    private static GameState DeclareCountedMax(ValidatedBuild build, GameState state, StatusId status) =>
        (state.ReadTally(status), build.ReadStackCap(status)) switch
        {
            (Optional<StackTally>.Some tally, Optional<GameValue>.Some { Value: GameValue.Known cap }) => state.PutTally(
                status,
                new StackTally(StackCount.From((int)cap.Value), StackCount.From(0), tally.Value.Changes.Add(new StackChange(
                    state.Step, PreviousPass: false, Optional.None<ElementId>(), "you", Affinity.Neutral, new StackMove.DeclaredMax(),
                    StackCount.From((int)cap.Value), StackCount.From(0)))),
                atMax: true),
            _ => state,
        };

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
            (var kind, true, _) when kind.IsBuff() => new Opening(state.DropBuff(definition.Id), [], []),
            (KeywordKind.Debuff, _, true) => new Opening(state.DropDebuff(definition.Id), [], []),
            _ => BlockAction(state, $"{definition.Name} isn't active — nothing ends."),
        };
}

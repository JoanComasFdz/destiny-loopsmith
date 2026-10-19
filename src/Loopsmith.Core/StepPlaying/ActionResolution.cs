using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.StepPlaying;

/// <summary>Pure entry point: no I/O, no randomness. Same build + state + step ⇒ same resolution.</summary>
public static class ActionResolution
{
    /// <summary>Fresh spawn: no buffs, a clean pack, nothing on the ground (abilities are always available, ADRs D5).</summary>
    public static GameState CreateInitialState() =>
        new(0, [], new TargetState(EnemyTier.Minor, []), [], Optional.None<AbilityKind>());

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
    /// equipped weapon's hit, kill, reload, slide over an ammo brick and Ready step (always, as a finisher: ADRs D37), the pickups on the ground and the states the player can declare now (<see cref="ListDeclarations"/>).
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
            new PlayerAction.ReloadWeapon(weapon.Slot),
            new PlayerAction.SlideOverAmmo(weapon.Slot),
            new PlayerAction.ReadyWeapon(weapon.Slot),
        });
        var pickups = state.ListPickups().Select(pickup => (PlayerAction)new PlayerAction.CollectPickups(pickup));
        var declarations = ListDeclarations(build, state).Select(declaration => (PlayerAction)new PlayerAction.Declare(declaration));
        return [.. offensive, .. classAbility, .. finisher, .. weapons, .. pickups, .. declarations];
    }

    /// <summary>
    /// The declarations that hold now (ADRs D3): a new pack (always, ADRs D7), critical health or a hit at it when an
    /// equipped rule reacts to them (<see cref="ListCriticalHealthDeclarations"/>), <c>max:</c> for each active buff that stacks and isn't at max
    /// yet, <c>end:</c> for each active buff and each debuff on the pack.
    /// </summary>
    public static ImmutableArray<StateDeclaration> ListDeclarations(ValidatedBuild build, GameState state)
    {
        var criticalHealth = ListCriticalHealthDeclarations(build, state);
        var glossary = build.Catalog.Glossary;
        var maxes = state.Buffs
            .Where(buff => glossary.IsStacking(buff.Status) && !buff.AtMax)
            .Select(buff => (StateDeclaration)new StateDeclaration.ReachMax(buff.Status));
        var ends = state.Buffs.Select(buff => buff.Status).Concat(state.Target.Debuffs)
            .Select(status => (StateDeclaration)new StateDeclaration.EndStatus(status));
        return [new StateDeclaration.NewPack(), .. criticalHealth, .. maxes, .. ends];
    }

    /// <summary>
    /// "Critical health" (<c>health:critical</c>) while you aren't at it, when an equipped rule reacts to it or to a hit
    /// at it, or a rule or passive needs it (<c>has: critical-health</c>: Mechaneer's Tricksleeves); "Hit at critical health" (<c>health:hit</c>) while you are, when an equipped rule reacts to it
    /// (<c>on: { hitAtCriticalHealth: {} }</c>). In a build where nothing reacts to them they set nothing off, so they
    /// aren't offered; ending critical health is an <c>end:</c> like any buff's.
    /// </summary>
    public static ImmutableArray<StateDeclaration> ListCriticalHealthDeclarations(ValidatedBuild build, GameState state)
    {
        var rules = build.Equipped.SelectMany(equipped => equipped.Element.Rules).ToImmutableArray();
        var onReach = rules.Any(rule => rule.On is Trigger.CriticalHealth);
        var onHit = rules.Any(rule => rule.On is Trigger.HitAtCriticalHealth);
        var guarded = rules.Any(rule => rule.When.Any(condition => condition.IsNaming(DeclaredStatuses.CriticalHealth)))
            || build.Equipped.Any(equipped => equipped.Element.Passives.Any(passive => passive.IsSwitchedOnBy(DeclaredStatuses.CriticalHealth)));
        var atCritical = state.HasBuff(DeclaredStatuses.CriticalHealth);
        return (atCritical, onReach || onHit || guarded, onHit) switch
        {
            (false, true, _) => [new StateDeclaration.CriticalHealth()],
            (true, _, true) => [new StateDeclaration.HitAtCriticalHealth()],
            _ => [],
        };
    }

    /// <summary>
    /// Whether an equipped rule reacts to sliding over an ammo brick with this weapon in hand (Spark of Momentum): the step
    /// always holds, but with nothing reacting to it, it sets nothing off.
    /// </summary>
    public static bool IsAmmoSlideReactedTo(ValidatedBuild build, WeaponLoadout weapon)
    {
        var slid = new GameEvent.SlidOverAmmo(new DamageOrigin.Weapon(weapon.Slot, weapon.Type, weapon.Ammo, weapon.Archetype));
        var superType = build.ReadSuperType();
        return build.Equipped.Any(equipped => equipped.Element.Rules.Any(rule => rule.On.IsTriggeredBy(slid, superType)));
    }

    /// <summary>
    /// Whether an equipped rule reacts to readying this weapon (Mechaneer's Tricksleeves on a sidearm, ADRs D42): the step
    /// always holds, but with nothing reacting to it, it sets nothing off. A rule's guard isn't read: what it needs may come
    /// from the steps before (critical health).
    /// </summary>
    public static bool IsReadyReactedTo(ValidatedBuild build, WeaponLoadout weapon)
    {
        var readied = new GameEvent.WeaponReadied(new DamageOrigin.Weapon(weapon.Slot, weapon.Type, weapon.Ammo, weapon.Archetype));
        var superType = build.ReadSuperType();
        return build.Equipped.Any(equipped => equipped.Element.Rules.Any(rule => rule.On.IsTriggeredBy(readied, superType)));
    }

    /// <summary>The ability a step casts (an offensive ability, the class ability), under way for the whole step; none for any other step.</summary>
    public static Optional<AbilityKind> ReadCast(PlayerAction action) =>
        action.Match(
            cast => Optional.Some(cast.Kind.ToAbilityKind()),
            _ => Optional.Some(AbilityKind.ClassAbility),
            _ => Optional.None<AbilityKind>(),
            _ => Optional.None<AbilityKind>(),
            _ => Optional.None<AbilityKind>(),
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
            use => UseClassAbility(build, state, use),
            fire => FireWeapon(build, state, fire),
            collect => CollectPickups(state, collect),
            declare => Declare(build, state, declare.Declaration),
            _ => PerformFinisher(state),
            reload => ReloadWeapon(build, state, reload),
            slide => SlideOverAmmo(build, state, slide),
            ready => ReadyWeapon(build, state, ready));

    /// <summary>
    /// Never blocked: the finisher, then its hit and kill on one enemy of the pack. A finisher has no damage type and is
    /// no weapon or ability, so only "any source" kills and hits react to it.
    /// </summary>
    private static Opening PerformFinisher(GameState state)
    {
        var origin = new DamageOrigin.Finisher();
        return new Opening(state, [new PendingEvent.Ready(new GameEvent.Finisher()), new PendingEvent.HitTarget(origin, DamageSpan.Blow), new PendingEvent.KillTarget(origin)], []);
    }

    /// <summary>
    /// Never blocked: casting, its energy fully expended when the use spends the last charge (<see cref="ListExpendedEvents"/>),
    /// then the strike on every target (ADRs D4, D5).
    /// </summary>
    private static Opening CastAbility(ValidatedBuild build, GameState state, PlayerAction.CastAbility cast)
    {
        var kind = cast.Kind.ToAbilityKind();
        var origin = new DamageOrigin.Ability(kind, build.Build.Subclass.ToDamageType(), cast.Input);
        var strike = ListStrikeEvents(origin, cast.Hit, cast.Targets, ShieldBreak.None, HitZone.Body);
        return new Opening(state, [new PendingEvent.Ready(new GameEvent.AbilityCast(kind, cast.Input)), .. ListExpendedEvents(build, kind, cast.Charge), .. strike], []);
    }

    /// <summary>
    /// On the ground or with its air move, the class ability is cast: every class ability rule fires either way. Its energy
    /// is fully expended when the use spends the last charge (<see cref="ListExpendedEvents"/>).
    /// </summary>
    private static Opening UseClassAbility(ValidatedBuild build, GameState state, PlayerAction.UseClassAbility use) =>
        new(state, [new PendingEvent.Ready(new GameEvent.AbilityCast(AbilityKind.ClassAbility, use.Input)), .. ListExpendedEvents(build, AbilityKind.ClassAbility, use.Charge)], []);

    /// <summary>
    /// The grenade's, melee's or class ability's energy fully expended (ADRs D36): on every use when the build gives it one
    /// charge, on the use the player marks as the last one (<c>grenade:last</c>) when it gives more (Shinobu's Vow). A Super
    /// has no such trigger.
    /// </summary>
    private static ImmutableArray<PendingEvent> ListExpendedEvents(ValidatedBuild build, AbilityKind kind, ChargeUse charge) =>
        kind != AbilityKind.Super && (charge == ChargeUse.Last || build.CountCharges(kind) == ChargeCount.One)
            ? [new PendingEvent.Ready(new GameEvent.EnergyExpended(kind))]
            : [];

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
            some => new Opening(state, ListStrikeEvents(new DamageOrigin.Weapon(fire.Slot, some.Value.Type, some.Value.Ammo, some.Value.Archetype), fire.Hit, fire.Targets, fire.Shield, fire.Zone), []),
            _ => BlockAction(state, $"No weapon in the {fire.Slot} slot — nothing happens."));
    }

    /// <summary>
    /// The player reloads the weapon (ADRs D37): one reload event from it, so the rules on its reload fire. No ammo is
    /// counted, so a reload is never needed and never blocked but for an empty slot.
    /// </summary>
    private static Opening ReloadWeapon(ValidatedBuild build, GameState state, PlayerAction.ReloadWeapon reload)
    {
        var weapon = build.Build.Weapons.Select(w => w.Slot == reload.Slot ? Optional.Some(w) : Optional.None<WeaponLoadout>()).FindFirstSome();
        return weapon.Match(
            some => new Opening(state, [new PendingEvent.Ready(new GameEvent.Reloaded(new DamageOrigin.Weapon(reload.Slot, some.Value.Type, some.Value.Ammo, some.Value.Archetype)))], []),
            _ => BlockAction(state, $"No weapon in the {reload.Slot} slot — nothing happens."));
    }

    /// <summary>
    /// The player slides over an ammo brick with the weapon in hand: one event from that weapon, so the rules on it fire
    /// (Spark of Momentum). No ammo is counted, so whether the brick is of an ammo you aren't full on is the player's call;
    /// never blocked but for an empty slot.
    /// </summary>
    private static Opening SlideOverAmmo(ValidatedBuild build, GameState state, PlayerAction.SlideOverAmmo slide)
    {
        var weapon = build.Build.Weapons.Select(w => w.Slot == slide.Slot ? Optional.Some(w) : Optional.None<WeaponLoadout>()).FindFirstSome();
        return weapon.Match(
            some => new Opening(state, [new PendingEvent.Ready(new GameEvent.SlidOverAmmo(new DamageOrigin.Weapon(slide.Slot, some.Value.Type, some.Value.Ammo, some.Value.Archetype)))], []),
            _ => BlockAction(state, $"No weapon in the {slide.Slot} slot — nothing happens."));
    }

    /// <summary>
    /// The player readies (swaps to) a weapon: one event from that weapon, so the rules on it fire (Mechaneer's
    /// Tricksleeves). Which weapon is in hand isn't kept (ADRs D42); never blocked but for an empty slot.
    /// </summary>
    private static Opening ReadyWeapon(ValidatedBuild build, GameState state, PlayerAction.ReadyWeapon ready)
    {
        var weapon = build.Build.Weapons.Select(w => w.Slot == ready.Slot ? Optional.Some(w) : Optional.None<WeaponLoadout>()).FindFirstSome();
        return weapon.Match(
            some => new Opening(state, [new PendingEvent.Ready(new GameEvent.WeaponReadied(new DamageOrigin.Weapon(ready.Slot, some.Value.Type, some.Value.Ammo, some.Value.Archetype)))], []),
            _ => BlockAction(state, $"No weapon in the {ready.Slot} slot — nothing happens."));
    }

    /// <summary>
    /// One action against N enemies (ADRs D4): N per-enemy hits, then N kills for a kill — each cascades fully, so
    /// later hits see the debuffs earlier ones applied — then one <see cref="GameEvent.TargetsHit"/> for the
    /// multi-target triggers ("hit 3+ enemies"). A hit step's hits are sustained damage, a kill step's its killing blows
    /// (ADRs D34). A step the player says breaks a shield breaks one, with its first hit; a precision step's kills are all
    /// precision kills (ADRs D36).
    /// </summary>
    private static ImmutableArray<PendingEvent> ListStrikeEvents(DamageOrigin origin, HitOutcome hit, TargetCount targets, ShieldBreak shield, HitZone zone)
    {
        var hits = Enumerable.Repeat<PendingEvent>(new PendingEvent.HitTarget(origin, hit == HitOutcome.Kill ? DamageSpan.Blow : DamageSpan.Sustained), targets.Value).ToImmutableArray();
        var broken = shield == ShieldBreak.Breaks ? new PendingEvent[] { new PendingEvent.Ready(new GameEvent.ShieldBroken(origin)) } : [];
        var kills = hit == HitOutcome.Kill
            ? Enumerable.Repeat<PendingEvent>(new PendingEvent.KillTarget(origin, zone), targets.Value)
            : [];
        return [hits[0], .. broken, .. hits[1..], .. kills, new PendingEvent.Ready(new GameEvent.TargetsHit(origin, targets, hit, zone))];
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
            _ => DeclareCriticalHealth(state),
            _ => DeclareHitAtCriticalHealth(state));

    /// <summary>
    /// "Critical health" (ADRs D3): no damage is modelled (ADRs D1), so the player says their shields broke. It puts
    /// <see cref="DeclaredStatuses.CriticalHealth"/> on you, until the player declares it ended (their shields are full
    /// again), and <see cref="GameEvent.CriticalHealth"/> cascades. Blocked while already at critical health.
    /// </summary>
    private static Opening DeclareCriticalHealth(GameState state) =>
        state.HasBuff(DeclaredStatuses.CriticalHealth)
            ? BlockAction(state, $"Already at critical health — declare it ended first (end:{DeclaredStatuses.CriticalHealth}).")
            : new Opening(state.PutBuff(DeclaredStatuses.CriticalHealth), [new PendingEvent.Ready(new GameEvent.CriticalHealth())], []);

    /// <summary>
    /// "Hit at critical health" (ADRs D3): the player says they are damaged while at critical health, and
    /// <see cref="GameEvent.HitAtCriticalHealth"/> cascades. Blocked unless a step before it reached critical health.
    /// </summary>
    private static Opening DeclareHitAtCriticalHealth(GameState state) =>
        state.HasBuff(DeclaredStatuses.CriticalHealth)
            ? new Opening(state, [new PendingEvent.Ready(new GameEvent.HitAtCriticalHealth())], [])
            : BlockAction(state, "Not at critical health — declare Critical health (health:critical) first.");

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
                    state.Step, new StackChangeOrigin.Player(), new StackMove.DeclaredMax(), StackCount.From((int)cap.Value), StackCount.From(0)))),
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

    /// <summary>
    /// "Amplified ends", "Jolt ends": the buff (with its declaration, and the buffs that end with it) or the debuff is
    /// removed; no event.
    /// </summary>
    private static Opening DeclareEnd(KeywordGlossary glossary, GameState state, StatusId status) =>
        glossary.FindStatus(status).Match(
            some => EndStatus(glossary, state, some.Value),
            _ => BlockAction(state, $"No buff or debuff '{status}' in the rules — nothing ends."));

    private static Opening EndStatus(KeywordGlossary glossary, GameState state, StatusDefinition definition) =>
        (definition.Kind, state.HasBuff(definition.Id), state.TargetHas(definition.Id)) switch
        {
            (var kind, true, _) when kind.IsBuff() => new Opening(state.EndBuff(glossary, definition.Id), [], []),
            (KeywordKind.Debuff, _, true) => new Opening(state.DropDebuff(definition.Id), [], []),
            _ => BlockAction(state, $"{definition.Name} isn't active — nothing ends."),
        };
}

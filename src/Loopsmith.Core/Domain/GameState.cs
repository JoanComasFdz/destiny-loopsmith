using System.Collections.Immutable;
using Dunet;
using Loopsmith.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>
/// A buff on the player: present, and — for a status that stacks — whether it is at its maximum (declared with
/// <c>max:</c>, ADRs D3, or a counted buff's sure stacks at its cap). A counted buff (Armor Charge, ADRs D33) holds its
/// <see cref="Tally"/>; any other buff none: the engine counts nothing else (ADRs D1). <see cref="GivenBy"/>: the element
/// whose rule gave it last (the enhanced Stats for All on the weapon that shot, not the base one on another); none for a
/// counted buff (its tally says where each stack came from) or one the player declared.
/// </summary>
public sealed record ActiveBuff(StatusId Status, bool AtMax, Optional<StackTally> Tally, Optional<ElementId> GivenBy);

/// <summary>
/// The stacks of a counted buff (ADRs D33): those it surely holds after the steps so far, those it may hold besides
/// (gained only from chance rules, ADRs D8), and each change since it was last gained from none.
/// </summary>
public sealed record StackTally(StackCount Sure, StackCount Chance, ImmutableArray<StackChange> Changes);

/// <summary>One change to a counted buff: at which step of the pass, where it came from, what it did, and the stacks after it.</summary>
public sealed record StackChange(int Step, StackChangeOrigin Origin, StackMove Move, StackCount SureAfter, StackCount ChanceAfter);

/// <summary>Where a change to a counted buff came from: an element's rule, the player's declaration, or the pass before.</summary>
[Union]
public partial record StackChangeOrigin
{
    partial record Element(ElementId Id, string Name, Affinity Affinity);
    partial record Player();
    partial record PassBefore();
}

/// <summary>What a change did to a counted buff's stacks.</summary>
[Union]
public partial record StackMove
{
    partial record Gained(StackCount Sure, StackCount Chance, StackCount Wasted);   // Wasted: beyond the cap
    partial record Spent(StackCount Sure, StackCount Chance);
    partial record DeclaredMax();                                                   // the player said it is at its cap
    partial record CarriedOver();                                                   // what the pass before ended with
}

/// <summary>"The pack in front of you" (ADRs D7): one tier plus the debuffs present on it.</summary>
public sealed record TargetState(EnemyTier Tier, ImmutableArray<StatusId> Debuffs);

/// <summary>
/// Immutable snapshot of what is present — buffs on you, debuffs on the pack, pickups on the ground — and what the
/// player declared. It holds no number but the step and a counted buff's stacks (ADRs D1, D33); the StepPlaying slice
/// plays steps against it, (state, step) → (state, fired). Abilities are always available (ADRs D5).
/// <see cref="Casting"/> is the ability the step casts, under way right then (Flow State's dodge,
/// <see cref="Condition.During"/>); none for any other step.
/// </summary>
public sealed record GameState(
    int Step,
    ImmutableArray<ActiveBuff> Buffs,
    TargetState Target,
    ImmutableArray<GroundPickup> Pickups,
    Optional<AbilityKind> Casting);

/// <summary>
/// A pickup on the ground, and whether only chance rules dropped it (ADRs D8): what picking it up gives is then a chance
/// too. One rule that always fires dropping it too makes it sure.
/// </summary>
public sealed record GroundPickup(PickupId Pickup, Likelihood Likelihood);

/// <summary>What the player chooses to do next. Abilities and weapons say how many enemies they hit or kill (ADRs D4).</summary>
[Union]
public partial record PlayerAction
{
    partial record CastAbility(OffensiveAbility Kind, HitOutcome Hit, TargetCount Targets, AbilityInput Input = AbilityInput.Press, ChargeUse Charge = ChargeUse.Plain, EnemyTier Rank = EnemyTier.Minor);   // a secondary: melee:slide, grenade:hold; its last charge: grenade:last; a kill of a stronger enemy: grenade:kill:elite
    partial record UseClassAbility(AbilityInput Input = AbilityInput.Press, ChargeUse Charge = ChargeUse.Plain);   // a secondary: the air move that spends the charge (Ascension)
    partial record FireWeapon(WeaponSlot Slot, HitOutcome Hit, TargetCount Targets, ShieldBreak Shield = ShieldBreak.None, HitZone Zone = HitZone.Body, EnemyTier Rank = EnemyTier.Minor);   // breaking a combatant's shield: kinetic:kill:shield; precision kills: kinetic:kill:precision; a kill of a stronger enemy: kinetic:kill:elite
    partial record CollectPickups(PickupId Pickup);
    partial record Declare(StateDeclaration Declaration);   // a state only play decides (ADRs D3)
    partial record PerformFinisher(EnemyTier Rank = EnemyTier.Minor);   // a finisher on one enemy of the pack: it kills it; of a stronger one: finisher:elite
    partial record ReloadWeapon(WeaponSlot Slot);           // the player reloads that weapon: no ammo is counted (ADRs D37)
    partial record PickUpAmmo(AmmoBrick Brick, BrickPickup Pickup = BrickPickup.Plain);   // the player picks up an ammo brick, or slides over it (ammo:heavy:slide): no ammo is counted (ADRs D37, D43,
    partial record ReadyWeapon(WeaponSlot Slot);            // the player readies (swaps to) that weapon: which weapon is in hand isn't kept (ADRs D44)
}

/// <summary>
/// What the player declares as a step of its own (ADRs D3): <c>max:&lt;status&gt;</c>, <c>end:&lt;status&gt;</c>,
/// <c>pack:new</c>, <c>health:critical</c>, <c>health:hit</c>.
/// </summary>
[Union]
public partial record StateDeclaration
{
    partial record ReachMax(StatusId Status);    // a stacking buff on you is at its maximum
    partial record EndStatus(StatusId Status);   // a buff on you, or a debuff on the pack, has ended
    partial record NewPack();                    // the next enemies are a new pack, with no debuffs (ADRs D7)
    partial record CriticalHealth();             // your shields broke: you are at critical health until you declare it ended (no damage is modelled, ADRs D1)
    partial record HitAtCriticalHealth();        // you are damaged while at critical health (no damage is modelled, ADRs D1)
}

/// <summary>
/// The statuses the engine itself puts on you (ADRs D3): reaching critical health (<c>health:critical</c>) puts
/// <see cref="CriticalHealth"/> on you, until the player declares it ended (<c>end:critical-health</c>: your shields are
/// full again); being damaged at critical health (<c>health:hit</c>) needs it. The glossary names it.
/// </summary>
public static class DeclaredStatuses
{
    public static readonly StatusId CriticalHealth = StatusId.From("critical-health");
}

/// <summary>A concrete thing that happened — matched against <see cref="Trigger"/> patterns.</summary>
[Union]
public partial record GameEvent
{
    partial record AbilityCast(AbilityKind Kind, AbilityInput Input = AbilityInput.Press);
    partial record Damaged(DamageOrigin Origin, EnemyTier Tier, ImmutableArray<StatusId> TargetHas, DamageSpan Span = DamageSpan.Blow);   // Span: a hit step's is sustained (ADRs D34)
    partial record Killed(DamageOrigin Origin, EnemyTier Tier, ImmutableArray<StatusId> TargetHas, HitZone Zone = HitZone.Body);   // Zone: a precision kill the player says (ADRs D36)
    partial record TargetsHit(DamageOrigin Origin, TargetCount Targets, HitOutcome Hit, HitZone Zone = HitZone.Body);   // once per action, after its per-enemy events
    partial record PickedUp(PickupId Pickup);
    partial record BuffGained(StatusId Status);   // a grant of the buff (every grant, for a status that stacks)
    partial record StacksMaxed(StatusId Status);  // only from a declared max: step
    partial record Finisher();                    // before the finisher's hit and kill (origin DamageOrigin.Finisher)
    partial record CriticalHealth();              // only from a declared health:critical step
    partial record ShieldBroken(DamageOrigin Origin);   // a weapon step the player says breaks a combatant's shield (kinetic:kill:shield), after its first hit
    partial record EnergyExpended(AbilityKind Kind);    // a use that spends the ability's last charge: every use with one charge, a grenade:last with more (ADRs D36)
    partial record Reloaded(DamageOrigin Origin);       // a weapon reloaded: a Reload step, or an outcome that reloads it (ADRs D37)
    partial record HitAtCriticalHealth();         // only from a declared health:hit step, while at critical health
    partial record AmmoPickedUp(AmmoBrick Brick, BrickPickup Pickup);   // only from a declared ammo brick pickup (ADRs D43,
    partial record WeaponReadied(DamageOrigin Origin);  // only from a declared Ready step (ADRs D44)
}

/// <summary>
/// An outcome as it was applied, with how far to trust it (an energy grant only explains — ADRs D5). <see cref="Spend"/>:
/// the stacks of a counted buff it spent, and what a table gives for them (ADRs D33).
/// </summary>
public sealed record AppliedOutcome(Outcome Outcome, Certainty Certainty, Optional<string> Caveat, Optional<StackSpend> Spend);

/// <summary>
/// The stacks of a counted buff an outcome spent — sure and chance ones — and, for an outcome whose value goes by them
/// (a Kickstart's energy, Bulwark Finisher's duration), the value for the sure stacks and the value with the chance ones
/// too (ADRs D33). None of them when the stacks spent are outside the source's table.
/// </summary>
public sealed record StackSpend(StatusId Status, StackCount Sure, StackCount Chance, Optional<GameValue> AtSure, Optional<GameValue> WithChance);

/// <summary>
/// One bullet of a build note: source → trigger → outcomes (reason), at cascade depth.
/// <see cref="EventIndex"/> numbers the events of a step, so bullets of the same event can be grouped.
/// <see cref="NotStackedWith"/> names the element this rule gave way to (<see cref="Rule.DoesNotStackWith"/>): it
/// matched, but applied nothing, so <see cref="Outcomes"/> is empty. <see cref="On"/> and <see cref="When"/> are the
/// rule's own trigger and guards: what it needed (a debuffed target, a buff, a declared maximum).
/// <see cref="CausedBy"/> is the place, among the step's fired rules, of the rule whose outcome raised the event this
/// one fired on; none for the step's own events (the cast, the hit, the pickup, the declaration).
/// </summary>
public sealed record FiredRule(
    ElementId Source,
    string SourceName,
    ElementKind SourceKind,
    Affinity Affinity,
    GameEvent Trigger,
    ImmutableArray<AppliedOutcome> Outcomes,
    Optional<string> Reason,
    Likelihood Likelihood,
    int Depth,
    int EventIndex,
    Optional<string> NotStackedWith,
    Trigger On,
    ImmutableArray<Condition> When,
    Optional<int> CausedBy);

/// <summary>
/// A passive whose conditions hold in the current state (shown next to the state, not as a bullet); a chance when they
/// hold only by a counted buff's chance stacks (ADRs D33).
/// </summary>
public sealed record ActivePassive(ElementId Source, string SourceName, Affinity Affinity, PassiveRule Passive, Likelihood Likelihood);

/// <summary>
/// Result of one step: new state, ordered fired rules, and what the player can do now.
/// <see cref="Blocked"/> holds the reason when the step could not happen at all (nothing to pick up, no weapon in
/// that slot, a declaration that doesn't hold) — abilities are never blocked.
/// </summary>
public sealed record Resolution(
    PlayerAction Action,
    GameState State,
    ImmutableArray<FiredRule> Fired,
    ImmutableArray<ActivePassive> ActivePassives,
    ImmutableArray<PlayerAction> NowAvailable,
    ImmutableArray<string> Notes,
    Optional<string> Blocked);

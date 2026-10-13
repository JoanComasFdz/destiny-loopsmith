using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>What fires a rule. Each case carries exactly its own data (no "tag + optionals").</summary>
[Union]
public partial record Trigger
{
    partial record AbilityCast(AbilityKind Kind);                          // any use of the ability, its secondary ones too
    partial record KillAny(DamageSource Via);
    partial record KillOfTier(DamageSource Via, EnemyTier Tier);
    partial record KillDebuffed(DamageSource Via, ImmutableArray<StatusId> TargetHas);
    partial record KillMultiple(DamageSource Via, TargetCount AtLeast);        // kill at least N enemies in one action
    partial record Damage(DamageSource Via);
    partial record DamageDebuffed(DamageSource Via, ImmutableArray<StatusId> TargetHas);
    partial record DamageMultiple(DamageSource Via, TargetCount AtLeast);      // hit at least N enemies in one action
    partial record PickUp(PickupId Pickup);
    partial record BuffGained(StatusId Status);
    partial record StacksMaxed(StatusId Status);
    partial record Finisher();                                                 // the player does a finisher on an enemy of the pack
    partial record AbilityCastAs(AbilityKind Kind, AbilityInput Input);        // only that use of the ability: the melee while sliding, the class ability's air move
}

/// <summary>A guard on the current state, checked after the trigger matched.</summary>
[Union]
public partial record Condition
{
    partial record HasBuff(StatusId Status);
    partial record LacksBuff(StatusId Status);
    partial record TargetHas(StatusId Status);
    partial record AtMax(StatusId Status);   // the player declared this buff at its maximum (ADRs D3)
    partial record During(AbilityKind Ability);   // the step casts this ability: it is under way at the step's end (Flow State's dodge)
}

[Union]
public partial record EnergyGrant
{
    partial record Fraction(GameValue Amount);   // of one charge: 0.15 = 15 %
    partial record Full();
}

/// <summary>
/// The game's consequences. ("Effect" is reserved for side effects described as data — see Orchestration.)
/// Energy outcomes (<see cref="GrantEnergy"/>, <see cref="ConvertStacksToEnergy"/>'s energy, <see cref="ResetCooldown"/>,
/// <see cref="GrantLeastChargedEnergy"/>), <see cref="ModifyDamage"/> and <see cref="RestoreHealth"/> are facts shown with
/// their value (ADRs D1, D5).
/// </summary>
[Union]
public partial record Outcome
{
    partial record GrantEnergy(AbilityKind To, EnergyGrant Amount);
    partial record ConvertStacksToEnergy(StatusId Consumed, AbilityKind To, GameValue PerStack);
    partial record ApplyBuff(StatusId Status, Optional<Seconds> Duration, StackCount Stacks);   // Stacks: the grant, a fact ("+1 Bolt Charge")
    partial record RemoveBuff(StatusId Status);
    partial record DebuffTarget(StatusId Status, Optional<Seconds> Duration);
    partial record Spawn(PickupId Pickup, int Count);
    partial record SpawnSummon(SummonId Summon, int Count);
    partial record StrikeTarget(StatusId Via, HitOutcome Hit);   // keyword damage: lightning strike, jolt chain…
    partial record ModifyDamage(DamageSource Against, GameValue Change);
    partial record RestoreHealth(GameValue Amount, bool IncludesAllies);
    partial record ResetCooldown(AbilityKind Which);
    partial record Collect(PickupId Pickup);   // picks it up from the ground when it is there (Powerful Attraction): a pickup event
    partial record GrantLeastChargedEnergy(EnergyGrant Amount);   // to whichever ability has the least energy: the game picks, so a fact
}

/// <summary>
/// "When <see cref="On"/> happens and every <see cref="When"/> holds, then <see cref="Then"/>."
/// <see cref="DoesNotStackWith"/>: when a rule of one of these elements fires on the same event, this rule gives
/// nothing — the game doesn't stack the two (Tempest Strike's Bolt Charge with Dielectric's).
/// </summary>
public sealed record Rule(
    Trigger On,
    ImmutableArray<Condition> When,
    ImmutableArray<Outcome> Then,
    Optional<string> Reason,
    Likelihood Likelihood,
    ImmutableArray<ElementId> DoesNotStackWith);

public sealed record WeaponStatChange(string Stat, GameValue Change);

/// <summary>Always-on modifiers while equipped (and while <see cref="PassiveRule.When"/> holds).</summary>
[Union]
public partial record Passive
{
    partial record ExtraStacks(StatusId Status, StackCount Extra);           // Spark of Frequency: a caveat on each grant
    partial record ExtraCharges(AbilityKind Ability, int Extra);            // a fact (ADRs D5)
    partial record ModifyDamage(DamageSource Against, GameValue Change);     // Flashover, stat bonuses
    partial record ResistDamage(PveAndPvp Amount);                           // Spark of Resistance: any damage, "25% [10%]"
    partial record ModifyWeaponStats(ImmutableArray<string> Archetypes, ImmutableArray<WeaponStatChange> Changes);
    partial record RaiseMaxStacks(StatusId Status, GameValue Max);           // Charged Up: the cap with it equipped, per copy ("4 | 5 | 6"), a fact (ADRs D3)
    partial record ModifyStat(StatKind Stat, GameValue Change);              // Grenade Font: "+20 Grenade" while it holds, a fact never added to the stats (ADRs D1)
    partial record ResistCombatantDamage(CombatantDamage Against, GameValue Amount);   // Arc Resistance: combatants only, nothing in PvP
}

/// <summary>
/// The combatants' damage a resistance reduces (PvE only): all of it (the Guardian's base), one damage type (Arc
/// Resistance), the Super's element (Harmonic Resistance), from within or beyond a range (Melee Damage Resistance: 5 m,
/// Sniper Damage Resistance: 30 m), area, blast and explosive damage (Concussive Dampener).
/// </summary>
[Union]
public partial record CombatantDamage
{
    partial record All();
    partial record OfType(DamageType Type);
    partial record OfSuperType();
    partial record WithinRange(decimal Meters);
    partial record BeyondRange(decimal Meters);
    partial record Blast();
}

public sealed record PassiveRule(Passive Modifier, ImmutableArray<Condition> When, Optional<string> Reason);

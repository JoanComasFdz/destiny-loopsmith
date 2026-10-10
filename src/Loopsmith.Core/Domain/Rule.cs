using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>What fires a rule. Each case carries exactly its own data (no "tag + optionals").</summary>
[Union]
public partial record Trigger
{
    partial record AbilityCast(AbilityKind Kind, bool Airborne = false);   // airborne: only casts made in the air
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
}

/// <summary>A guard on the current state, checked after the trigger matched.</summary>
[Union]
public partial record Condition
{
    partial record HasBuff(StatusId Status);
    partial record LacksBuff(StatusId Status);
    partial record TargetHas(StatusId Status);
}

[Union]
public partial record EnergyGrant
{
    partial record Fraction(GameValue Amount);   // of one charge: 0.15 = 15 %
    partial record Full();
}

/// <summary>
/// The game's consequences. ("Effect" is reserved for side effects described as data — see Orchestration.)
/// Energy outcomes (<see cref="GrantEnergy"/>, <see cref="ConvertStacksToEnergy"/>, <see cref="ResetCooldown"/>) are
/// explanations: ability energy isn't simulated (ADRs D5).
/// </summary>
[Union]
public partial record Outcome
{
    partial record GrantEnergy(AbilityKind To, EnergyGrant Amount);
    partial record ConvertStacksToEnergy(StatusId Consumed, AbilityKind To, GameValue PerStack);
    partial record ApplyBuff(StatusId Status, Optional<Seconds> Duration, StackCount Stacks, bool Restarts = false);   // Restarts: replaces the active stacks (re-arming Slice)
    partial record RemoveBuff(StatusId Status);
    partial record DebuffTarget(StatusId Status, Optional<Seconds> Duration);
    partial record Spawn(PickupId Pickup, int Count);
    partial record SpawnSummon(SummonId Summon, int Count);
    partial record StrikeTarget(StatusId Via, HitOutcome Hit);   // keyword damage: lightning strike, jolt chain…
    partial record ModifyDamage(DamageSource Against, GameValue Change);
    partial record RestoreHealth(GameValue Amount, bool IncludesAllies);
    partial record ResetCooldown(AbilityKind Which);
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
    partial record ExtraStacks(StatusId Status, StackCount Extra);           // Spark of Frequency
    partial record ExtraCharges(AbilityKind Ability, int Extra);            // explained only (ADRs D5)
    partial record ModifyDamage(DamageSource Against, GameValue Change);     // Flashover, stat bonuses
    partial record ResistDamage(GameValue Amount);                           // Spark of Resistance
    partial record ModifyWeaponStats(ImmutableArray<string> Archetypes, ImmutableArray<WeaponStatChange> Changes);
}

public sealed record PassiveRule(Passive Modifier, ImmutableArray<Condition> When, Optional<string> Reason);

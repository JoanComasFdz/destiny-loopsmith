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
    partial record KillMultiple(DamageSource Via, TargetCount AtLeast, Optional<Seconds> Within);   // kill at least N enemies in one action (within: the source's window, "2 in 3 s", a fact one action meets)
    partial record Damage(DamageSource Via, DamageSpan Needs = DamageSpan.Blow);   // Needs: Sustained for a source that needs several hits or a share of the enemy's health (ADRs D34)
    partial record DamageDebuffed(DamageSource Via, ImmutableArray<StatusId> TargetHas, DamageSpan Needs = DamageSpan.Blow);
    partial record DamageMultiple(DamageSource Via, TargetCount AtLeast, Optional<Seconds> Within);   // hit at least N enemies in one action (within: the source's window, "3 in 3 s")
    partial record PickUp(PickupId Pickup);
    partial record BuffGained(StatusId Status);
    partial record StacksMaxed(StatusId Status);
    partial record Finisher();                                                 // the player does a finisher on an enemy of the pack
    partial record AbilityCastAs(AbilityKind Kind, AbilityInput Input);        // only that use of the ability: the melee while sliding, the class ability's air move
    partial record KillFewer(DamageSource Via, TargetCount FewerThan);         // kill fewer than N enemies in one action (at least one)
    partial record CriticalHealth();                                           // the player declares they reached critical health (ADRs D3)
    partial record ShieldBroken(DamageSource Via);                             // a combatant's shield broken by that source (the player says which hit breaks it, ADRs D36)
    partial record EnergyExpended(AbilityKind Kind);                           // the ability's energy fully expended: its last charge used (ADRs D36)
    partial record DamageFewer(DamageSource Via, TargetCount FewerThan);       // hit fewer than N enemies in one action (at least one)
    partial record KillBetween(DamageSource Via, TargetCount AtLeast, TargetCount FewerThan);   // kill at least N and fewer than M enemies in one action (Reaper's Tithe's x2 on exactly 2)
    partial record Reload(DamageSource Via);                                   // a weapon reloaded: the player's Reload step, or an outcome that reloads it (ADRs D37)
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
    partial record BelowMax(StatusId Status);   // this buff is on you, not declared at its maximum: a count below it may still do (ADRs D3, D8)
    partial record Holds(StatusId Status, StackCount AtLeast);   // a counted buff (Armor Charge) holds at least this many stacks (ADRs D33)
}

/// <summary>
/// A value by stacks, as a source gives it in a table (ADRs D33): the first value is for <see cref="From"/>, the next for one
/// more, and so on ("70% | 80% | 90% | 100% at x3 | x4 | x5 | x6": from 3). A count outside the table has no value ("?").
/// </summary>
public sealed record StackTable(StackCount From, ImmutableArray<decimal> Values);

/// <summary>
/// The energy <see cref="Outcome.ConvertStacksToEnergy"/> gives for the stacks it spends: an amount per stack, a table by the
/// stacks spent (the finishers: x3 → 70%), or a table by charges, each copy of the element and each stack spent counting
/// as one (the Kickstarts: 2 mods and x2 → 4 charges → 28.4%).
/// </summary>
[Union]
public partial record StackEnergy
{
    partial record PerStack(GameValue Amount);
    partial record BySpent(StackTable Table);
    partial record ByCharges(StackTable Table);
}

/// <summary>
/// How long a buff a rule applies lasts, as its source says: not stated (the glossary's, if any), a fixed time, or a time
/// by the stacks the same rule spent before it (Bulwark Finisher's overshield, ADRs D33).
/// </summary>
[Union]
public partial record BuffDuration
{
    partial record Unstated();
    partial record Fixed(Seconds Time);
    partial record BySpent(StackTable Table);
}

/// <summary>
/// The stacks one grant of a buff gives, as its source states it: a fixed count ("+1 Bolt Charge"), or one by the copies of
/// the element equipped ("1 | 2": Shield Break Charge's x1 with one copy, x2 with two; the last for more copies).
/// </summary>
[Union]
public partial record StackGrant
{
    partial record Fixed(StackCount Count);
    partial record PerModCount(ImmutableArray<StackCount> Counts);
}

/// <summary>
/// The energy a grant gives: a fraction of one charge, a full charge, or a fraction that goes by the archetype of the
/// weapon holding the element (<see cref="ByArchetype"/>: Demolitionist's 10%, 20% on a Shotgun). The build check binds
/// it to the weapon's archetype when that is known (<c>WeaponBinding</c>); unbound, it reads both.
/// </summary>
[Union]
public partial record EnergyGrant
{
    partial record Fraction(GameValue Amount);   // of one charge: 0.15 = 15 %
    partial record Full();
    partial record ByArchetype(GameValue Otherwise, ImmutableArray<string> Archetypes, GameValue OnArchetypes);   // OnArchetypes on a weapon of one of those archetypes, Otherwise on any other
}

/// <summary>How much a heal restores: a share of your health ("?" when the source gives none), or HP (Unrelenting's 65).</summary>
[Union]
public partial record HealAmount
{
    partial record Share(GameValue Value);   // 0.4 = 40 % of your health
    partial record Points(GameValue Value);  // HP: 65
}

/// <summary>
/// The game's consequences.
/// Energy outcomes (<see cref="GrantEnergy"/>, <see cref="ConvertStacksToEnergy"/>'s energy, <see cref="ResetCooldown"/>,
/// <see cref="GrantLeastChargedEnergy"/>), <see cref="ModifyDamage"/> and <see cref="RestoreHealth"/> are facts shown with
/// their value (ADRs D1, D5).
/// </summary>
[Union]
public partial record Outcome
{
    partial record GrantEnergy(AbilityKind To, EnergyGrant Amount);
    partial record ConvertStacksToEnergy(StatusId Consumed, AbilityKind To, StackEnergy Energy);   // spends every stack
    partial record ApplyBuff(StatusId Status, BuffDuration Duration, StackGrant Stacks);   // Stacks: the grant, a fact ("+1 Bolt Charge")
    partial record RemoveBuff(StatusId Status);
    partial record DebuffTarget(StatusId Status, Optional<Seconds> Duration);
    partial record Spawn(PickupId Pickup, int Count);
    partial record SpawnSummon(SummonId Summon, int Count);
    partial record StrikeTarget(StatusId Via, HitOutcome Hit);   // keyword damage: lightning strike, jolt chain…
    partial record ModifyDamage(DamageSource Against, GameValue Change);
    partial record RestoreHealth(HealAmount Amount, bool IncludesAllies);
    partial record ResetCooldown(AbilityKind Which);
    partial record Collect(PickupId Pickup);   // picks it up from the ground when it is there (Powerful Attraction): a pickup event
    partial record GrantLeastChargedEnergy(EnergyGrant Amount);   // to whichever ability has the least energy: the game picks, so a fact
    partial record SpendStacks(StatusId Status, StackCount Count);   // a fixed number of a counted buff's stacks (Healthy Finisher's x1, ADRs D33)
    partial record WeaponStrike(DamageSource Weapon, Optional<DamageType> Type, HitOutcome Hit);   // this weapon's damage dealt by an effect (Veteran's Wisdom's shockwave): Weapon is "this weapon" until bound to its slot; Type none: the weapon's own
    partial record ReloadWeapon(DamageSource Weapon);   // reloads that weapon (weapon:this, slot:power): a reload, as if the player had reloaded it (ADRs D37)
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

/// <summary>
/// The weapons a change to weapon stats reaches: the build's weapons of those archetypes (Ionic Overclock's fusions), or the
/// weapon holding the element — "this weapon" as a rule says it (Stats for All), bound by the build check to its slot.
/// </summary>
[Union]
public partial record ChangedWeapons
{
    partial record OfArchetypes(ImmutableArray<string> Archetypes);
    partial record ThisWeapon();          // the weapon holding the element, until the build check binds it (WeaponBinding)
    partial record InSlot(WeaponSlot Slot);   // the weapon in that slot: "this weapon", bound
}

/// <summary>Always-on modifiers while equipped (and while <see cref="PassiveRule.When"/> holds).</summary>
[Union]
public partial record Passive
{
    partial record ExtraStacks(StatusId Status, StackCount Extra);           // Spark of Frequency: a caveat on each grant
    partial record ExtraCharges(AbilityKind Ability, int Extra);            // a fact (ADRs D5)
    partial record ModifyDamage(DamageSource Against, GameValue Change);     // Flashover, stat bonuses
    partial record ResistDamage(PveAndPvp Amount);                           // Spark of Resistance: any damage, "25% [10%]"
    partial record ModifyWeaponStats(ChangedWeapons Weapons, ImmutableArray<WeaponStatChange> Changes);
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

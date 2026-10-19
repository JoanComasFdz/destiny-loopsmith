namespace Loopsmith.Core.Domain;

// Closed tags with no data of their own. Data-carrying vocabulary lives in Dunet unions.

public enum GuardianClass { Hunter, Titan, Warlock }

public enum Subclass { Arc, Solar, Void, Stasis, Strand, Prismatic }

/// <summary>Damage type of a weapon, ability or keyword (Prismatic is a subclass, not a damage type).</summary>
public enum DamageType { Kinetic, Arc, Solar, Void, Stasis, Strand }

/// <summary>Colour identity of a build element or keyword — what the trace calls the "source element".</summary>
public enum Affinity { Neutral, Kinetic, Arc, Solar, Void, Stasis, Strand, Prismatic }

public enum AbilityKind { Grenade, Melee, ClassAbility, Super }

/// <summary>Abilities that hit something (a class ability is used, not aimed — see <c>PlayerAction.UseClassAbility</c>).</summary>
public enum OffensiveAbility { Grenade, Melee, Super }

/// <summary>
/// How an ability is used: its plain press, or the input that makes it a secondary ability an aspect enables — the
/// melee while sliding (Tempest Strike's uppercut, Lightning Surge) or in the air (Ballistic Slam), the class ability's
/// air move (Ascension: the air move key in the air spends the class ability's charge), the grenade held (Chaos
/// Accelerant, Bleak Watcher, Unbreakable), the Super's attacks a rule tells apart (Arc Staff: the heavy attack in the
/// air and the light-light-heavy combo, Palm Strike, blind; guarding reflects attacks). A secondary is still
/// that ability: every rule on the ability fires for it too, and only rules that name the input tell them apart
/// (<see cref="AbilityInputs.CanTake"/> says which ability takes which input).
/// </summary>
public enum AbilityInput { Press, Slide, Air, Hold, Combo, Guard }

public enum ElementKind
{
    Keyword,        // core statuses/pickups whose rules are always active (Bolt Charge, Ionic Trace…)
    Super,
    Grenade,
    Melee,
    ClassAbility,
    Jump,           // a movement ability (High Jump, Blink…): inert, movement has no part in a loop
    Aspect,
    Fragment,
    ExoticArmor,
    ExoticWeapon,
    Catalyst,       // an exotic weapon's catalyst: equipped only while the build turns it on
    ArmorSetBonus,
    ArmorMod,
    ArtifactPerk,
    WeaponPerk,
    WeaponMod,      // a weapon's mod (Backup Mag, Synergy): one per weapon, in its mod socket
}

public enum KeywordKind
{
    Buff,           // lives on the player (Amplified, Bolt Charge, Armor Charge…)
    WeaponBuff,     // lives on the player's weapons (Slice, Unraveling Rounds): a buff to the engine, shown with the weapons
    Debuff,         // lives on the target (Jolt, Sever, Unravel, Blind…)
}

/// <summary>
/// An enemy's rank (compendium/2026-10-19/Game Mechanics#176-179): rank-and-file (<see cref="Minor"/>), <see cref="Elite"/>,
/// then <see cref="Miniboss"/> and <see cref="Champion"/> (one tier), then <see cref="Boss"/>. The pack in front of you is minor
/// (ADRs D7); a kill step the player says is of a stronger enemy (<c>energy:kill:elite</c>, ADRs D45) kills one of that rank.
/// </summary>
public enum EnemyTier { Minor, Elite, Miniboss, Champion, Boss }

public enum HitOutcome { Damage, Kill }

/// <summary>
/// Which charge of an ability a use spends (ADRs D36): <see cref="Plain"/>, any; <see cref="Last"/>, the one that leaves its
/// energy fully expended — the player says it when the build gives the ability more than one charge (Shinobu's Vow's second
/// grenade). With one charge every use is the last one.
/// </summary>
public enum ChargeUse { Plain, Last }

/// <summary>Whether a weapon's hit or kill breaks a combatant's shield, with damage of its element (ADRs D36): the player says it.</summary>
public enum ShieldBreak { None, Breaks }

/// <summary>
/// Where a weapon's hits and kills land (ADRs D36): <see cref="Body"/>, anywhere; <see cref="Precision"/>, the enemy's weak
/// spot (a headshot) — the player says it. A kill trigger's <c>hit:</c> names one (<see cref="ZoneNeed"/>).
/// </summary>
public enum HitZone { Body, Precision }

/// <summary>
/// What the player can say about a step besides its hit and its count (ADRs D36): a weapon step <see cref="BreaksShield"/>
/// or lands <see cref="Precision"/> hits and kills, an ability step spends the <see cref="LastCharge"/>, an ammo brick is picked up
/// by sliding over it (<see cref="Slide"/>, ADRs D43).
/// </summary>
public enum StepOption { BreaksShield, LastCharge, Precision, Slide }

/// <summary>
/// How much damage one hit stands for (ADRs D34): <see cref="Blow"/>, one blow (a kill step's killing blow, a strike, a
/// summon's hit, a finisher); <see cref="Sustained"/>, a hit step's — the player keeps shooting the enemy without killing
/// it, enough for any number of hits or share of its health a source needs (To Shreds' 6 instances, Thunderlord's 10th hit).
/// </summary>
public enum DamageSpan { Blow, Sustained }

public enum WeaponSlot { Kinetic, Energy, Power }

/// <summary>
/// A weapon's ammo type, Bungie's manifest's (<c>equippingBlock.ammoType</c>: 1, 2, 3): Special weapons sit in the kinetic and
/// energy slots too, so a slot doesn't say it. No ammo is counted (ADRs D37): it only says which weapon's damage a rule names
/// (Spark of Beacons's Special or Heavy Arc kills).
/// </summary>
public enum AmmoType { Primary, Special, Heavy }

/// <summary>
/// The ammo brick the player picks up (ADRs D43): a Special or a Heavy one. Bricks drop from enemies, which only play decides,
/// so the player declares the pickup; no ammo is counted (ADRs D37). Overflow reacts to both, Lead from Gold to a Heavy one.
/// </summary>
public enum AmmoBrick { Special, Heavy }

/// <summary>
/// How the player picks an ammo brick up (ADRs D43): <see cref="Plain"/>, walking over it; <see cref="Slide"/>, sliding over it
/// (Spark of Momentum reacts to that only). A slide is still a pickup: every rule on a pickup fires for it too.
/// </summary>
public enum BrickPickup { Plain, Slide }

/// <summary>
/// How a brick must be picked up for a trigger to match (ADRs D43), its <c>by:</c>: <see cref="AnyPickup"/>, walked or slid
/// over (no <c>by:</c>); <see cref="Slide"/>, only slid over (<c>by: slide</c>: Spark of Momentum).
/// </summary>
public enum BrickNeed { AnyPickup, Slide }

public enum StatKind { Weapons, Health, Class, Grenade, Super, Melee }

/// <summary>Deterministic v1: a <see cref="Chance"/> rule still fires, but the trace marks it as "chance".</summary>
public enum Likelihood { Always, Chance }

/// <summary>How much we trust a number an outcome carries ("~" = approximate, "?" = unknown, never applied).</summary>
public enum Certainty { Known, Approximate, Unknown }

/// <summary>Only the distinctions the app branches on (see docs/CODING_RULES.md).</summary>
public enum Severity { Blocking, Warning, Info }

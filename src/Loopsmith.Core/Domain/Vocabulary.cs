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
/// Accelerant, Bleak Watcher, Unbreakable). A secondary is still
/// that ability: every rule on the ability fires for it too, and only rules that name the input tell them apart
/// (<see cref="AbilityInputs.CanTake"/> says which ability takes which input).
/// </summary>
public enum AbilityInput { Press, Slide, Air, Hold }

public enum ElementKind
{
    Keyword,        // core statuses/pickups whose rules are always active (Bolt Charge, Ionic Trace…)
    Super,
    Grenade,
    Melee,
    ClassAbility,
    Aspect,
    Fragment,
    ExoticArmor,
    ExoticWeapon,
    Catalyst,       // an exotic weapon's catalyst: equipped only while the build turns it on
    ArmorSetBonus,
    ArmorMod,
    ArtifactPerk,
    WeaponPerk,
}

public enum KeywordKind
{
    Buff,           // lives on the player (Amplified, Bolt Charge, Armor Charge…)
    WeaponBuff,     // lives on the player's weapons (Slice, Unraveling Rounds): a buff to the engine, shown with the weapons
    Debuff,         // lives on the target (Jolt, Sever, Unravel, Blind…)
}

public enum EnemyTier { Minor, Major, Boss, Champion }

public enum HitOutcome { Damage, Kill }

public enum WeaponSlot { Kinetic, Energy, Power }

public enum StatKind { Weapons, Health, Class, Grenade, Super, Melee }

/// <summary>Deterministic v1: a <see cref="Chance"/> rule still fires, but the trace marks it as "chance".</summary>
public enum Likelihood { Always, Chance }

/// <summary>How much we trust a number an outcome carries ("~" = approximate, "?" = unknown, never applied).</summary>
public enum Certainty { Known, Approximate, Unknown }

/// <summary>Only the distinctions the app branches on (see CONVENTIONS.md).</summary>
public enum Severity { Blocking, Warning, Info }

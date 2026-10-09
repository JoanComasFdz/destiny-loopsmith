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
    ArmorSetBonus,
    ArmorMod,
    ArtifactPerk,
    WeaponPerk,
}

public enum KeywordKind
{
    Buff,           // lives on the player (Amplified, Bolt Charge, Armor Charge…)
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

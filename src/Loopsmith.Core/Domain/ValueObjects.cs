using Vogen;

namespace Loopsmith.Core.Domain;

// Value objects — validated once, at the boundary (RuleParsing / BuildParsing / LoopFiles, and action tokens in
// Phrasing.ActionTokenParsing).
// Inside the pure core a value object is known-valid.

/// <summary>Stable authored identity of a build element: a kebab-case slug ("shinobus-vow").</summary>
[ValueObject<string>]
public readonly partial struct ElementId
{
    private static Validation Validate(string value) =>
        IsSlug(value) ? Validation.Ok : Validation.Invalid($"'{value}' is not a kebab-case id");

    internal static bool IsSlug(string value) =>
        value.Length > 0
        && value.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
        && value[0] != '-'
        && value[^1] != '-';
}

/// <summary>
/// Stable authored identity of a rule or a passive: its element's id and a number, never reused within the element
/// ("combination-blow-2"). The rule file's path in front of it names it where people read it (ADRs D40).
/// </summary>
[ValueObject<string>]
public readonly partial struct RuleId
{
    private static Validation Validate(string value) =>
        ElementId.IsSlug(value) ? Validation.Ok : Validation.Invalid($"'{value}' is not a kebab-case rule id");
}

/// <summary>Buff or debuff in the keyword vocabulary ("bolt-charge", "jolt").</summary>
[ValueObject<string>]
public readonly partial struct StatusId
{
    private static Validation Validate(string value) =>
        ElementId.IsSlug(value) ? Validation.Ok : Validation.Invalid($"'{value}' is not a kebab-case status id");
}

/// <summary>Something that lands on the ground or tracks to you ("orb-of-power", "ionic-trace").</summary>
[ValueObject<string>]
public readonly partial struct PickupId
{
    private static Validation Validate(string value) =>
        ElementId.IsSlug(value) ? Validation.Ok : Validation.Invalid($"'{value}' is not a kebab-case pickup id");
}

/// <summary>An autonomous ally that deals damage on its own ("threadling").</summary>
[ValueObject<string>]
public readonly partial struct SummonId
{
    private static Validation Validate(string value) =>
        ElementId.IsSlug(value) ? Validation.Ok : Validation.Invalid($"'{value}' is not a kebab-case summon id");
}

/// <summary>Bungie manifest hash — the long-term identity (D14); optional until the manifest join exists. Never 0.</summary>
[ValueObject<uint>]
public readonly partial struct ItemHash
{
    private static Validation Validate(uint value) =>
        value > 0 ? Validation.Ok : Validation.Invalid("A manifest hash is never 0");
}

/// <summary>A player's copy of an item, as Bungie's profile names it (<c>itemInstanceId</c>): a number, written as text.</summary>
[ValueObject<string>]
public readonly partial struct ItemInstanceId
{
    private static Validation Validate(string value) =>
        IsNumber(value) ? Validation.Ok : Validation.Invalid($"'{value}' is not an item instance id");

    internal static bool IsNumber(string value) =>
        value.Length > 0 && value.All(char.IsAsciiDigit);
}

/// <summary>A Destiny membership's id (<c>membershipId</c>): a number, written as text.</summary>
[ValueObject<string>]
public readonly partial struct MembershipId
{
    private static Validation Validate(string value) =>
        ItemInstanceId.IsNumber(value) ? Validation.Ok : Validation.Invalid($"'{value}' is not a membership id");
}

/// <summary>A Destiny membership's platform, as Bungie numbers it (<c>membershipType</c>: 1 Xbox, 2 PlayStation, 3 Steam…).</summary>
[ValueObject<int>]
public readonly partial struct MembershipType
{
    private static Validation Validate(int value) =>
        value > 0 ? Validation.Ok : Validation.Invalid("A membership type is a platform's number, above 0");
}

/// <summary>Version of the rule catalog a build was authored against (requirements FR-10, ADRs D15).</summary>
[ValueObject<string>]
public readonly partial struct CatalogVersion
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("Catalog version must not be empty") : Validation.Ok;
}

[ValueObject<decimal>]
public readonly partial struct Seconds
{
    private static Validation Validate(decimal value) =>
        value >= 0m ? Validation.Ok : Validation.Invalid("Seconds must not be negative");
}

/// <summary>
/// How many enemies one action hits (or kills), 1..20. The player says it — the engine can't know how many enemies a
/// grenade or a burst of fire catches (ADRs D4).
/// </summary>
[ValueObject<int>]
[Instance("One", 1)]
[Instance("Most", Maximum)]
public readonly partial struct TargetCount
{
    public const int Maximum = 20;

    private static Validation Validate(int value) =>
        value is >= 1 and <= Maximum ? Validation.Ok : Validation.Invalid($"Target count must be within 1..{Maximum}");
}

[ValueObject<int>]
public readonly partial struct StackCount
{
    private static Validation Validate(int value) =>
        value >= 0 ? Validation.Ok : Validation.Invalid("Stacks must not be negative");
}

/// <summary>
/// How many charges an ability holds (Shinobu's Vow: two grenades), at least one: its profile's, or that plus every
/// <c>extraCharges</c>. A fact of the build, added up and compared with one, never counted down (ADRs D36).
/// </summary>
[ValueObject<int>]
[Instance("One", 1)]
public readonly partial struct ChargeCount
{
    private static Validation Validate(int value) =>
        value >= 1 ? Validation.Ok : Validation.Invalid("A charge count is at least 1");
}

/// <summary>How many copies of an element a build equips (mods stack: two Grenade Kickstarts), at least one.</summary>
[ValueObject<int>]
[Instance("One", 1)]
public readonly partial struct CopyCount
{
    private static Validation Validate(int value) =>
        value >= 1 ? Validation.Ok : Validation.Invalid("A copy count is at least 1");
}

/// <summary>How many fragments an aspect makes room for (none or more); a subclass's room is its aspects' added up.</summary>
[ValueObject<int>]
[Instance("Zero", 0)]
public readonly partial struct FragmentSlots
{
    private static Validation Validate(int value) =>
        value >= 0 ? Validation.Ok : Validation.Invalid("Fragment slots are never negative");
}

/// <summary>A plug category of Bungie's manifest (<c>v404.repackage.machinegun0.masterwork</c>: what a socket takes).</summary>
[ValueObject<string>]
public readonly partial struct PlugCategoryId
{
    private static Validation Validate(string value) =>
        value.Length > 0 && !value.Any(char.IsWhiteSpace) ? Validation.Ok : Validation.Invalid("A plug category is one word, not empty");
}

/// <summary>Armor energy: what a mod costs (Bungie's plug <c>energyCost</c>; none given: 0) or what a copy holds (its <c>energyCapacity</c>).</summary>
[ValueObject<int>]
[Instance("Zero", 0)]
public readonly partial struct ArmorEnergy
{
    private static Validation Validate(int value) =>
        value >= 0 ? Validation.Ok : Validation.Invalid("Armor energy is never negative");
}

/// <summary>A copy's gear tier, 1 to 5 (Bungie's <c>gearTier</c>; DIM's diamonds).</summary>
[ValueObject<int>]
public readonly partial struct GearTier
{
    private static Validation Validate(int value) =>
        value is >= 1 and <= 5 ? Validation.Ok : Validation.Invalid("A gear tier is 1 to 5");
}

/// <summary>A copy's power level (Bungie's instance <c>primaryStat</c>: 550), at least 1; DIM writes it on its square's footer.</summary>
[ValueObject<int>]
public readonly partial struct PowerLevel
{
    private static Validation Validate(int value) =>
        value >= 1 ? Validation.Ok : Validation.Invalid("A power level is at least 1");
}

/// <summary>How many pieces of an armor set: what a set perk needs, or what a loadout wears (at least one).</summary>
[ValueObject<int>]
public readonly partial struct PieceCount
{
    private static Validation Validate(int value) =>
        value >= 1 ? Validation.Ok : Validation.Invalid("A piece count is at least 1");
}

/// <summary>Armor 3.0 stat value (0–200).</summary>
[ValueObject<int>]
public readonly partial struct StatValue
{
    private static Validation Validate(int value) =>
        value is >= 0 and <= 200 ? Validation.Ok : Validation.Invalid("Stat must be within 0..200");
}

/// <summary>Date of a Compendium snapshot (provenance).</summary>
[ValueObject<DateOnly>]
public readonly partial struct SnapshotDate;

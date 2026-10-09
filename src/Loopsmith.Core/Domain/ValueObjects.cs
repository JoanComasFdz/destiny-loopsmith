using Vogen;

namespace Loopsmith.Core.Domain;

// Value objects — validated once, at the boundary (RuleParsing / BuildParsing).
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

/// <summary>Bungie manifest hash — the long-term identity (D1); optional until the manifest join exists.</summary>
[ValueObject<uint>]
public readonly partial struct ItemHash;

/// <summary>Version of the rule catalog a build was authored against (FR-8).</summary>
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
/// grenade or a burst of fire catches (ADRs D22).
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

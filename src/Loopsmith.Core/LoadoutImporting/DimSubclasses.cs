using System.Collections.Immutable;
using Loopsmith.Core.Domain;

namespace Loopsmith.Core.LoadoutImporting;

/// <summary>A subclass item of the manifest: which class and subclass a loadout's subclass hash is.</summary>
internal sealed record SubclassItem(GuardianClass Class, Subclass Subclass, string Name);

/// <summary>
/// The subclass items Loopsmith recognises in a DIM loadout until the manifest join (requirements FR-13): the nine Light
/// subclasses, whose hashes DIM's own source lists (<c>src/app/loadout-drawer/loadout-utils.ts</c>, <c>oldToNewItems</c>,
/// DIM commit d7c02e5). Stasis, Strand and Prismatic subclasses aren't in it yet.
/// </summary>
internal static class DimSubclasses
{
    internal static readonly ImmutableDictionary<uint, SubclassItem> ByHash = ImmutableDictionary.CreateRange<uint, SubclassItem>(
    [
        new(2328211300, new SubclassItem(GuardianClass.Hunter, Subclass.Arc, "Arcstrider")),
        new(2932390016, new SubclassItem(GuardianClass.Titan, Subclass.Arc, "Striker")),
        new(3168997075, new SubclassItem(GuardianClass.Warlock, Subclass.Arc, "Stormcaller")),
        new(2240888816, new SubclassItem(GuardianClass.Hunter, Subclass.Solar, "Gunslinger")),
        new(2550323932, new SubclassItem(GuardianClass.Titan, Subclass.Solar, "Sunbreaker")),
        new(3941205951, new SubclassItem(GuardianClass.Warlock, Subclass.Solar, "Dawnblade")),
        new(2453351420, new SubclassItem(GuardianClass.Hunter, Subclass.Void, "Nightstalker")),
        new(2842471112, new SubclassItem(GuardianClass.Titan, Subclass.Void, "Sentinel")),
        new(2849050827, new SubclassItem(GuardianClass.Warlock, Subclass.Void, "Voidwalker")),
    ]);
}

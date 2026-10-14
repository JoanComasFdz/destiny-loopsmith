using Dunet;

namespace Loopsmith.Core.Domain;

/// <summary>A link that shares a DIM loadout (docs/loop-format.md "Starting from a DIM link"). <c>Link</c> is the link as an absolute URL.</summary>
[Union]
public partial record DimLink
{
    /// <summary><c>dim.gg/&lt;id&gt;</c>: the loadout is on DIM's servers; a host asks DIM for it and hands the answer back.</summary>
    partial record Shared(DimShareId ShareId, string Link);

    /// <summary><c>…/loadouts?loadout=&lt;JSON&gt;</c> (D2ArmorPicker, guardian.report): the loadout is in the link itself.</summary>
    partial record Inline(string Loadout, string Link);
}

/// <summary>Where a DIM loadout lists an item: equipped, plugged into the subclass, an armor mod, an artifact perk.</summary>
public enum LoadoutPart { Item, SubclassPlug, ArmorMod, ArtifactPerk }

/// <summary>
/// Something a DIM loadout has that its build leaves out: no element of the rule catalog has its manifest hash yet.
/// Kept with the build (unknown is data), so whoever opens the loop sees what is missing.
/// </summary>
public sealed record LeftOutItem(LoadoutPart Part, ItemHash Hash);

/// <summary>
/// Why a DIM loadout's item is outside its build: no rule knows it yet (a gap), it has no part in a loop — a
/// legendary armor piece (stats and mod slots; its set's bonuses count from the pieces worn), a jump the rules don't have —
/// and why, or Loopsmith doesn't support it and ignores it (a raid's, an activity's or a retired season's mod, ADRs D33).
/// </summary>
[Union]
public partial record LeftOutReason
{
    partial record NotInRules();
    partial record NoLoopEffect(string Why);
    partial record NotSupported(string Why);
}

public static class LeftOutReading
{
    /// <summary>A gap: no rule knows the item yet (not one with no part in a loop).</summary>
    public static bool IsMissing(this LeftOutReason reason) =>
        reason.Match(
            _ => true,
            _ => false,
            _ => false);

    /// <summary>Ignored: Loopsmith doesn't support the item (a raid's or an activity's mod), so it is neither a gap nor known.</summary>
    public static bool IsIgnored(this LeftOutReason reason) =>
        reason is LeftOutReason.NotSupported;
}

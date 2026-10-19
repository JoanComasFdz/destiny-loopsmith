using Dunet;

namespace Loopsmith.Core.Domain;

/// <summary>Where a build holds an item it leaves out: worn as an item, plugged into the subclass, an armor mod, an artifact perk.</summary>
public enum LoadoutPart { Item, SubclassPlug, ArmorMod, ArtifactPerk }

/// <summary>
/// Something a build has that no element of the rule catalog places: a legendary armor piece (picked, or written in the
/// build file), a mod, plug or artifact perk the rules don't have by manifest hash yet. Kept with the build (unknown is
/// data), so whoever opens the loop sees what is missing.
/// </summary>
public sealed record LeftOutItem(LoadoutPart Part, ItemHash Hash);

/// <summary>
/// Why a build's item is outside its rules: no rule knows it yet (a gap), it has no part in a loop — a
/// legendary armor piece (stats and mod slots; its set's bonuses count from the pieces worn), a jump the rules don't have —
/// and why, or Loopsmith doesn't support it and ignores it (a raid's, an activity's or a retired season's mod, ADRs D35).
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

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

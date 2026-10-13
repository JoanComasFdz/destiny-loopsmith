using System.Collections.Immutable;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>
/// Pure: what Bungie's plug categories say of an armor piece's mod sockets (ADRs D30). A piece's socket takes the mods
/// of one category: the general one (<c>enhancements.v2_general</c>: stat mods), its slot's
/// (<c>enhancements.v2_head</c> … <c>enhancements.v2_class_item</c>), or one of its own — an artifice socket, a raid's
/// or an activity's (<c>enhancements.raid_v800</c>, <c>enhancements.season_outlaw</c>). Other categories in the game's
/// "ARMOR MODS" sockets aren't mods a build chooses (tuning, masterworks, Armor 1.0's and retired activity sockets).
/// </summary>
public static class ArmorModCategories
{
    /// <summary>The general mods' category: one socket on every piece.</summary>
    public static readonly PlugCategoryId General = PlugCategoryId.From("enhancements.v2_general");

    private static readonly ImmutableDictionary<ArmorSlot, PlugCategoryId> SlotCategories = new Dictionary<ArmorSlot, PlugCategoryId>
    {
        [ArmorSlot.Helmet] = PlugCategoryId.From("enhancements.v2_head"),
        [ArmorSlot.Arms] = PlugCategoryId.From("enhancements.v2_arms"),
        [ArmorSlot.Chest] = PlugCategoryId.From("enhancements.v2_chest"),
        [ArmorSlot.Legs] = PlugCategoryId.From("enhancements.v2_legs"),
        [ArmorSlot.ClassItem] = PlugCategoryId.From("enhancements.v2_class_item"),
    }.ToImmutableDictionary();

    /// <summary>An artifice socket's mods (+3 to a stat).</summary>
    public static readonly PlugCategoryId Artifice = PlugCategoryId.From("enhancements.artifice");

    /// <summary>How a raid's sockets' categories start (<c>enhancements.raid_v800</c>)…</summary>
    public const string RaidPrefix = "enhancements.raid_";

    /// <summary>…and an activity's or an older raid's (<c>enhancements.season_outlaw</c>: Last Wish).</summary>
    public const string ActivityPrefix = "enhancements.season_";

    /// <summary>The category of a slot's own mods (Helmet: <c>enhancements.v2_head</c>).</summary>
    public static PlugCategoryId ToSlotCategory(ArmorSlot slot) =>
        SlotCategories[slot];

    /// <summary>The slot whose mods a category holds; none for the general mods and a piece's own sockets.</summary>
    public static Optional<ArmorSlot> FindSlot(PlugCategoryId category) =>
        SlotCategories.Where(entry => entry.Value == category).Select(entry => Optional.Some(entry.Key)).FindFirstSome();

}

/// <summary>An armor mod the build holds: its plug (the copy, by hash) and, when the rules know it, its element.</summary>
public sealed record HeldMod(ItemHash Plug, Optional<ElementId> Element);

/// <summary>
/// One mod socket of an armor piece in the designer: the category it takes, the mod in it (none: empty), and its place —
/// which piece holds it (none: the slot holds no piece Loopsmith can read sockets of) and its number in that row.
/// </summary>
public sealed record ModSocket(PlugCategoryId Category, Optional<HeldMod> Mod, ModSocketPlace Place);

/// <summary>Where a socket is: an armor slot's row (its slot mods and own sockets), or the General row; its index in the row.</summary>
public sealed record ModSocketPlace(Optional<ArmorSlot> Slot, int Index);

/// <summary>
/// What a piece's energy holds: what its mods cost (its slot mods, own sockets and the general mods it carries) and its
/// capacity (the player's copy's, after a Bungie login; none: not known, "?").
/// </summary>
public sealed record ArmorEnergyUse(ArmorEnergy Used, Optional<ArmorEnergy> Capacity);

/// <summary>An armor slot's mod sockets in the designer, and its energy.</summary>
public sealed record PieceModSockets(ArmorSlot Slot, bool PieceKnown, ImmutableArray<ModSocket> Sockets, ArmorEnergyUse Energy);

/// <summary>
/// The build's mods as the designer lays them out: each slot's sockets (its slot mods and own sockets), the General row
/// (every piece's general socket; the general mods go wherever the energy lets them, as DIM assigns them), the mods no
/// free socket takes (more than the pieces hold, or of a category no piece worn has) and the ones that aren't socket
/// mods (a tuning mod, which goes in a Tier 5 copy's tuning socket; one the listing doesn't have).
/// </summary>
public sealed record ModLayout(ImmutableArray<PieceModSockets> Pieces, ImmutableArray<ModSocket> General, ImmutableArray<HeldMod> Unplaced, ImmutableArray<HeldMod> Unsocketed);

/// <summary>
/// A mod the mod picker offers for a socket: the listing's mod, whether the rules know it (a mod that changes only
/// stats needs no rule), whether its element is the subclass's, and why it can't go in when it can't (its energy is more
/// than the piece has left).
/// </summary>
public sealed record ModOption(ListedMod Mod, bool InRules, bool OfSubclassElement, Optional<string> Refused);

/// <summary>
/// What the mod picker lists for a socket: the socket (the mod in it), the mods of its category (the subclass's element
/// first: <see cref="Element"/>, none for Prismatic), and its piece's energy (none for the General row: the general mods
/// go wherever the energy lets them).
/// </summary>
public sealed record ModOptions(ModSocket Socket, ImmutableArray<ModOption> Options, Optional<Affinity> Element, Optional<ArmorEnergyUse> Energy);

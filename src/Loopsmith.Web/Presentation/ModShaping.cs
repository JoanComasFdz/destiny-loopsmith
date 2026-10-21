using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>A mod picked in the designer: the socket it goes in and the mod (by hash).</summary>
public sealed record ModPick(ModSocketPlace Place, ItemHash Mod);

/// <summary>
/// A mod socket's square: where it is, the mod in it (none: an empty socket), what it takes ("Helmet mod"; in short, on
/// an empty square: "Helmet") and the mod's energy cost.
/// </summary>
public sealed record ModSocketView(ModSocketPlace Place, Optional<IconTile> Mod, string Takes, string ShortTakes, Optional<int> Cost);

/// <summary>An armor slot's mod sockets and its energy line ("Energy 4 / 10", "4 / ?" when the capacity isn't known), over when its mods cost more than it holds.</summary>
public sealed record PieceModsView(ArmorSlot Slot, ImmutableArray<ModSocketView> Sockets, string Energy, bool Over);

/// <summary>
/// The build's mods in its sockets: each slot's row, the General row, the mods no free socket takes, the ones that go in
/// no mod socket (tuning) and the ones Loopsmith ignores (a raid's or an activity's, ADRs D35).
/// </summary>
public sealed record ModsView(ImmutableArray<PieceModsView> Pieces, ImmutableArray<ModSocketView> General, ImmutableArray<IconTile> Unplaced, ImmutableArray<IconTile> Other, ImmutableArray<IconTile> Ignored);

/// <summary>A mod of the mod picker: its square, cost, what Bungie says it needs, whether the rules know it, whether it is in the socket, and why it can't go in.</summary>
public sealed record ModOptionView(ItemHash Hash, IconTile Tile, int Cost, ImmutableArray<string> Requirements, bool InRules, bool OfElement, bool Picked, Optional<string> Refused);

/// <summary>
/// The mod picker: its title, the options, the subclass's element (for the filter; none for Prismatic, or when no option
/// is of it), the piece's energy line and the mod in the socket (its "About" opens its details).
/// </summary>
public sealed record ModPickerView(string Title, ImmutableArray<ModOptionView> Options, Optional<Affinity> Element, Optional<string> Energy, Optional<IconTile> Held);

/// <summary>Pure: Core's mod layout and mod options (LoopDesigning.LayOutMods, ListModOptions) as the designer shows them.</summary>
public static class ModShaping
{
    public static ModsView ShapeMods(ValidatedBuild build, ModLayout layout, ItemListing listing)
    {
        var index = listing.Mods.GroupBy(mod => mod.Item.Hash).ToImmutableDictionary(group => group.Key, group => group.First());
        return new ModsView(
            [.. layout.Pieces.Select(piece => new PieceModsView(
                piece.Slot,
                [.. piece.Sockets.Select(socket => ShapeSocket(build, listing, index, socket))],
                DescribeEnergy(piece.Energy),
                IsOver(piece.Energy)))],
            [.. layout.General.Select(socket => ShapeSocket(build, listing, index, socket))],
            [.. layout.Unplaced.Select(mod => LoadoutShaping.DescribeHeldMod(build, mod, Find(index, mod.Plug)))],
            LoadoutShaping.GroupModCopies(layout.Unsocketed.Select(mod => LoadoutShaping.DescribeHeldMod(build, mod, Find(index, mod.Plug)))),
            LoadoutShaping.GroupModCopies(layout.Ignored.Select(mod => LoadoutShaping.DescribeHeldMod(build, mod, Find(index, mod.Plug)))));
    }

    public static ModPickerView ShapePicker(ValidatedBuild build, ItemListing listing, ModOptions options) =>
        new(
            $"Pick a {DescribeTakes(listing, options.Socket.Category).ToLowerInvariant()}",
            [.. options.Options.Select(option => new ModOptionView(
                option.Mod.Item.Hash,
                LoadoutShaping.DescribeListedMod(build, option.Mod, option.InRules),
                option.Mod.Cost.Value,
                option.Mod.Requirements,
                option.InRules,
                option.OfSubclassElement,
                options.Socket.Mod.Map(mod => mod.Plug) == Optional.Some(option.Mod.Item.Hash),
                option.Refused))],
            options.Options.Any(option => option.OfSubclassElement) ? options.Element : Optional.None<Affinity>(),
            options.Energy.Map(DescribeEnergy),
            options.Socket.Mod.Map(mod => LoadoutShaping.DescribeHeldMod(build, mod, listing.Mods.Where(listed => listed.Item.Hash == mod.Plug).Select(Optional.Some).FindFirstSome())));

    /// <summary>"Energy 4 / 10": what the piece's mods cost and what its copy holds ("?" when not known).</summary>
    public static string DescribeEnergy(ArmorEnergyUse energy) =>
        $"Energy {energy.Used.Value} / {energy.Capacity.Match(capacity => capacity.Value.Value.ToString(CultureInfo.InvariantCulture), _ => DomainPhrasing.Unknown)}";

    /// <summary>What a socket takes, as the game words its mods: "Helmet mod", "General mod", "Artifice mod", else its mods' type.</summary>
    public static string DescribeTakes(ItemListing listing, PlugCategoryId category) =>
        category.DescribeModSocket().UnwrapOr(
            listing.Mods.Where(mod => mod.Category == category).SelectMany(mod => mod.Item.Type.Match(type => new[] { type.Value }, _ => [])).DefaultIfEmpty("Mod").First());

    /// <summary>The picker's options the filters let through: of the subclass's element only (when ticked), and whose name holds the search text.</summary>
    public static ImmutableArray<ModOptionView> FilterOptions(ModPickerView view, bool elementOnly, string query) =>
    [
        .. view.Options
            .Where(option => !elementOnly || option.OfElement)
            .Where(option => query.Length == 0 || option.Tile.Name.Contains(query, StringComparison.OrdinalIgnoreCase)),
    ];

    /// <summary>The element filter's name ("Arc"); empty when the picker has none.</summary>
    public static string DescribeElementName(ModPickerView view) =>
        view.Element.Match(element => element.Value.ToString(), _ => "");

    private static ModSocketView ShapeSocket(ValidatedBuild build, ItemListing listing, ImmutableDictionary<ItemHash, ListedMod> index, ModSocket socket) =>
        new(
            socket.Place,
            socket.Mod.Map(mod => LoadoutShaping.DescribeHeldMod(build, mod, Find(index, mod.Plug))),
            DescribeTakes(listing, socket.Category),
            socket.Category.DescribeModSocketShort(),
            socket.Mod.Bind(mod => Find(index, mod.Plug)).Map(found => found.Cost.Value));

    private static bool IsOver(ArmorEnergyUse energy) =>
        energy.Capacity.Match(capacity => energy.Used.Value > capacity.Value.Value, _ => false);

    private static Optional<ListedMod> Find(ImmutableDictionary<ItemHash, ListedMod> index, ItemHash plug) =>
        index.TryGetValue(plug, out var found) ? Optional.Some(found) : Optional.None<ListedMod>();
}

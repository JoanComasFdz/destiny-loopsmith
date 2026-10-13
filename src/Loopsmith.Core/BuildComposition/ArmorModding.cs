using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: a build's armor mods laid out in its pieces' mod sockets, and a mod picked into a socket or taken out of one
/// (docs/loop-format.md "Armor mods", ADRs D30). The build holds its mods as a list (its <c>armorMods</c>, the copies
/// <c>armorModPlugs</c> names and the mods it leaves out), as a DIM loadout does; the sockets come from the listing of
/// every armor piece (Bungie's manifest): each piece's slot mods and own sockets (artifice, raid) in its row, every
/// piece's general socket in the General row. A slot with no piece whose sockets Loopsmith can read holds what the build
/// has for it and one free socket of its slot's mods, and a general one. The mods fill the sockets in the build's order;
/// the general mods go to the pieces with the most energy left, the costliest first, as DIM assigns them. A piece's
/// energy is what its mods cost against its copy's capacity (after a Bungie login; else "?"): a pick that takes a piece
/// with a known capacity over it is refused.
/// </summary>
public static class ArmorModding
{
    /// <summary>The mods the build holds, in its order: the ones the rules know (each its copy), then the ones it leaves out.</summary>
    public static ImmutableArray<HeldMod> ListHeldMods(RuleCatalog catalog, Build build)
    {
        var known = build.ArmorMods.Aggregate(
            (Held: ImmutableArray<HeldMod>.Empty, Plugs: build.ArmorModPlugs),
            (taken, id) => ChoosePlug(catalog, id, taken.Plugs).Match(
                chosen => (taken.Held.Add(new HeldMod(chosen.Value.Plug, Optional.Some(id))), chosen.Value.Others),
                _ => taken));
        return
        [
            .. known.Held,
            .. build.LeftOut.Where(item => item.Part == LoadoutPart.ArmorMod).Select(item => new HeldMod(item.Hash, Optional.None<ElementId>())),
        ];
    }

    /// <summary>The build's mods in its pieces' sockets (<paramref name="capacities"/>: each slot's copy's energy, when known).</summary>
    public static ModLayout LayOutMods(RuleCatalog catalog, Build build, ItemListing listing, ImmutableDictionary<ArmorSlot, ArmorEnergy> capacities) =>
        LayOut(catalog, build, ToModIndex(listing), listing, capacities, ListHeldMods(catalog, build));

    /// <summary>
    /// What the mod picker lists for the socket at <paramref name="place"/>: every mod of its category, the subclass's
    /// element first, each marked when the rules know it and refused when it takes its piece over its energy; or why
    /// there is no such socket.
    /// </summary>
    public static Result<ModOptions, string> ListModOptions(
        RuleCatalog catalog, Build build, ItemListing listing, ImmutableDictionary<ArmorSlot, ArmorEnergy> capacities, ModSocketPlace place)
    {
        var index = ToModIndex(listing);
        var held = ListHeldMods(catalog, build);
        var layout = LayOut(catalog, build, index, listing, capacities, held);
        var element = ToAffinity(build.Subclass);
        return FindSocket(layout, place).Map(socket => new ModOptions(
            socket,
            [
                .. listing.Mods
                    .Where(mod => mod.Category == socket.Category)
                    .Select(mod => new ModOption(
                        mod,
                        FindModElement(catalog, mod.Item.Hash).IsSome() || ChangesOnlyStats(mod.Category),
                        FindModElement(catalog, mod.Item.Hash).Match(found => Optional.Some(found.Value.Affinity) == element, _ => false),
                        CheckEnergy(catalog, build, index, listing, capacities, layout, socket, mod)))
                    .OrderByDescending(option => option.OfSubclassElement),
            ],
            element,
            place.Slot.Bind(slot => layout.Pieces.Where(piece => piece.Slot == slot).Select(piece => Optional.Some(piece.Energy)).FindFirstSome())));
    }

    /// <summary>The build with <paramref name="mod"/> in the socket at <paramref name="place"/> (in place of the one there); or why it can't go in.</summary>
    public static Result<Build, string> PutMod(
        RuleCatalog catalog, Build build, ItemListing listing, ImmutableDictionary<ArmorSlot, ArmorEnergy> capacities, ModSocketPlace place, ItemHash mod)
    {
        var index = ToModIndex(listing);
        var layout = LayOut(catalog, build, index, listing, capacities, ListHeldMods(catalog, build));
        return FindSocket(layout, place).Bind(socket => FindListedMod(index, mod)
            .Bind(listed => listed.Category == socket.Category
                ? new Result<ListedMod, string>.Ok(listed)
                : Refuse<ListedMod>($"{listed.Item.Name} doesn't go in that socket."))
            .Bind(listed => CheckEnergy(catalog, build, index, listing, capacities, layout, socket, listed).Match(
                why => Refuse<Build>(why.Value),
                _ => new Result<Build, string>.Ok(HoldMods(catalog, build, ReplaceMod(layout, place, Optional.Some(ToHeldMod(catalog, mod))))))));
    }

    /// <summary>The build without the mod in the socket at <paramref name="place"/>; or why not, when there is none there.</summary>
    public static Result<Build, string> ClearMod(RuleCatalog catalog, Build build, ItemListing listing, ImmutableDictionary<ArmorSlot, ArmorEnergy> capacities, ModSocketPlace place)
    {
        var layout = LayOutMods(catalog, build, listing, capacities);
        return FindSocket(layout, place).Bind(socket => socket.Mod.IsSome()
            ? new Result<Build, string>.Ok(HoldMods(catalog, build, ReplaceMod(layout, place, Optional.None<HeldMod>())))
            : Refuse<Build>("That socket holds no mod."));
    }

    // ── the layout ──────────────────────────────────────────────────────────────────

    /// <summary>A socket before it is filled: its category and the slot whose piece it is on.</summary>
    private sealed record OpenSocket(PlugCategoryId Category, ArmorSlot Owner);

    private static ModLayout LayOut(
        RuleCatalog catalog, Build build, ImmutableDictionary<ItemHash, ListedMod> index, ItemListing listing, ImmutableDictionary<ArmorSlot, ArmorEnergy> capacities, ImmutableArray<HeldMod> held)
    {
        var categorised = held.Select(mod => (Mod: mod, Listed: index.TryGetValue(mod.Plug, out var found) ? Optional.Some(found) : Optional.None<ListedMod>())).ToImmutableArray();
        var sockets = Enum.GetValues<ArmorSlot>()
            .Select(slot => (Slot: slot, Sockets: FindPieceSockets(catalog, build, listing, slot)))
            .ToImmutableArray();
        var rows = sockets
            .Select(entry => (entry.Slot, Known: entry.Sockets.IsSome(), Sockets: entry.Sockets.Match(
                found => [.. found.Value.Where(category => category != ArmorModCategories.General)],
                _ => Enumerable.Repeat(ArmorModCategories.ToSlotCategory(entry.Slot), CountHeld(categorised, ArmorModCategories.ToSlotCategory(entry.Slot)) + 1).ToImmutableArray())))
            .ToImmutableArray();
        var general = sockets
            .SelectMany(entry => Enumerable.Repeat(entry.Slot, entry.Sockets.Match(found => found.Value.Count(category => category == ArmorModCategories.General), _ => 1)))
            .Select(slot => new OpenSocket(ArmorModCategories.General, slot))
            .ToImmutableArray();
        var open = rows.SelectMany(row => row.Sockets.Select(category => new OpenSocket(category, row.Slot))).ToImmutableArray();
        var filled = FillSockets(open, categorised.Where(entry => entry.Listed.Match(found => found.Value.Category != ArmorModCategories.General, _ => true)));
        var generalMods = categorised.Where(entry => entry.Listed.Match(found => found.Value.Category == ArmorModCategories.General, _ => false)).ToImmutableArray();
        var generalFilled = FillSockets(general, generalMods);
        var ownUse = Enum.GetValues<ArmorSlot>().ToImmutableDictionary(
            slot => slot,
            slot => filled.Sockets.Where(entry => entry.Socket.Owner == slot).Sum(entry => entry.Mod.Match(mod => FindCost(index, mod.Value), _ => 0)));
        var generalUse = AssignGeneralMods(general, generalFilled.Sockets.SelectMany(entry => ToSequence(entry.Mod)).Select(mod => FindCost(index, mod)), ownUse, capacities);
        return new ModLayout(
            [
                .. rows.Select(row => new PieceModSockets(
                    row.Slot,
                    row.Known,
                    [.. filled.Sockets.Where(entry => entry.Socket.Owner == row.Slot).Select((entry, at) => new ModSocket(entry.Socket.Category, entry.Mod, new ModSocketPlace(Optional.Some(row.Slot), at)))],
                    new ArmorEnergyUse(ArmorEnergy.From(ownUse[row.Slot] + generalUse.GetValueOrDefault(row.Slot, 0)), capacities.TryGetValue(row.Slot, out var capacity) ? Optional.Some(capacity) : Optional.None<ArmorEnergy>()))),
            ],
            [.. generalFilled.Sockets.Select((entry, at) => new ModSocket(entry.Socket.Category, entry.Mod, new ModSocketPlace(Optional.None<ArmorSlot>(), at)))],
            [.. filled.Left.Concat(generalFilled.Left).Where(mod => index.ContainsKey(mod.Plug))],
            [.. filled.Left.Where(mod => !index.ContainsKey(mod.Plug))]);
    }

    /// <summary>The mod sockets of the piece the build wears in a slot, by category, when the listing has them; none for no piece (or one it can't read).</summary>
    private static Optional<ImmutableArray<PlugCategoryId>> FindPieceSockets(RuleCatalog catalog, Build build, ItemListing listing, ArmorSlot slot) =>
        ArmorSwapping.ListPieces(catalog, build)
            .Where(piece => piece.Slot == slot)
            .SelectMany(piece => new[] { piece.Hash }.Concat(catalog.FindElementByHash(piece.Hash).Match(element => element.Value.Hashes.AsEnumerable(), _ => [])))
            .Select(hash => listing.ModSockets.TryGetValue(hash, out var sockets) ? Optional.Some(sockets) : Optional.None<ImmutableArray<PlugCategoryId>>())
            .FindFirstSome();

    private static int CountHeld(ImmutableArray<(HeldMod Mod, Optional<ListedMod> Listed)> held, PlugCategoryId category) =>
        held.Count(entry => entry.Listed.Match(found => found.Value.Category == category, _ => false));

    /// <summary>The mods in the sockets that take their category, in order (each the first free one); what no socket takes is left.</summary>
    private static (ImmutableArray<(OpenSocket Socket, Optional<HeldMod> Mod)> Sockets, ImmutableArray<HeldMod> Left) FillSockets(
        ImmutableArray<OpenSocket> sockets, IEnumerable<(HeldMod Mod, Optional<ListedMod> Listed)> mods) =>
        mods.Aggregate(
            (Sockets: sockets.Select(socket => (Socket: socket, Mod: Optional.None<HeldMod>())).ToImmutableArray(), Left: ImmutableArray<HeldMod>.Empty),
            (filling, entry) =>
            {
                var free = entry.Listed.Bind(found => filling.Sockets
                    .Select((socket, place) => (socket, place))
                    .Where(open => open.socket.Socket.Category == found.Category && !open.socket.Mod.IsSome())
                    .Select(open => Optional.Some(open.place))
                    .FindFirstSome());
                return free.Match(
                    at => (filling.Sockets.SetItem(at.Value, (filling.Sockets[at.Value].Socket, Optional.Some(entry.Mod))), filling.Left),
                    _ => (filling.Sockets, filling.Left.Add(entry.Mod)));
            });

    /// <summary>
    /// What the general mods add to each piece's energy: the costliest first, each to the piece with a free general socket
    /// and the most energy left (a piece whose capacity isn't known has all it needs), the earlier slot on a tie.
    /// </summary>
    private static ImmutableDictionary<ArmorSlot, int> AssignGeneralMods(
        ImmutableArray<OpenSocket> sockets, IEnumerable<int> costs, ImmutableDictionary<ArmorSlot, int> ownUse, ImmutableDictionary<ArmorSlot, ArmorEnergy> capacities) =>
        costs.OrderByDescending(cost => cost).Aggregate(
            (Free: sockets.Select(socket => socket.Owner).ToImmutableArray(), Added: ImmutableDictionary<ArmorSlot, int>.Empty),
            (assigning, cost) =>
            {
                var chosen = assigning.Free
                    .Distinct()
                    .OrderByDescending(slot => capacities.TryGetValue(slot, out var capacity) ? capacity.Value - ownUse[slot] - assigning.Added.GetValueOrDefault(slot, 0) : int.MaxValue)
                    .ThenBy(slot => slot)
                    .Select(Optional.Some)
                    .FindFirstSome();
                return chosen.Match(
                    slot => (assigning.Free.Remove(slot.Value), assigning.Added.SetItem(slot.Value, assigning.Added.GetValueOrDefault(slot.Value, 0) + cost)),
                    _ => assigning);
            }).Added;

    // ── picking ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Why <paramref name="mod"/> can't go in <paramref name="socket"/>: it takes a piece with a known capacity over it
    /// (or further over); none when it fits.
    /// </summary>
    private static Optional<string> CheckEnergy(
        RuleCatalog catalog, Build build, ImmutableDictionary<ItemHash, ListedMod> index, ItemListing listing, ImmutableDictionary<ArmorSlot, ArmorEnergy> capacities,
        ModLayout layout, ModSocket socket, ListedMod mod)
    {
        var after = LayOut(catalog, build, index, listing, capacities, ReplaceMod(layout, socket.Place, Optional.Some(ToHeldMod(catalog, mod.Item.Hash))));
        var over = after.Pieces
            .Where(piece => piece.Energy.Capacity.Match(capacity => piece.Energy.Used.Value > capacity.Value.Value, _ => false))
            .Where(piece => layout.Pieces.Single(before => before.Slot == piece.Slot).Energy.Used.Value < piece.Energy.Used.Value)
            .Select(Optional.Some)
            .FindFirstSome();
        return over.Map(piece => socket.Place.Slot.Match(
            slot => $"{mod.Item.Name} costs {mod.Cost.Value} energy: the {slot.Value.DescribeArmorSlot().ToLowerInvariant()} has {DescribeLeft(layout, index, socket, slot.Value)} left.",
            _ => $"{mod.Item.Name} costs {mod.Cost.Value} energy: no piece has that much left for a general mod."));
    }

    /// <summary>The energy a piece has left for the socket's mod (what the mod there now costs counts as left).</summary>
    private static int DescribeLeft(ModLayout layout, ImmutableDictionary<ItemHash, ListedMod> index, ModSocket socket, ArmorSlot slot)
    {
        var energy = layout.Pieces.Single(piece => piece.Slot == slot).Energy;
        var freed = socket.Mod.Match(mod => FindCost(index, mod.Value), _ => 0);
        return Math.Max(0, energy.Capacity.Match(capacity => capacity.Value.Value, _ => 0) - energy.Used.Value + freed);
    }

    /// <summary>The build's mods with the socket at <paramref name="place"/> holding <paramref name="mod"/> (none: empty), in the layout's order: the rows, the General row, then what no socket takes.</summary>
    private static ImmutableArray<HeldMod> ReplaceMod(ModLayout layout, ModSocketPlace place, Optional<HeldMod> mod) =>
    [
        .. layout.Pieces.SelectMany(piece => piece.Sockets)
            .Concat(layout.General)
            .SelectMany(socket => ToSequence(socket.Place == place ? mod : socket.Mod)),
        .. layout.Unplaced,
        .. layout.Unsocketed,
    ];

    /// <summary>
    /// The build holding <paramref name="mods"/>: the ones the rules know in <c>armorMods</c>, the rest left out; an
    /// element one of whose copies isn't its first hash has every copy in <c>armorModPlugs</c>, in order, so each keeps
    /// its place.
    /// </summary>
    private static Build HoldMods(RuleCatalog catalog, Build build, ImmutableArray<HeldMod> mods)
    {
        var mixed = mods
            .Where(mod => mod.Element.Match(id => FindFirstHash(catalog, id.Value) != Optional.Some(mod.Plug), _ => false))
            .SelectMany(mod => ToSequence(mod.Element))
            .ToImmutableHashSet();
        return build with
        {
            ArmorMods = [.. mods.SelectMany(mod => ToSequence(mod.Element))],
            ArmorModPlugs = [.. mods.Where(mod => mod.Element.Match(id => mixed.Contains(id.Value), _ => false)).Select(mod => mod.Plug)],
            LeftOut =
            [
                .. build.LeftOut.Where(item => item.Part != LoadoutPart.ArmorMod),
                .. mods.Where(mod => !mod.Element.IsSome()).Select(mod => new LeftOutItem(LoadoutPart.ArmorMod, mod.Plug)),
            ],
        };
    }

    private static HeldMod ToHeldMod(RuleCatalog catalog, ItemHash plug) =>
        new(plug, FindModElement(catalog, plug).Map(element => element.Id));

    /// <summary>The rules' armor mod a plug is (one of its element's hashes).</summary>
    private static Optional<BuildElement> FindModElement(RuleCatalog catalog, ItemHash plug) =>
        catalog.FindElementByHash(plug).Bind(element => element.Kind == ElementKind.ArmorMod ? Optional.Some(element) : Optional.None<BuildElement>());

    /// <summary>An element mod's copy: the first of <paramref name="plugs"/> that is one of its hashes (the rest stay for the next), else its first hash; none for an element without one.</summary>
    private static Optional<(ItemHash Plug, ImmutableArray<ItemHash> Others)> ChoosePlug(RuleCatalog catalog, ElementId id, ImmutableArray<ItemHash> plugs)
    {
        var hashes = catalog.Elements.TryGetValue(id, out var element) ? element.Hashes : [];
        var named = plugs.Where(hashes.Contains).Select(Optional.Some).FindFirstSome();
        return named.Match(
            plug => Optional.Some((plug.Value, plugs.Remove(plug.Value))),
            _ => FindFirstHash(catalog, id).Map(first => (first, plugs)));
    }

    private static Optional<ItemHash> FindFirstHash(RuleCatalog catalog, ElementId id) =>
        catalog.Elements.TryGetValue(id, out var element) && !element.Hashes.IsEmpty ? Optional.Some(element.Hashes[0]) : Optional.None<ItemHash>();

    private static Result<ModSocket, string> FindSocket(ModLayout layout, ModSocketPlace place) =>
        layout.Pieces.SelectMany(piece => piece.Sockets).Concat(layout.General)
            .Where(socket => socket.Place == place)
            .Select(socket => (Result<ModSocket, string>)new Result<ModSocket, string>.Ok(socket))
            .DefaultIfEmpty(new Result<ModSocket, string>.Error("The build has no such mod socket."))
            .First();

    private static Result<ListedMod, string> FindListedMod(ImmutableDictionary<ItemHash, ListedMod> index, ItemHash mod) =>
        index.TryGetValue(mod, out var found)
            ? new Result<ListedMod, string>.Ok(found)
            : Refuse<ListedMod>("Loopsmith doesn't know that mod (it isn't in Bungie's manifest as Loopsmith read it).");

    private static ImmutableDictionary<ItemHash, ListedMod> ToModIndex(ItemListing listing) =>
        listing.Mods.GroupBy(mod => mod.Item.Hash).ToImmutableDictionary(group => group.Key, group => group.First());

    /// <summary>A held mod's energy cost (the listing's; one it lacks is in no socket, so it costs no piece anything).</summary>
    private static int FindCost(ImmutableDictionary<ItemHash, ListedMod> index, HeldMod mod) =>
        index.TryGetValue(mod.Plug, out var found) ? found.Cost.Value : 0;

    /// <summary>True for the mods that change only stats (the general mods, an artifice socket's): no rule needs to know them.</summary>
    private static bool ChangesOnlyStats(PlugCategoryId category) =>
        category == ArmorModCategories.General || category == ArmorModCategories.Artifice;

    /// <summary>The subclass's element; none for Prismatic (its abilities are of several).</summary>
    private static Optional<Affinity> ToAffinity(Subclass subclass) =>
        subclass switch
        {
            Subclass.Arc => Optional.Some(Affinity.Arc),
            Subclass.Solar => Optional.Some(Affinity.Solar),
            Subclass.Void => Optional.Some(Affinity.Void),
            Subclass.Stasis => Optional.Some(Affinity.Stasis),
            Subclass.Strand => Optional.Some(Affinity.Strand),
            _ => Optional.None<Affinity>(),
        };

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(found => new[] { found.Value }, _ => []);

    private static Result<T, string> Refuse<T>(string why) =>
        new Result<T, string>.Error(why);
}

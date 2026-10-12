using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: one of a build's armor pieces swapped for another (a piece the player picked from their own armor,
/// docs/loop-format.md "Stats", or from every piece in the game, "Every item"), or taken off. The build's piece in that
/// slot — its exotic, or a legendary piece it leaves out — becomes the new one: an exotic the rules know is the build's
/// exotic, any other piece a left-out item. Which copy it wears is recorded (a piece of the game is none), and its set
/// bonuses follow the pieces it now wears: the bonuses of every set the old or the new pieces are from are the ones
/// those pieces grant (two Luminopotent pieces: its 2-piece bonus only); other bonuses stay.
/// </summary>
public static class ArmorSwapping
{
    /// <summary>The build wearing <paramref name="piece"/> (copy <paramref name="copy"/>) in <paramref name="slot"/>; or why it can't.</summary>
    public static Result<Build, string> SwapPiece(RuleCatalog catalog, Build build, ArmorSlot slot, ItemHash piece, ItemInstanceId copy) =>
        CheckPiece(catalog, build, slot, piece).Map(_ => WearPiece(catalog, build, slot, piece, Optional.Some(copy)));

    /// <summary>
    /// The build wearing <paramref name="piece"/> — a piece of the game, picked from every piece: no copy — in
    /// <paramref name="slot"/>; or why it can't (the excerpt must know it: a pick reads Bungie's manifest for it first).
    /// </summary>
    public static Result<Build, string> PutPiece(RuleCatalog catalog, Build build, ArmorSlot slot, ItemHash piece) =>
        CheckPiece(catalog, build, slot, piece).Map(_ => WearPiece(catalog, build, slot, piece, Optional.None<ItemInstanceId>()));

    /// <summary>The build with nothing in <paramref name="slot"/> (its set bonuses follow the pieces left); or why not, when it wears nothing there.</summary>
    public static Result<Build, string> RemovePiece(RuleCatalog catalog, Build build, ArmorSlot slot)
    {
        var before = ListPieces(catalog, build);
        var emptied = EmptySlot(catalog, build, before, slot);
        var after = ListPieces(catalog, emptied);
        return before.Any(worn => worn.Slot == slot)
            ? new Result<Build, string>.Ok(emptied with { ArmorSetBonuses = ListSetBonuses(catalog, build.ArmorSetBonuses, before, after) })
            : Refuse<Build>($"The build wears no {slot.DescribeArmorSlot().ToLowerInvariant()} piece Loopsmith knows.");
    }

    /// <summary>
    /// What "Every item" lists for <paramref name="slot"/>: every piece of <paramref name="listing"/> the build can wear
    /// there (that slot's, the build's class; one exotic), the one it wears there first, then by name (a name's versions
    /// side by side), each with its set; with a note on the exotic rule.
    /// </summary>
    public static ArmorItemOptions ListItemOptions(RuleCatalog catalog, Build build, ItemListing listing, ArmorSlot slot)
    {
        var worn = ListPieces(catalog, build).Where(piece => piece.Slot == slot).Select(piece => piece.Hash).ToImmutableHashSet();
        var otherExotic = FindExoticElsewhere(catalog, build, slot);
        return new ArmorItemOptions(
            slot,
            [
                .. listing.Items
                    .Where(item => CheckItem(build.Class, slot, otherExotic, Optional.Some(item)).IsOk())
                    .Select(item => new ArmorItemOption(item, FindSet(listing, item), worn.Contains(item.Hash), DescribeUnheldPerks(item)))
                    .OrderByDescending(option => option.Worn),
            ],
            [.. DescribeExoticRule(catalog, build, slot).Match(note => new[] { note.Value }, _ => [])]);
    }

    /// <summary>What a build can't hold of a piece: an exotic class item's two perks (its spirits), which nothing picks yet.</summary>
    private static Optional<string> DescribeUnheldPerks(ManifestItem item) =>
        item.Kind is ManifestKind.Armor { Slot: ArmorSlot.ClassItem } && item.Tier == Optional.Some(ItemTier.Exotic)
            ? Optional.Some("Its two spirits can't be picked yet: the build wears it without them.")
            : Optional.None<string>();

    private static Optional<ArmorSet> FindSet(ItemListing listing, ManifestItem item) =>
        item.Kind is ManifestKind.Armor { Set: Optional<ItemHash>.Some set } && listing.Sets.TryGetValue(set.Value, out var found)
            ? Optional.Some(found)
            : Optional.None<ArmorSet>();

    /// <summary>
    /// Whether the build can wear <paramref name="piece"/> in <paramref name="slot"/>: a piece the excerpt knows, for that
    /// slot, for the build's class (or any class), and an exotic only when the build wears none in another slot; or why not.
    /// </summary>
    public static Result<Unit, string> CheckPiece(RuleCatalog catalog, Build build, ArmorSlot slot, ItemHash piece) =>
        CheckItem(build.Class, slot, FindExoticElsewhere(catalog, build, slot), catalog.Manifest.FindItem(piece));

    /// <summary>
    /// Whether <paramref name="item"/> (of the excerpt, or of the listing) can go in <paramref name="slot"/>: armor for that
    /// slot, for <paramref name="guardianClass"/> (or any class), and an exotic only when <paramref name="otherExotic"/>
    /// (the build's exotic in another slot) is none. Or why not.
    /// </summary>
    private static Result<Unit, string> CheckItem(GuardianClass guardianClass, ArmorSlot slot, Optional<string> otherExotic, Optional<ManifestItem> item)
    {
        var armor = item.Bind(found => found.Kind is ManifestKind.Armor kind ? Optional.Some(kind) : Optional.None<ManifestKind.Armor>());
        var exotic = item.Match(found => found.Value.Tier == Optional.Some(ItemTier.Exotic), _ => false);
        return armor.Match(
            known => known.Value.Slot != slot
                ? Refuse($"{item.Match(found => found.Value.Name, _ => "That piece")} isn't for the {slot.DescribeArmorSlot().ToLowerInvariant()}.")
                : known.Value.Class.Match(wornBy => wornBy.Value != guardianClass, _ => false)
                    ? Refuse($"{item.Match(found => found.Value.Name, _ => "That piece")} is {DescribeClassArmor(known.Value.Class)}, and the build is for a {guardianClass}.")
                    : exotic && otherExotic.IsSome()
                        ? Refuse($"A build wears one exotic armor piece: swap {otherExotic.UnwrapOr("its exotic")} for a legendary piece first.")
                        : new Result<Unit, string>.Ok(new Unit.Value()),
            _ => Refuse("Loopsmith doesn't know that piece yet (Bungie's manifest wasn't read for it), so it can't put it in the build."));
    }

    /// <summary>What the armor picker says when the build's exotic is in another slot (no exotic can be picked here); none when it isn't.</summary>
    public static Optional<string> DescribeExoticRule(RuleCatalog catalog, Build build, ArmorSlot slot) =>
        FindExoticElsewhere(catalog, build, slot).Map(name => $"A build wears one exotic armor piece: swap {name} for a legendary piece to pick an exotic here.");

    /// <summary>The name of the build's exotic piece when it is in another slot than <paramref name="slot"/>.</summary>
    private static Optional<string> FindExoticElsewhere(RuleCatalog catalog, Build build, ArmorSlot slot) =>
        ListPieces(catalog, build)
            .Where(worn => worn.Slot != slot && worn.Exotic)
            .Select(worn => Optional.Some(worn.Name))
            .FindFirstSome();

    /// <summary>A piece the build wears: its slot, its hash, its name and whether it is exotic.</summary>
    private sealed record WornPiece(ArmorSlot Slot, ItemHash Hash, string Name, bool Exotic);

    /// <summary>The build's pieces the excerpt knows: its exotic (the first of its element's hashes that is armor), then the armor it leaves out.</summary>
    private static ImmutableArray<WornPiece> ListPieces(RuleCatalog catalog, Build build) =>
    [
        .. build.ExoticArmor
            .Bind(id => catalog.Elements.TryGetValue(id, out var element)
                ? element.Hashes.Select(hash => ToWornPiece(catalog, hash)).FindFirstSome()
                : Optional.None<WornPiece>())
            .Match(found => new[] { found.Value }, _ => []),
        .. build.LeftOut
            .Where(entry => entry.Part == LoadoutPart.Item)
            .SelectMany(entry => ToWornPiece(catalog, entry.Hash).Match(found => new[] { found.Value }, _ => [])),
    ];

    private static Optional<WornPiece> ToWornPiece(RuleCatalog catalog, ItemHash hash) =>
        catalog.Manifest.FindItem(hash).Bind(item => item.Kind is ManifestKind.Armor armor
            ? Optional.Some(new WornPiece(armor.Slot, hash, item.Name, item.Tier == Optional.Some(ItemTier.Exotic)))
            : Optional.None<WornPiece>());

    private static Build WearPiece(RuleCatalog catalog, Build build, ArmorSlot slot, ItemHash piece, Optional<ItemInstanceId> copy)
    {
        var before = ListPieces(catalog, build);
        var emptied = EmptySlot(catalog, build, before, slot);
        var exoticElement = catalog.FindElementByHash(piece).Bind(element => element.Kind == ElementKind.ExoticArmor ? Optional.Some(element.Id) : Optional.None<ElementId>());
        var worn = emptied with
        {
            ExoticArmor = exoticElement.Match(found => Optional.Some(found.Value), _ => emptied.ExoticArmor),
            LeftOut = exoticElement.IsSome() ? emptied.LeftOut : emptied.LeftOut.Add(new LeftOutItem(LoadoutPart.Item, piece)),
            ArmorCopies = [.. emptied.ArmorCopies.Where(entry => entry.Piece != piece), .. copy.Match(found => new[] { new ArmorCopy(piece, found.Value) }, _ => [])],
        };
        var after = ListPieces(catalog, worn);
        return worn with { ArmorSetBonuses = ListSetBonuses(catalog, build.ArmorSetBonuses, before, after) };
    }

    /// <summary>The build without the pieces it wears in <paramref name="slot"/> (of <paramref name="before"/>): its exotic when it is there, those left-out items and their copies.</summary>
    private static Build EmptySlot(RuleCatalog catalog, Build build, ImmutableArray<WornPiece> before, ArmorSlot slot)
    {
        var removed = before.Where(worn => worn.Slot == slot).Select(worn => worn.Hash).ToImmutableHashSet();
        var exoticRemoved = build.ExoticArmor.Match(
            id => catalog.Elements.TryGetValue(id.Value, out var element) && element.Hashes.Any(removed.Contains),
            _ => false);
        return build with
        {
            ExoticArmor = exoticRemoved ? Optional.None<ElementId>() : build.ExoticArmor,
            LeftOut = [.. build.LeftOut.Where(entry => entry.Part != LoadoutPart.Item || !removed.Contains(entry.Hash))],
            ArmorCopies = [.. build.ArmorCopies.Where(entry => !removed.Contains(entry.Piece))],
        };
    }

    /// <summary>
    /// The bonuses the pieces now grant, for every set the pieces before or after are from; the build's other bonuses
    /// (a set it wears no piece of, a bonus in no set the excerpt has) as they were.
    /// </summary>
    private static ImmutableArray<ElementId> ListSetBonuses(RuleCatalog catalog, ImmutableArray<ElementId> declared, ImmutableArray<WornPiece> before, ImmutableArray<WornPiece> after)
    {
        var worn = catalog.Manifest.CountSetPieces(after.Select(piece => piece.Hash));
        var affected = catalog.Manifest.CountSetPieces(before.Concat(after).Select(piece => piece.Hash)).Select(set => set.Set.Hash).ToImmutableHashSet();
        var kept = declared.Where(id => !FindSetsOfBonus(catalog, id).Any(affected.Contains));
        var granted = worn
            .SelectMany(set => set.ListActivePerks())
            .SelectMany(perk => catalog.FindElementByHash(perk.Perk).Match(
                element => element.Value.Kind == ElementKind.ArmorSetBonus ? new[] { element.Value.Id } : [],
                _ => []));
        return [.. kept.Concat(granted).Distinct()];
    }

    private static IEnumerable<ItemHash> FindSetsOfBonus(RuleCatalog catalog, ElementId bonus) =>
        catalog.Elements.TryGetValue(bonus, out var element)
            ? element.Hashes.SelectMany(hash => catalog.Manifest.FindSetOfPerk(hash).Match(found => new[] { found.Value.Set.Hash }, _ => []))
            : [];

    private static string DescribeClassArmor(Optional<GuardianClass> wornBy) =>
        wornBy.Match(only => $"{only.Value} armor", _ => "armor");

    private static Result<Unit, string> Refuse(string why) =>
        new Result<Unit, string>.Error(why);

    private static Result<T, string> Refuse<T>(string why) =>
        new Result<T, string>.Error(why);
}

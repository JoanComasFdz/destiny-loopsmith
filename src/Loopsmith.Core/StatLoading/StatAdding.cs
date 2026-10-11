using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.StatLoading;

/// <summary>
/// Pure: a build's stats from the player's own armor, added up the way DIM adds up a loadout's (docs/loop-format.md,
/// "Stats"). The build's pieces (a DIM loadout's: its exotic and the legendary pieces it left out) are matched with the
/// player's copies of them; each copy gives its roll (its armor-stat plugs) and its masterwork (Armor 3.0's adds only to
/// its stats at 0; Armor 2.0's +2 to each at full energy). Then the build's stat mods and tuning (Balanced Tuning, like
/// the masterwork, only to a piece's stats at 0, one per tuning socket) and its fragments' stats are added. Every number
/// comes from the manifest excerpt's <c>statPlugs</c>; a piece whose roll it doesn't know can't be added up.
/// </summary>
public static class StatAdding
{
    private const int LegacyFullEnergy = 10;
    private const int LegacyMasterworkBonus = 2;
    private const int MaxStat = 200;

    /// <summary>What a copy of a piece gives: its roll, that roll with its masterwork, and whether it has a tuning socket.</summary>
    private sealed record PieceRoll(StatPoints Rolled, StatPoints Worn, bool HasTuningSocket);

    /// <summary>
    /// The pieces the build wears: its exotic (by its element's hashes) and the armor its DIM loadout left out (the
    /// legendary pieces), each with its slot and name from the manifest excerpt; in slot order.
    /// </summary>
    public static ImmutableArray<ArmorPiece> ListArmorPieces(RuleCatalog catalog, Build build) =>
    [
        .. build.ExoticArmor.Match(exotic => ToSequence(ToExoticPiece(catalog, exotic.Value)), _ => [])
            .Concat(build.LeftOut
                .Where(item => item.Part == LoadoutPart.Item)
                .Select(item => item.Hash)
                .Distinct()
                .SelectMany(hash => ToSequence(ToPiece(catalog.Manifest, hash))))
            .OrderBy(piece => piece.Slot),
    ];

    /// <summary>For each piece the build wears, the player's copies of it (the most points first); and their characters of the build's class.</summary>
    public static StatChoices ListStatChoices(RuleCatalog catalog, Build build, BungieProfile profile) =>
        new(
            [.. ListArmorPieces(catalog, build).Select(piece => new PieceChoice(piece, ListCopies(catalog, piece, profile)))],
            [.. profile.Characters.Where(character => character.Class == build.Class)]);

    /// <summary>
    /// The build's stats with the copies picked (by slot, the instance id; a slot not picked takes its first copy): the
    /// armor, then the build's mods and fragments, each a part of the total. An error when the build doesn't name a
    /// piece for every slot, or the player doesn't own one, or Loopsmith can't read one's roll.
    /// </summary>
    public static Result<StatTotals, string> AddUpStats(RuleCatalog catalog, Build build, StatChoices choices, ImmutableDictionary<ArmorSlot, string> picks)
    {
        var plugs = catalog.Manifest.StatPlugs;
        var unnamed = Enum.GetValues<ArmorSlot>().Where(slot => !choices.Pieces.Any(choice => choice.Piece.Slot == slot)).ToImmutableArray();
        var unowned = choices.Pieces.Where(choice => choice.Copies.IsEmpty).Select(choice => choice.Piece.Name).ToImmutableArray();
        var copies = choices.Pieces.Where(choice => !choice.Copies.IsEmpty).Select(choice => (choice.Piece, Copy: PickCopy(choice, picks))).ToImmutableArray();
        var rolls = copies.Select(chosen => (chosen.Piece, Roll: ReadRoll(plugs, chosen.Copy.Armor))).ToImmutableArray();
        var unread = rolls.Where(chosen => !chosen.Roll.IsSome()).Select(chosen => chosen.Piece.Name).ToImmutableArray();
        return !unnamed.IsEmpty
            ? Fail($"The build names no armor piece for {JoinWithAnd(unnamed.Select(DescribeSlot))} (a DIM loadout's pieces), so Loopsmith can't add up its stats from your armor. Use a character's current stats, or type them.")
            : !unowned.IsEmpty
                ? Fail($"Your account has no {JoinWithAnd(unowned)}, so Loopsmith can't add up the build's stats from your armor.")
                : !unread.IsEmpty
                    ? Fail($"Loopsmith can't read the stats of your {JoinWithAnd(unread)} (Bungie's data may be newer than Loopsmith's). Type the stats instead.")
                    : new Result<StatTotals, string>.Ok(AddUp(catalog, build, [.. rolls.SelectMany(chosen => ToSequence(chosen.Roll))]));
    }

    /// <summary>A stat as typed: empty for unknown ("?"), else a whole number 0..200; or why it isn't one.</summary>
    public static Result<Optional<StatValue>, string> ReadStatInput(StatKind stat, string text) =>
        text.Trim().Length == 0
            ? new Result<Optional<StatValue>, string>.Ok(Optional.None<StatValue>())
            : ParseStat(text.Trim()).Match<Result<Optional<StatValue>, string>>(
                value => new Result<Optional<StatValue>, string>.Ok(Optional.Some(value.Value)),
                _ => new Result<Optional<StatValue>, string>.Error($"{stat}: a whole number from 0 to 200, or empty for \"?\"."));

    private static Optional<StatValue> ParseStat(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && StatValue.TryFrom(number).IsSuccess
            ? Optional.Some(StatValue.From(number))
            : Optional.None<StatValue>();

    /// <summary>What a copy gives: its roll and masterwork; none when the excerpt knows none of its armor-stat plugs.</summary>
    public static Optional<StatPoints> ReadCopyPoints(RuleCatalog catalog, OwnedArmor armor) =>
        ReadRoll(catalog.Manifest.StatPlugs, armor).Map(roll => roll.Worn);

    private static StatTotals AddUp(RuleCatalog catalog, Build build, ImmutableArray<PieceRoll> rolls)
    {
        var mods = ListModPlugs(catalog, build);
        var balanced = mods.Where(mod => mod.Plug.OnlyEmptyStats).ToImmutableArray();
        var sockets = rolls.Where(roll => roll.HasTuningSocket).ToImmutableArray();
        var copies = balanced.Sum(mod => mod.Copies);
        var tuned = sockets.Take(copies).ToImmutableArray();
        var parts = ImmutableArray.Create(new StatPart("Armor", rolls.Select(roll => roll.Worn).Sum()))
            .AddRange(mods.Where(mod => !mod.Plug.OnlyEmptyStats).Select(mod => new StatPart(DescribeCopies(mod.Name, mod.Copies), mod.Plug.ToPoints(build.Class).Times(mod.Copies))))
            .AddRange(tuned.IsEmpty ? [] : balanced.Select(mod => new StatPart(DescribeCopies(mod.Name, tuned.Length), tuned.Select(roll => mod.Plug.ToPointsOnEmpty(roll.Rolled)).Sum())))
            .AddRange(ListSubclassPlugs(catalog, build).Select(plug => new StatPart(plug.Name, plug.Plug.ToPoints(build.Class))));
        var total = parts.Select(part => part.Points).Sum();
        var untuned = copies - tuned.Length;
        var notes = ImmutableArray.Create(untuned > 0 ? $"{DescribeCopies(balanced[0].Name, untuned)} found no tuning socket on these pieces, so it adds nothing." : "")
            .AddRange(StatPointsArithmetic.AllStats.Select(stat => DescribeClamped(stat, total.Read(stat))))
            .Where(note => note.Length > 0)
            .ToImmutableArray();
        return new StatTotals(ToStatLine(total), parts, notes);
    }

    /// <summary>A mod or fragment of the build that changes a stat: its name, its stat plug and its copies.</summary>
    private sealed record NamedPlug(string Name, StatPlug Plug, int Copies);

    /// <summary>The build's mods with a stat plug (a stat mod, a tuning mod): its armor mods' and its DIM loadout's left-out mods, by copies.</summary>
    private static ImmutableArray<NamedPlug> ListModPlugs(RuleCatalog catalog, Build build) =>
    [
        .. build.ArmorMods.SelectMany(mod => ToSequence(FindElementPlug(catalog, mod)))
            .Concat(build.LeftOut.Where(item => item.Part == LoadoutPart.ArmorMod).Select(item => item.Hash))
            .Where(hash => catalog.Manifest.StatPlugs.TryGetValue(hash, out var plug) && plug.Kind is StatPlugKind.Mod or StatPlugKind.Tuning)
            .GroupBy(hash => hash)
            .Select(group => new NamedPlug(DescribeHash(catalog, group.Key), catalog.Manifest.StatPlugs[group.Key], group.Count())),
    ];

    /// <summary>The build's subclass plugs with stats (Spark of Shock's −10 Grenade): its aspects, fragments and abilities, and left-out subclass plugs.</summary>
    private static ImmutableArray<NamedPlug> ListSubclassPlugs(RuleCatalog catalog, Build build) =>
    [
        .. new[] { build.Abilities.Super, build.Abilities.Grenade, build.Abilities.Melee, build.Abilities.ClassAbility }
            .SelectMany(ability => ability.Match(found => new[] { found.Value }, _ => []))
            .Concat(build.Aspects)
            .Concat(build.Fragments)
            .SelectMany(element => ToSequence(FindElementPlug(catalog, element)))
            .Concat(build.LeftOut.Where(item => item.Part == LoadoutPart.SubclassPlug).Select(item => item.Hash))
            .Where(hash => catalog.Manifest.StatPlugs.TryGetValue(hash, out var plug) && plug.Kind == StatPlugKind.Subclass)
            .Distinct()
            .Select(hash => new NamedPlug(DescribeHash(catalog, hash), catalog.Manifest.StatPlugs[hash], 1)),
    ];

    /// <summary>The first of an element's hashes the excerpt has a stat plug for.</summary>
    private static Optional<ItemHash> FindElementPlug(RuleCatalog catalog, ElementId id) =>
        catalog.Elements.TryGetValue(id, out var element)
            ? element.Hashes.Where(hash => catalog.Manifest.StatPlugs.ContainsKey(hash)).Select(Optional.Some).FindFirstSome()
            : Optional.None<ItemHash>();

    private static ImmutableArray<PieceCopy> ListCopies(RuleCatalog catalog, ArmorPiece piece, BungieProfile profile) =>
    [
        .. profile.Armor
            .Where(armor => piece.Hashes.Contains(armor.Hash))
            .Select(armor => new PieceCopy(armor, ReadCopyPoints(catalog, armor)))
            .OrderByDescending(copy => copy.Points.Match(points => ToTotal(points.Value), _ => -1)),
    ];

    private static PieceCopy PickCopy(PieceChoice choice, ImmutableDictionary<ArmorSlot, string> picks) =>
        picks.TryGetValue(choice.Piece.Slot, out var instance)
            ? choice.Copies.Where(copy => copy.Armor.InstanceId == instance).Select(Optional.Some).FindFirstSome().UnwrapOr(choice.Copies[0])
            : choice.Copies[0];

    /// <summary>
    /// A copy's roll (its armor-stat and intrinsic plugs' stats), and that roll with its masterwork: Armor 3.0's
    /// masterwork plug adds only to the stats at 0; an Armor 2.0 piece (no such plug) gets +2 to each at full energy.
    /// </summary>
    private static Optional<PieceRoll> ReadRoll(ImmutableDictionary<ItemHash, StatPlug> statPlugs, OwnedArmor armor)
    {
        var plugs = armor.Plugs.SelectMany(hash => statPlugs.TryGetValue(hash, out var plug) ? new[] { plug } : []).ToImmutableArray();
        var rolled = plugs.Where(plug => plug.Kind == StatPlugKind.Base).Select(plug => plug.ToPoints()).Sum();
        var masterworks = plugs.Where(plug => plug.Kind == StatPlugKind.Masterwork).ToImmutableArray();
        var worn = masterworks.IsEmpty
            ? rolled.Add(armor.EnergyCapacity.Match(energy => energy.Value >= LegacyFullEnergy, _ => false) ? AllStatsBy(LegacyMasterworkBonus) : StatPointsArithmetic.Zero)
            : rolled.Add(masterworks.Select(plug => plug.ToPointsOnEmpty(rolled)).Sum());
        return plugs.Any(plug => plug.Kind == StatPlugKind.Base)
            ? Optional.Some(new PieceRoll(rolled, worn, plugs.Any(plug => plug.Kind == StatPlugKind.Tuning)))
            : Optional.None<PieceRoll>();
    }

    private static StatPoints AllStatsBy(int value) =>
        new(value, value, value, value, value, value);

    private static Optional<ArmorPiece> ToExoticPiece(RuleCatalog catalog, ElementId exotic) =>
        catalog.Elements.TryGetValue(exotic, out var element)
            ? element.Hashes.Select(hash => catalog.Manifest.FindItem(hash).Bind(item => item.ReadArmorSlot())).FindFirstSome()
                .Map(slot => new ArmorPiece(slot, element.Name, element.Hashes))
            : Optional.None<ArmorPiece>();

    private static Optional<ArmorPiece> ToPiece(ManifestExcerpt manifest, ItemHash hash) =>
        manifest.FindItem(hash).Bind(item => item.Kind is ManifestKind.Armor armor
            ? Optional.Some(new ArmorPiece(armor.Slot, item.Name, [hash]))
            : Optional.None<ArmorPiece>());

    private static string DescribeHash(RuleCatalog catalog, ItemHash hash) =>
        catalog.Manifest.FindItem(hash).Map(item => item.Name)
            .Match(name => name.Value, _ => catalog.FindElementByHash(hash).Match(element => element.Value.Name, _ => $"plug {hash.Value}"));

    private static string DescribeCopies(string name, int copies) =>
        copies > 1 ? $"{name} ×{copies}" : name;

    /// <summary>"Grenade adds up to 215, and the game counts 200": a total past 0..200, as the game holds it.</summary>
    private static string DescribeClamped(StatKind stat, int value) =>
        value > MaxStat
            ? $"{stat} adds up to {value.ToString(CultureInfo.InvariantCulture)}, and the game counts {MaxStat}."
            : value < 0 ? $"{stat} adds up to {value.ToString(CultureInfo.InvariantCulture)}, and the game counts 0." : "";

    private static StatLine ToStatLine(StatPoints total) =>
        new(ToStat(total.Weapons), ToStat(total.Health), ToStat(total.Class), ToStat(total.Grenade), ToStat(total.Super), ToStat(total.Melee));

    private static Optional<StatValue> ToStat(int value) =>
        Optional.Some(StatValue.From(Math.Clamp(value, 0, MaxStat)));

    private static int ToTotal(StatPoints points) =>
        points.Weapons + points.Health + points.Class + points.Grenade + points.Super + points.Melee;

    private static string DescribeSlot(ArmorSlot slot) =>
        slot switch
        {
            ArmorSlot.Helmet => "the helmet",
            ArmorSlot.Arms => "the arms",
            ArmorSlot.Chest => "the chest",
            ArmorSlot.Legs => "the legs",
            _ => "the class item",
        };

    private static string JoinWithAnd(IEnumerable<string> parts)
    {
        var list = parts.ToImmutableArray();
        return list.Length <= 1 ? string.Concat(list) : $"{string.Join(", ", list[..^1])} and {list[^1]}";
    }

    private static Result<StatTotals, string> Fail(string why) =>
        new Result<StatTotals, string>.Error(why);

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);
}

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
/// the masterwork, only to a piece's stats at 0, one per tuning socket) and its fragments' stats are added. The numbers
/// come from the manifest excerpt's <c>statPlugs</c>, but for Armor 2.0's masterwork, which Bungie's data doesn't hold:
/// it is DIM's rule (<c>generateAssumedMasterworkStats</c> in DIM's <c>inventory/store/stats.ts</c>). A piece whose roll
/// the excerpt doesn't know can't be added up.
/// </summary>
public static class StatAdding
{
    /// <summary>DIM's rule for an Armor 2.0 piece (no masterwork plug): at full energy, +2 to each stat.</summary>
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

    /// <summary>For each piece the build wears, the player's copies of it (the most points first).</summary>
    public static StatChoices ListStatChoices(RuleCatalog catalog, Build build, BungieProfile profile) =>
        new([.. ListArmorPieces(catalog, build).Select(piece => new PieceChoice(piece, ListCopies(catalog, piece, profile)))]);

    /// <summary>
    /// The build's stats from the copies it wears (<see cref="Build.ArmorCopies"/>, when the player owns them; else
    /// their copy with the most points): each piece, then the build's mods and fragments, each a part of the total. An
    /// error when the build doesn't name a piece for every slot, or the player doesn't own one, or Loopsmith can't read
    /// one's roll.
    /// </summary>
    public static Result<StatTotals, string> AddUpStats(RuleCatalog catalog, Build build, StatChoices choices)
    {
        var plugs = catalog.Manifest.StatPlugs;
        var unnamed = Enum.GetValues<ArmorSlot>().Where(slot => !choices.Pieces.Any(choice => choice.Piece.Slot == slot)).ToImmutableArray();
        var unowned = choices.Pieces.Where(choice => choice.Copies.IsEmpty).Select(choice => choice.Piece.Name).ToImmutableArray();
        var copies = choices.Pieces.SelectMany(choice => ToSequence(FindWornCopy(choice, build.ArmorCopies).Map(copy => (choice.Piece, Copy: copy)))).ToImmutableArray();
        var rolls = copies.Select(chosen => (chosen.Piece, Roll: ReadRoll(plugs, chosen.Copy.Armor))).ToImmutableArray();
        var unread = rolls.Where(chosen => !chosen.Roll.IsSome()).Select(chosen => chosen.Piece.Name).ToImmutableArray();
        return !unnamed.IsEmpty
            ? Fail($"The build names no armor piece for {JoinWithAnd(unnamed.Select(DescribeSlot))} (a DIM loadout's pieces), so Loopsmith can't add up its stats from your armor. Pick a piece for it with ⋯, or type the stats.")
            : !unowned.IsEmpty
                ? Fail($"Your account has no {JoinWithAnd(unowned)}, so Loopsmith can't add up the build's stats from your armor.")
                : !unread.IsEmpty
                    ? Fail($"Loopsmith can't read the stats of your {JoinWithAnd(unread)} (Bungie's data may be newer than Loopsmith's). Type the stats instead.")
                    : new Result<StatTotals, string>.Ok(AddUp(catalog, build, [.. rolls.SelectMany(chosen => ToSequence(chosen.Roll.Map(roll => (chosen.Piece, roll))))]));
    }

    /// <summary>
    /// The copy of a piece the build wears, when the player owns it (by instance id: a dim.gg share's, or picked); else
    /// their first (the most points); none when they own no copy.
    /// </summary>
    public static Optional<PieceCopy> FindWornCopy(PieceChoice choice, ImmutableArray<ArmorCopy> worn)
    {
        var instances = worn.Where(copy => choice.Piece.Hashes.Contains(copy.Piece)).Select(copy => copy.Instance).ToImmutableHashSet();
        return choice.Copies.Where(copy => instances.Contains(copy.Armor.InstanceId))
            .Concat(choice.Copies)
            .Select(Optional.Some)
            .FindFirstSome();
    }

    /// <summary>
    /// Whether the copies the build wears are the player's: every piece's copy in their account (the share is theirs),
    /// some, or none (the build names no copy, or none of them is theirs).
    /// </summary>
    public static LoadoutOwnership ReadOwnership(RuleCatalog catalog, Build build, BungieProfile profile)
    {
        var owned = profile.Armor.Select(armor => armor.InstanceId).ToImmutableHashSet();
        var pieces = ListArmorPieces(catalog, build)
            .Select(piece => (piece.Name, Mine: build.ArmorCopies.Any(copy => piece.Hashes.Contains(copy.Piece) && owned.Contains(copy.Instance))))
            .ToImmutableArray();
        var missing = pieces.Where(piece => !piece.Mine).Select(piece => piece.Name).ToImmutableArray();
        return missing.Length == pieces.Length
            ? new LoadoutOwnership.NotOwn()
            : missing.IsEmpty
                ? new LoadoutOwnership.Own()
                : new LoadoutOwnership.PartlyOwn(missing);
    }

    /// <summary>What the Stats section says after the login, for whose the build's copies are.</summary>
    public static string DescribeOwnership(LoadoutOwnership ownership) =>
        ownership.Match(
            _ => "Your armor is loaded: the build wears this loadout's own pieces, and its stats are theirs. Tap ⋯ next to a piece to wear another.",
            partly => $"This loadout's pieces are in your account, but for {JoinWithAnd(partly.Missing)}: tap ⋯ next to {(partly.Missing.Length == 1 ? "it" : "each")} in the build to pick one of yours.",
            _ => "You can now pick your armor: tap ⋯ next to each armor piece in the build. Its stats follow the pieces it wears.");

    /// <summary>
    /// The player's pieces for a slot, for the armor picker: the copy the build wears first, then its other copies, then
    /// the rest by name. Its note counts the pieces the excerpt can't place. What the build can wear of them (its class,
    /// one exotic) is BuildComposition's to say (<c>ArmorSwapping.CheckPiece</c>).
    /// </summary>
    public static ArmorOptions ListArmorOptions(RuleCatalog catalog, Build build, BungieProfile profile, ArmorSlot slot)
    {
        var pieces = ListArmorPieces(catalog, build);
        var piece = pieces.Where(found => found.Slot == slot).Select(Optional.Some).FindFirstSome();
        var worn = piece
            .Bind(found => FindWornCopy(new PieceChoice(found, ListCopies(catalog, found, profile)), build.ArmorCopies))
            .Map(copy => copy.Armor.InstanceId);
        var named = profile.Armor.Select(armor => (Armor: armor, Item: catalog.Manifest.FindItem(armor.Hash))).ToImmutableArray();
        var options = named
            .SelectMany(entry => ToSequence(entry.Item).Select(item => (entry.Armor, Item: item)))
            .Where(entry => entry.Item.Kind is ManifestKind.Armor armor && armor.Slot == slot)
            .Select(entry => new ArmorOption(entry.Armor, entry.Item, ReadCopyPoints(catalog, entry.Armor), worn == Optional.Some(entry.Armor.InstanceId)))
            .OrderByDescending(option => option.Worn)
            .ThenByDescending(option => piece.Match(found => found.Value.Hashes.Contains(option.Armor.Hash), _ => false))
            .ThenBy(option => option.Item.Name, StringComparer.Ordinal)
            .ThenByDescending(option => option.Points.Match(points => ToTotal(points.Value), _ => -1))
            .ToImmutableArray();
        var unnamed = named.Count(entry => !entry.Item.IsSome());
        ImmutableArray<string> notes = unnamed > 0 ? [$"{unnamed} of your armor pieces aren't listed: Loopsmith can't tell their slot yet (Bungie's manifest wasn't read for them)."] : [];
        return new ArmorOptions(slot, options, notes);
    }

    /// <summary>
    /// The player's copy the build wears in a slot, as DIM resolves a loadout's item: the one the build names (a dim.gg
    /// share's, or picked) when it is in their account, else their copy of that piece with the most points (the copy the
    /// stats add up and the picker ticks: <see cref="FindWornCopy"/>); none when they own no copy, or the build names no piece.
    /// </summary>
    public static Optional<OwnedArmor> FindWornArmor(RuleCatalog catalog, Build build, BungieProfile profile, ArmorSlot slot) =>
        ListArmorPieces(catalog, build).Where(piece => piece.Slot == slot).Select(Optional.Some).FindFirstSome()
            .Bind(piece => FindWornCopy(new PieceChoice(piece, ListCopies(catalog, piece, profile)), build.ArmorCopies))
            .Map(copy => copy.Armor);

    /// <summary>
    /// The player's copy of a build's weapon, as DIM resolves a loadout's item: the one the build names (a dim.gg share's)
    /// when it is in their account, else their copy of that weapon with the highest tier; none when they own none.
    /// </summary>
    public static Optional<OwnedWeapon> FindWornWeapon(BungieProfile profile, WeaponLoadout weapon)
    {
        var copies = profile.Weapons.Where(owned => weapon.Hash == Optional.Some(owned.Hash)).ToImmutableArray();
        return profile.Weapons.Where(owned => weapon.Instance == Optional.Some(owned.InstanceId))
            .Concat(copies.OrderByDescending(owned => owned.Marks.Tier.Match(tier => tier.Value.Value, _ => 0)))
            .Select(Optional.Some)
            .FindFirstSome();
    }

    /// <summary>The player's copy with this instance id, or why there is none.</summary>
    public static Result<OwnedArmor, string> FindArmor(BungieProfile profile, ItemInstanceId instance) =>
        profile.Armor.Where(armor => armor.InstanceId == instance).Select(Optional.Some).FindFirstSome()
            .ToResult(() => "That piece isn't in your account any more: log in again to read your armor.");


    /// <summary>What a copy gives: its roll and masterwork; none when the excerpt knows none of its armor-stat plugs.</summary>
    public static Optional<StatPoints> ReadCopyPoints(RuleCatalog catalog, OwnedArmor armor) =>
        ReadRoll(catalog.Manifest.StatPlugs, armor).Map(roll => roll.Worn);

    private static StatTotals AddUp(RuleCatalog catalog, Build build, ImmutableArray<(ArmorPiece Piece, PieceRoll Roll)> worn)
    {
        var rolls = worn.Select(entry => entry.Roll).ToImmutableArray();
        var mods = ListModPlugs(catalog, build);
        var balanced = mods.Where(mod => mod.Plug is StatPlug.BalancedTuning).ToImmutableArray();
        var sockets = rolls.Where(roll => roll.HasTuningSocket).ToImmutableArray();
        var copies = balanced.Sum(mod => mod.Copies);
        var tuned = sockets.Take(copies).ToImmutableArray();
        var parts = worn.Select(entry => new StatPart(entry.Piece.Name, entry.Roll.Worn)).ToImmutableArray()
            .AddRange(mods.Where(mod => mod.Plug is not StatPlug.BalancedTuning).Select(mod => new StatPart(DescribeCopies(mod.Name, mod.Copies), mod.Plug.ToPoints(build.Class).MultiplyBy(mod.Copies))))
            .AddRange(tuned.IsEmpty ? [] : balanced.Select(mod => new StatPart(DescribeCopies(mod.Name, tuned.Length), tuned.Select(roll => mod.Plug.ToPointsOnEmpty(roll.Rolled)).Sum())))
            .AddRange(ListSubclassPlugs(catalog, build).Select(plug => new StatPart(plug.Name, plug.Plug.ToPoints(build.Class))));
        var total = parts.Select(part => part.Points).Sum();
        var untuned = copies - tuned.Length;
        var notes = balanced.Take(1).Where(_ => untuned > 0)
            .Select(mod => $"{DescribeCopies(mod.Name, untuned)} found no tuning socket on these pieces, so it adds nothing.")
            .Concat(StatPointsArithmetic.AllStats.SelectMany(stat => ToSequence(DescribeClamped(stat, total.ReadPoints(stat)))))
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
            .Where(hash => catalog.Manifest.StatPlugs.TryGetValue(hash, out var plug) && plug is StatPlug.StatMod or StatPlug.Tuning or StatPlug.BalancedTuning)
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
            .Where(hash => catalog.Manifest.StatPlugs.TryGetValue(hash, out var plug) && plug is StatPlug.Fragment)
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

    /// <summary>
    /// A copy's roll (its armor-stat and intrinsic plugs' stats), and that roll with its masterwork: Armor 3.0's
    /// masterwork plug adds only to the stats at 0; an Armor 2.0 piece (no such plug) gets +2 to each at full energy.
    /// </summary>
    private static Optional<PieceRoll> ReadRoll(ImmutableDictionary<ItemHash, StatPlug> statPlugs, OwnedArmor armor)
    {
        var plugs = armor.Plugs.SelectMany(hash => statPlugs.TryGetValue(hash, out var plug) ? new[] { plug } : []).ToImmutableArray();
        var rolled = plugs.Where(plug => plug is StatPlug.Rolled).Select(plug => plug.ToPoints()).Sum();
        var masterworks = plugs.Where(plug => plug is StatPlug.Masterwork).ToImmutableArray();
        var worn = masterworks.IsEmpty
            ? rolled.Add(armor.EnergyCapacity.Match(energy => energy.Value >= LegacyFullEnergy, _ => false) ? StatPointsArithmetic.ToPointsInEachStat(LegacyMasterworkBonus) : StatPointsArithmetic.Zero)
            : rolled.Add(masterworks.Select(plug => plug.ToPointsOnEmpty(rolled)).Sum());
        return plugs.Any(plug => plug is StatPlug.Rolled)
            ? Optional.Some(new PieceRoll(rolled, worn, plugs.Any(plug => plug is StatPlug.TuningSocket or StatPlug.Tuning or StatPlug.BalancedTuning)))
            : Optional.None<PieceRoll>();
    }

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

    /// <summary>"Grenade adds up to 215, and the game counts 200": a total past 0..200, as the game holds it; none within it.</summary>
    private static Optional<string> DescribeClamped(StatKind stat, int value) =>
        value > MaxStat || value < 0
            ? Optional.Some($"{stat} adds up to {value.ToString(CultureInfo.InvariantCulture)}, and the game counts {Math.Clamp(value, 0, MaxStat).ToString(CultureInfo.InvariantCulture)}.")
            : Optional.None<string>();

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

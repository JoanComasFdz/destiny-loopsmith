using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Loopsmith.Functional;
using static Loopsmith.ManifestTools.Core.DefinitionReading;

namespace Loopsmith.ManifestTools.Core;

/// <summary>What Loopsmith calls an item (its <c>kind</c>), and its weapon or armor slot where it has one.</summary>
public sealed record ItemKind(string Kind, Optional<string> Slot);

/// <summary>
/// An item's definition → what the excerpt says of it: its kind and slot, its sockets' perks and mods, its intrinsic perk,
/// catalyst and breaker, an artifact's tiers and a subclass's plugs.
/// </summary>
public static partial class ItemReading
{
    private static readonly FrozenDictionary<string, string> WeaponBuckets =
        new Dictionary<string, string> { ["Kinetic Weapons"] = "kinetic", ["Energy Weapons"] = "energy", ["Power Weapons"] = "power" }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string> ArmorBuckets = new Dictionary<string, string>
    {
        ["Helmet"] = "helmet", ["Gauntlets"] = "arms", ["Chest Armor"] = "chest", ["Leg Armor"] = "legs", ["Class Armor"] = "classItem",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string> ModSlots = new Dictionary<string, string>
    {
        ["Helmet Armor Mod"] = "helmet", ["Arms Armor Mod"] = "arms", ["Chest Armor Mod"] = "chest", ["Leg Armor Mod"] = "legs",
        ["Class Item Armor Mod"] = "classItem", ["General Armor Mod"] = "general",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string> ExactKinds = new Dictionary<string, string>
    {
        ["Super Ability"] = "super", ["Class Ability"] = "classAbility", ["Movement Ability"] = "movement",
        ["Artifact Perk"] = "artifactPerk", ["Artifact"] = "artifact", ["Trait"] = "weaponPerk", ["Enhanced Trait"] = "weaponPerk",
        ["Weapon Mod"] = "weaponMod", ["Enhanced Weapon Mod"] = "weaponMod", ["Intrinsic"] = "intrinsic",
    }.ToFrozenDictionary();

    private static readonly ImmutableArray<(string Suffix, string Kind)> SuffixKinds =
        [(" Grenade", "grenade"), (" Melee", "melee"), (" Aspect", "aspect"), (" Fragment", "fragment")];

    /// <summary>A raid's, an activity's or a retired season's mods, and the Dreaming City's: not supported (ADRs D35).</summary>
    private static readonly ImmutableArray<string> ActivityModPrefixes = ["enhancements.raid_", "enhancements.season_"];

    private static readonly FrozenSet<string> ActivityModCategories = FrozenSet.Create("enhancements.rivens_curse");

    /// <summary>The plug category of an exotic's empty catalyst socket.</summary>
    private const string EmptyCatalyst = "v400.empty.exotic.masterwork";

    /// <summary>Only a weapon's mod socket takes them.</summary>
    private static readonly FrozenSet<string> ModSocketCategories =
        FrozenSet.Create("v400.weapon.mod_guns", "v400.weapon.mod_damage", "v400.weapon.mod_magazine");

    /// <summary>The plug category of a mod socket's empty plugs ("Empty Mod Socket", "Random Masterwork").</summary>
    private const string EmptyMod = "v400.weapon.mod_empty";

    /// <summary>What an artifact's perk sockets take…</summary>
    private const string ArtifactPerks = "artifact_perks";

    /// <summary>…and its reset socket too, with its Reset Artifact plug.</summary>
    private const string ArtifactReset = "artifact_reset";

    public static ItemKind ClassifyItem(JsonElement item, ManifestTable buckets)
    {
        var typeName = ReadString(item, "itemTypeDisplayName").UnwrapOr("");
        var bucketHash = ReadObject(item, "inventory").Bind(inventory => ReadNumber(inventory, "bucketTypeHash"));
        var bucket = bucketHash.Bind(buckets.FindDefinition).Map(ReadName).UnwrapOr("");
        var category = ReadPlugCategory(item);
        return bucket == "Subclass" ? new ItemKind("subclass", Optional.None<string>())
            : WeaponBuckets.TryGetValue(bucket, out var weaponSlot) ? new ItemKind("weapon", Optional.Some(weaponSlot))
            : ArmorBuckets.TryGetValue(bucket, out var armorSlot) ? new ItemKind("armor", Optional.Some(armorSlot))
            : ActivityModPrefixes.Any(prefix => category.StartsWith(prefix, StringComparison.Ordinal)) || ActivityModCategories.Contains(category) ? new ItemKind("activityMod", Optional.None<string>())
            : typeName.EndsWith("Armor Mod", StringComparison.Ordinal)
                ? new ItemKind("armorMod", ModSlots.TryGetValue(typeName, out var modSlot) ? Optional.Some(modSlot) : Optional.None<string>())
            : ExactKinds.TryGetValue(typeName, out var exact) ? new ItemKind(exact, Optional.None<string>())
            : new ItemKind(
                SuffixKinds.Where(pair => typeName.EndsWith(pair.Suffix, StringComparison.Ordinal)).Select(pair => pair.Kind).FirstOrDefault("other"),
                Optional.None<string>());
    }

    /// <summary>The item's <c>plug.plugCategoryIdentifier</c>, "" when it has none.</summary>
    public static string ReadPlugCategory(JsonElement item) =>
        ReadObject(item, "plug").Bind(plug => ReadString(plug, "plugCategoryIdentifier")).UnwrapOr("");

    /// <summary>
    /// The weapon's trait ("frames") columns, each the perks it can roll in order (a fixed column: its one perk). None when
    /// the weapon has no sockets or a column's perks can't be read: its columns are then unknown, not empty.
    /// </summary>
    public static Optional<ImmutableArray<ImmutableArray<long>>> ReadTraits(JsonElement item, ManifestTable socketTypes, long perkCategory, ManifestTable plugSets) =>
        ReadSetObject(item, "sockets").Bind(sockets =>
        {
            ImmutableArray<ImmutableArray<long>> columns = [.. ReadCategorySockets(sockets, perkCategory)
                .Where(entry => ReadWhitelist(entry, socketTypes).Contains("frames"))
                .Select(entry => ReadColumn(entry, plugSets))];
            return columns.Any(column => column.IsEmpty) ? Optional.None<ImmutableArray<ImmutableArray<long>>>() : Optional.Some(columns);
        });

    /// <summary>
    /// The weapon mods a weapon's mod socket takes (the socket of its WEAPON MODS category that takes weapon mods, not its
    /// masterwork's): every plug of its plug set in order, each once, without the empty ones. Empty when it has no mod socket.
    /// </summary>
    public static ImmutableArray<long> ReadMods(JsonElement item, ManifestTable socketTypes, long modCategory, ManifestTable plugSets, ManifestTable items)
    {
        var sockets = ReadObject(item, "sockets").UnwrapOr(EmptyObject);
        var plugs = ReadCategorySockets(sockets, modCategory)
            .Where(entry => ReadWhitelist(entry, socketTypes).Any(ModSocketCategories.Contains))
            .SelectMany(entry => ReadSetNumber(entry, "reusablePlugSetHash").Match(
                plugSet => ReadPlugItems(plugSets, plugSet.Value, rollableOnly: false),
                _ => []));
        return [.. plugs.Distinct().Where(hash => items.FindDefinition(hash).Match(mod => ReadPlugCategory(mod.Value) != EmptyMod, _ => false))];
    }

    /// <summary>A trait socket's perks: those of its random (else curated) plug set that can roll now, each once, or its one fixed perk.</summary>
    public static ImmutableArray<long> ReadColumn(JsonElement entry, ManifestTable plugSets) =>
        ReadSetNumber(entry, "randomizedPlugSetHash")
            .Match(random => Optional.Some(random.Value), _ => ReadSetNumber(entry, "reusablePlugSetHash"))
            .Match(
                plugSet => [.. ReadPlugItems(plugSets, plugSet.Value, rollableOnly: true).Distinct()],
                _ => ReadSetNumber(entry, "singleInitialItemHash").Match(single => [single.Value], _ => ImmutableArray<long>.Empty));

    /// <summary>An artifact's perk sockets (not its reset socket), in socket order.</summary>
    public static ImmutableArray<JsonElement> ReadArtifactSockets(JsonElement item, ManifestTable socketTypes) =>
        [.. ReadSocketEntries(item).Where(entry => ReadSetNumber(entry, "reusablePlugSetHash").IsSome()
            && ReadWhitelist(entry, socketTypes) is var whitelist && whitelist.Contains(ArtifactPerks) && !whitelist.Contains(ArtifactReset))];

    /// <summary>
    /// An artifact's tiers (Artifacts 2.0): its perk sockets' plug sets, each once in socket order, are its tiers' reach (the
    /// first takes Tier 1's perks, the next Tier 1's and 2's, …), so a tier is the perks of its plug set that an earlier one
    /// doesn't have, in the plug set's order, without a socket's initial (empty) plug. Also, for each perk socket, the
    /// highest tier it takes (1-based).
    /// </summary>
    public static (ImmutableArray<ImmutableArray<long>> Tiers, ImmutableArray<int> Slots) ReadArtifactTiers(JsonElement item, ManifestTable plugSets, ManifestTable socketTypes)
    {
        var sockets = ReadArtifactSockets(item, socketTypes);
        ImmutableArray<long> sets = [.. sockets.Select(entry => ReadNumber(entry, "reusablePlugSetHash").UnwrapOr(0)).Distinct()];
        var tiers = sets.Aggregate(
            (Tiers: ImmutableArray<ImmutableArray<long>>.Empty, Seen: ImmutableHashSet<long>.Empty),
            (done, plugSet) =>
            {
                var empty = sockets.Where(entry => ReadNumber(entry, "reusablePlugSetHash").UnwrapOr(0) == plugSet)
                    .Select(entry => ReadNumber(entry, "singleInitialItemHash"))
                    .OfType<Optional<long>.Some>().Select(some => some.Value).ToImmutableHashSet();
                ImmutableArray<long> tier = [.. ReadPlugItems(plugSets, plugSet, rollableOnly: false).Distinct()
                    .Where(hash => !done.Seen.Contains(hash) && !empty.Contains(hash))];
                return (done.Tiers.Add(tier), done.Seen.Union(tier));
            }).Tiers;
        return (tiers, [.. sockets.Select(entry => sets.IndexOf(ReadNumber(entry, "reusablePlugSetHash").UnwrapOr(0)) + 1)]);
    }

    /// <summary>An artifact's perks: every perk of its tiers, Tier 1's first.</summary>
    public static ImmutableArray<long> ReadArtifactPerks(JsonElement item, ManifestTable plugSets, ManifestTable socketTypes) =>
        [.. ReadArtifactTiers(item, plugSets, socketTypes).Tiers.SelectMany(tier => tier)];

    /// <summary>
    /// Everything a subclass can hold: every plug of the plug sets of its sockets (super, abilities, aspects, fragments)
    /// that can roll, each once, in the manifest's order (the caller drops the empty sockets' plugs).
    /// </summary>
    public static ImmutableArray<long> ReadSubclassPlugs(JsonElement item, ManifestTable plugSets) =>
        [.. ReadSocketEntries(item)
            .SelectMany(entry => ReadSetNumber(entry, "reusablePlugSetHash").Match(
                plugSet => ReadPlugItems(plugSets, plugSet.Value, rollableOnly: true),
                _ => []))
            .Distinct()];

    /// <summary>
    /// The item's intrinsic perk: the first named plug of an <c>intrinsics</c> socket in that category (a weapon's frame or
    /// exotic perk, an exotic armor's perk).
    /// </summary>
    public static Optional<long> ReadIntrinsic(JsonElement item, long category, ManifestTable items)
    {
        var sockets = ReadObject(item, "sockets").UnwrapOr(EmptyObject);
        var entries = ReadArray(sockets, "socketEntries");
        return ReadArray(sockets, "socketCategories")
            .Where(group => ReadNumber(group, "socketCategoryHash") is Optional<long>.Some found && found.Value == category)
            .SelectMany(group => ReadNumbers(ReadArray(group, "socketIndexes")))
            .Select(index => index < entries.Length
                ? ReadNumber(entries[(int)index], "singleInitialItemHash").Bind(items.FindDefinition)
                : Optional.None<JsonElement>())
            .Select(plug => plug.Bind(found =>
                ReadPlugCategory(found) == "intrinsics" && ReadName(found).Length > 0 ? ReadNumber(found, "hash") : Optional.None<long>()))
            .FindFirstSome();
    }

    /// <summary>
    /// The breaker an intrinsic frame gives its weapon: a perk of the plug named with a breaker in brackets
    /// ("[Shield-Piercing] Barrier" on Adaptive Frame, "[Disruption] Overload" on Rapid-Fire Frame), as DIM reads it.
    /// </summary>
    public static Optional<long> ReadFrameBreaker(JsonElement plug, ManifestTable perks, ManifestTable breakerTypes)
    {
        var names = breakerTypes.Entries
            .GroupBy(pair => TextWriting.ToBreakerName(ReadName(pair.Value)))
            .ToFrozenDictionary(group => group.Key, group => group.Last().Key);
        return ReadArray(plug, "perks")
            .Select(entry => ReadNumber(entry, "perkHash").Bind(perks.FindDefinition).Map(ReadName).UnwrapOr(""))
            .SelectMany(name => FindBracketedNames().Matches(name).Select(match => TextWriting.ToBreakerName(match.Groups[1].Value)))
            .Select(token => names.TryGetValue(token, out var breaker) ? Optional.Some(breaker) : Optional.None<long>())
            .FindFirstSome();
    }

    /// <summary>The sandbox perks a plug grants that the game shows (a catalyst's), in order.</summary>
    public static ImmutableArray<long> ReadGrantedPerks(JsonElement item, ManifestTable perks) =>
        [.. ReadArray(item, "perks")
            .Select(entry => ReadNumber(entry, "perkHash"))
            .OfType<Optional<long>.Some>()
            .Select(some => some.Value)
            .Where(hash => perks.FindDefinition(hash).Match(
                perk => IsSet(perk.Value, "isDisplayable", missing: false) && ReadName(perk.Value).Length > 0,
                _ => false))];

    /// <summary>An exotic weapon's catalyst: a plug of its catalyst socket's own category that grants a perk the game shows.</summary>
    public static Optional<long> ReadCatalyst(JsonElement item, ManifestTable socketTypes, FrozenDictionary<string, ImmutableArray<long>> plugsByCategory, ManifestTable items, ManifestTable perks) =>
        ReadSocketEntries(item)
            .Select(entry => ReadWhitelist(entry, socketTypes))
            .Where(whitelist => whitelist.Contains(EmptyCatalyst))
            .SelectMany(whitelist => whitelist.Where(category => category != EmptyCatalyst))
            .SelectMany(category => plugsByCategory.TryGetValue(category, out var plugs) ? plugs : [])
            .Where(plug => items.FindDefinition(plug).Match(found => !ReadGrantedPerks(found.Value, perks).IsEmpty, _ => false))
            .Select(Optional.Some)
            .FirstOrDefault(Optional.None<long>());

    /// <summary>
    /// Bungie's description of an item; for a subclass plug (an aspect, a fragment) whose own is empty, its perks' texts:
    /// the perk of its own name gives its effect, the others their name or "name: text" (a fragment's stat change).
    /// </summary>
    public static string ReadDescription(JsonElement item, ManifestTable perks) =>
        ReadText(ReadDisplay(item), "description").Match(
            own => own.Value,
            _ => string.Join("\n", ReadArray(item, "perks")
                .Select(entry => ReadNumber(entry, "perkHash").Bind(perks.FindDefinition).UnwrapOr(EmptyObject))
                .Where(perk => IsSet(perk, "isDisplayable", missing: false))
                .Select(perk => (Name: ReadName(perk), Text: ReadString(ReadDisplay(perk), "description").UnwrapOr("")))
                .Where(perk => perk.Name.Length > 0 || perk.Text.Length > 0)
                .Select(perk => perk.Name == ReadName(item) || perk.Name == perk.Text || perk.Name.Length == 0 ? perk.Text
                    : perk.Text.Length > 0 ? $"{perk.Name}: {perk.Text}"
                    : perk.Name)));

    /// <summary>The sockets of a socket category, in the category's order (an index past the sockets is skipped).</summary>
    private static ImmutableArray<JsonElement> ReadCategorySockets(JsonElement sockets, long category)
    {
        var entries = ReadArray(sockets, "socketEntries");
        return [.. ReadArray(sockets, "socketCategories")
            .Where(group => ReadNumber(group, "socketCategoryHash") is Optional<long>.Some found && found.Value == category)
            .SelectMany(group => ReadNumbers(ReadArray(group, "socketIndexes")))
            .Where(index => index >= 0 && index < entries.Length)
            .Select(index => entries[(int)index])];
    }

    private static ImmutableArray<JsonElement> ReadSocketEntries(JsonElement item) =>
        ReadObject(item, "sockets").Match(sockets => ReadArray(sockets.Value, "socketEntries"), _ => []);

    /// <summary>The plug categories a socket's type takes (its <c>plugWhitelist</c>), in order.</summary>
    public static ImmutableArray<string> ReadWhitelist(JsonElement entry, ManifestTable socketTypes) =>
        [.. ReadNumber(entry, "socketTypeHash").Bind(socketTypes.FindDefinition).Match(
            socketType => ReadArray(socketType.Value, "plugWhitelist").Select(allowed => ReadString(allowed, "categoryIdentifier").UnwrapOr("")),
            _ => [])];

    /// <summary>A plug set's plugs in order; with <paramref name="rollableOnly"/>, only those that can roll now.</summary>
    private static IEnumerable<long> ReadPlugItems(ManifestTable plugSets, long plugSet, bool rollableOnly) =>
        plugSets.FindDefinition(plugSet).Match(
            found => ReadArray(found.Value, "reusablePlugItems")
                .Where(plug => !rollableOnly || IsSet(plug, "currentlyCanRoll", missing: true))
                .Select(plug => ReadNumber(plug, "plugItemHash"))
                .OfType<Optional<long>.Some>()
                .Select(some => some.Value),
            _ => []);

    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex FindBracketedNames();
}

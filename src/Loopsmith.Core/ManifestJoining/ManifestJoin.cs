using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.ManifestJoining.ManifestEntryReading;

namespace Loopsmith.Core.ManifestJoining;

/// <summary>
/// Pure: Bungie's full manifest → the excerpt of what some hashes need (ADRs D14), the way
/// <c>tools/manifest/extract_manifest.py</c> writes <c>rules/manifest.yaml</c>: each item's name, kind, type, rarity,
/// description, icon and a weapon's or armor's season watermark; a weapon's slot, damage type, breaker (its own, else its
/// intrinsic frame's: a frame's perk named with the breaker, "[Shield-Piercing] Barrier"), trait columns (and their perks), intrinsic perk and an
/// exotic's catalyst (and the perks it grants); armor's slot, intrinsic perk and set (and the set's perks). The breaker
/// types and an icon's overlays aren't joined: they come from <c>rules/manifest.yaml</c>. The host
/// holds the manifest and answers what the join asks, round by round: each round walks from the wanted hashes
/// through what has been read, and asks for what the walk reaches but hasn't read; when nothing is left to ask, the
/// excerpt is built from the same walk. What the manifest doesn't have is asked once and left out.
/// </summary>
public static partial class ManifestJoin
{
    private const string EmptyCatalyst = "v400.empty.exotic.masterwork";   // the plug category of an exotic's empty catalyst socket
    private const string ArtifactPerkCategory = "artifact_perks";          // what an artifact's perk sockets take…
    private const string ArtifactResetCategory = "artifact_reset";        // …and its reset socket too, with its Reset Artifact plug
    private const string PerkCategoryName = "WEAPON PERKS";
    private const string WeaponIntrinsicCategoryName = "INTRINSIC TRAITS";
    private const string ArmorIntrinsicCategoryName = "ARMOR PERKS";

    private static readonly ImmutableArray<ManifestTable> SmallTables =
        [ManifestTable.InventoryBucket, ManifestTable.SocketCategory, ManifestTable.EquipableItemSet, ManifestTable.BreakerType];

    private static readonly ImmutableDictionary<string, WeaponSlot> WeaponBuckets = new Dictionary<string, WeaponSlot>
    {
        ["Kinetic Weapons"] = WeaponSlot.Kinetic, ["Energy Weapons"] = WeaponSlot.Energy, ["Power Weapons"] = WeaponSlot.Power,
    }.ToImmutableDictionary();

    private static readonly ImmutableDictionary<string, ArmorSlot> ArmorBuckets = new Dictionary<string, ArmorSlot>
    {
        ["Helmet"] = ArmorSlot.Helmet, ["Gauntlets"] = ArmorSlot.Arms, ["Chest Armor"] = ArmorSlot.Chest,
        ["Leg Armor"] = ArmorSlot.Legs, ["Class Armor"] = ArmorSlot.ClassItem,
    }.ToImmutableDictionary();

    private static readonly ImmutableDictionary<string, ArmorSlot> ModSlots = new Dictionary<string, ArmorSlot>
    {
        ["Helmet Armor Mod"] = ArmorSlot.Helmet, ["Arms Armor Mod"] = ArmorSlot.Arms, ["Chest Armor Mod"] = ArmorSlot.Chest,
        ["Leg Armor Mod"] = ArmorSlot.Legs, ["Class Item Armor Mod"] = ArmorSlot.ClassItem,
    }.ToImmutableDictionary();

    private static readonly ImmutableDictionary<int, ItemTier> Tiers = new Dictionary<int, ItemTier>
    {
        [2] = ItemTier.Basic, [3] = ItemTier.Common, [4] = ItemTier.Rare, [5] = ItemTier.Legendary, [6] = ItemTier.Exotic,
    }.ToImmutableDictionary();

    private static readonly ImmutableDictionary<int, DamageType> DamageTypes = new Dictionary<int, DamageType>
    {
        [1] = DamageType.Kinetic, [2] = DamageType.Arc, [3] = DamageType.Solar, [4] = DamageType.Void,
        [6] = DamageType.Stasis, [7] = DamageType.Strand,
    }.ToImmutableDictionary();

    private static readonly ManifestAnswer NoAnswer = new(
        ImmutableDictionary<ManifestEntryKey, JsonElement>.Empty,
        ImmutableDictionary<PlugCategoryId, ImmutableArray<ItemHash>>.Empty);

    /// <summary>A join of what these hashes need: its first question to the manifest (or, for none, an empty excerpt).</summary>
    public static ManifestJoinStep StartJoin(IEnumerable<ItemHash> wanted) =>
        TakeStep(new ManifestJoinProgress([.. wanted.Distinct()], NoAnswer, [], [], []));

    /// <summary>The join with the manifest's answer to its last question: its next question, or the excerpt.</summary>
    public static ManifestJoinStep ContinueJoin(ManifestJoinProgress progress, ManifestAnswer answer) =>
        TakeStep(progress with
        {
            Read = new ManifestAnswer(progress.Read.Entries.SetItems(answer.Entries), progress.Read.Plugs.SetItems(answer.Plugs)),
        });

    /// <summary>The hashes the excerpt lacks (what the full manifest is read for), each once.</summary>
    public static ImmutableArray<ItemHash> ListUnknown(ManifestExcerpt excerpt, IEnumerable<ItemHash> hashes) =>
        [.. hashes.Distinct().Where(hash => !excerpt.Items.ContainsKey(hash))];

    /// <summary>Of <paramref name="hashes"/>, those not in <paramref name="asked"/> (a host reads the manifest for each once).</summary>
    public static ImmutableArray<ItemHash> ListUnasked(ImmutableHashSet<ItemHash> asked, IEnumerable<ItemHash> hashes) =>
        [.. hashes.Distinct().Where(hash => !asked.Contains(hash))];

    /// <summary>The hashes a build names that the excerpt lacks: its weapons, their roll picks, what it left out.</summary>
    public static ImmutableArray<ItemHash> ListUnknownBuildHashes(ManifestExcerpt excerpt, Build build) =>
        ListUnknown(excerpt, build.Weapons
            .SelectMany(weapon => ToSequence(weapon.Hash).Concat(weapon.Roll.SelectMany(ToSequence)))
            .Concat(build.LeftOut.Select(item => item.Hash)));

    // ── one round ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// What has been read, each entry parsed, and each plug category's plug items; and, looked up once per round
    /// rather than per item, the socket categories by name and the armor set each set perk belongs to.
    /// </summary>
    private sealed record Reading(
        ImmutableDictionary<ManifestEntryKey, JsonElement> Entries,
        ImmutableDictionary<PlugCategoryId, ImmutableArray<ItemHash>> Plugs,
        ImmutableDictionary<string, ItemHash> CategoriesByName,
        ImmutableDictionary<ItemHash, ItemHash> SetsByPerk);

    /// <summary>
    /// What a walk reaches: entries, whole tables and plug categories, repeats and all — each walk step gathers its parts'
    /// lists once (<see cref="Combine"/>) and a round makes them sets once, so a walk over hundreds of items stays cheap.
    /// </summary>
    private sealed record Reach(ImmutableArray<ManifestEntryKey> Entries, ImmutableArray<ManifestTable> Tables, ImmutableArray<PlugCategoryId> Plugs)
    {
        internal static readonly Reach None = new([], [], []);
    }

    private static ManifestJoinStep TakeStep(ManifestJoinProgress progress)
    {
        var reading = ToReading(progress.Read);
        var reach = WalkWanted(reading, progress.Wanted);
        var entries = reach.Entries.ToImmutableHashSet().Except(progress.AskedEntries);
        var tables = reach.Tables.ToImmutableHashSet().Except(progress.AskedTables);
        var plugs = reach.Plugs.ToImmutableHashSet().Except(progress.AskedPlugs);
        return entries.IsEmpty && tables.IsEmpty && plugs.IsEmpty
            ? new ManifestJoinStep.Done(BuildExcerpt(reading, progress.Wanted))
            : new ManifestJoinStep.Ask(
                progress with
                {
                    AskedEntries = progress.AskedEntries.Union(entries),
                    AskedTables = progress.AskedTables.Union(tables),
                    AskedPlugs = progress.AskedPlugs.Union(plugs),
                },
                new ManifestQuery(
                    [.. entries.OrderBy(key => key.Table).ThenBy(key => key.Hash.Value)],
                    [.. tables.OrderBy(table => table)],
                    [.. plugs.OrderBy(plug => plug.Value, StringComparer.Ordinal)]));
    }

    // ── the walk: what each wanted hash needs read ───────────────────────────────

    private static Reach WalkWanted(Reading reading, ImmutableArray<ItemHash> wanted) =>
        Combine(wanted
            .SelectMany(hash => new[]
            {
                ToReach(ManifestTable.InventoryItem, hash),
                ToReach(ManifestTable.SandboxPerk, hash),
                FindEntry(reading, ManifestTable.InventoryItem, hash).Match(
                    item => WalkItem(reading, item.Value),
                    _ => FindEntry(reading, ManifestTable.SandboxPerk, hash).Match(
                        _ => WalkSetOfPerk(reading, hash),
                        _ => Reach.None)),
            })
            .Prepend(Reach.None with { Tables = SmallTables }));

    private static Reach WalkItem(Reading reading, JsonElement item) =>
        ClassifyItem(reading, item) switch
        {
            KindWord.Weapon => Join(
                ListPerkNeeds(reading, item),
                WalkWeaponSockets(reading, item)),
            KindWord.Artifact => Join(
                ListPerkNeeds(reading, item),
                WalkArtifactSockets(reading, item)),
            KindWord.Armor => Combine([
                ListPerkNeeds(reading, item),
                WalkIntrinsicSockets(reading, item, ArmorIntrinsicCategoryName),
                item.ReadHash("equippingBlock", "equipableItemSetHash").Match(set => WalkSet(reading, set.Value), _ => Reach.None)]),
            _ => ListPerkNeeds(reading, item),
        };

    /// <summary>The sandbox perks of an item (an aspect's or a fragment's text is its perks').</summary>
    private static Reach ListPerkNeeds(Reading reading, JsonElement item) =>
        ToEntryReach(ManifestTable.SandboxPerk, item.ReadArray("perks").SelectMany(perk => ToSequence(perk.ReadHash("perkHash"))));

    private static Reach WalkWeaponSockets(Reading reading, JsonElement weapon)
    {
        var entries = weapon.ReadArray("sockets", "socketEntries");
        var types = ToEntryReach(ManifestTable.SocketType, entries.SelectMany(entry => ToSequence(entry.ReadHash("socketTypeHash"))));
        var columns = ListTraitSockets(reading, weapon).Select(socket => WalkColumn(reading, socket));
        var catalyst = ListCatalystCategories(reading, weapon).Select(category => WalkCatalystCategory(reading, category));
        return Combine(columns.Concat(catalyst).Append(types).Append(WalkIntrinsicSockets(reading, weapon, WeaponIntrinsicCategoryName)).Append(WalkFramePerks(reading, weapon)));
    }

    /// <summary>The sandbox perks of a weapon's intrinsic frame, once it is read: the perk named with its breaker is among them.</summary>
    private static Reach WalkFramePerks(Reading reading, JsonElement weapon) =>
        FindIntrinsic(reading, weapon, WeaponIntrinsicCategoryName)
            .Bind(frame => FindEntry(reading, ManifestTable.InventoryItem, frame))
            .Match(frame => ListPerkNeeds(reading, frame.Value), _ => Reach.None);

    /// <summary>An artifact's sockets: their types (which take artifact perks) and their plug sets (the perks it unlocks).</summary>
    private static Reach WalkArtifactSockets(Reading reading, JsonElement artifact) =>
        Combine(artifact.ReadArray("sockets", "socketEntries").SelectMany(socket => new[]
        {
            socket.ReadHash("socketTypeHash").Match(type => ToReach(ManifestTable.SocketType, type.Value), _ => Reach.None),
            IsArtifactPerkSocket(reading, socket)
                ? socket.ReadHash("reusablePlugSetHash").Match(set => ToReach(ManifestTable.PlugSet, set.Value), _ => Reach.None)
                : Reach.None,
        }));

    private static Reach WalkColumn(Reading reading, JsonElement socket) =>
        ReadPlugSet(socket).Match(
            set => FindEntry(reading, ManifestTable.PlugSet, set.Value).Match(
                found => Join(ToReach(ManifestTable.PlugSet, set.Value), ToEntryReach(ManifestTable.InventoryItem, ListRollable(found.Value))),
                _ => ToReach(ManifestTable.PlugSet, set.Value)),
            _ => socket.ReadHash("singleInitialItemHash").Match(initial => ToReach(ManifestTable.InventoryItem, initial.Value), _ => Reach.None));

    private static Reach WalkCatalystCategory(Reading reading, PlugCategoryId category) =>
        reading.Plugs.TryGetValue(category, out var plugs)
            ? Combine(plugs
                .SelectMany(plug => new[]
                {
                    ToReach(ManifestTable.InventoryItem, plug),
                    FindEntry(reading, ManifestTable.InventoryItem, plug).Match(item => ListPerkNeeds(reading, item.Value), _ => Reach.None),
                })
                .Prepend(Reach.None with { Plugs = [category] }))
            : Reach.None with { Plugs = [category] };

    private static Reach WalkIntrinsicSockets(Reading reading, JsonElement item, string categoryName) =>
        ToEntryReach(ManifestTable.InventoryItem, ListSocketsInCategory(reading, item, categoryName).SelectMany(socket => ToSequence(socket.ReadHash("singleInitialItemHash"))));

    /// <summary>A set's entry (a small table, read whole) and the perks its piece counts grant.</summary>
    private static Reach WalkSet(Reading reading, ItemHash set) =>
        FindEntry(reading, ManifestTable.EquipableItemSet, set).Match(
            found => ToEntryReach(ManifestTable.SandboxPerk, found.Value.ReadArray("setPerks").SelectMany(perk => ToSequence(perk.ReadHash("sandboxPerkHash")))),
            _ => Reach.None);

    /// <summary>A wanted sandbox perk that is a set's perk (a set bonus's hash): its set and the set's other perks.</summary>
    private static Reach WalkSetOfPerk(Reading reading, ItemHash perk) =>
        FindSetOfPerk(reading, perk).Match(set => WalkSet(reading, set.Value), _ => Reach.None);

    // ── the excerpt ─────────────────────────────────────────────────────────────────

    private sealed record ReadItem(ItemHash Hash, JsonElement Entry);

    private static ManifestExcerpt BuildExcerpt(Reading reading, ImmutableArray<ItemHash> wanted)
    {
        var wantedItems = wanted
            .SelectMany(hash => ToSequence(FindEntry(reading, ManifestTable.InventoryItem, hash).Map(entry => new ReadItem(hash, entry))))
            .ToImmutableArray();
        var weapons = wantedItems.Where(item => ClassifyItem(reading, item.Entry) == KindWord.Weapon).ToImmutableArray();
        var armor = wantedItems.Where(item => ClassifyItem(reading, item.Entry) == KindWord.Armor).ToImmutableArray();
        var catalysts = weapons.SelectMany(weapon => ToSequence(FindCatalyst(reading, weapon.Entry))).ToImmutableHashSet();
        var related = weapons
            .SelectMany(weapon => ReadTraitColumns(reading, weapon.Entry).Match(columns => columns.Value.SelectMany(column => column), _ => []))
            .Concat(weapons.SelectMany(weapon => ToSequence(FindIntrinsic(reading, weapon.Entry, WeaponIntrinsicCategoryName))))
            .Concat(armor.SelectMany(piece => ToSequence(FindIntrinsic(reading, piece.Entry, ArmorIntrinsicCategoryName))))
            .Concat(catalysts);
        var sets = armor
            .SelectMany(piece => ToSequence(piece.Entry.ReadHash("equippingBlock", "equipableItemSetHash")))
            .Concat(wanted.SelectMany(hash => ToSequence(FindSetOfPerk(reading, hash))))
            .Distinct()
            .SelectMany(set => ToSequence(FindEntry(reading, ManifestTable.EquipableItemSet, set).Map(entry => new ReadItem(set, entry))))
            .ToImmutableArray();
        var perks = wanted.Where(hash => !FindEntry(reading, ManifestTable.InventoryItem, hash).IsSome())
            .Concat(catalysts.SelectMany(plug => FindEntry(reading, ManifestTable.InventoryItem, plug).Match(entry => ListGrantedPerks(reading, entry.Value), _ => [])))
            .Concat(sets.SelectMany(set => set.Entry.ReadArray("setPerks").SelectMany(perk => ToSequence(perk.ReadHash("sandboxPerkHash")))));
        var items = wantedItems.Select(item => item.Hash).Concat(related).Distinct()
            .SelectMany(hash => ToSequence(ToManifestItem(reading, hash, catalysts.Contains(hash))))
            .Concat(perks.Distinct().SelectMany(hash => ToSequence(ToPerkItem(reading, hash))))
            .GroupBy(item => item.Hash)
            .ToImmutableDictionary(group => group.Key, group => group.First());
        return new ManifestExcerpt(
            Optional.None<string>(),
            ImmutableDictionary<DamageType, string>.Empty,
            sets.Select(set => ToArmorSet(set.Hash, set.Entry)).ToImmutableDictionary(set => set.Hash),
            items,
            ImmutableDictionary<ItemHash, StatPlug>.Empty,
            ImmutableDictionary<ItemHash, BreakerType>.Empty,
            new ItemOverlays([], Optional.None<string>(), Optional.None<string>()),
            ImmutableDictionary<ItemHash, KeywordTrait>.Empty,
            ImmutableDictionary<StatKind, ArmorStatIcon>.Empty);
    }

    private static Optional<ManifestItem> ToManifestItem(Reading reading, ItemHash hash, bool isCatalyst) =>
        FindEntry(reading, ManifestTable.InventoryItem, hash).Bind(item =>
        {
            var word = isCatalyst ? KindWord.Catalyst : ClassifyItem(reading, item);
            return ToKind(reading, item, word).Map(kind => new ManifestItem(
                hash,
                item.ReadText("displayProperties", "name").UnwrapOr(""),
                kind,
                item.ReadText("itemTypeDisplayName"),
                item.ReadInteger("inventory", "tierType").Bind(tier => Tiers.TryGetValue(tier, out var found) ? Optional.Some(found) : Optional.None<ItemTier>()),
                ReadDescription(reading, item),
                item.ReadString("displayProperties", "icon").Bind(icon => icon.Length > 0 ? Optional.Some(icon) : Optional.None<string>()),
                kind is ManifestKind.Weapon or ManifestKind.Armor
                    ? item.ReadString("iconWatermark").Bind(watermark => watermark.Length > 0 ? Optional.Some(watermark) : Optional.None<string>())
                    : Optional.None<string>()));
        });

    /// <summary>A sandbox perk (a catalyst's or an armor set's): name, Bungie's description, icon; no item type.</summary>
    private static Optional<ManifestItem> ToPerkItem(Reading reading, ItemHash hash) =>
        FindEntry(reading, ManifestTable.SandboxPerk, hash).Map(perk => new ManifestItem(
            hash,
            perk.ReadText("displayProperties", "name").UnwrapOr(""),
            new ManifestKind.Perk(),
            Optional.None<string>(),
            Optional.None<ItemTier>(),
            perk.ReadText("displayProperties", "description"),
            perk.ReadString("displayProperties", "icon").Bind(icon => icon.Length > 0 ? Optional.Some(icon) : Optional.None<string>()),
            Optional.None<string>()));

    private static ArmorSet ToArmorSet(ItemHash hash, JsonElement set) =>
        new(
            hash,
            set.ReadText("displayProperties", "name").UnwrapOr(""),
            [
                .. set.ReadArray("setPerks")
                    .SelectMany(perk => ToSequence(perk.ReadInteger("requiredSetCount").Bind(count => ToPieceCount(count))
                        .Bind(count => perk.ReadHash("sandboxPerkHash").Map(perkHash => new SetPerk(count, perkHash)))))
                    .OrderBy(perk => perk.Pieces.Value),
            ]);

    /// <summary>The item's kind and its own data; none for a weapon or armor whose bucket gives no slot.</summary>
    private static Optional<ManifestKind> ToKind(Reading reading, JsonElement item, KindWord word) =>
        word switch
        {
            KindWord.Weapon => ReadBucketName(reading, item).Bind(bucket => WeaponBuckets.TryGetValue(bucket, out var slot) ? Optional.Some(slot) : Optional.None<WeaponSlot>())
                .Map(slot => (ManifestKind)new ManifestKind.Weapon(
                    slot,
                    item.ReadInteger("defaultDamageType").Bind(type => DamageTypes.TryGetValue(type, out var found) ? Optional.Some(found) : Optional.None<DamageType>()),
                    ReadTraitColumns(reading, item).Match(
                        columns => [.. columns.Value.Select(column => new TraitColumn(column))],
                        _ => ImmutableArray<TraitColumn>.Empty),
                    FindIntrinsic(reading, item, WeaponIntrinsicCategoryName),
                    FindCatalyst(reading, item),
                    new[] { item.ReadHash("breakerTypeHash"), FindFrameBreaker(reading, item) }.FindFirstSome())),
            KindWord.Armor => ReadBucketName(reading, item).Bind(bucket => ArmorBuckets.TryGetValue(bucket, out var slot) ? Optional.Some(slot) : Optional.None<ArmorSlot>())
                .Map(slot => (ManifestKind)new ManifestKind.Armor(
                    slot,
                    FindIntrinsic(reading, item, ArmorIntrinsicCategoryName),
                    item.ReadHash("equippingBlock", "equipableItemSetHash"),
                    item.ReadInteger("classType").Bind(ToGuardianClass))),
            KindWord.ArmorMod => Optional.Some<ManifestKind>(new ManifestKind.ArmorMod(item.ReadText("itemTypeDisplayName")
                .Bind(type => ModSlots.TryGetValue(type, out var slot) ? Optional.Some(slot) : Optional.None<ArmorSlot>()))),
            KindWord.Catalyst => Optional.Some<ManifestKind>(new ManifestKind.ExoticCatalyst(ListGrantedPerks(reading, item))),
            KindWord.Artifact => Optional.Some<ManifestKind>(new ManifestKind.Artifact(ListArtifactPerks(reading, item))),
            KindWord.Subclass => Optional.Some<ManifestKind>(new ManifestKind.Subclass([])),
            KindWord.Super => Optional.Some<ManifestKind>(new ManifestKind.Super()),
            KindWord.Grenade => Optional.Some<ManifestKind>(new ManifestKind.Grenade()),
            KindWord.Melee => Optional.Some<ManifestKind>(new ManifestKind.Melee()),
            KindWord.ClassAbility => Optional.Some<ManifestKind>(new ManifestKind.ClassAbility()),
            KindWord.Movement => Optional.Some<ManifestKind>(new ManifestKind.Movement()),
            KindWord.Aspect => Optional.Some<ManifestKind>(new ManifestKind.Aspect()),
            KindWord.Fragment => Optional.Some<ManifestKind>(new ManifestKind.Fragment()),
            KindWord.WeaponPerk => Optional.Some<ManifestKind>(new ManifestKind.WeaponPerk()),
            KindWord.Intrinsic => Optional.Some<ManifestKind>(new ManifestKind.IntrinsicPerk()),
            KindWord.ArtifactPerk => Optional.Some<ManifestKind>(new ManifestKind.ArtifactPerk()),
            _ => Optional.Some<ManifestKind>(new ManifestKind.Other()),
        };

    // ── what the manifest's entries say ─────────────────────────────────────────────

    private enum KindWord
    {
        Subclass, Super, Grenade, Melee, ClassAbility, Movement, Aspect, Fragment, Weapon, WeaponPerk, Intrinsic, Catalyst, Armor,
        ArmorMod, ArtifactPerk, Artifact, Other,
    }

    private static readonly ImmutableDictionary<string, KindWord> ExactTypes = new Dictionary<string, KindWord>
    {
        ["Super Ability"] = KindWord.Super, ["Class Ability"] = KindWord.ClassAbility, ["Movement Ability"] = KindWord.Movement,
        ["Artifact Perk"] = KindWord.ArtifactPerk, ["Artifact"] = KindWord.Artifact, ["Trait"] = KindWord.WeaponPerk, ["Enhanced Trait"] = KindWord.WeaponPerk,
        ["Intrinsic"] = KindWord.Intrinsic,
    }.ToImmutableDictionary();

    private static readonly ImmutableArray<(string Suffix, KindWord Kind)> TypeSuffixes =
        [(" Grenade", KindWord.Grenade), (" Melee", KindWord.Melee), (" Aspect", KindWord.Aspect), (" Fragment", KindWord.Fragment)];

    /// <summary>What Loopsmith calls the item: by its bucket (subclass, weapon, armor), else by its type as the game words it.</summary>
    private static KindWord ClassifyItem(Reading reading, JsonElement item)
    {
        var bucket = ReadBucketName(reading, item).UnwrapOr("");
        var type = item.ReadString("itemTypeDisplayName").UnwrapOr("");
        return bucket == "Subclass" ? KindWord.Subclass
            : WeaponBuckets.ContainsKey(bucket) ? KindWord.Weapon
            : ArmorBuckets.ContainsKey(bucket) ? KindWord.Armor
            : type.EndsWith("Armor Mod", StringComparison.Ordinal) ? KindWord.ArmorMod
            : ExactTypes.TryGetValue(type, out var exact) ? exact
            : TypeSuffixes.Where(entry => type.EndsWith(entry.Suffix, StringComparison.Ordinal)).Select(entry => entry.Kind).DefaultIfEmpty(KindWord.Other).First();
    }

    private static Optional<string> ReadBucketName(Reading reading, JsonElement item) =>
        item.ReadHash("inventory", "bucketTypeHash")
            .Bind(bucket => FindEntry(reading, ManifestTable.InventoryBucket, bucket))
            .Bind(bucket => bucket.ReadString("displayProperties", "name"));

    /// <summary>Bungie's description; for a subclass plug whose own is empty, its perks' (its effect, then e.g. "-10 Grenade ▼").</summary>
    private static Optional<string> ReadDescription(Reading reading, JsonElement item)
    {
        var name = item.ReadString("displayProperties", "name").UnwrapOr("");
        var lines = item.ReadArray("perks")
            .SelectMany(entry => ToSequence(entry.ReadHash("perkHash").Bind(hash => FindEntry(reading, ManifestTable.SandboxPerk, hash))))
            .Where(perk => perk.ReadFlag("isDisplayable", false))
            .SelectMany(perk => ToSequence(DescribePerkLine(perk, name)));
        return item.ReadText("displayProperties", "description").Match(
            own => Optional.Some(own.Value),
            _ => string.Join("\n", lines) is { Length: > 0 } text ? Optional.Some(text) : Optional.None<string>());
    }

    private static Optional<string> DescribePerkLine(JsonElement perk, string itemName)
    {
        var name = perk.ReadText("displayProperties", "name");
        var text = perk.ReadText("displayProperties", "description");
        return name.Match(
            named => text.Match(
                described => Optional.Some(named.Value == itemName || named.Value == described.Value ? described.Value : $"{named.Value}: {described.Value}"),
                _ => Optional.Some(named.Value)),
            _ => text);
    }

    private static Optional<ItemHash> FindCategory(Reading reading, string name) =>
        reading.CategoriesByName.TryGetValue(name, out var category) ? Optional.Some(category) : Optional.None<ItemHash>();

    private static ImmutableArray<JsonElement> ListSocketsInCategory(Reading reading, JsonElement item, string categoryName)
    {
        var entries = item.ReadArray("sockets", "socketEntries");
        return FindCategory(reading, categoryName).Match(
            category => [.. item.ReadArray("sockets", "socketCategories")
                .Where(group => group.ReadHash("socketCategoryHash") == Optional.Some(category.Value))
                .SelectMany(group => group.ReadArray("socketIndexes"))
                .SelectMany(index => index.TryGetInt32(out var at) && at >= 0 && at < entries.Length ? new[] { entries[at] } : [])],
            _ => ImmutableArray<JsonElement>.Empty);
    }

    private static ImmutableArray<string> ReadWhitelist(Reading reading, JsonElement socket) =>
        [
            .. socket.ReadHash("socketTypeHash")
                .Bind(type => FindEntry(reading, ManifestTable.SocketType, type))
                .Match(type => type.Value.ReadArray("plugWhitelist"), _ => ImmutableArray<JsonElement>.Empty)
                .SelectMany(entry => ToSequence(entry.ReadString("categoryIdentifier"))),
        ];

    /// <summary>A weapon's trait ("frames") sockets: the perk sockets whose socket type takes frames.</summary>
    private static ImmutableArray<JsonElement> ListTraitSockets(Reading reading, JsonElement weapon) =>
        [.. ListSocketsInCategory(reading, weapon, PerkCategoryName).Where(socket => ReadWhitelist(reading, socket).Contains("frames"))];

    /// <summary>A weapon's trait columns, each the perks it can roll now in order; none when a column can't be read (unknown, not empty).</summary>
    private static Optional<ImmutableArray<ImmutableArray<ItemHash>>> ReadTraitColumns(Reading reading, JsonElement weapon)
    {
        var columns = ListTraitSockets(reading, weapon).Select(socket => ReadColumn(reading, socket)).ToImmutableArray();
        return weapon.ReadPath("sockets").IsSome() && columns.All(column => !column.IsEmpty)
            ? Optional.Some(columns)
            : Optional.None<ImmutableArray<ImmutableArray<ItemHash>>>();
    }

    private static ImmutableArray<ItemHash> ReadColumn(Reading reading, JsonElement socket) =>
        ReadPlugSet(socket).Match(
            set => FindEntry(reading, ManifestTable.PlugSet, set.Value).Match(found => ListRollable(found.Value), _ => ImmutableArray<ItemHash>.Empty),
            _ => [.. ToSequence(socket.ReadHash("singleInitialItemHash"))]);

    private static Optional<ItemHash> ReadPlugSet(JsonElement socket) =>
        socket.ReadHash("randomizedPlugSetHash").Match(set => Optional.Some(set.Value), _ => socket.ReadHash("reusablePlugSetHash"));

    /// <summary>
    /// An artifact's perks: every plug of the plug sets of its sockets that take artifact perks (not its reset socket), but
    /// a socket's initial (empty) one, socket by socket, each once.
    /// </summary>
    private static ImmutableArray<ItemHash> ListArtifactPerks(Reading reading, JsonElement artifact) =>
        [
            .. artifact.ReadArray("sockets", "socketEntries")
                .Where(socket => IsArtifactPerkSocket(reading, socket))
                .SelectMany(socket => ListSocketPlugs(reading, socket))
                .Distinct(),
        ];

    private static bool IsArtifactPerkSocket(Reading reading, JsonElement socket)
    {
        var whitelist = ReadWhitelist(reading, socket);
        return whitelist.Contains(ArtifactPerkCategory) && !whitelist.Contains(ArtifactResetCategory);
    }

    private static IEnumerable<ItemHash> ListSocketPlugs(Reading reading, JsonElement socket)
    {
        var empty = socket.ReadHash("singleInitialItemHash");
        return ToSequence(socket.ReadHash("reusablePlugSetHash"))
            .SelectMany(set => ToSequence(FindEntry(reading, ManifestTable.PlugSet, set)))
            .SelectMany(plugSet => plugSet.ReadArray("reusablePlugItems"))
            .SelectMany(plug => ToSequence(plug.ReadHash("plugItemHash")))
            .Where(plug => Optional.Some(plug) != empty);
    }

    /// <summary>A plug set's perks that can roll now, each once, in order.</summary>
    private static ImmutableArray<ItemHash> ListRollable(JsonElement plugSet) =>
        [
            .. plugSet.ReadArray("reusablePlugItems")
                .Where(plug => plug.ReadFlag("currentlyCanRoll", true))
                .SelectMany(plug => ToSequence(plug.ReadHash("plugItemHash")))
                .Distinct(),
        ];

    /// <summary>
    /// The breaker a weapon's intrinsic frame gives it, as DIM reads it: the breaker type named in brackets in one of the
    /// frame's perks ("[Shield-Piercing] Barrier" on Adaptive Frame, "[Disruption] Overload" on Rapid-Fire Frame).
    /// </summary>
    private static Optional<ItemHash> FindFrameBreaker(Reading reading, JsonElement weapon)
    {
        var breakers = reading.Entries
            .Where(entry => entry.Key.Table == ManifestTable.BreakerType)
            .SelectMany(entry => ToSequence(entry.Value.ReadText("displayProperties", "name").Map(name => (Name: ToBreakerName(name), Hash: entry.Key.Hash))))
            .ToImmutableArray();
        return FindIntrinsic(reading, weapon, WeaponIntrinsicCategoryName)
            .Bind(frame => FindEntry(reading, ManifestTable.InventoryItem, frame))
            .Bind(frame => frame.ReadArray("perks")
                .SelectMany(perk => ToSequence(perk.ReadHash("perkHash").Bind(hash => FindEntry(reading, ManifestTable.SandboxPerk, hash))))
                .SelectMany(perk => ToSequence(perk.ReadString("displayProperties", "name")))
                .SelectMany(name => CreateBracketedNamePattern().Matches(name).Select(match => ToBreakerName(match.Groups[1].Value)))
                .SelectMany(name => breakers.Where(breaker => breaker.Name == name).Select(breaker => breaker.Hash))
                .Select(Optional.Some)
                .FindFirstSome());
    }

    /// <summary>A breaker's name as frames' perks and the breaker table both write it ("Shield-Piercing", "Shield Piercing").</summary>
    private static string ToBreakerName(string name) => name.Replace('-', ' ').Trim().ToLowerInvariant();

    [System.Text.RegularExpressions.GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex CreateBracketedNamePattern();

    /// <summary>The first named plug of an intrinsic socket in that category (a weapon's frame or exotic perk, an exotic armor's perk).</summary>
    private static Optional<ItemHash> FindIntrinsic(Reading reading, JsonElement item, string categoryName) =>
        ListSocketsInCategory(reading, item, categoryName)
            .SelectMany(socket => ToSequence(socket.ReadHash("singleInitialItemHash")))
            .Where(plug => FindEntry(reading, ManifestTable.InventoryItem, plug).Match(
                found => found.Value.ReadString("plug", "plugCategoryIdentifier") == Optional.Some("intrinsics")
                    && found.Value.ReadText("displayProperties", "name").IsSome(),
                _ => false))
            .Select(Optional.Some)
            .FindFirstSome();

    /// <summary>The categories of an exotic's catalyst socket (the one that takes the empty catalyst), the empty one aside.</summary>
    private static ImmutableArray<PlugCategoryId> ListCatalystCategories(Reading reading, JsonElement weapon) =>
        [
            .. weapon.ReadArray("sockets", "socketEntries")
                .Select(socket => ReadWhitelist(reading, socket))
                .Where(whitelist => whitelist.Contains(EmptyCatalyst))
                .SelectMany(whitelist => whitelist.Where(category => category != EmptyCatalyst))
                .Distinct()
                .SelectMany(category => ToSequence(ToPlugCategory(category))),
        ];

    /// <summary>An exotic's catalyst: a plug of its catalyst socket's own category that grants a perk the game shows.</summary>
    private static Optional<ItemHash> FindCatalyst(Reading reading, JsonElement weapon) =>
        ListCatalystCategories(reading, weapon)
            .SelectMany(category => reading.Plugs.TryGetValue(category, out var plugs) ? plugs : [])
            .Where(plug => FindEntry(reading, ManifestTable.InventoryItem, plug).Match(item => !ListGrantedPerks(reading, item.Value).IsEmpty, _ => false))
            .Select(Optional.Some)
            .FindFirstSome();

    /// <summary>The sandbox perks a plug grants that the game shows (a catalyst's), in order.</summary>
    private static ImmutableArray<ItemHash> ListGrantedPerks(Reading reading, JsonElement item) =>
        [
            .. item.ReadArray("perks")
                .SelectMany(entry => ToSequence(entry.ReadHash("perkHash")))
                .Where(hash => FindEntry(reading, ManifestTable.SandboxPerk, hash).Match(
                    perk => perk.Value.ReadFlag("isDisplayable", false) && perk.Value.ReadText("displayProperties", "name").IsSome(),
                    _ => false)),
        ];

    private static Optional<ItemHash> FindSetOfPerk(Reading reading, ItemHash perk) =>
        reading.SetsByPerk.TryGetValue(perk, out var set) ? Optional.Some(set) : Optional.None<ItemHash>();

    /// <summary>What has been read, with its two lookups: each socket category by its name, each set perk's set (the lowest hash, when two have it).</summary>
    private static Reading ToReading(ManifestAnswer read) =>
        new(
            read.Entries,
            read.Plugs,
            read.Entries
                .Where(entry => entry.Key.Table == ManifestTable.SocketCategory)
                .SelectMany(entry => ToSequence(entry.Value.ReadString("displayProperties", "name")).Select(name => (Name: name, entry.Key.Hash)))
                .GroupBy(entry => entry.Name, StringComparer.Ordinal)
                .ToImmutableDictionary(group => group.Key, group => ItemHash.From(group.Min(entry => entry.Hash.Value)), StringComparer.Ordinal),
            read.Entries
                .Where(entry => entry.Key.Table == ManifestTable.EquipableItemSet)
                .SelectMany(entry => entry.Value.ReadArray("setPerks").SelectMany(setPerk => ToSequence(setPerk.ReadHash("sandboxPerkHash"))).Select(perk => (Perk: perk, Set: entry.Key.Hash)))
                .GroupBy(entry => entry.Perk)
                .ToImmutableDictionary(group => group.Key, group => ItemHash.From(group.Min(entry => entry.Set.Value))));

    // ── small helpers ───────────────────────────────────────────────────────────────

    private static Optional<JsonElement> FindEntry(Reading reading, ManifestTable table, ItemHash hash) =>
        reading.Entries.TryGetValue(new ManifestEntryKey(table, hash), out var entry) ? Optional.Some(entry) : Optional.None<JsonElement>();

    private static Reach ToReach(ManifestTable table, ItemHash hash) =>
        Reach.None with { Entries = [new ManifestEntryKey(table, hash)] };

    /// <summary>The entries of <paramref name="table"/> for <paramref name="hashes"/>, as one reach.</summary>
    private static Reach ToEntryReach(ManifestTable table, IEnumerable<ItemHash> hashes) =>
        Reach.None with { Entries = [.. hashes.Select(hash => new ManifestEntryKey(table, hash))] };

    private static Reach Join(Reach left, Reach right) =>
        Combine([left, right]);

    /// <summary>Every reach in one: each of their lists gathered once.</summary>
    private static Reach Combine(IEnumerable<Reach> reaches)
    {
        var all = reaches.ToImmutableArray();
        return new([.. all.SelectMany(reach => reach.Entries)], [.. all.SelectMany(reach => reach.Tables)], [.. all.SelectMany(reach => reach.Plugs)]);
    }

    /// <summary>An item's class (<c>classType</c>: 0 Titan, 1 Hunter, 2 Warlock); none for any class (3).</summary>
    private static Optional<GuardianClass> ToGuardianClass(int classType) =>
        classType switch
        {
            0 => Optional.Some(GuardianClass.Titan),
            1 => Optional.Some(GuardianClass.Hunter),
            2 => Optional.Some(GuardianClass.Warlock),
            _ => Optional.None<GuardianClass>(),
        };

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);
}

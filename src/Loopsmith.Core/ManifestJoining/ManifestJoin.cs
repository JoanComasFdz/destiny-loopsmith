using System.Collections.Immutable;
using System.Text.Json;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.ManifestJoining.ManifestEntryReading;

namespace Loopsmith.Core.ManifestJoining;

/// <summary>
/// Pure: Bungie's full manifest → the excerpt of what some hashes need (ADRs D14), the way
/// <c>tools/manifest/extract_manifest.py</c> writes <c>rules/manifest.yaml</c>: each item's name, kind, type, rarity,
/// description and icon; a weapon's slot, damage type, trait columns (and their perks), intrinsic perk and an
/// exotic's catalyst (and the perks it grants); armor's slot, intrinsic perk and set (and the set's perks). The host
/// holds the manifest and answers what the join asks, round by round: each round walks from the wanted hashes
/// through what has been read, and asks for what the walk reaches but hasn't read; when nothing is left to ask, the
/// excerpt is built from the same walk. What the manifest doesn't have is asked once and left out.
/// </summary>
public static class ManifestJoin
{
    private const string EmptyCatalyst = "v400.empty.exotic.masterwork";   // the plug category of an exotic's empty catalyst socket
    private const string PerkCategoryName = "WEAPON PERKS";
    private const string WeaponIntrinsicCategoryName = "INTRINSIC TRAITS";
    private const string ArmorIntrinsicCategoryName = "ARMOR PERKS";

    private static readonly ImmutableArray<ManifestTable> SmallTables =
        [ManifestTable.InventoryBucket, ManifestTable.SocketCategory, ManifestTable.EquipableItemSet];

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
        ImmutableDictionary<ManifestEntryKey, string>.Empty,
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

    /// <summary>What has been read, each entry parsed, and each plug category's plug items.</summary>
    private sealed record Reading(
        ImmutableDictionary<ManifestEntryKey, JsonElement> Entries,
        ImmutableDictionary<PlugCategoryId, ImmutableArray<ItemHash>> Plugs);

    /// <summary>What a walk reaches: entries, whole tables and plug categories; each walk step adds to it.</summary>
    private sealed record Reach(ImmutableHashSet<ManifestEntryKey> Entries, ImmutableHashSet<ManifestTable> Tables, ImmutableHashSet<PlugCategoryId> Plugs)
    {
        internal static readonly Reach None = new([], [], []);
    }

    private static ManifestJoinStep TakeStep(ManifestJoinProgress progress)
    {
        var reading = new Reading(ParseEntries(progress.Read.Entries), progress.Read.Plugs);
        var reach = WalkWanted(reading, progress.Wanted);
        var entries = reach.Entries.Except(progress.AskedEntries);
        var tables = reach.Tables.Except(progress.AskedTables);
        var plugs = reach.Plugs.Except(progress.AskedPlugs);
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
        wanted.Aggregate(
            Reach.None with { Tables = [.. SmallTables] },
            (reach, hash) => Join(Join(reach, Join(ToReach(ManifestTable.InventoryItem, hash), ToReach(ManifestTable.SandboxPerk, hash))),
                FindEntry(reading, ManifestTable.InventoryItem, hash).Match(
                    item => WalkItem(reading, item.Value),
                    _ => FindEntry(reading, ManifestTable.SandboxPerk, hash).Match(
                        _ => WalkSetOfPerk(reading, hash),
                        _ => Reach.None))));

    private static Reach WalkItem(Reading reading, JsonElement item) =>
        ClassifyItem(reading, item) switch
        {
            KindWord.Weapon => Join(
                ListPerkNeeds(reading, item),
                WalkWeaponSockets(reading, item)),
            KindWord.Armor => Join(
                ListPerkNeeds(reading, item),
                Join(WalkIntrinsicSockets(reading, item, ArmorIntrinsicCategoryName),
                    item.ReadHash("equippingBlock", "equipableItemSetHash").Match(set => WalkSet(reading, set.Value), _ => Reach.None))),
            _ => ListPerkNeeds(reading, item),
        };

    /// <summary>The sandbox perks of an item (an aspect's or a fragment's text is its perks').</summary>
    private static Reach ListPerkNeeds(Reading reading, JsonElement item) =>
        item.ReadArray("perks").Aggregate(Reach.None, (reach, perk) =>
            perk.ReadHash("perkHash").Match(hash => Join(reach, ToReach(ManifestTable.SandboxPerk, hash.Value)), _ => reach));

    private static Reach WalkWeaponSockets(Reading reading, JsonElement weapon)
    {
        var entries = weapon.ReadArray("sockets", "socketEntries");
        var types = entries.Aggregate(Reach.None, (reach, entry) =>
            entry.ReadHash("socketTypeHash").Match(hash => Join(reach, ToReach(ManifestTable.SocketType, hash.Value)), _ => reach));
        var columns = ListTraitSockets(reading, weapon).Aggregate(Reach.None, (reach, socket) => Join(reach, WalkColumn(reading, socket)));
        var catalyst = ListCatalystCategories(reading, weapon).Aggregate(Reach.None, (reach, category) => Join(reach, WalkCatalystCategory(reading, category)));
        return Join(Join(types, columns), Join(catalyst, WalkIntrinsicSockets(reading, weapon, WeaponIntrinsicCategoryName)));
    }

    private static Reach WalkColumn(Reading reading, JsonElement socket) =>
        ReadPlugSet(socket).Match(
            set => FindEntry(reading, ManifestTable.PlugSet, set.Value).Match(
                found => ListRollable(found.Value).Aggregate(ToReach(ManifestTable.PlugSet, set.Value), (reach, plug) => Join(reach, ToReach(ManifestTable.InventoryItem, plug))),
                _ => ToReach(ManifestTable.PlugSet, set.Value)),
            _ => socket.ReadHash("singleInitialItemHash").Match(initial => ToReach(ManifestTable.InventoryItem, initial.Value), _ => Reach.None));

    private static Reach WalkCatalystCategory(Reading reading, PlugCategoryId category) =>
        reading.Plugs.TryGetValue(category, out var plugs)
            ? plugs.Aggregate(Reach.None with { Plugs = [category] }, (reach, plug) => Join(reach, Join(ToReach(ManifestTable.InventoryItem, plug),
                FindEntry(reading, ManifestTable.InventoryItem, plug).Match(item => ListPerkNeeds(reading, item.Value), _ => Reach.None))))
            : Reach.None with { Plugs = [category] };

    private static Reach WalkIntrinsicSockets(Reading reading, JsonElement item, string categoryName) =>
        ListSocketsInCategory(reading, item, categoryName).Aggregate(Reach.None, (reach, socket) =>
            socket.ReadHash("singleInitialItemHash").Match(initial => Join(reach, ToReach(ManifestTable.InventoryItem, initial.Value)), _ => reach));

    /// <summary>A set's entry (a small table, read whole) and the perks its piece counts grant.</summary>
    private static Reach WalkSet(Reading reading, ItemHash set) =>
        FindEntry(reading, ManifestTable.EquipableItemSet, set).Match(
            found => found.Value.ReadArray("setPerks").Aggregate(Reach.None, (reach, perk) =>
                perk.ReadHash("sandboxPerkHash").Match(hash => Join(reach, ToReach(ManifestTable.SandboxPerk, hash.Value)), _ => reach)),
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
            items);
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
                item.ReadString("displayProperties", "icon").Bind(icon => icon.Length > 0 ? Optional.Some(icon) : Optional.None<string>())));
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
            perk.ReadString("displayProperties", "icon").Bind(icon => icon.Length > 0 ? Optional.Some(icon) : Optional.None<string>())));

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
                    FindCatalyst(reading, item))),
            KindWord.Armor => ReadBucketName(reading, item).Bind(bucket => ArmorBuckets.TryGetValue(bucket, out var slot) ? Optional.Some(slot) : Optional.None<ArmorSlot>())
                .Map(slot => (ManifestKind)new ManifestKind.Armor(
                    slot,
                    FindIntrinsic(reading, item, ArmorIntrinsicCategoryName),
                    item.ReadHash("equippingBlock", "equipableItemSetHash"))),
            KindWord.ArmorMod => Optional.Some<ManifestKind>(new ManifestKind.ArmorMod(item.ReadText("itemTypeDisplayName")
                .Bind(type => ModSlots.TryGetValue(type, out var slot) ? Optional.Some(slot) : Optional.None<ArmorSlot>()))),
            KindWord.Catalyst => Optional.Some<ManifestKind>(new ManifestKind.ExoticCatalyst(ListGrantedPerks(reading, item))),
            KindWord.Subclass => Optional.Some<ManifestKind>(new ManifestKind.Subclass()),
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
        ArmorMod, ArtifactPerk, Other,
    }

    private static readonly ImmutableDictionary<string, KindWord> ExactTypes = new Dictionary<string, KindWord>
    {
        ["Super Ability"] = KindWord.Super, ["Class Ability"] = KindWord.ClassAbility, ["Movement Ability"] = KindWord.Movement,
        ["Artifact Perk"] = KindWord.ArtifactPerk, ["Trait"] = KindWord.WeaponPerk, ["Enhanced Trait"] = KindWord.WeaponPerk,
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
        reading.Entries
            .Where(entry => entry.Key.Table == ManifestTable.SocketCategory && entry.Value.ReadString("displayProperties", "name") == Optional.Some(name))
            .Select(entry => Optional.Some(entry.Key.Hash))
            .FindFirstSome();

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

    /// <summary>A plug set's perks that can roll now, each once, in order.</summary>
    private static ImmutableArray<ItemHash> ListRollable(JsonElement plugSet) =>
        [
            .. plugSet.ReadArray("reusablePlugItems")
                .Where(plug => plug.ReadFlag("currentlyCanRoll", true))
                .SelectMany(plug => ToSequence(plug.ReadHash("plugItemHash")))
                .Distinct(),
        ];

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
        reading.Entries
            .Where(entry => entry.Key.Table == ManifestTable.EquipableItemSet
                && entry.Value.ReadArray("setPerks").Any(setPerk => setPerk.ReadHash("sandboxPerkHash") == Optional.Some(perk)))
            .Select(entry => Optional.Some(entry.Key.Hash))
            .FindFirstSome();

    // ── small helpers ───────────────────────────────────────────────────────────────

    private static Optional<JsonElement> FindEntry(Reading reading, ManifestTable table, ItemHash hash) =>
        reading.Entries.TryGetValue(new ManifestEntryKey(table, hash), out var entry) ? Optional.Some(entry) : Optional.None<JsonElement>();

    private static Reach ToReach(ManifestTable table, ItemHash hash) =>
        Reach.None with { Entries = [new ManifestEntryKey(table, hash)] };

    private static Reach Join(Reach left, Reach right) =>
        new(left.Entries.Union(right.Entries), left.Tables.Union(right.Tables), left.Plugs.Union(right.Plugs));

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);
}

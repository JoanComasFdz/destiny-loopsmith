using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// Every weapon and armor piece in the game in the build, without a login (docs/loop-format.md, "Every item"): the
/// pickers list the listing's items for the slot the build can wear there, a pick fills an empty slot or replaces what
/// is there (a weapon with no roll and its catalyst off, a piece with no copy: the stats stay), and "Empty this slot"
/// takes a weapon or a piece off. A build wears one exotic weapon and one exotic armor piece.
/// </summary>
public sealed class ItemPickingTests
{
    private static readonly RuleCatalog Catalog =
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static readonly ItemHash Festival = ItemHash.From(4019651319u);
    private static readonly ItemHash KineticRifle = ItemHash.From(9301u);
    private static readonly ItemHash PowerLauncher = ItemHash.From(9302u);
    private static readonly ItemHash PowerExotic = ItemHash.From(9303u);
    private static readonly ItemHash EnergyRifle = ItemHash.From(9304u);
    private static readonly ItemHash HunterMask = ItemHash.From(9311u);
    private static readonly ItemHash TitanHelm = ItemHash.From(9312u);
    private static readonly ItemHash ExoticMask = ItemHash.From(9313u);
    private static readonly ItemHash ExoticCloak = ItemHash.From(9314u);
    private static readonly ItemHash TestingSet = ItemHash.From(9400u);

    /// <summary>What "Every item" reads from Bungie's lite item table: no perks.</summary>
    private static readonly ItemListing Listing = new(
        [
            ToWeapon(PowerExotic, "Alpha Machine Gun", WeaponSlot.Power, DamageType.Arc, ItemTier.Exotic, []),
            ToWeapon(Festival, "Festival Flight", WeaponSlot.Kinetic, DamageType.Strand, ItemTier.Legendary, []),
            ToWeapon(EnergyRifle, "Lightning Rifle", WeaponSlot.Energy, DamageType.Arc, ItemTier.Legendary, []),
            ToWeapon(KineticRifle, "Plain Rifle", WeaponSlot.Kinetic, DamageType.Kinetic, ItemTier.Legendary, []),
            ToWeapon(PowerLauncher, "Zeta Launcher", WeaponSlot.Power, DamageType.Solar, ItemTier.Legendary, []),
            ToArmor(ExoticMask, "Exotic Mask", ArmorSlot.Helmet, ItemTier.Exotic, Optional.None<ItemHash>(), GuardianClass.Hunter),
            ToArmor(ExoticCloak, "Relativism", ArmorSlot.ClassItem, ItemTier.Exotic, Optional.None<ItemHash>(), GuardianClass.Hunter),
            ToArmor(HunterMask, "Testing Mask", ArmorSlot.Helmet, ItemTier.Legendary, Optional.Some(TestingSet), GuardianClass.Hunter),
            ToArmor(TitanHelm, "Testing Helm", ArmorSlot.Helmet, ItemTier.Legendary, Optional.Some(TestingSet), GuardianClass.Titan),
        ],
        ImmutableDictionary.CreateRange([KeyValuePair.Create(TestingSet, new ArmorSet(TestingSet, "Testing", []))]));

    /// <summary>What the join reads for a pick: the item with its trait columns.</summary>
    private static readonly ManifestExcerpt Joined = new(
        Optional.None<string>(),
        ImmutableDictionary<DamageType, string>.Empty,
        ImmutableDictionary.CreateRange([KeyValuePair.Create(TestingSet, new ArmorSet(TestingSet, "Testing", []))]),
        ImmutableDictionary.CreateRange(
        [
            KeyValuePair.Create(PowerExotic, ToWeapon(PowerExotic, "Alpha Machine Gun", WeaponSlot.Power, DamageType.Arc, ItemTier.Exotic, [[9321u, 9322u]])),
            KeyValuePair.Create(EnergyRifle, ToWeapon(EnergyRifle, "Lightning Rifle", WeaponSlot.Energy, DamageType.Arc, ItemTier.Legendary, [[9331u, 9332u], [9333u]])),
            KeyValuePair.Create(HunterMask, ToArmor(HunterMask, "Testing Mask", ArmorSlot.Helmet, ItemTier.Legendary, Optional.Some(TestingSet), GuardianClass.Hunter)),
        ]),
        ImmutableDictionary<ItemHash, StatPlug>.Empty,
        ImmutableDictionary<ItemHash, BreakerType>.Empty,
        new ItemOverlays([], Optional.None<string>(), Optional.None<string>()),
        ImmutableDictionary<ItemHash, KeywordTrait>.Empty,
        ImmutableDictionary<StatKind, ArmorStatIcon>.Empty);

    [Fact]
    public void Every_item_lists_the_slots_weapons_the_one_the_build_wears_first()
    {
        var session = OpenHandWrittenLoop(Catalog);

        var kinetic = LoopDesigning.ListWeaponItems(session.Build, Listing, new WeaponTarget.InPlaceOf(0));
        var power = LoopDesigning.ListWeaponItems(session.Build, Listing, new WeaponTarget.IntoEmpty(WeaponSlot.Power));
        var taken = LoopDesigning.ListWeaponItems(session.Build, Listing, new WeaponTarget.IntoEmpty(WeaponSlot.Kinetic));

        Assert.Equal([Festival, KineticRifle], kinetic.Options.Select(option => option.Item.Hash));
        Assert.Equal([true, false], kinetic.Options.Select(option => option.Worn));
        Assert.Equal([PowerExotic, PowerLauncher], power.Options.Select(option => option.Item.Hash));
        Assert.Equal(Optional.Some(WeaponSlot.Power), power.Slot);
        Assert.Empty(taken.Options);                                                             // the build has a kinetic weapon: it is picked in its place
    }

    [Fact]
    public void Every_item_marks_an_exotic_the_rules_dont_know_and_never_a_legendary()
    {
        var session = OpenHandWrittenLoop(Catalog);
        var thunderlord = ToWeapon(ItemHash.From(3325463374u), "Thunderlord", WeaponSlot.Power, DamageType.Arc, ItemTier.Exotic, []) with
        {
            Kind = new ManifestKind.Weapon(WeaponSlot.Power, Optional.Some(DamageType.Arc), [], Optional.Some(ItemHash.From(4148158229u)), Optional.None<ItemHash>(), Optional.None<ItemHash>(), []),
        };                                                                                       // the rules name it by its intrinsic, Reign Havoc
        var shinobu = ToArmor(ItemHash.From(1053737370u), "Shinobu's Vow", ArmorSlot.Arms, ItemTier.Exotic, Optional.None<ItemHash>(), GuardianClass.Hunter);
        var unknownArms = ToArmor(ItemHash.From(9315u), "Unknown Grips", ArmorSlot.Arms, ItemTier.Exotic, Optional.None<ItemHash>(), GuardianClass.Hunter);
        var listing = Listing.Add(thunderlord).Add(shinobu).Add(unknownArms);

        var power = LoopDesigning.ListWeaponItems(session.Build, listing, new WeaponTarget.IntoEmpty(WeaponSlot.Power));
        var arms = LoopDesigning.ListArmorItems(session.Build, listing, ArmorSlot.Arms);

        Assert.Equal(
            ["Alpha Machine Gun False", "Thunderlord True", "Zeta Launcher True"],
            power.Options.Select(option => $"{option.Item.Name} {option.InRules}").Order(StringComparer.Ordinal));
        Assert.Equal(
            ["Shinobu's Vow True", "Unknown Grips False"],
            arms.Options.Select(option => $"{option.Item.Name} {option.InRules}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_weapon_of_the_game_fills_an_empty_slot_with_no_roll_and_its_catalyst_off()
    {
        var session = OpenHandWrittenLoop(Catalog with { Manifest = Catalog.Manifest.AddExcerpt(Joined) });

        var picked = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickWeaponItem(session, new WeaponTarget.IntoEmpty(WeaponSlot.Power), PowerExotic)).Value;
        var gun = picked.Build.Build.Weapons[^1];

        Assert.Equal((WeaponSlot.Power, "Alpha Machine Gun", Optional.Some(PowerExotic)), (gun.Slot, gun.Name, gun.Hash));
        Assert.Equal((0, false, Optional.None<ItemInstanceId>(), Optional.None<ItemHash>()), (gun.Roll.Length, gun.Catalyst, gun.Instance, gun.Mod));
        Assert.Contains("{slot: power, name: Alpha Machine Gun, type: arc, archetype: auto-rifle, perks: [], hash: 9303}", picked.Design.Build.Text, StringComparison.Ordinal);
        Assert.Equal(session.Design.Steps.Length, picked.Resolutions.Length);                    // the steps replay
        var energy = LoopDesigning.ListWeaponItems(picked.Build, Listing.Add(ToWeapon(ItemHash.From(9305u), "Beta Exotic", WeaponSlot.Energy, DamageType.Void, ItemTier.Exotic, [])), new WeaponTarget.InPlaceOf(1));
        Assert.DoesNotContain(energy.Options, option => option.Item.Tier == Optional.Some(ItemTier.Exotic));
        Assert.Equal(["A build wears one exotic weapon: swap Alpha Machine Gun for a legendary weapon to pick an exotic here."], energy.Notes);
    }

    [Fact]
    public void A_weapon_of_the_game_replaces_a_hand_written_placeholder()
    {
        var session = OpenHandWrittenLoop(Catalog with { Manifest = Catalog.Manifest.AddExcerpt(Joined) });

        var picked = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickWeaponItem(session, new WeaponTarget.InPlaceOf(1), EnergyRifle)).Value;

        Assert.Equal(["Festival Flight", "Lightning Rifle"], picked.Build.Build.Weapons.Select(weapon => weapon.Name));
        Assert.DoesNotContain("Any Arc weapon", picked.Design.Build.Text, StringComparison.Ordinal);
        Assert.Contains("Loopsmith doesn't know that weapon yet",
            Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.PickWeaponItem(OpenHandWrittenLoop(Catalog), new WeaponTarget.InPlaceOf(1), EnergyRifle)).Failure,
            StringComparison.Ordinal);                                                           // not joined yet: the build doesn't change
    }

    [Fact]
    public void A_pick_whose_manifest_read_failed_says_so_and_one_that_read_adds_the_item()
    {
        var session = OpenHandWrittenLoop(Catalog);

        var failed = LoopDesigning.UsePickedItemManifest(session, new Result<ManifestExcerpt, string>.Error("Failed to fetch"));
        var read = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.UsePickedItemManifest(session, new Result<ManifestExcerpt, string>.Ok(Joined))).Value;

        Assert.Equal("Loopsmith couldn't read Bungie's manifest for it (Failed to fetch). Try again.", Assert.IsType<Result<DesignSession, string>.Error>(failed).Failure);
        Assert.Empty(LoopDesigning.ListUnknownItemHashes(read, EnergyRifle));
        Assert.Equal([EnergyRifle], LoopDesigning.ListUnknownItemHashes(session, EnergyRifle));
        Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickWeaponItem(read, new WeaponTarget.InPlaceOf(1), EnergyRifle));
    }

    [Fact]
    public void Emptying_a_weapon_slot_takes_the_weapon_off_and_blocks_the_steps_that_use_it()
    {
        var session = OpenHandWrittenLoop(Catalog);
        var energyStep = session.Design.Steps.IndexOf(session.Design.Steps.First(step => step.Action is PlayerAction.FireWeapon { Slot: WeaponSlot.Energy }));

        var emptied = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.EmptyWeaponSlot(session, 1)).Value;

        Assert.Equal(["Festival Flight"], emptied.Build.Build.Weapons.Select(weapon => weapon.Name));
        Assert.False(session.Resolutions[energyStep].Blocked.IsSome());
        Assert.True(emptied.Resolutions[energyStep].Blocked.IsSome());
        Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.EmptyWeaponSlot(emptied, 1));
    }

    [Fact]
    public void Every_item_lists_the_builds_class_pieces_for_the_slot_with_their_set()
    {
        var session = OpenHandWrittenLoop(Catalog);

        var helmets = LoopDesigning.ListArmorItems(session.Build, Listing, ArmorSlot.Helmet);

        Assert.Equal([HunterMask], helmets.Options.Select(option => option.Item.Hash));         // not the Titan's; no exotic: the build wears Shinobu's Vow
        Assert.Equal(Optional.Some("Testing"), helmets.Options[0].Set.Map(set => set.Name));
        Assert.Equal(["A build wears one exotic armor piece: swap Shinobu's Vow for a legendary piece to pick an exotic here."], helmets.Notes);
    }

    [Fact]
    public void A_piece_of_the_game_goes_in_its_slot_and_the_stats_stay()
    {
        var session = OpenHandWrittenLoop(Catalog with { Manifest = Catalog.Manifest.AddExcerpt(Joined) });

        var picked = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickArmorItem(session, ArmorSlot.Helmet, HunterMask)).Value;

        Assert.Contains(picked.Build.Build.LeftOut, item => item.Hash == HunterMask && item.Part == LoadoutPart.Item);
        Assert.Empty(picked.Build.Build.ArmorCopies);
        Assert.Equal(session.Build.Build.Stats, picked.Build.Build.Stats);
        Assert.Equal(session.Build.Build.ExoticArmor, picked.Build.Build.ExoticArmor);
        Assert.Contains(HunterMask, LoopDesigning.ListArmorItems(picked.Build, Listing, ArmorSlot.Helmet).Options.Where(option => option.Worn).Select(option => option.Item.Hash));
    }

    [Fact]
    public void Emptying_the_exotics_slot_takes_the_exotic_off_and_an_exotic_class_item_says_its_spirits_cant_be_picked()
    {
        var session = OpenHandWrittenLoop(Catalog);

        var emptied = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.EmptyArmorSlot(session, ArmorSlot.Arms)).Value;
        var cloaks = LoopDesigning.ListArmorItems(emptied.Build, Listing, ArmorSlot.ClassItem);

        Assert.Equal(Optional.Some("shinobus-vow"), session.Build.Build.ExoticArmor.Map(id => id.Value));
        Assert.False(emptied.Build.Build.ExoticArmor.IsSome());
        Assert.DoesNotContain("exoticArmor:", emptied.Design.Build.Text, StringComparison.Ordinal);
        var relativism = Assert.Single(cloaks.Options);
        Assert.Equal(Optional.Some("Its two spirits can't be picked yet: the build wears it without them."), relativism.Note);
        Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.EmptyArmorSlot(emptied, ArmorSlot.Arms));
    }

    [Fact]
    public void Emptying_a_set_pieces_slot_drops_the_bonus_its_pieces_no_longer_grant()
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        var session = LoopDesigning.StartSavedDimDesign(Catalog, new SourceText(path, File.ReadAllText(RepoFiles.ToPath(path))))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        var emptied = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.EmptyArmorSlot(session, ArmorSlot.Helmet)).Value;

        Assert.Equal(["luminopotent-2pc", "luminopotent-4pc"], session.Build.Build.ArmorSetBonuses.Select(id => id.Value).Order());
        Assert.Equal(["luminopotent-2pc"], emptied.Build.Build.ArmorSetBonuses.Select(id => id.Value));
        Assert.DoesNotContain(emptied.Build.Build.LeftOut, item => item.Hash == ItemHash.From(593554567u));   // the Luminopotent Mask
    }

    private static DesignSession OpenHandWrittenLoop(RuleCatalog catalog)
    {
        const string path = "builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml";
        return LoopDesigning.ImportLoop(catalog, new SourceText(path, File.ReadAllText(RepoFiles.ToPath(path))))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
    }

    private static ManifestItem ToWeapon(ItemHash hash, string name, WeaponSlot slot, DamageType type, ItemTier tier, uint[][] traits) =>
        new(hash, name,
            new ManifestKind.Weapon(slot, Optional.Some(type), [.. traits.Select(column => new TraitColumn([.. column.Select(ItemHash.From)]))],
                Optional.None<ItemHash>(), Optional.None<ItemHash>(), Optional.None<ItemHash>(), []),
            Optional.Some("Auto Rifle"), Optional.Some(tier), Optional.None<string>(), Optional.None<string>(), Optional.None<string>());

    private static ManifestItem ToArmor(ItemHash hash, string name, ArmorSlot slot, ItemTier tier, Optional<ItemHash> set, GuardianClass wornBy) =>
        new(hash, name, new ManifestKind.Armor(slot, Optional.None<ItemHash>(), set, Optional.Some(wornBy)),
            Optional.None<string>(), Optional.Some(tier), Optional.None<string>(), Optional.None<string>(), Optional.None<string>());
}

/// <summary>A listing with one more item (in name order, as the listing keeps them).</summary>
internal static class ItemListingTesting
{
    public static ItemListing Add(this ItemListing listing, ManifestItem item) =>
        listing with { Items = [.. listing.Items.Append(item).OrderBy(found => found.Name, StringComparer.OrdinalIgnoreCase)] };
}

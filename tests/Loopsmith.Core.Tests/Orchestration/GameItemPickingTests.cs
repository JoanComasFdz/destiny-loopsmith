using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// Any weapon or armor piece of the game, without a Bungie login (docs/loop-format.md, "Weapons and armor of the game"):
/// the pickers list the game's items a slot of the build can take (its slot, its class, one exotic), an exotic the rules
/// don't know marked; a pick goes into the build with no copy behind it — a weapon with no perk picked, a piece without
/// changing the stats — and stays there through export and import.
/// </summary>
public sealed class GameItemPickingTests
{
    private static readonly RuleCatalog Catalog =
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static readonly ItemHash Thunderlord = ItemHash.From(3325463374u);
    private static readonly ItemHash Festival = ItemHash.From(4019651319u);
    private static readonly ItemHash ShinobusVow = ItemHash.From(1611241895u);
    private static readonly ItemHash LuminopotentMask = ItemHash.From(593554567u);
    private static readonly ImmutableArray<ItemHash> Luminopotent = [LuminopotentMask, ItemHash.From(2883433250u), ItemHash.From(1047792392u), ItemHash.From(2107326067u)];

    // Items of the game the bundled excerpt doesn't have: the join reads them for the one picked.
    private static readonly ItemHash GameCannon = ItemHash.From(9301u);
    private static readonly ItemHash GameExoticRifle = ItemHash.From(9302u);
    private static readonly ItemHash GameExoticMask = ItemHash.From(9303u);
    private static readonly ItemHash TitanHelm = ItemHash.From(9304u);

    private static readonly ManifestItem Cannon = ToWeapon(GameCannon, "Game Cannon", WeaponSlot.Kinetic, DamageType.Kinetic, ItemTier.Legendary);
    private static readonly ManifestItem ExoticRifle = ToWeapon(GameExoticRifle, "Game Exotic Rifle", WeaponSlot.Energy, DamageType.Void, ItemTier.Exotic);
    private static readonly ManifestItem ExoticMask = ToArmor(GameExoticMask, "Game Exotic Mask", ArmorSlot.Helmet, ItemTier.Exotic, Optional.Some(GuardianClass.Hunter));
    private static readonly ManifestItem Helm = ToArmor(TitanHelm, "Titan Helm", ArmorSlot.Helmet, ItemTier.Legendary, Optional.Some(GuardianClass.Titan));

    private static readonly GameItems Game = new(
        [Cannon, ExoticRifle, Catalog.Manifest.Items[Thunderlord]],
        [ExoticMask, Helm, Catalog.Manifest.Items[ShinobusVow], .. Luminopotent.Select(hash => Catalog.Manifest.Items[hash])],
        ImmutableDictionary<ItemHash, ManifestItem>.Empty);

    [Fact]
    public void Each_slot_lists_the_games_items_it_can_take_and_marks_the_exotics_the_rules_dont_know()
    {
        var session = StartScratch();

        var kinetic = LoopDesigning.ListGameWeaponOptions(session.Build, Game, new WeaponTarget.IntoEmpty(WeaponSlot.Kinetic));
        var energy = LoopDesigning.ListGameWeaponOptions(session.Build, Game, new WeaponTarget.IntoEmpty(WeaponSlot.Energy));
        var power = LoopDesigning.ListGameWeaponOptions(session.Build, Game, new WeaponTarget.IntoEmpty(WeaponSlot.Power));
        var helmets = LoopDesigning.ListGameArmorOptions(session.Build, Game, ArmorSlot.Helmet);

        Assert.Equal([("Game Cannon", true)], kinetic.Options.Select(option => (option.Item.Name, option.InRules)));
        Assert.Equal([("Game Exotic Rifle", false)], energy.Options.Select(option => (option.Item.Name, option.InRules)));
        Assert.Equal([("Thunderlord", true)], power.Options.Select(option => (option.Item.Name, option.InRules)));   // its intrinsic perk is in the rules
        Assert.Equal([("Game Exotic Mask", false), ("Luminopotent Mask", true)], helmets.Options.Select(option => (option.Item.Name, option.InRules))); // not the Titan's
        Assert.Empty(kinetic.Notes);
    }

    [Fact]
    public void A_weapon_of_the_game_fills_an_empty_slot_with_no_perk_picked_and_stays_through_export()
    {
        var session = StartScratch();

        var picked = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickGameWeapon(session, new WeaponTarget.IntoEmpty(WeaponSlot.Power), Thunderlord)).Value;
        var weapon = Assert.Single(picked.Build.Build.Weapons);
        var reopened = LoopDesigning.ImportLoop(Catalog, new SourceText("arc-hunter.loop.yaml", LoopDesigning.ExportLoop(picked)))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Equal(("Thunderlord", Optional.Some(Thunderlord), false, Optional.None<ItemInstanceId>()), (weapon.Name, weapon.Hash, weapon.Catalyst, weapon.Instance));
        Assert.Empty(weapon.Roll);
        Assert.Contains("{slot: power, name: Thunderlord, type: arc, archetype: machine-gun, perks: [], hash: 3325463374}", picked.Design.Build.Text, StringComparison.Ordinal);
        Assert.True(LoopDesigning.HasTriggerSource(picked.Build));
        Assert.Equal([Thunderlord], reopened.Build.Build.Weapons.Select(reread => reread.Hash.UnwrapOr(Festival)));
    }

    [Fact]
    public void A_weapon_the_excerpt_lacks_is_read_first_then_replaces_the_builds_weapon_in_its_place()
    {
        var session = OpenOwnersSession();
        var festival = session.Build.Build.Weapons.IndexOf(session.Build.Build.Weapons.Single(weapon => weapon.Hash == Optional.Some(Festival)));
        var target = new WeaponTarget.InPlaceOf(festival);

        var unread = Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.PickGameWeapon(session, target, GameCannon));
        var joined = LoopDesigning.UseJoinedManifest(session, new Result<ManifestExcerpt, string>.Ok(ToExcerpt(Cannon)))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        var picked = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickGameWeapon(joined, target, GameCannon)).Value;

        Assert.Equal([GameCannon], LoopDesigning.ListUnknownPickHashes(session, GameCannon));
        Assert.Empty(LoopDesigning.ListUnknownPickHashes(session, Thunderlord));
        Assert.StartsWith("Loopsmith doesn't know that weapon yet", unread.Failure, StringComparison.Ordinal);
        Assert.Equal(("Game Cannon", Optional.Some(GameCannon)), (picked.Build.Build.Weapons[festival].Name, picked.Build.Build.Weapons[festival].Hash));
        Assert.Equal(session.Build.Build.Weapons.Length, picked.Build.Build.Weapons.Length);
        Assert.DoesNotContain("Festival Flight", picked.Design.Build.Text, StringComparison.Ordinal);
        Assert.Contains(LoopDesigning.ListGameWeaponOptions(picked.Build, Game, target).Options, option => option.Item.Hash == GameCannon && option.Worn);
    }

    [Fact]
    public void A_build_wears_one_exotic_weapon_but_its_exotic_can_be_swapped_for_another()
    {
        var session = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickGameWeapon(StartScratch(), new WeaponTarget.IntoEmpty(WeaponSlot.Power), Thunderlord)).Value;
        var reread = LoopDesigning.UseJoinedManifest(session, new Result<ManifestExcerpt, string>.Ok(ToExcerpt(ExoticRifle))).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        var energy = LoopDesigning.ListGameWeaponOptions(session.Build, Game, new WeaponTarget.IntoEmpty(WeaponSlot.Energy));
        var refused = Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.PickGameWeapon(reread, new WeaponTarget.IntoEmpty(WeaponSlot.Energy), GameExoticRifle));

        Assert.Empty(energy.Options);
        Assert.Equal(["A build wears one exotic weapon: swap Thunderlord for a legendary weapon to pick an exotic here."], energy.Notes);
        Assert.Equal("A build wears one exotic weapon: swap Thunderlord for a legendary weapon first.", refused.Failure);
        Assert.Contains(LoopDesigning.ListGameWeaponOptions(session.Build, Game, new WeaponTarget.InPlaceOf(0)).Options, option => option.Item.Hash == Thunderlord && option.Worn);
    }

    [Fact]
    public void A_piece_of_the_game_is_worn_without_a_copy_its_set_bonuses_follow_and_the_stats_stay()
    {
        var session = StartScratch();

        var vow = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickGameArmor(session, ArmorSlot.Arms, ShinobusVow)).Value;
        var twoPieces = new[] { (ArmorSlot.Helmet, Luminopotent[0]), (ArmorSlot.Chest, Luminopotent[1]) }
            .Aggregate(vow, (current, pick) => Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickGameArmor(current, pick.Item1, pick.Item2)).Value);
        var helmets = LoopDesigning.ListGameArmorOptions(twoPieces.Build, Game, ArmorSlot.Helmet);

        Assert.Equal(Optional.Some(ElementId.From("shinobus-vow")), vow.Build.Build.ExoticArmor);
        Assert.Empty(twoPieces.Build.Build.ArmorCopies);
        Assert.Equal(session.Build.Build.Stats, twoPieces.Build.Build.Stats);
        Assert.Equal(["luminopotent-2pc"], twoPieces.Build.Build.ArmorSetBonuses.Select(id => id.Value));
        Assert.Equal([("Luminopotent Mask", true)], helmets.Options.Select(option => (option.Item.Name, option.Worn)));   // no second exotic
        Assert.Equal(["A build wears one exotic armor piece: swap Shinobu's Vow for a legendary piece to pick an exotic here."], helmets.Notes);
    }

    private static DesignSession StartScratch() =>
        LoopDesigning.StartScratchDesign(Catalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static DesignSession OpenOwnersSession()
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        var json = File.ReadAllText(RepoFiles.ToPath(path));                                     // impure
        return LoopDesigning.StartSavedDimDesign(Catalog, new SourceText(path, json))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
    }

    private static ManifestItem ToWeapon(ItemHash hash, string name, WeaponSlot slot, DamageType type, ItemTier tier) =>
        new(hash, name, new ManifestKind.Weapon(slot, Optional.Some(type), [], Optional.None<ItemHash>(), Optional.None<ItemHash>(), Optional.None<ItemHash>()),
            Optional.Some("Hand Cannon"), Optional.Some(tier), Optional.None<string>(), Optional.None<string>(), Optional.None<string>());

    private static ManifestItem ToArmor(ItemHash hash, string name, ArmorSlot slot, ItemTier tier, Optional<GuardianClass> wornBy) =>
        new(hash, name, new ManifestKind.Armor(slot, Optional.None<ItemHash>(), Optional.None<ItemHash>(), wornBy),
            Optional.Some("Helmet"), Optional.Some(tier), Optional.None<string>(), Optional.None<string>(), Optional.None<string>());

    private static ManifestExcerpt ToExcerpt(ManifestItem item) =>
        new(
            Optional.None<string>(),
            ImmutableDictionary<DamageType, string>.Empty,
            ImmutableDictionary<ItemHash, ArmorSet>.Empty,
            ImmutableDictionary<ItemHash, ManifestItem>.Empty.Add(item.Hash, item),
            ImmutableDictionary<ItemHash, StatPlug>.Empty,
            ImmutableDictionary<ItemHash, BreakerType>.Empty,
            new ItemOverlays([], Optional.None<string>(), Optional.None<string>()),
            ImmutableDictionary<ItemHash, KeywordTrait>.Empty,
            ImmutableDictionary<StatKind, ArmorStatIcon>.Empty);
}

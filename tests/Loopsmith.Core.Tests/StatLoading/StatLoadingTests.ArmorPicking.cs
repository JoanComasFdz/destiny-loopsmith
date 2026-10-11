using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.StatLoading;

/// <summary>
/// The player's own armor in the build, after a Bungie login (docs/loop-format.md, "Stats"): a dim.gg share names the
/// copy of each piece, so the share's owner gets its stats at once; anyone else picks their own pieces with ⋯, and a
/// piece picked swaps into the build — its set bonuses follow the pieces it wears.
/// </summary>
public sealed partial class StatLoadingTests
{
    // The owner's share's own copies (equipped[].id), and a second set no rule knows (two pieces, its 2- and 4-piece perks).
    // Properties, not fields: they read StatLoadingTests.cs's fields, and static fields of a partial class initialise in no set order.
    private static ImmutableDictionary<ItemHash, string> ShareCopies => ImmutableDictionary.CreateRange(
    [
        KeyValuePair.Create(Mask, "6917530185805135602"),
        KeyValuePair.Create(ItemHash.From(2883433250u), "6917530186678878648"),
        KeyValuePair.Create(ItemHash.From(1047792392u), "6917530186076125866"),
        KeyValuePair.Create(ItemHash.From(2107326067u), "6917530187450929950"),
        KeyValuePair.Create(Vow, "6917530187134607359"),
    ]);

    private static readonly ItemHash WandererHood = ItemHash.From(9001u);
    private static readonly ItemHash WandererBoots = ItemHash.From(9002u);
    private static readonly ItemHash WarlockHood = ItemHash.From(9003u);
    private static readonly ItemHash ExoticHelmet = ItemHash.From(9004u);

    private static ManifestExcerpt OtherArmor => new(
        Optional.None<string>(),
        ImmutableDictionary<DamageType, string>.Empty,
        ImmutableDictionary.CreateRange([KeyValuePair.Create(ItemHash.From(9100u), new ArmorSet(ItemHash.From(9100u), "Wanderer",
            [new SetPerk(PieceCount.From(2), ItemHash.From(9101u)), new SetPerk(PieceCount.From(4), ItemHash.From(9102u))]))]),
        ImmutableDictionary.CreateRange(
        [
            ToArmorItem(WandererHood, "Wanderer Hood", ArmorSlot.Helmet, ItemTier.Legendary, Optional.Some(ItemHash.From(9100u)), GuardianClass.Hunter),
            ToArmorItem(WandererBoots, "Wanderer Boots", ArmorSlot.Legs, ItemTier.Legendary, Optional.Some(ItemHash.From(9100u)), GuardianClass.Hunter),
            ToArmorItem(WarlockHood, "Warlock Hood", ArmorSlot.Helmet, ItemTier.Legendary, Optional.None<ItemHash>(), GuardianClass.Warlock),
            ToArmorItem(ExoticHelmet, "Celestial Nighthawk", ArmorSlot.Helmet, ItemTier.Exotic, Optional.None<ItemHash>(), GuardianClass.Hunter),
        ]),
        ImmutableDictionary<ItemHash, StatPlug>.Empty,
        ImmutableDictionary<ItemHash, BreakerType>.Empty,
        new ItemOverlays([], Optional.None<string>(), Optional.None<string>()),
        ImmutableDictionary<ItemHash, KeywordTrait>.Empty);

    [Fact]
    public void A_dim_gg_share_names_the_copy_of_each_armor_piece_and_the_build_file_keeps_them()
    {
        var build = OpenOwnersBuild();
        var file = WriteOwnersBuildFile();

        Assert.Equal(ShareCopies.OrderBy(copy => copy.Value, StringComparer.Ordinal).Select(copy => new ArmorCopy(copy.Key, ItemInstanceId.From(copy.Value))),
            build.Build.ArmorCopies.OrderBy(copy => copy.Instance.Value, StringComparer.Ordinal));
        Assert.Contains("armorCopies: [{hash: 593554567, instance: \"6917530185805135602\"}, {hash: 2883433250, instance: \"6917530186678878648\"}", file.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Logging_in_with_the_shares_own_account_sets_the_stats_from_its_pieces()
    {
        var session = OpenOwnersSession(Catalog);

        var login = LoopDesigning.ApplyBungieLogin(session, ReadProfile([.. OwnShare(tuningSockets: 3)]));

        Assert.StartsWith("Your armor is loaded: the build wears this loadout's own pieces, and its stats are theirs.", login.Message, StringComparison.Ordinal);
        Assert.Equal(new StatLine(ToKnownStat(27), ToKnownStat(27), ToKnownStat(100), ToKnownStat(190), ToKnownStat(125), ToKnownStat(27)), login.Session.Build.Build.Stats);
        Assert.Contains("stats: {health: 27, melee: 27, grenade: 190, super: 125, class: 100, weapons: 27}", login.Session.Design.Build.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Someone_elses_share_asks_the_player_to_pick_their_own_pieces_and_sets_nothing()
    {
        var session = OpenOwnersSession(Catalog);
        var partly = OwnShare(tuningSockets: 0).Select(item => item.Hash == Mask ? item with { Instance = "1001" } : item).ToImmutableArray();

        var other = LoopDesigning.ApplyBungieLogin(session, ReadProfile([.. OwnAll(tuningSockets: 0)]));
        var some = LoopDesigning.ApplyBungieLogin(session, ReadProfile(partly));

        Assert.StartsWith("You can now pick your armor: tap ⋯ next to each armor piece in the build", other.Message, StringComparison.Ordinal);
        Assert.Equal(session.Design.Build.Text, other.Session.Design.Build.Text);
        Assert.Equal("This loadout's pieces are in your account, but for Luminopotent Mask: tap ⋯ next to it in the build to pick one of yours. Tap ⋯ next to a weapon to pick one of yours.", some.Message);
        Assert.Equal(session.Design.Build.Text, some.Session.Design.Build.Text);
    }

    [Fact]
    public void Two_pieces_of_another_set_leave_Luminopotents_2_piece_bonus_and_give_the_other_sets()
    {
        var session = OpenOwnersSession(Catalog with { Manifest = Catalog.Manifest.AddExcerpt(OtherArmor) });
        var profile = ReadProfile([.. OwnShare(tuningSockets: 0), ToRolledItem(WandererHood, "3001"), ToRolledItem(WandererBoots, "3002")]);

        var helmet = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickArmorPiece(session, profile, ArmorSlot.Helmet, ItemInstanceId.From("3001"))).Value;
        var legs = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickArmorPiece(helmet, profile, ArmorSlot.Legs, ItemInstanceId.From("3002"))).Value;

        var sets = LoopDesigning.ListArmorSets(legs.Build).ToImmutableDictionary(set => set.Set.Name);
        Assert.Equal([ElementId.From("luminopotent-2pc"), ElementId.From("luminopotent-4pc")], session.Build.Build.ArmorSetBonuses);
        Assert.Equal([ElementId.From("luminopotent-2pc")], helmet.Build.Build.ArmorSetBonuses);   // 3 Luminopotent pieces: no 4-piece bonus
        Assert.Equal([ElementId.From("luminopotent-2pc")], legs.Build.Build.ArmorSetBonuses);
        Assert.All(["Luminopotent", "Wanderer"], set =>
        {
            Assert.Equal(Optional.Some(PieceCount.From(2)), sets[set].Pieces);
            Assert.Equal([true, false], sets[set].Perks.Select(perk => perk.Active));   // its 2-piece perk, not its 4-piece one
        });
        Assert.Contains("{hash: 9001, instance: \"3001\"}", legs.Design.Build.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("593554567", legs.Design.Build.Text, StringComparison.Ordinal);
        Assert.True(legs.Build.Build.Stats.Grenade.IsSome());   // the stats follow the armor
    }

    [Fact]
    public void An_exotic_while_the_build_wears_one_or_another_classs_piece_cant_be_worn()
    {
        var session = OpenOwnersSession(Catalog with { Manifest = Catalog.Manifest.AddExcerpt(OtherArmor) });
        var profile = ReadProfile([.. OwnShare(tuningSockets: 0), ToRolledItem(ExoticHelmet, "3004"), ToRolledItem(WarlockHood, "3003")]);

        Assert.Equal(
            new Result<DesignSession, string>.Error("A build wears one exotic armor piece: swap Shinobu's Vow for a legendary piece first."),
            LoopDesigning.PickArmorPiece(session, profile, ArmorSlot.Helmet, ItemInstanceId.From("3004")));
        Assert.Equal(
            new Result<DesignSession, string>.Error("Warlock Hood is Warlock armor, and the build is for a Hunter."),
            LoopDesigning.PickArmorPiece(session, profile, ArmorSlot.Helmet, ItemInstanceId.From("3003")));
    }

    [Fact]
    public void The_picker_lists_the_slots_pieces_for_the_class_the_worn_copy_first_and_says_what_it_leaves_out()
    {
        var session = OpenOwnersSession(Catalog with { Manifest = Catalog.Manifest.AddExcerpt(OtherArmor) });
        var profile = ReadProfile(
        [
            .. OwnShare(tuningSockets: 0),
            new FakeItem(Mask, "1002", "vault", [FindRollPlug(StatKind.Grenade, 20), FullMasterwork], 11),
            ToRolledItem(WandererHood, "3001"),
            ToRolledItem(WarlockHood, "3003"),
            ToRolledItem(ExoticHelmet, "3004"),
            ToRolledItem(ItemHash.From(4242u), "3005"),   // a piece the excerpt doesn't know
        ]);

        var options = LoopDesigning.ListArmorOptions(session.Build, profile, ArmorSlot.Helmet);

        Assert.Equal(["6917530185805135602", "1002", "3001"], options.Options.Select(option => option.Armor.InstanceId.Value));
        Assert.Equal([true, false, false], options.Options.Select(option => option.Worn));
        Assert.Equal(
            ["A build wears one exotic armor piece: swap Shinobu's Vow for a legendary piece to pick an exotic here.",
                "1 of your armor pieces aren't listed: Loopsmith can't tell their slot yet (Bungie's manifest wasn't read for them)."],
            options.Notes);
    }

    [Fact]
    public void A_hand_written_build_wears_a_picked_piece_and_its_declared_set_bonuses_then_follow_the_pieces()
    {
        var session = LoopDesigning.CreateSession(OpenHandWrittenBuild(), new LoopDesign("Test", Optional.None<string>(), Optional.None<string>(), Optional.None<CatalogVersion>(), ReadBuildFile(), []));
        var profile = ReadProfile([.. OwnShare(tuningSockets: 0)]);

        var options = LoopDesigning.ListArmorOptions(session.Build, profile, ArmorSlot.Helmet);
        var worn = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickArmorPiece(session, profile, ArmorSlot.Helmet, ItemInstanceId.From("6917530185805135602"))).Value;

        Assert.Equal(["6917530185805135602"], options.Options.Select(option => option.Armor.InstanceId.Value));
        Assert.False(options.Options[0].Worn);                                                  // the build names no helmet
        Assert.Equal([new LeftOutItem(LoadoutPart.Item, Mask)], worn.Build.Build.LeftOut);
        Assert.Equal([new ArmorCopy(Mask, ItemInstanceId.From("6917530185805135602"))], worn.Build.Build.ArmorCopies);
        Assert.Empty(worn.Build.Build.ArmorSetBonuses);                                          // one Luminopotent piece grants none
        Assert.Equal(ElementId.From("shinobus-vow"), worn.Build.Build.ExoticArmor.UnwrapOr(ElementId.From("none")));
        Assert.DoesNotContain("armorSetBonuses", worn.Design.Build.Text, StringComparison.Ordinal);
        Assert.EndsWith("leftOut: {items: [593554567]}\narmorCopies: [{hash: 593554567, instance: \"6917530185805135602\"}]\n", worn.Design.Build.Text, StringComparison.Ordinal);
    }

    /// <summary>Every piece of the owner's share as its own copy (its id), each a Grenade 30 · Super 25 · Class 20 roll, masterworked.</summary>
    private static IEnumerable<FakeItem> OwnShare(int tuningSockets) =>
        OwnAll(tuningSockets).Select(item => item with { Instance = ShareCopies[item.Hash] });

    private static FakeItem ToRolledItem(ItemHash hash, string instance) =>
        new(hash, instance, "vault", [FindRollPlug(StatKind.Grenade, 30), FindRollPlug(StatKind.Super, 25), FindRollPlug(StatKind.Class, 20), FullMasterwork], 11);

    private static KeyValuePair<ItemHash, ManifestItem> ToArmorItem(ItemHash hash, string name, ArmorSlot slot, ItemTier tier, Optional<ItemHash> set, GuardianClass wornBy) =>
        KeyValuePair.Create(hash, new ManifestItem(hash, name, new ManifestKind.Armor(slot, Optional.None<ItemHash>(), set, Optional.Some(wornBy)),
            Optional.None<string>(), Optional.Some(tier), Optional.None<string>(), Optional.None<string>()));

    private static DesignSession OpenOwnersSession(RuleCatalog catalog)
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        var json = File.ReadAllText(RepoFiles.ToPath(path));                                     // impure
        return LoopDesigning.StartSavedDimDesign(catalog, new SourceText(path, json))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
    }
}

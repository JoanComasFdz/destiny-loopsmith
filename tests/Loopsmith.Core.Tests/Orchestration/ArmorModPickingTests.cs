using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// Armor mods picked in the designer (docs/loop-format.md "Armor mods"): each piece's mod sockets from the listing of
/// every piece, a slot without a piece holding what the build has and one free socket, the mod picker's list (the
/// subclass's element first, the rules' marked), the exact copy kept, and the energy a piece's copy holds refusing a
/// pick that doesn't fit.
/// </summary>
public sealed class ArmorModPickingTests
{
    private static readonly RuleCatalog Catalog =
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static readonly ItemHash ArcSiphon = ItemHash.From(2724068510u);
    private static readonly ItemHash CheapArcSiphon = ItemHash.From(3847471926u);
    private static readonly ItemHash SolarSiphon = ItemHash.From(4255093903u);
    private static readonly ItemHash UnknownHelmetMod = ItemHash.From(9501u);
    private static readonly ItemHash GrenadeMod = ItemHash.From(1435557120u);
    private static readonly ItemHash LuminopotentMask = ItemHash.From(593554567u);
    private static readonly ItemInstanceId MaskCopy = ItemInstanceId.From("6917530000000000001");
    private static readonly PlugCategoryId Head = PlugCategoryId.From("enhancements.v2_head");

    private static readonly ItemListing Listing = new(
        [],
        ImmutableDictionary<ItemHash, ArmorSet>.Empty,
        [
            ToMod(ArcSiphon, "Arc Siphon", Head, 3),
            ToMod(CheapArcSiphon, "Arc Siphon", Head, 1, "Must Be Selected in the Seasonal Artifact"),
            ToMod(GrenadeMod, "Grenade Mod", ArmorModCategories.General, 3),
            ToMod(SolarSiphon, "Solar Siphon", Head, 3),
            ToMod(UnknownHelmetMod, "Zeta Helmet Mod", Head, 2),
        ],
        ImmutableDictionary<ItemHash, ImmutableArray<PlugCategoryId>>.Empty.Add(LuminopotentMask, [ArmorModCategories.General, Head, Head, Head]));

    private static readonly ModSocketPlace HelmetFirst = new(Optional.Some(ArmorSlot.Helmet), 0);
    private static readonly ModSocketPlace HelmetSecond = new(Optional.Some(ArmorSlot.Helmet), 1);
    private static readonly ModSocketPlace GeneralFirst = new(Optional.None<ArmorSlot>(), 0);
    private static readonly Optional<BungieProfile> NoLogin = Optional.None<BungieProfile>();

    [Fact]
    public void A_slot_without_a_piece_holds_one_free_socket_and_the_general_row_one_per_slot()
    {
        var layout = LoopDesigning.LayOutMods(StartScratch().Build, Listing, NoLogin);

        var helmet = layout.Pieces.Single(piece => piece.Slot == ArmorSlot.Helmet);
        Assert.False(helmet.PieceKnown);
        Assert.Equal([Head], helmet.Sockets.Select(socket => socket.Category));
        Assert.All(layout.Pieces, piece => Assert.Single(piece.Sockets));
        Assert.Equal(5, layout.General.Length);
        Assert.Equal((0, Optional.None<ArmorEnergy>()), (helmet.Energy.Used.Value, helmet.Energy.Capacity));   // "?" without a login
        Assert.Empty(layout.Unplaced);
    }

    [Fact]
    public void A_picked_mod_goes_in_its_socket_and_into_the_embedded_build_its_copy_kept()
    {
        var session = StartScratch();

        var first = Pick(session, HelmetFirst, ArcSiphon);
        var second = Pick(first, HelmetSecond, CheapArcSiphon);
        var helmet = LoopDesigning.LayOutMods(second.Build, Listing, NoLogin).Pieces.Single(piece => piece.Slot == ArmorSlot.Helmet);

        Assert.Equal(["arc-siphon", "arc-siphon"], second.Build.Build.ArmorMods.Select(id => id.Value));
        Assert.Equal([ArcSiphon, CheapArcSiphon], second.Build.Build.ArmorModPlugs);               // each copy in its place: one is the cheaper
        Assert.Contains("armorMods: [arc-siphon, arc-siphon]", second.Design.Build.Text, StringComparison.Ordinal);
        Assert.Contains("armorModPlugs: [2724068510, 3847471926]", second.Design.Build.Text, StringComparison.Ordinal);
        Assert.Equal([Optional.Some(ArcSiphon), Optional.Some(CheapArcSiphon), Optional.None<ItemHash>()], helmet.Sockets.Select(socket => socket.Mod.Map(mod => mod.Plug)));
        Assert.Equal(4, helmet.Energy.Used.Value);
        Assert.Equal(2, second.Build.Equipped.Single(element => element.Element.Id.Value == "arc-siphon").Count);  // stacked copies
        var reopened = LoopDesigning.ImportLoop(Catalog, new SourceText("a.loop.yaml", LoopDesigning.ExportLoop(second))).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        Assert.Equal(second.Build.Build.ArmorModPlugs, reopened.Build.Build.ArmorModPlugs);
    }

    [Fact]
    public void A_mod_the_rules_dont_know_is_left_out_and_clearing_a_socket_takes_its_mod_off()
    {
        var picked = Pick(Pick(StartScratch(), HelmetFirst, UnknownHelmetMod), GeneralFirst, GrenadeMod);

        Assert.Empty(picked.Build.Build.ArmorMods);
        Assert.Equal([UnknownHelmetMod, GrenadeMod], picked.Build.Build.LeftOut.Where(item => item.Part == LoadoutPart.ArmorMod).Select(item => item.Hash));
        Assert.Equal(Optional.Some(GrenadeMod), LoopDesigning.LayOutMods(picked.Build, Listing, NoLogin).General[0].Mod.Map(mod => mod.Plug));

        var cleared = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.ClearMod(picked, Listing, NoLogin, HelmetFirst)).Value;

        Assert.Equal([GrenadeMod], cleared.Build.Build.LeftOut.Where(item => item.Part == LoadoutPart.ArmorMod).Select(item => item.Hash));
        Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.ClearMod(cleared, Listing, NoLogin, HelmetFirst));   // nothing there now
        Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.PickMod(cleared, Listing, NoLogin, HelmetFirst, GrenadeMod)); // a general mod doesn't go in a helmet socket
    }

    [Fact]
    public void The_picker_lists_the_sockets_mods_the_subclasss_element_first_and_marks_the_rules()
    {
        var options = Assert.IsType<Result<ModOptions, string>.Ok>(LoopDesigning.ListModOptions(StartScratch().Build, Listing, NoLogin, HelmetFirst)).Value;

        Assert.Equal(Optional.Some(Affinity.Arc), options.Element);
        Assert.Equal(
            ["Arc Siphon 3 True True", "Arc Siphon 1 True True", "Solar Siphon 3 True False", "Zeta Helmet Mod 2 False False"],
            options.Options.Select(option => $"{option.Mod.Item.Name} {option.Mod.Cost.Value} {option.InRules} {option.OfSubclassElement}"));
        Assert.All(options.Options, option => Assert.False(option.Refused.IsSome()));             // no capacity known: nothing is refused
    }

    [Fact]
    public void A_piece_with_its_sockets_and_its_copys_energy_refuses_a_mod_that_doesnt_fit()
    {
        var profile = new BungieProfile(
            [new OwnedArmor(MaskCopy, LuminopotentMask, new ArmorPlace.Vault(), [], Optional.Some(5), new GearMarks(Optional.None<GearTier>(), false, Optional.None<ItemHash>(), Optional.None<PowerLevel>()))],
            [],
            []);
        var worn = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickArmorPiece(StartScratch(), profile, ArmorSlot.Helmet, MaskCopy)).Value;
        var login = Optional.Some(profile);
        var siphoned = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickMod(worn, Listing, login, HelmetFirst, ArcSiphon)).Value;

        var layout = LoopDesigning.LayOutMods(siphoned.Build, Listing, login);
        var helmet = layout.Pieces.Single(piece => piece.Slot == ArmorSlot.Helmet);
        Assert.True(helmet.PieceKnown);
        Assert.Equal(3, helmet.Sockets.Length);                                                   // the mask's three helmet sockets
        Assert.Equal((3, Optional.Some(ArmorEnergy.From(5))), (helmet.Energy.Used.Value, helmet.Energy.Capacity));
        var options = Assert.IsType<Result<ModOptions, string>.Ok>(LoopDesigning.ListModOptions(siphoned.Build, Listing, login, HelmetSecond)).Value;
        Assert.Equal(
            Optional.Some("Solar Siphon costs 3 energy: the helmet has 2 left."),
            options.Options.Single(option => option.Mod.Item.Hash == SolarSiphon).Refused);
        Assert.False(options.Options.Single(option => option.Mod.Item.Hash == CheapArcSiphon).Refused.IsSome());
        Assert.Equal(
            "Solar Siphon costs 3 energy: the helmet has 2 left.",
            Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.PickMod(siphoned, Listing, login, HelmetSecond, SolarSiphon)).Failure);
        var swapped = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickMod(siphoned, Listing, login, HelmetFirst, SolarSiphon)).Value; // in place of the Arc Siphon: it fits
        Assert.Equal(["solar-siphon"], swapped.Build.Build.ArmorMods.Select(id => id.Value));
        var general = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickMod(siphoned, Listing, login, GeneralFirst, GrenadeMod)).Value;
        Assert.Equal(3, LoopDesigning.LayOutMods(general.Build, Listing, login).Pieces.Single(piece => piece.Slot == ArmorSlot.Helmet).Energy.Used.Value); // it goes to a piece with room
    }

    private static DesignSession Pick(DesignSession session, ModSocketPlace place, ItemHash mod) =>
        LoopDesigning.PickMod(session, Listing, NoLogin, place, mod).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static DesignSession StartScratch() =>
        LoopDesigning.StartScratchDesign(Catalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static ListedMod ToMod(ItemHash hash, string name, PlugCategoryId category, int cost, params string[] requirements) =>
        new(
            new ManifestItem(hash, name, new ManifestKind.ArmorMod(ArmorModCategories.FindSlot(category)), Optional.None<string>(), Optional.Some(ItemTier.Common), Optional.None<string>(), Optional.None<string>(), Optional.None<string>()),
            category,
            ArmorEnergy.From(cost),
            [.. requirements]);
}

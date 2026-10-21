using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// A raid's, an activity's or a retired season's armor mod isn't supported (ADRs D35): a build that has one keeps
/// it in its <c>leftOut</c>, but it is ignored — not a gap ("Not in Loopsmith yet" doesn't count it), in no mod
/// socket, and the build check says so in one info line. What it is comes from Bungie's manifest (the join gives it as an
/// <see cref="ManifestKind.ActivityMod"/>).
/// </summary>
public sealed class ActivityModsTests
{
    private static readonly ItemHash HerdThinner = ItemHash.From(2142852459u);
    private static readonly ItemHash TakenBarrier = ItemHash.From(3570105787u);
    private static readonly ItemHash ArcSiphon = ItemHash.From(2724068510u);
    private static readonly ItemHash UnknownHelmetMod = ItemHash.From(9501u);
    private static readonly PlugCategoryId Head = PlugCategoryId.From("enhancements.v2_head");
    private static readonly ModSocketPlace HelmetFirst = new(Optional.Some(ArmorSlot.Helmet), 0);
    private static readonly Optional<BungieProfile> NoLogin = Optional.None<BungieProfile>();

    /// <summary>The rules, with what the full manifest says of the two raid mods (as the browser's join adds it).</summary>
    private static readonly RuleCatalog Catalog = AddActivityMods(
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure)));

    private static readonly ItemListing Listing = new(
        [],
        ImmutableDictionary<ItemHash, ArmorSet>.Empty,
        [
            new ListedMod(
                new ManifestItem(ArcSiphon, "Arc Siphon", new ManifestKind.ArmorMod(Optional.Some(ArmorSlot.Helmet)), Optional.None<string>(), Optional.Some(ItemTier.Common), Optional.None<string>(), Optional.None<string>(), Optional.None<string>()),
                Head,
                ArmorEnergy.From(3),
                []),
        ],
        ImmutableDictionary<ItemHash, ImmutableArray<PlugCategoryId>>.Empty);

    /// <summary>The Arc Hunter with Gambler's Dodge and the mods: Arc Siphon, then left out Herd Thinner twice, Taken Barrier and a mod no rule knows.</summary>
    private const string BuildFile = """
        name: Raid Hunter
        class: hunter
        subclass: arc
        classAbility: gamblers-dodge
        armorMods: [arc-siphon]
        leftOut: { armorMods: [2142852459, 2142852459, 3570105787, 9501] }
        """;

    [Fact]
    public void A_builds_raid_mods_are_kept_in_the_build_but_ignored_and_the_build_check_says_so()
    {
        var session = StartDesign();
        var build = session.Build;
        var reopened = LoopDesigning.ImportLoop(Catalog, new SourceText("raid.loop.yaml", LoopDesigning.ExportLoop(session)))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Equal(
            new LeftOutReason.NotSupported("Not supported: raid, activity and seasonal mods are ignored. It sets nothing off."),
            LoopDesigning.ClassifyLeftOut(build, new LeftOutItem(LoadoutPart.ArmorMod, HerdThinner)));
        Assert.Equal([UnknownHelmetMod], LoopDesigning.ListMissing(build).Select(item => item.Hash));   // only the real gap: "Not in Loopsmith yet" counts 1 armor mod
        Assert.Equal("1 armor mod", LoopDesigning.ListMissing(build).DescribeLeftOut());
        Assert.Equal(["arc-siphon"], build.Build.ArmorMods.Select(id => id.Value));
        Assert.Contains(
            new BuildIssue(Severity.Info, "Ignored from the build: Herd Thinner ×2, Taken Barrier — raid, activity and seasonal mods aren't supported."),
            build.Issues);
        Assert.Equal(
            [HerdThinner, HerdThinner, TakenBarrier, UnknownHelmetMod],
            reopened.Build.Build.LeftOut.Where(item => item.Part == LoadoutPart.ArmorMod).Select(item => item.Hash));   // kept, so a shared loop still says what the build has
        Assert.Contains(reopened.Build.Issues, issue => issue.Message.StartsWith("Ignored from the build: Herd Thinner ×2, Taken Barrier", StringComparison.Ordinal));
    }

    [Fact]
    public void Ignored_mods_take_no_socket_and_stay_in_the_build_when_another_mod_is_picked()
    {
        var session = StartDesign();

        var layout = LoopDesigning.LayOutMods(session.Build, Listing, NoLogin);
        var picked = LoopDesigning.ClearMod(session, Listing, NoLogin, HelmetFirst)
            .Bind(cleared => LoopDesigning.PickMod(cleared, Listing, NoLogin, HelmetFirst, ArcSiphon))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Equal([HerdThinner, HerdThinner, TakenBarrier], layout.Ignored.Select(mod => mod.Plug));
        Assert.Equal([UnknownHelmetMod], layout.Unsocketed.Select(mod => mod.Plug));                  // a gap the listing doesn't have: "Tuning and other mods", as before
        Assert.Empty(layout.Unplaced);
        Assert.DoesNotContain(layout.Pieces.SelectMany(piece => piece.Sockets).Concat(layout.General), socket => socket.Mod.Match(mod => mod.Value.Plug == HerdThinner, _ => false));
        Assert.Equal(
            [UnknownHelmetMod, HerdThinner, HerdThinner, TakenBarrier],
            picked.Build.Build.LeftOut.Where(item => item.Part == LoadoutPart.ArmorMod).Select(item => item.Hash));        // the layout's order: the ignored ones last
    }

    [Fact]
    public void A_build_without_them_has_no_ignored_line()
    {
        var session = LoopDesigning.StartScratchDesign(Catalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.DoesNotContain(session.Build.Issues, issue => issue.Message.StartsWith("Ignored", StringComparison.Ordinal));
        Assert.Empty(LoopDesigning.LayOutMods(session.Build, Listing, NoLogin).Ignored);
    }

    private static DesignSession StartDesign() =>
        LoopDesigning.StartDesign(Catalog, new SourceText("raid-hunter.build.yaml", BuildFile), "Raid Hunter loop")
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static RuleCatalog AddActivityMods(RuleCatalog catalog) =>
        catalog with
        {
            Manifest = catalog.Manifest with
            {
                Items = catalog.Manifest.Items
                    .Add(HerdThinner, ToActivityMod(HerdThinner, "Herd Thinner", "Deep Stone Crypt Raid Mod"))
                    .Add(TakenBarrier, ToActivityMod(TakenBarrier, "Taken Barrier", "Last Wish Raid Mod")),
            },
        };

    private static ManifestItem ToActivityMod(ItemHash hash, string name, string type) =>
        new(hash, name, new ManifestKind.ActivityMod(), Optional.Some(type), Optional.Some(ItemTier.Common), Optional.None<string>(), Optional.None<string>(), Optional.None<string>());
}

using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// The artifact and its perks picked in the designer (docs/loop-format.md "Artifact"): the seven artifacts of Artifacts
/// 2.0 with three tiers of seven perks and seven slots (two take Tier 1, three Tiers 1–2, two Tiers 1–3), the limits
/// that follow (7 perks, 5 of Tiers 2–3, 2 of Tier 3), the build with another artifact and perks, kept in the loop file.
/// </summary>
public sealed class ArtifactPickingTests
{
    private static readonly RuleCatalog Catalog =
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private const string SkipGrenadesPath = "builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml";

    private static readonly ItemHash TabletOfRuin = ItemHash.From(3821685388u);
    private static readonly ItemHash HuntersJournal = ItemHash.From(2180638678u);
    private static readonly ItemHash Dielectric = ItemHash.From(3585856470u);           // Tier 1, in the rules
    private static readonly ItemHash UnravelingOrbs = ItemHash.From(3585856464u);       // Tier 1, in the rules
    private static readonly ItemHash PhotonicFlare = ItemHash.From(3585856465u);        // Tier 1, in the rules
    private static readonly ItemHash TierOneUnknown = ItemHash.From(3585856469u);       // Tier 1, not in the rules
    private static readonly ItemHash Flashover = ItemHash.From(2596646298u);            // Tier 2
    private static readonly ItemHash HordeShuttle = ItemHash.From(2596646301u);         // Tier 2
    private static readonly ItemHash TierTwoUnknown = ItemHash.From(2596646296u);       // Tier 2
    private static readonly ItemHash DefibrillatingBlast = ItemHash.From(1319563986u);  // Tier 3
    private static readonly ItemHash ToShreds = ItemHash.From(1319563985u);             // Tier 3
    private static readonly ItemHash TierThreeUnknown = ItemHash.From(1319563991u);     // Tier 3

    [Fact]
    public void The_seven_artifacts_each_have_three_tiers_of_seven_perks_and_seven_slots()
    {
        var build = StartScratch().Build;

        var artifacts = LoopDesigning.ListArtifacts(build);

        Assert.Equal(
            ["Encrypted Data Disk", "Hunter's Journal", "Implement of Curiosity", "NPA Repulsion Regulator", "Queensfoil Censer", "Slayer Baron Apothecary Satchel", "Tablet of Ruin"],
            artifacts.Select(item => item.Name));
        Assert.All(artifacts, item =>
        {
            var artifact = Assert.IsType<ManifestKind.Artifact>(item.Kind);
            Assert.Equal([7, 7, 7], artifact.Tiers.Select(tier => tier.Perks.Length));
            Assert.Equal([1, 1, 2, 2, 2, 3, 3], artifact.Slots);
        });
    }

    [Fact]
    public void A_loop_that_doesnt_name_its_artifact_shows_the_one_its_perks_are_from_with_its_counts()
    {
        var session = ImportLoop(SkipGrenadesPath);

        var choice = LoopDesigning.ReadArtifactChoice(session.Build);
        var tablet = FindTablet(session);

        Assert.Equal(Optional.Some(TabletOfRuin), choice.Artifact);
        Assert.Equal(
            [UnravelingOrbs, PhotonicFlare, Dielectric, Flashover, HordeShuttle, ToShreds, DefibrillatingBlast],
            choice.Perks.OrderBy(perk => LoopDesigning.FindArtifactTier(tablet, perk).UnwrapOr(0)).ThenBy(perk => perk.Value));
        Assert.Equal(["7/7 selected", "Tier 2–3: 4/5", "Tier 3: 2/2"], LoopDesigning.CountArtifactLimits(tablet, choice.Perks).Select(limit => limit.DescribeArtifactLimit()));
    }

    [Fact]
    public void A_perk_that_would_break_a_limit_names_it()
    {
        var tablet = FindTablet(ImportLoop(SkipGrenadesPath));
        ImmutableArray<ItemHash> twoOfTierThree = [DefibrillatingBlast, ToShreds];
        ImmutableArray<ItemHash> fiveOfTiersTwoAndThree = [DefibrillatingBlast, ToShreds, Flashover, HordeShuttle, TierTwoUnknown];
        ImmutableArray<ItemHash> seven = [.. fiveOfTiersTwoAndThree, Dielectric, UnravelingOrbs];

        Assert.Equal("At most 2 perks of Tier 3 can be active.", Describe(LoopDesigning.FindBrokenArtifactLimit(tablet, twoOfTierThree, TierThreeUnknown)));
        Assert.False(LoopDesigning.FindBrokenArtifactLimit(tablet, twoOfTierThree, Flashover).IsSome());
        Assert.Equal("At most 5 perks of Tier 2–3 can be active.", Describe(LoopDesigning.FindBrokenArtifactLimit(tablet, fiveOfTiersTwoAndThree, ItemHash.From(2596646300u))));
        Assert.False(LoopDesigning.FindBrokenArtifactLimit(tablet, fiveOfTiersTwoAndThree, Dielectric).IsSome());
        Assert.Equal("At most 7 perks can be active.", Describe(LoopDesigning.FindBrokenArtifactLimit(tablet, seven, TierOneUnknown)));
        Assert.False(LoopDesigning.FindBrokenArtifactLimit(tablet, seven, Dielectric).IsSome());   // picked already: tapping it removes it
    }

    [Fact]
    public void The_perks_fill_the_slots_that_take_the_fewest_tiers_first()
    {
        var tablet = FindTablet(ImportLoop(SkipGrenadesPath));

        var placed = LoopDesigning.PlaceArtifactPerks(tablet, [Dielectric, DefibrillatingBlast, Flashover, UnravelingOrbs, TierOneUnknown]);

        Assert.Equal(
            [Dielectric, UnravelingOrbs, Flashover, TierOneUnknown, null, DefibrillatingBlast, null],
            Assert.IsType<Optional<ImmutableArray<Optional<ItemHash>>>.Some>(placed).Value.Select(slot => slot.Match<ItemHash?>(perk => perk.Value, _ => null)));
        Assert.False(LoopDesigning.PlaceArtifactPerks(tablet, [DefibrillatingBlast, ToShreds, TierThreeUnknown]).IsSome());
    }

    [Fact]
    public void Removing_a_perk_replays_the_loop_without_it_and_the_choice_survives_export_and_import()
    {
        var session = ImportLoop(SkipGrenadesPath);
        var flashover = ElementId.From("flashover");
        Assert.True(LoopDesigning.IsEquipped(session.Build, flashover));

        var edited = Edit(session, new ArtifactChoice(Optional.Some(TabletOfRuin), [Dielectric, UnravelingOrbs, TierOneUnknown, HordeShuttle, DefibrillatingBlast, ToShreds]));
        var reopened = LoopDesigning.ImportLoop(Catalog, new SourceText("a.loop.yaml", LoopDesigning.ExportLoop(edited))).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.False(LoopDesigning.IsEquipped(edited.Build, flashover));
        Assert.Contains("artifact: 3821685388", edited.Design.Build.Text, StringComparison.Ordinal);
        Assert.Contains($"artifactPerks: [{TierOneUnknown.Value}]", edited.Design.Build.Text, StringComparison.Ordinal);   // in leftOut: the rules don't know it
        Assert.Equal(
            LoopDesigning.ReadArtifactChoice(edited.Build).Perks.Order(),
            LoopDesigning.ReadArtifactChoice(reopened.Build).Perks.Order());
        Assert.Equal(Optional.Some(TabletOfRuin), reopened.Build.Build.Artifact);
        Assert.Equal(session.Design.Steps, edited.Design.Steps);
    }

    [Fact]
    public void Another_artifact_with_no_perks_and_no_artifact_at_all_can_be_picked()
    {
        var session = ImportLoop(SkipGrenadesPath);

        var journal = Edit(session, new ArtifactChoice(Optional.Some(HuntersJournal), []));
        var none = Edit(journal, new ArtifactChoice(Optional.None<ItemHash>(), []));

        Assert.Equal(Optional.Some(HuntersJournal), LoopDesigning.ReadArtifactChoice(journal.Build).Artifact);
        Assert.Empty(journal.Build.Build.ArtifactPerks);
        Assert.Equal("Hunter's Journal", Assert.IsType<Optional<BuildArtifact>.Some>(LoopDesigning.FindArtifact(journal.Build)).Value.Item.Name);
        Assert.False(LoopDesigning.FindArtifact(none.Build).IsSome());
        Assert.DoesNotContain("artifact:", none.Design.Build.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("artifactPerks:", none.Design.Build.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Perks_that_dont_fit_or_arent_the_artifacts_are_refused()
    {
        var session = ImportLoop(SkipGrenadesPath);

        Assert.Equal("These perks don't fit Tablet of Ruin's slots.", Refusal(session, new ArtifactChoice(Optional.Some(TabletOfRuin), [DefibrillatingBlast, ToShreds, TierThreeUnknown])));
        Assert.Equal("Flashover isn't one of Hunter's Journal's perks.", Refusal(session, new ArtifactChoice(Optional.Some(HuntersJournal), [Flashover])));
        Assert.Equal("An artifact perk can be picked once.", Refusal(session, new ArtifactChoice(Optional.Some(TabletOfRuin), [Flashover, Flashover])));
        Assert.Equal("Artifact perks need an artifact.", Refusal(session, new ArtifactChoice(Optional.None<ItemHash>(), [Flashover])));
    }

    [Fact]
    public void An_artifact_shows_the_builds_perks_else_the_last_picked_for_it_else_none()
    {
        var build = ImportLoop(SkipGrenadesPath).Build;
        var remembered = ImmutableDictionary<ItemHash, ImmutableArray<ItemHash>>.Empty
            .Add(HuntersJournal, [ItemHash.From(3116632771u)])
            .Add(TabletOfRuin, [Flashover]);

        Assert.Equal(LoopDesigning.ReadArtifactChoice(build).Perks, LoopDesigning.RecallArtifactPerks(build, remembered, TabletOfRuin));
        Assert.Equal([ItemHash.From(3116632771u)], LoopDesigning.RecallArtifactPerks(build, remembered, HuntersJournal));
        Assert.Empty(LoopDesigning.RecallArtifactPerks(build, remembered, ItemHash.From(1014202826u)));
    }

    private static string Describe(Optional<ArtifactLimit> limit) =>
        limit.Match(found => found.Value.DescribeBrokenArtifactLimit(), _ => "fits");

    private static ManifestKind.Artifact FindTablet(DesignSession session) =>
        Assert.IsType<Optional<ManifestKind.Artifact>.Some>(LoopDesigning.FindArtifactTiers(session.Build, TabletOfRuin)).Value;

    private static DesignSession Edit(DesignSession session, ArtifactChoice choice) =>
        LoopDesigning.EditArtifact(session, choice).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static string Refusal(DesignSession session, ArtifactChoice choice) =>
        Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.EditArtifact(session, choice)).Failure;

    private static DesignSession ImportLoop(string path) =>
        FileSourceFetching.ReadTextFile(RepoFiles.ToPath(path))
            .Bind(file => LoopDesigning.ImportLoop(Catalog, file))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static DesignSession StartScratch() =>
        LoopDesigning.StartScratchDesign(Catalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}

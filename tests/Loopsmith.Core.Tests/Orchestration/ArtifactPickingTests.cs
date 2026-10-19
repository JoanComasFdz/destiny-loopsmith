using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// The artifact and its perks picked in the designer (docs/loop-format.md "Artifact"): the seven artifacts of Artifacts
/// 2.0 with three tiers of seven perks and seven slots (two take Tier 1, three Tiers 1–2, two Tiers 1–3), what each slot
/// can hold, and the build with another artifact and the perk in each slot, kept by slot in the loop file.
/// </summary>
public sealed class ArtifactPickingTests
{
    private static readonly RuleCatalog Catalog =
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    /// <summary>A build that names seven of Tablet of Ruin's perks and not its artifact.</summary>
    private const string SevenPerksBuild = """
        name: Test
        class: hunter
        subclass: arc
        artifactPerks: [defibrillating-blast, flashover, dielectric, to-shreds, horde-shuttle, unraveling-orbs, photonic-flare]
        """;

    private static readonly ItemHash TabletOfRuin = ItemHash.From(3821685388u);
    private static readonly ItemHash HuntersJournal = ItemHash.From(2180638678u);
    private static readonly ItemHash Dielectric = ItemHash.From(3585856470u);           // Tier 1, in the rules
    private static readonly ItemHash UnravelingOrbs = ItemHash.From(3585856464u);       // Tier 1, in the rules
    private static readonly ItemHash PhotonicFlare = ItemHash.From(3585856465u);        // Tier 1, in the rules
    private static readonly ItemHash TierOneUnknown = ItemHash.From(3585856469u);       // Tier 1, not in the rules
    private static readonly ItemHash Flashover = ItemHash.From(2596646298u);            // Tier 2
    private static readonly ItemHash HordeShuttle = ItemHash.From(2596646301u);         // Tier 2
    private static readonly ItemHash DefibrillatingBlast = ItemHash.From(1319563986u);  // Tier 3
    private static readonly ItemHash ToShreds = ItemHash.From(1319563985u);             // Tier 3
    private static readonly ItemHash TierThreeUnknown = ItemHash.From(1319563991u);     // Tier 3
    private static readonly Optional<ItemHash> Empty = Optional.None<ItemHash>();

    [Fact]
    public void The_seven_artifacts_each_have_three_tiers_of_seven_perks_seven_slots_and_their_flavour_text()
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
            Assert.StartsWith("A relic from a past journey", artifact.Flavor.UnwrapOr(""), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void A_loop_that_doesnt_name_its_artifact_shows_the_one_its_perks_are_from_each_perk_in_the_slot_that_takes_the_fewest_tiers()
    {
        var session = StartSevenPerkLoop();

        var choice = LoopDesigning.ReadArtifactChoice(session.Build);

        Assert.Equal(Optional.Some(TabletOfRuin), choice.Artifact);
        Assert.Equal<Optional<ItemHash>>(
            [Optional.Some(Dielectric), Optional.Some(UnravelingOrbs), Optional.Some(Flashover), Optional.Some(HordeShuttle), Optional.Some(PhotonicFlare), Optional.Some(DefibrillatingBlast), Optional.Some(ToShreds)],
            choice.Slots);
        var artifact = Assert.IsType<Optional<BuildArtifact>.Some>(LoopDesigning.FindArtifact(session.Build)).Value;   // other artifacts share some of the seven
        Assert.Equal("Tablet of Ruin", artifact.Item.Name);
        Assert.Equal(
            [DefibrillatingBlast, Flashover, Dielectric, ToShreds, HordeShuttle, UnravelingOrbs, PhotonicFlare],
            artifact.Perks.Select(perk => perk.Hash));                                               // in the build's order
        Assert.All(artifact.Perks, perk => Assert.True(perk.Element.IsSome()));
    }

    [Fact]
    public void A_slot_lists_the_perks_of_the_tiers_it_takes_tier_1_first()
    {
        var tablet = FindTablet(StartSevenPerkLoop());

        Assert.Equal(7, LoopDesigning.ListArtifactSlotOptions(tablet, 0).Length);
        Assert.Equal(14, LoopDesigning.ListArtifactSlotOptions(tablet, 2).Length);
        Assert.Equal(21, LoopDesigning.ListArtifactSlotOptions(tablet, 6).Length);
        Assert.Equal(tablet.Tiers[0].Perks, LoopDesigning.ListArtifactSlotOptions(tablet, 6).Take(7));
        Assert.DoesNotContain(Flashover, LoopDesigning.ListArtifactSlotOptions(tablet, 1));
        Assert.Empty(LoopDesigning.ListArtifactSlotOptions(tablet, 7));
    }

    [Fact]
    public void Each_perk_is_read_back_into_the_slot_it_was_put_in_and_the_loop_replays_without_the_one_taken_out()
    {
        var session = StartSevenPerkLoop();
        var flashover = ElementId.From("flashover");
        Assert.True(LoopDesigning.IsEquipped(session.Build, flashover));
        ImmutableArray<Optional<ItemHash>> slots =
            [Optional.Some(Dielectric), Empty, Optional.Some(HordeShuttle), Optional.Some(TierOneUnknown), Empty, Optional.Some(DefibrillatingBlast), Optional.Some(UnravelingOrbs)];

        var edited = Edit(session, new ArtifactChoice(Optional.Some(TabletOfRuin), slots));
        var reopened = LoopDesigning.ImportLoop(Catalog, new SourceText("a.loop.yaml", LoopDesigning.ExportLoop(edited))).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.False(LoopDesigning.IsEquipped(edited.Build, flashover));
        Assert.Contains($"artifact: {{hash: 3821685388, slots: [{Dielectric.Value}, \"?\", {HordeShuttle.Value}, {TierOneUnknown.Value}, \"?\", {DefibrillatingBlast.Value}, {UnravelingOrbs.Value}]}}", edited.Design.Build.Text, StringComparison.Ordinal);
        Assert.Equal(["dielectric", "horde-shuttle", "defibrillating-blast", "unraveling-orbs"], reopened.Build.Build.ArtifactPerks.Select(id => id.Value));   // in slot order
        Assert.Contains($"artifactPerks: [{TierOneUnknown.Value}]", edited.Design.Build.Text, StringComparison.Ordinal);   // in leftOut: the rules don't know it
        Assert.Equal(slots, LoopDesigning.ReadArtifactChoice(reopened.Build).Slots);
        Assert.Equal(session.Design.Steps, edited.Design.Steps);
    }

    [Fact]
    public void Another_artifact_with_empty_slots_and_no_artifact_at_all_can_be_picked()
    {
        var session = StartSevenPerkLoop();

        var journal = Edit(session, new ArtifactChoice(Optional.Some(HuntersJournal), [.. Enumerable.Repeat(Empty, 7)]));
        var none = Edit(journal, new ArtifactChoice(Optional.None<ItemHash>(), []));

        Assert.Equal(Optional.Some(HuntersJournal), LoopDesigning.ReadArtifactChoice(journal.Build).Artifact);
        Assert.All(LoopDesigning.ReadArtifactChoice(journal.Build).Slots, slot => Assert.False(slot.IsSome()));
        Assert.Empty(journal.Build.Build.ArtifactPerks);
        Assert.Equal("Hunter's Journal", Assert.IsType<Optional<BuildArtifact>.Some>(LoopDesigning.FindArtifact(journal.Build)).Value.Item.Name);
        Assert.False(LoopDesigning.FindArtifact(none.Build).IsSome());
        Assert.DoesNotContain("artifact:", none.Design.Build.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("artifactPerks:", none.Design.Build.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_perk_in_a_slot_that_doesnt_take_its_tier_or_that_isnt_the_artifacts_is_refused()
    {
        var session = StartSevenPerkLoop();

        Assert.Equal("Defibrillating Blast is a Tier 3 perk: slot 1 takes up to Tier 1.",
            Refusal(session, Slots(TabletOfRuin, (0, DefibrillatingBlast))));
        Assert.Equal("Flashover is a Tier 2 perk: slot 2 takes up to Tier 1.", Refusal(session, Slots(TabletOfRuin, (1, Flashover))));
        Assert.Equal("Flashover isn't one of Hunter's Journal's perks.", Refusal(session, Slots(HuntersJournal, (2, Flashover))));
        Assert.Equal("An artifact perk can be picked once.", Refusal(session, Slots(TabletOfRuin, (5, ToShreds), (6, ToShreds))));
        Assert.Equal("Artifact perks need an artifact.", Refusal(session, new ArtifactChoice(Optional.None<ItemHash>(), [Optional.Some(Flashover)])));
        Assert.Equal("Tablet of Ruin has 7 perk slots, not 2.", Refusal(session, new ArtifactChoice(Optional.Some(TabletOfRuin), [Empty, Empty])));
        Assert.False(Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.EditArtifact(session, Slots(TabletOfRuin, (6, TierThreeUnknown), (5, ToShreds), (4, PhotonicFlare)))).Value.Build.Issues.Any(issue => issue.Severity == Severity.Blocking));
    }

    [Fact]
    public void Placing_perks_leaves_out_one_no_free_slot_takes()
    {
        var tablet = FindTablet(StartSevenPerkLoop());

        var placed = ArtifactEditing.PlacePerks(tablet, [DefibrillatingBlast, ToShreds, TierThreeUnknown, Dielectric]);

        Assert.Equal<Optional<ItemHash>>([Optional.Some(Dielectric), Empty, Empty, Empty, Empty, Optional.Some(DefibrillatingBlast), Optional.Some(ToShreds)], placed);
    }

    [Fact]
    public void An_artifact_shows_the_last_slots_picked_for_it_else_the_builds_else_empty_ones()
    {
        var build = StartSevenPerkLoop().Build;
        ImmutableArray<Optional<ItemHash>> journalSlots = [Optional.Some(ItemHash.From(3116632771u)), Empty, Empty, Empty, Empty, Empty, Empty];
        var remembered = ImmutableDictionary<ItemHash, ImmutableArray<Optional<ItemHash>>>.Empty.Add(HuntersJournal, journalSlots);

        Assert.Equal(LoopDesigning.ReadArtifactChoice(build).Slots, LoopDesigning.RecallArtifactSlots(build, remembered, TabletOfRuin));
        Assert.Equal(journalSlots, LoopDesigning.RecallArtifactSlots(build, remembered, HuntersJournal));
        Assert.Equal(7, LoopDesigning.RecallArtifactSlots(build, remembered, ItemHash.From(1014202826u)).Length);
        Assert.All(LoopDesigning.RecallArtifactSlots(build, remembered, ItemHash.From(1014202826u)), slot => Assert.False(slot.IsSome()));
    }

    private static ArtifactChoice Slots(ItemHash artifact, params (int Slot, ItemHash Perk)[] picks) =>
        new(Optional.Some(artifact), [.. Enumerable.Range(0, 7).Select(at => picks.Where(pick => pick.Slot == at).Select(pick => Optional.Some(pick.Perk)).FirstOrDefault(Empty))]);

    private static ManifestKind.Artifact FindTablet(DesignSession session) =>
        Assert.IsType<Optional<ManifestKind.Artifact>.Some>(LoopDesigning.FindArtifactTiers(session.Build, TabletOfRuin)).Value;

    private static DesignSession Edit(DesignSession session, ArtifactChoice choice) =>
        LoopDesigning.EditArtifact(session, choice).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static string Refusal(DesignSession session, ArtifactChoice choice) =>
        Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.EditArtifact(session, choice)).Failure;

    /// <summary>The seven-perk build with one step (a new pack), so an edit has steps to keep.</summary>
    private static DesignSession StartSevenPerkLoop()
    {
        var started = LoopDesigning.StartDesign(Catalog, new SourceText("build.yaml", SevenPerksBuild), "Test")
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        return LoopDesigning.AppendStep(started, new PlayerAction.Declare(new StateDeclaration.NewPack()), Optional.None<string>());
    }

    private static DesignSession StartScratch() =>
        LoopDesigning.StartScratchDesign(Catalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}

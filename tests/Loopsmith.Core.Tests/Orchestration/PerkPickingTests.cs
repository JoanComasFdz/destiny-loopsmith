using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// Picking a weapon's perks in the designer, column by column, among what the manifest excerpt says it rolls with: the
/// pick goes into the embedded build's <c>roll</c>, the rules equip what they know of it, the steps replay.
/// </summary>
public sealed class PerkPickingTests
{
    private const uint Slice = 923806249;
    private const uint EnhancedSlice = 3422796781;
    private const uint AttritionOrbs = 243981275;

    private static readonly ItemHash FestivalFlight = ItemHash.From(4019651319u);
    private static readonly ItemHash Thunderlord = ItemHash.From(3325463374u);
    private static readonly ItemHash CrisisInverted = ItemHash.From(2888266564u);

    /// <summary>A hand-written build: Festival Flight naming Slice and Attrition Orbs, with comments of its own.</summary>
    private const string HandWrittenBuild = """
        name: Test
        class: hunter
        subclass: arc
        classAbility: gamblers-dodge
        weapons:
          # Slice and Attrition Orbs, named by the build.
          - { hash: 4019651319, perks: [slice, attrition-orbs] } # Festival Flight
        """;

    private static readonly RuleCatalog Catalog =
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    [Fact]
    public void A_pick_replaces_the_perk_the_build_named_in_that_column_and_keeps_the_rest_of_the_file()
    {
        var session = StartHandWrittenLoop();

        var picked = Pick(session, 0, 0, Optional.Some(ItemHash.From(EnhancedSlice)));
        var flight = picked.Build.Build.Weapons[0];

        Assert.Equal(["attrition-orbs"], flight.Perks.Select(id => id.Value));
        Assert.Equal([Optional.Some(ItemHash.From(EnhancedSlice))], flight.Roll);
        Assert.Contains(picked.Build.Equipped, equipped => equipped.Element.Id.Value == "slice-enhanced");  // the enhanced trait is its own element
        Assert.DoesNotContain(picked.Build.Equipped, equipped => equipped.Element.Id.Value == "slice");
        Assert.DoesNotContain(picked.Build.Issues, issue => issue.Message.Contains("not in the rule catalog yet", StringComparison.Ordinal));
        Assert.Contains("# Slice and Attrition Orbs, named by the build.", picked.Design.Build.Text, StringComparison.Ordinal);
        Assert.Equal(
            session.Design.Build.Text.Split('\n').Where(line => !line.Contains("Festival Flight", StringComparison.Ordinal)),
            picked.Design.Build.Text.Split('\n').Where(line => !line.Contains("Festival Flight", StringComparison.Ordinal)));
        Assert.Equal(session.Design.Steps.Length, picked.Resolutions.Length);                                // the steps replay with the new build
    }

    [Fact]
    public void A_perk_the_rules_know_is_equipped_by_its_hash_and_clearing_it_writes_no_roll()
    {
        var session = Pick(StartWithThreeWeapons(), 0, 0, Optional.Some(ItemHash.From(Slice)));

        var picked = Pick(session, 0, 1, Optional.Some(ItemHash.From(AttritionOrbs)));
        var sliceCleared = Pick(picked, 0, 0, Optional.None<ItemHash>());
        var cleared = Pick(sliceCleared, 0, 1, Optional.None<ItemHash>());

        Assert.Equal([Optional.Some(ItemHash.From(Slice)), Optional.Some(ItemHash.From(AttritionOrbs))], picked.Build.Build.Weapons[0].Roll);
        Assert.Contains("roll: [923806249, 243981275]", picked.Design.Build.Text, StringComparison.Ordinal);
        Assert.Contains(picked.Build.Equipped, equipped => equipped.Element.Id.Value == "attrition-orbs");
        Assert.DoesNotContain(session.Build.Equipped, equipped => equipped.Element.Id.Value == "attrition-orbs");
        Assert.Contains("roll: [\"?\", 243981275]", sliceCleared.Design.Build.Text, StringComparison.Ordinal);
        Assert.Empty(cleared.Build.Build.Weapons[0].Roll);
        Assert.DoesNotContain("roll:", cleared.Design.Build.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_picked_perk_is_equipped_and_held_by_its_weapon()
    {
        var session = StartWithThreeWeapons();

        var picked = Pick(session, 0, 0, Optional.Some(ItemHash.From(Slice)));

        var flight = picked.Build.Build.Weapons[0];
        Assert.Equal(("Festival Flight", Optional.Some(ItemHash.From(Slice))), (flight.Name, flight.Roll[0]));
        Assert.Contains(picked.Build.Equipped, equipped => equipped.Element.Id.Value == "slice");
        Assert.Empty(LoopDesigning.ListWeaponsHolding(session.Build, ElementId.From("slice")));         // a weapon picked has no roll
        Assert.Equal([0], LoopDesigning.ListWeaponsHolding(picked.Build, ElementId.From("slice")));     // Festival Flight
        Assert.Empty(LoopDesigning.ListWeaponsHolding(picked.Build, ElementId.From("attrition-orbs"))); // not picked
    }

    [Fact]
    public void Clearing_a_named_perk_leaves_its_column_unknown()
    {
        var session = StartHandWrittenLoop();

        var cleared = Pick(session, 0, 0, Optional.None<ItemHash>());

        Assert.Equal(["attrition-orbs"], cleared.Build.Build.Weapons[0].Perks.Select(id => id.Value));
        Assert.Empty(cleared.Build.Build.Weapons[0].Roll);
        Assert.DoesNotContain(cleared.Build.Equipped, equipped => equipped.Element.Id.Value == "slice");
    }

    [Fact]
    public void A_column_that_does_not_roll_a_perk_it_does_not_have_and_a_weapon_without_columns_cannot_be_picked()
    {
        var session = StartWithThreeWeapons();

        var fixedColumn = PickError(session, 1, 0, Optional.Some(ItemHash.From(1419069769u)));
        var wrongColumn = PickError(session, 0, 0, Optional.Some(ItemHash.From(AttritionOrbs)));
        var noColumn = PickError(session, 0, 2, Optional.None<ItemHash>());
        var noWeapon = PickError(session, 3, 0, Optional.None<ItemHash>());

        Assert.Equal("Thunderlord's column 1 doesn't roll: it is always Lightning Rounds.", fixedColumn);
        Assert.Equal("Attrition Orbs (Trait) isn't a perk of Festival Flight's column 1.", wrongColumn);
        Assert.Equal("Festival Flight has no perk column 3 in the manifest excerpt.", noColumn);
        Assert.Equal("The build has no weapon 4.", noWeapon);
    }

    [Fact]
    public void A_roll_pick_outside_its_column_is_a_warning()
    {
        var build = new WeaponLoadout(WeaponSlot.Kinetic, "Festival Flight", DamageType.Strand, Optional.None<AmmoType>(), Optional.None<string>(), [], Optional.Some(ItemHash.From(4019651319u)),
            [Optional.Some(ItemHash.From(AttritionOrbs)), Optional.None<ItemHash>(), Optional.Some(ItemHash.From(Slice))], false, Optional.None<ItemInstanceId>(), Optional.None<ItemHash>());
        var session = StartWithThreeWeapons();

        var validated = BuildValidation.ValidateBuild(session.Build.Build with { Weapons = [build] }, Catalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Equal(
            [
                "Festival Flight's roll: Attrition Orbs (Trait) isn't a perk of its column 1 in the manifest excerpt.",
                "Festival Flight's roll: Slice (Trait) isn't a perk of its column 3 in the manifest excerpt.",
            ],
            validated.Issues.Where(issue => issue.Severity == Severity.Warning && issue.Message.Contains("roll", StringComparison.Ordinal)).Select(issue => issue.Message));
        Assert.Contains(validated.Equipped, equipped => equipped.Element.Id.Value == "attrition-orbs");        // shown in column 1, so it counts
        Assert.DoesNotContain(validated.Equipped, equipped => equipped.Element.Id.Value == "slice");           // there is no column 3 to show it in
    }

    [Fact]
    public void A_column_holds_one_perk_and_the_build_check_equips_what_the_designer_shows()
    {
        var session = StartWithThreeWeapons();
        var flight = session.Build.Build.Weapons[0] with { Perks = [ElementId.From("slice"), ElementId.From("attrition-orbs")], Roll = [Optional.Some(ItemHash.From(EnhancedSlice))] };

        var validated = BuildValidation.ValidateBuild(session.Build.Build with { Weapons = [flight] }, Catalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        var slots = LoopDesigning.ListPerkSlots(validated, flight);

        Assert.Equal(
            [Optional.Some(ItemHash.From(EnhancedSlice)), Optional.Some(ItemHash.From(AttritionOrbs))],
            slots.OfType<WeaponPerkSlot.Rolling>().Select(slot => slot.Perk));
        Assert.DoesNotContain(validated.Equipped, equipped => equipped.Element.Id.Value == "slice");
        Assert.Contains(validated.Equipped, equipped => equipped.Element.Id.Value == "attrition-orbs");
        Assert.Contains(validated.Issues, issue => issue.Severity == Severity.Warning
            && issue.Message == "Festival Flight: 'Slice' gives way to the other perk of its column — a column holds one perk.");
    }

    [Fact]
    public void An_exotic_lists_its_intrinsic_its_fixed_columns_a_perk_in_no_column_and_its_catalyst()
    {
        var session = StartWithThreeWeapons();
        var thunderlord = session.Build.Build.Weapons[1] with { Perks = [ElementId.From("slice")] };

        var slots = LoopDesigning.ListPerkSlots(session.Build, thunderlord);

        Assert.Equal(
            [
                new WeaponPerkSlot.Intrinsic(ItemHash.From(4148158229u)),
                new WeaponPerkSlot.Fixed(0, ItemHash.From(1419069769u)),
                new WeaponPerkSlot.Fixed(1, ItemHash.From(2779035018u)),
                new WeaponPerkSlot.Named(ElementId.From("slice")),
                new WeaponPerkSlot.Catalyst(ItemHash.From(1550063955u), false),
            ],
            slots);
    }

    [Fact]
    public void Picking_twice_in_a_column_keeps_one_perk_there()
    {
        var once = Pick(StartWithThreeWeapons(), 0, 0, Optional.Some(ItemHash.From(Slice)));

        var twice = Pick(once, 0, 0, Optional.Some(ItemHash.From(EnhancedSlice)));

        Assert.Equal([Optional.Some(ItemHash.From(EnhancedSlice))], twice.Build.Build.Weapons[0].Roll);
        Assert.Contains(once.Build.Equipped, equipped => equipped.Element.Id.Value == "slice");
        Assert.DoesNotContain(twice.Build.Equipped, equipped => equipped.Element.Id.Value == "slice");
    }

    /// <summary>The hand-written build with two steps: the class ability (it arms Slice) and a Festival Flight kill.</summary>
    private static DesignSession StartHandWrittenLoop()
    {
        var started = Start(LoopDesigning.StartDesign(Catalog, new SourceText("build.yaml", HandWrittenBuild), "Test"));
        var dodge = LoopDesigning.AppendStep(started, new PlayerAction.UseClassAbility(), Optional.None<string>());
        return LoopDesigning.AppendStep(dodge, new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One), Optional.None<string>());
    }

    /// <summary>An Arc Hunter from scratch with Festival Flight, Thunderlord and Crisis Inverted picked in the designer, in that order, with no roll.</summary>
    private static DesignSession StartWithThreeWeapons()
    {
        var scratch = Start(LoopDesigning.StartScratchDesign(Catalog));
        var flight = Start(LoopDesigning.PickWeaponItem(scratch, new WeaponTarget.IntoEmpty(WeaponSlot.Kinetic), FestivalFlight));
        var thunderlord = Start(LoopDesigning.PickWeaponItem(flight, new WeaponTarget.IntoEmpty(WeaponSlot.Power), Thunderlord));
        return Start(LoopDesigning.PickWeaponItem(thunderlord, new WeaponTarget.IntoEmpty(WeaponSlot.Energy), CrisisInverted));
    }

    private static DesignSession Start(Result<DesignSession, string> started) =>
        started.Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static DesignSession Pick(DesignSession session, int weapon, int column, Optional<ItemHash> perk) =>
        LoopDesigning.PickWeaponPerk(session, weapon, column, perk).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static string PickError(DesignSession session, int weapon, int column, Optional<ItemHash> perk) =>
        Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.PickWeaponPerk(session, weapon, column, perk)).Failure;
}

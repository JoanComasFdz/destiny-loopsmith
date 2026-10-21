using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// What an item's details and the loadout need from the core: an exotic weapon's intrinsic and catalyst (switched on
/// in the designer), armor sets from the pieces the build wears, an element's explanation, and "this weapon" (<c>slot:power</c>).
/// </summary>
public sealed class ItemDetailsTests
{
    private const int Thunderlord = 1;   // StartWithFlightAndThunderlord: Festival Flight, Thunderlord

    private static readonly ItemHash FestivalFlight = ItemHash.From(4019651319u);
    private static readonly ItemHash ThunderlordItem = ItemHash.From(3325463374u);
    private static readonly ItemHash LuminopotentMask = ItemHash.From(593554567u);
    private static readonly ItemHash LuminopotentCuirass = ItemHash.From(2883433250u);
    private static readonly ItemHash LuminopotentStrides = ItemHash.From(1047792392u);
    private static readonly ItemHash LuminopotentCloak = ItemHash.From(2107326067u);

    private static readonly RuleCatalog Catalog =
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    [Fact]
    public void An_exotic_weapons_intrinsic_and_fixed_perks_are_equipped_by_hash_and_its_catalyst_only_when_on()
    {
        var session = StartWithFlightAndThunderlord();

        var on = Edit(LoopDesigning.SetCatalyst(session, Thunderlord, true));
        var off = Edit(LoopDesigning.SetCatalyst(on, Thunderlord, false));

        Assert.All(new[] { "reign-havoc", "lightning-rounds", "feeding-frenzy" }, id => Assert.Contains(session.Build.Equipped, equipped => equipped.Element.Id.Value == id));
        Assert.DoesNotContain(session.Build.Equipped, equipped => equipped.Element.Id.Value == "thunderlord-catalyst");
        Assert.Contains(on.Build.Equipped, equipped => equipped.Element.Id.Value == "thunderlord-catalyst");
        Assert.Contains("{hash: 3325463374, perks: [], catalyst: true} # Thunderlord", on.Design.Build.Text, StringComparison.Ordinal);
        Assert.True(on.Build.Build.Weapons[Thunderlord].Catalyst);
        Assert.DoesNotContain("catalyst:", off.Design.Build.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(off.Build.Equipped, equipped => equipped.Element.Id.Value == "thunderlord-catalyst");
    }

    [Fact]
    public void A_weapon_without_a_catalyst_cant_turn_one_on_and_a_build_file_that_says_so_gets_a_warning()
    {
        var session = StartWithFlightAndThunderlord();
        var flight = session.Build.Build.Weapons[0] with { Catalyst = true };

        var error = Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.SetCatalyst(session, 0, true));
        var validated = BuildValidation.ValidateBuild(session.Build.Build with { Weapons = [flight] }, Catalog)
            .Match(ok => ok.Value, failure => throw new InvalidOperationException(failure.Failure));

        Assert.Equal("Festival Flight has no catalyst in the manifest excerpt.", error.Failure);
        Assert.Contains(validated.Issues, issue => issue.Severity == Severity.Warning
            && issue.Message == "Festival Flight has no catalyst in the manifest excerpt, so catalyst: true does nothing.");
    }

    [Fact]
    public void A_sets_armor_pieces_give_their_set_perks_as_set_bonuses()
    {
        var session = WearLuminopotent(Start(LoopDesigning.StartScratchDesign(Catalog)));

        var sets = LoopDesigning.ListArmorSets(session.Build);

        Assert.Equal(["luminopotent-2pc", "luminopotent-4pc"], session.Build.Build.ArmorSetBonuses.Select(id => id.Value));
        var luminopotent = Assert.Single(sets);
        Assert.Equal(("Luminopotent", Optional.Some(4)), (luminopotent.Set.Name, luminopotent.Pieces.Map(pieces => pieces.Value)));
        Assert.Equal(
            [(2, true, Optional.Some(ElementId.From("luminopotent-2pc"))), (4, true, Optional.Some(ElementId.From("luminopotent-4pc")))],
            luminopotent.Perks.Select(perk => (perk.Perk.Pieces.Value, perk.Active, perk.Element)));
        Assert.Empty(LoopDesigning.ListLooseSetBonuses(session.Build));
    }

    [Fact]
    public void A_build_file_that_names_one_set_bonus_shows_its_set_with_the_other_perk_inactive()
    {
        const string text = """
            name: Test
            class: hunter
            subclass: arc
            armorSetBonuses: [luminopotent-2pc]
            """;
        var twoPiece = Start(LoopDesigning.StartDesign(Catalog, new SourceText("build.yaml", text), "Test")).Build;

        var luminopotent = Assert.Single(LoopDesigning.ListArmorSets(twoPiece));

        Assert.False(luminopotent.Pieces.IsSome());
        Assert.Equal([(2, true), (4, false)], luminopotent.Perks.Select(perk => (perk.Perk.Pieces.Value, perk.Active)));
    }

    [Fact]
    public void An_elements_explanation_says_each_trigger_what_it_sets_off_and_why()
    {
        var session = StartWithFlightAndThunderlord();

        var lines = LoopDesigning.ExplainElement(session.Build, Catalog.Elements[ElementId.From("reign-havoc")])
            .Select(line => (line.Indent, Text: string.Concat(line.Spans.Select(span => span.Text))))
            .ToArray();

        Assert.Equal((0, "Kill target with heavy weapon"), lines[0]);
        Assert.Equal((1, "+1 Bolt Charge"), lines[1]);
        Assert.Equal(2, lines[2].Indent);
        Assert.Contains(
            LoopDesigning.ExplainElement(session.Build, Catalog.Elements[ElementId.From("thunderlord-catalyst")]),
            line => string.Concat(line.Spans.Select(span => span.Text)).Contains("refills", StringComparison.Ordinal));   // a strike refills it
        Assert.True(LoopDesigning.IsEquipped(session.Build, ElementId.From("reign-havoc")));
        Assert.False(LoopDesigning.IsEquipped(session.Build, ElementId.From("thunderlord-catalyst")));        // off
        var source = Catalog.Elements[ElementId.From("reign-havoc")].Source;
        Assert.Equal(("Destiny Data Compendium", "Exotic Weapons tab, row 144 · snapshot of 2026-10-09"), (source.DescribeProvenanceTitle(), source.DescribeProvenanceCite()));
    }

    [Fact]
    public void Slot_power_is_this_weapon_a_kill_with_another_arc_weapon_doesnt_fire_it()
    {
        var session = StartWithFlightAndThunderlord();
        PlayerAction Kill(WeaponSlot slot) => new PlayerAction.FireWeapon(slot, HitOutcome.Kill, TargetCount.One);

        var power = LoopDesigning.AppendStep(session, Kill(WeaponSlot.Power), Optional.None<string>()).Resolutions[^1];
        var energy = LoopDesigning.AppendStep(session, Kill(WeaponSlot.Energy), Optional.None<string>()).Resolutions[^1];

        Assert.Contains(power.Fired, fired => fired.Source.Value == "reign-havoc");
        Assert.DoesNotContain(energy.Fired, fired => fired.Source.Value is "reign-havoc" or "lightning-rounds");
    }

    /// <summary>An Arc Hunter from scratch with Festival Flight (Kinetic) and Thunderlord (Power, its catalyst off) picked in the designer.</summary>
    private static DesignSession StartWithFlightAndThunderlord()
    {
        var scratch = Start(LoopDesigning.StartScratchDesign(Catalog));
        var flight = Edit(LoopDesigning.PickWeaponItem(scratch, new WeaponTarget.IntoEmpty(WeaponSlot.Kinetic), FestivalFlight));
        return Edit(LoopDesigning.PickWeaponItem(flight, new WeaponTarget.IntoEmpty(WeaponSlot.Power), ThunderlordItem));
    }

    /// <summary>The design wearing the four Luminopotent pieces (helmet, chest, legs, class item), picked in the designer.</summary>
    private static DesignSession WearLuminopotent(DesignSession session)
    {
        var mask = Edit(LoopDesigning.PickArmorItem(session, ArmorSlot.Helmet, LuminopotentMask));
        var cuirass = Edit(LoopDesigning.PickArmorItem(mask, ArmorSlot.Chest, LuminopotentCuirass));
        var strides = Edit(LoopDesigning.PickArmorItem(cuirass, ArmorSlot.Legs, LuminopotentStrides));
        return Edit(LoopDesigning.PickArmorItem(strides, ArmorSlot.ClassItem, LuminopotentCloak));
    }

    private static DesignSession Start(Result<DesignSession, string> started) =>
        started.Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static DesignSession Edit(Result<DesignSession, string> edited) =>
        edited.Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}

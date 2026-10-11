using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// What an item's details and the loadout need from the core: an exotic weapon's intrinsic and catalyst (switched on
/// in the designer), armor sets from a DIM loadout's pieces, an element's explanation, and "this weapon" (<c>slot:power</c>).
/// </summary>
public sealed class ItemDetailsTests
{
    private const int Thunderlord = 1;   // the owner's DIM loadout: Festival Flight, Thunderlord, Crisis Inverted

    private static readonly RuleCatalog Catalog =
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    [Fact]
    public void An_exotic_weapons_intrinsic_and_fixed_perks_are_equipped_by_hash_and_its_catalyst_only_when_on()
    {
        var session = StartOwnersDimDesign();

        var on = Edit(LoopDesigning.SetCatalyst(session, Thunderlord, true));
        var off = Edit(LoopDesigning.SetCatalyst(on, Thunderlord, false));

        Assert.All(new[] { "reign-havoc", "lightning-rounds", "feeding-frenzy" }, id => Assert.Contains(session.Build.Equipped, equipped => equipped.Element.Id.Value == id));
        Assert.DoesNotContain(session.Build.Equipped, equipped => equipped.Element.Id.Value == "thunderlord-catalyst");
        Assert.Contains(on.Build.Equipped, equipped => equipped.Element.Id.Value == "thunderlord-catalyst");
        Assert.Contains("name: Thunderlord", on.Design.Build.Text, StringComparison.Ordinal);
        Assert.Contains("hash: 3325463374, catalyst: true, instance: \"6917529810172542011\"}", on.Design.Build.Text, StringComparison.Ordinal);   // the share's copy
        Assert.True(on.Build.Build.Weapons[Thunderlord].Catalyst);
        Assert.DoesNotContain("catalyst:", off.Design.Build.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(off.Build.Equipped, equipped => equipped.Element.Id.Value == "thunderlord-catalyst");
    }

    [Fact]
    public void A_weapon_without_a_catalyst_cant_turn_one_on_and_a_build_file_that_says_so_gets_a_warning()
    {
        var session = StartOwnersDimDesign();
        var flight = session.Build.Build.Weapons[0] with { Catalyst = true };

        var error = Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.SetCatalyst(session, 0, true));
        var validated = BuildValidation.ValidateBuild(session.Build.Build with { Weapons = [flight] }, Catalog)
            .Match(ok => ok.Value, failure => throw new InvalidOperationException(failure.Failure));

        Assert.Equal("Festival Flight has no catalyst in the manifest excerpt.", error.Failure);
        Assert.Contains(validated.Issues, issue => issue.Severity == Severity.Warning
            && issue.Message == "Festival Flight has no catalyst in the manifest excerpt, so catalyst: true does nothing.");
    }

    [Fact]
    public void A_dim_loadouts_armor_pieces_give_their_set_perks_as_set_bonuses()
    {
        var session = StartOwnersDimDesign();

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
        var build = RepoFiles.LoadBuild("builds/skip-grenade-hunter/build.yaml");
        var twoPiece = BuildValidation.ValidateBuild(build.Build with { ArmorSetBonuses = [ElementId.From("luminopotent-2pc")] }, build.Catalog)
            .Match(ok => ok.Value, failure => throw new InvalidOperationException(failure.Failure));

        var luminopotent = Assert.Single(LoopDesigning.ListArmorSets(twoPiece));

        Assert.False(luminopotent.Pieces.IsSome());
        Assert.Equal([(2, true), (4, false)], luminopotent.Perks.Select(perk => (perk.Perk.Pieces.Value, perk.Active)));
    }

    [Fact]
    public void An_elements_explanation_says_each_trigger_what_it_sets_off_and_why()
    {
        var session = StartOwnersDimDesign();

        var lines = LoopDesigning.ExplainElement(session.Build, Catalog.Elements[ElementId.From("reign-havoc")])
            .Select(line => (line.Indent, Text: string.Concat(line.Spans.Select(span => span.Text))))
            .ToArray();

        Assert.Equal((0, "Kill target with heavy weapon"), lines[0]);
        Assert.Equal((1, "+1 Bolt Charge"), lines[1]);
        Assert.Equal(2, lines[2].Indent);
        Assert.Empty(LoopDesigning.ExplainElement(session.Build, Catalog.Elements[ElementId.From("thunderlord-catalyst")]));   // no rule written
        Assert.True(LoopDesigning.IsEquipped(session.Build, ElementId.From("reign-havoc")));
        Assert.False(LoopDesigning.IsEquipped(session.Build, ElementId.From("thunderlord-catalyst")));        // off
        var source = Catalog.Elements[ElementId.From("reign-havoc")].Source;
        Assert.Equal(("Destiny Data Compendium", "Exotic Weapons tab, row 144 · snapshot of 2026-10-09"), (source.DescribeProvenanceTitle(), source.DescribeProvenanceCite()));
    }

    [Fact]
    public void Slot_power_is_this_weapon_a_kill_with_another_arc_weapon_doesnt_fire_it()
    {
        var session = StartOwnersDimDesign();
        PlayerAction Kill(WeaponSlot slot) => new PlayerAction.FireWeapon(slot, HitOutcome.Kill, TargetCount.One);

        var power = LoopDesigning.AppendStep(session, Kill(WeaponSlot.Power), Optional.None<string>()).Resolutions[^1];
        var energy = LoopDesigning.AppendStep(session, Kill(WeaponSlot.Energy), Optional.None<string>()).Resolutions[^1];

        Assert.Contains(power.Fired, fired => fired.Source.Value == "reign-havoc");
        Assert.DoesNotContain(energy.Fired, fired => fired.Source.Value is "reign-havoc" or "lightning-rounds");
    }

    private static DesignSession StartOwnersDimDesign()
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        return LoopDesigning.StartSavedDimDesign(Catalog, new SourceText(path, File.ReadAllText(RepoFiles.ToPath(path))))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
    }

    private static DesignSession Edit(Result<DesignSession, string> edited) =>
        edited.Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}

using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// The weapon perks' enhanced traits, in the real rules: every authored perk whose enhanced trait the manifest excerpt
/// has (the extractor adds every enhanced plug of a trait the rules author) has that trait's element, holding each of
/// its plugs, with the Compendium's ↑ figures — so a perk authored without its <c>enhanced:</c> block fails here.
/// </summary>
public sealed class EnhancedPerksCatalogTests
{
    private static readonly Result<ImmutableArray<SourceText>, string> RuleFiles = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"));   // impure

    private static readonly RuleCatalog Catalog =
        RuleFiles.Bind(CatalogLoading.ParseCatalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static readonly ImmutableArray<string> EnhancedTypes = ["Enhanced Trait", "Enhanced Origin Trait"];

    [Fact]
    public void Every_enhanced_plug_of_an_authored_perk_is_its_enhanced_traits()
    {
        var authoredNames = Catalog.Elements.Values
            .Where(element => element.Kind == ElementKind.WeaponPerk && !Catalog.EnhancedPerks.ContainsValue(element.Id))
            .Select(element => element.Name)
            .ToImmutableHashSet();
        var plugs = Catalog.Manifest.Items.Values
            .Where(item => item.Type.Match(type => EnhancedTypes.Contains(type.Value), _ => false) && authoredNames.Contains(item.Name))
            .ToImmutableArray();

        Assert.NotEmpty(plugs);
        Assert.All(plugs, plug => Assert.Equal(
            Optional.Some(Catalog.EnhancedPerks.Values.Single(id => Catalog.Elements[id].Name == $"{plug.Name} (enhanced)")),
            Catalog.FindElementByHash(plug.Hash).Map(element => element.Id)));
    }

    [Theory]
    [InlineData("slice", 3422796781u)]
    [InlineData("attrition-orbs", 984655331u)]
    [InlineData("feeding-frenzy", 1171147302u)]
    [InlineData("demolitionist", 1906147653u)]
    public void Each_authored_weapon_perk_has_its_enhanced_trait(string perk, uint enhancedHash)
    {
        var enhanced = Catalog.EnhancedPerks[ElementId.From(perk)];

        Assert.Equal(Optional.Some(enhanced), Catalog.FindElementByHash(ItemHash.From(enhancedHash)).Map(element => element.Id));
        Assert.Equal(Catalog.Elements[ElementId.From(perk)].Inert.IsSome(), Catalog.Elements[enhanced].Inert.IsSome());
    }

    [Theory]
    [InlineData(3422796781u, true)]    // the enhanced Slice
    [InlineData(1171147302u, true)]    // the enhanced Feeding Frenzy
    [InlineData(923806249u, false)]    // Slice
    [InlineData(2779035018u, false)]   // Feeding Frenzy
    public void An_enhanced_traits_plug_is_drawn_enhanced_and_its_base_perks_is_not(uint hash, bool enhanced)
    {
        Assert.Equal(enhanced, LoopDesigning.IsEnhancedPerk(Catalog, ItemHash.From(hash)));
    }

    [Fact]
    public void The_enhanced_traits_element_is_drawn_enhanced_and_any_plug_Bungie_types_one_too()
    {
        var unauthored = new ManifestItem(
            ItemHash.From(556), "Rampage", new ManifestKind.WeaponPerk(), Optional.Some("Enhanced Trait"), Optional.None<ItemTier>(), Optional.None<string>(), Optional.None<string>(), Optional.None<string>());
        var joined = Catalog with { Manifest = Catalog.Manifest with { Items = Catalog.Manifest.Items.Add(unauthored.Hash, unauthored) } };

        Assert.True(LoopDesigning.IsEnhancedPerk(Catalog, ElementId.From("slice-enhanced")));
        Assert.False(LoopDesigning.IsEnhancedPerk(Catalog, ElementId.From("slice")));
        Assert.True(LoopDesigning.IsEnhancedPerk(joined, unauthored.Hash));   // no rules know it: Bungie's type says it
        Assert.False(LoopDesigning.IsEnhancedPerk(Catalog, ItemHash.From(556)));   // a hash nobody knows
    }

    [Fact]
    public void The_enhanced_slice_arms_for_9_seconds_where_slice_arms_for_8()
    {
        Assert.Equal([Optional.Some(Seconds.From(8m))], ListBuffDurations("slice"));
        Assert.Equal([Optional.Some(Seconds.From(9m))], ListBuffDurations("slice-enhanced"));
    }

    private static IEnumerable<Optional<Seconds>> ListBuffDurations(string id) =>
        Catalog.Elements[ElementId.From(id)].Rules
            .SelectMany(rule => rule.Then)
            .OfType<Outcome.ApplyBuff>()
            .Select(buff => buff.Duration.ReadFixedTime())
            .Distinct();

    [Fact]
    public void The_enhanced_demolitionist_gives_the_compendiums_up_figures_by_archetype()
    {
        var amounts = new[] { "demolitionist", "demolitionist-enhanced" }
            .Select(id => Catalog.Elements[ElementId.From(id)].Rules
                .SelectMany(rule => rule.Then)
                .OfType<Outcome.GrantEnergy>()
                .Select(grant => Catalog.Glossary.DescribeOutcome(grant, Optional.None<DamageType>()))
                .Single())
            .ToArray();

        Assert.Equal(
            ["+10% grenade energy (20% on Fusion Rifles, Glaives, Shotguns and Sniper Rifles)", "+11% grenade energy (22% on Fusion Rifles, Glaives, Shotguns and Sniper Rifles)"],
            amounts);
    }
}

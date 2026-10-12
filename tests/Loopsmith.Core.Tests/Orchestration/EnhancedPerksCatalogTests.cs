using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// The weapon perks' enhanced traits, in the real rules: every authored perk whose enhanced trait the manifest excerpt
/// has (the extractor adds every enhanced plug of a trait the rules name) has that trait's element, holding each of
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
    public void Each_authored_weapon_perk_has_its_enhanced_trait(string perk, uint enhancedHash)
    {
        var enhanced = Catalog.EnhancedPerks[ElementId.From(perk)];

        Assert.Equal(Optional.Some(enhanced), Catalog.FindElementByHash(ItemHash.From(enhancedHash)).Map(element => element.Id));
        Assert.Equal(Catalog.Elements[ElementId.From(perk)].Inert.IsSome(), Catalog.Elements[enhanced].Inert.IsSome());
    }

    [Fact]
    public void The_enhanced_slice_arms_for_9_seconds_where_slice_arms_for_8()
    {
        var durations = (string id) => Catalog.Elements[ElementId.From(id)].Rules
            .SelectMany(rule => rule.Then)
            .OfType<Outcome.ApplyBuff>()
            .Select(buff => buff.Duration)
            .Distinct();

        Assert.Equal([Optional.Some(Seconds.From(8m))], durations("slice"));
        Assert.Equal([Optional.Some(Seconds.From(9m))], durations("slice-enhanced"));
    }
}

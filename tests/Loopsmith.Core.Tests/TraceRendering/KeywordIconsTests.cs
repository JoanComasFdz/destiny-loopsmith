using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Core.Tests.TraceRendering;

/// <summary>
/// The game's own icons next to a name (docs/rule-format.md, "Glossary"): a keyword shows its Bungie trait's (Jolted,
/// Woven Mail), else its element's manifest icon (Slice's perk); a step shows its weapon's, its ability's or its
/// keyword's; an event its weapon's. All from the real rules and their manifest excerpt.
/// </summary>
public sealed class KeywordIconsTests
{
    private static readonly Result<ImmutableArray<SourceText>, string> RuleFiles = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"));   // impure

    private static readonly RuleCatalog Catalog =
        RuleFiles.Bind(CatalogLoading.ParseCatalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static readonly KeywordRef Jolt = new KeywordRef.Status(StatusId.From("jolt"));

    [Fact]
    public void Every_trait_the_glossary_names_is_in_the_excerpt_with_its_icon()
    {
        var glossary = Catalog.Glossary;
        var traits = glossary.Statuses.Values.Select(status => status.Trait)
            .Concat(glossary.Pickups.Values.Select(pickup => pickup.Trait))
            .Concat(glossary.Summons.Values.Select(summon => summon.Trait))
            .SelectMany(trait => trait.Match(found => new[] { found.Value }, _ => []))
            .ToImmutableArray();

        Assert.True(traits.Length >= 9, $"{traits.Length} traits");
        Assert.All(traits, trait => Assert.True(Catalog.Manifest.KeywordTraits.ContainsKey(trait), $"trait {trait.Value} isn't in rules/manifest.yaml: rerun tools/manifest/"));
        Assert.Equal("Jolted", Catalog.Manifest.KeywordTraits[ItemHash.From(3221118171u)].Name);
    }

    [Fact]
    public void A_keyword_shows_its_traits_icon_else_its_elements_and_none_without_either()
    {
        var slice = new KeywordRef.Status(StatusId.From("slice"));
        var orb = new KeywordRef.Pickup(PickupId.From("orb-of-power"));

        Assert.Equal(Optional.Some(Catalog.Manifest.KeywordTraits[ItemHash.From(3221118171u)].Icon), IconFinding.FindKeywordIcon(Catalog, Jolt));
        Assert.Equal(Catalog.FindKeywordElement(slice).Bind(Catalog.FindElementItem).Bind(item => item.Icon), IconFinding.FindKeywordIcon(Catalog, slice));
        Assert.True(IconFinding.FindKeywordIcon(Catalog, slice).IsSome());
        Assert.False(IconFinding.FindKeywordIcon(Catalog, orb).IsSome());
        Assert.Equal(IconFinding.FindKeywordIcon(Catalog, Jolt), IconFinding.FindElementIcon(Catalog, ElementId.From("jolt")));   // a keyword's element: the keyword's icon
    }

    [Fact]
    public void A_steps_text_carries_each_keywords_icon_and_its_events_their_weapons()
    {
        var session = Play(new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One));
        var fired = LoopDesigning.ShapeStepOutcomes(session, session.Resolutions[0], new TraceOptions(false, false, false));
        var keywords = fired.Events.SelectMany(shaped => shaped.Sources.SelectMany(source => source.Outcomes.SelectMany(line => line.Parts)))
            .SelectMany(part => part.Keyword.Match(keyword => new[] { (part.Text, keyword.Value.Icon) }, _ => []))
            .ToImmutableArray();

        Assert.NotEmpty(keywords);
        Assert.All(keywords.Where(keyword => keyword.Text is "Bolt Charge" or "Jolted" or "Jolt"), keyword => Assert.True(keyword.Icon.IsSome(), keyword.Text));
        Assert.Equal(Optional.Some(WeaponSlot.Energy), fired.Events[0].Weapon);
        Assert.Equal(IconFinding.FindWeaponIcon(Catalog, session.Build.Build, WeaponSlot.Energy), fired.Events[0].WeaponIcon);
        Assert.True(fired.Events[0].WeaponIcon.IsSome());
    }

    [Fact]
    public void A_step_shows_its_weapons_its_abilitys_or_its_keywords_icon()
    {
        var build = Play().Build;
        var weapon = new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Damage, TargetCount.One);
        var grenade = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage, TargetCount.One);
        var trace = new PlayerAction.CollectPickups(PickupId.From("ionic-trace"));
        var max = new PlayerAction.Declare(new StateDeclaration.ReachMax(StatusId.From("bolt-charge")));
        var pack = new PlayerAction.Declare(new StateDeclaration.NewPack());

        Assert.Equal(build.Build.Weapons.Single(found => found.Slot == WeaponSlot.Kinetic).Hash.Bind(Catalog.Manifest.FindItem).Bind(item => item.Icon), LoopDesigning.FindActionIcon(build, weapon));
        Assert.Equal(build.Build.Abilities.Grenade.Bind(grenadeElement => IconFinding.FindElementIcon(Catalog, grenadeElement)), LoopDesigning.FindActionIcon(build, grenade));
        Assert.True(LoopDesigning.FindActionIcon(build, grenade).IsSome());
        Assert.Equal(Optional.Some(Catalog.Manifest.KeywordTraits[ItemHash.From(3824458961u)].Icon), LoopDesigning.FindActionIcon(build, trace));
        Assert.Equal(Optional.Some(Catalog.Manifest.KeywordTraits[ItemHash.From(2935077680u)].Icon), LoopDesigning.FindActionIcon(build, max));
        Assert.False(LoopDesigning.FindActionIcon(build, pack).IsSome());
    }

    private static DesignSession Play(params PlayerAction[] actions)
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        var json = File.ReadAllText(RepoFiles.ToPath(path));                                     // impure
        var session = LoopDesigning.StartSavedDimDesign(Catalog, new SourceText(path, json))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        return actions.Aggregate(session, (played, action) => LoopDesigning.AppendStep(played, action, Optional.None<string>()));
    }
}

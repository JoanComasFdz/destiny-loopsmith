using System.Collections.Immutable;
using Loopsmith.Core.CompendiumReading;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.CompendiumReading;

/// <summary>
/// The check on a made-up snapshot and made-up elements: it finds an element by its name or its alias in the cited row,
/// compares its numbers, says what is wrong, and lists what it can't check. No Compendium text (README, Licensing).
/// </summary>
public sealed class CompendiumCheckingTests
{
    private static readonly CompendiumSnapshot Snapshot = SnapshotReading.ReadSnapshot([
        new SourceText("06_Arc.csv", string.Join("\n",
            ",Fragments,,,,,",
            ",Testing,,\"Grants 15% [5%] grenade energy.\",,,",
            ",Melee Abilities,,,,,",
            ",Testing Punch,,Deals damage.,,,\"Base Cooldown: 50 seconds\n\nChunk Scalar: 0.9x\"")),
        new SourceText("05_Armor_Mods.csv", string.Join("\n",
            ",Helmet,Effect,Arms,Effect",
            ",\"Siphon\n[1 Energy]\",\"Two kills within 3 seconds spawn an orb.\",\"Testing Finisher\n[1 Energy]\",\"Restores health in full.\"")),
        new SourceText("16_Game_Mechanics.csv", "anything")], []);

    private static readonly CompendiumAliases NoAliases = new([], [], [], [], [], []);

    [Fact]
    public void An_element_its_cited_row_names_with_its_numbers_has_no_problem()
    {
        var check = Check(NoAliases, Punch("compendium/2026-01-01/Arc#4", cooldown: 50m, scalar: 0.9m));

        Assert.Empty(check.Problems);
        Assert.Equal(1, check.CheckedElements);
        Assert.Equal(2, check.CheckedNumbers);
    }

    [Fact]
    public void A_number_the_row_doesnt_state_is_a_problem_naming_the_element_its_citation_and_the_number()
    {
        var check = Check(NoAliases, Punch("compendium/2026-01-01/Arc#4", cooldown: 55m, scalar: 0.9m));

        var problem = Assert.Single(check.Problems);
        Assert.Equal("testing-punch", problem.Element.Value);
        Assert.Equal("compendium/2026-01-01/Arc#4", problem.Citation);
        Assert.Equal("ability.baseCooldown 55 is not a number 'Testing Punch' states", problem.Message);
    }

    [Fact]
    public void A_row_that_names_something_else_is_a_problem_listing_what_it_names()
    {
        var check = Check(NoAliases, Punch("compendium/2026-01-01/Arc#2", cooldown: 50m, scalar: 0.9m));

        var problem = Assert.Single(check.Problems);
        Assert.Equal("row 2 of Arc names 'Testing', not 'Testing Punch' (add an alias if it is one of them)", problem.Message);
    }

    [Fact]
    public void A_prefix_finds_a_fragment_and_its_PvE_number_is_checked_apart_from_the_PvP_one()
    {
        var aliases = NoAliases with { Prefixes = [new NameAffix("Arc", "Spark of", "fragments drop it")] };
        var fragment = Element("spark-of-testing", "Spark of Testing", "compendium/2026-01-01/Arc#2") with
        {
            Rules = [TestCatalog.On(new Trigger.AbilityCast(AbilityKind.Grenade), TestCatalog.Energy(AbilityKind.Grenade, new GameValue.Known(0.05m)))],
        };

        Assert.Single(Check(NoAliases, fragment).Problems, problem => problem.Message.StartsWith("row 2 of Arc names 'Testing'", StringComparison.Ordinal));
        Assert.Equal("grantEnergy 0.05 is not a number 'Testing' states", Assert.Single(Check(aliases, fragment).Problems).Message);
    }

    [Fact]
    public void Dropped_words_a_named_alias_and_a_worded_number_find_what_the_row_says_its_own_way()
    {
        var aliases = new CompendiumAliases(
            [],
            [],
            [new DroppedWords("Armor Mods", ["Arc"], "one row for every element")],
            [new EntryName(ElementId.From("testing-combo-siphon"), "Siphon", "a two-element Siphon")],
            [new WordedNumber(ElementId.From("testing-finisher"), 1m, "the row says it in words")],
            []);
        var finisher = Element("testing-finisher", "Testing Finisher", "compendium/2026-01-01/Armor Mods#2") with
        {
            Rules = [TestCatalog.On(new Trigger.Finisher(), new Outcome.RestoreHealth(new HealAmount.Share(new GameValue.Known(1m)), false, 1))],
        };

        var check = Check(aliases,
            Element("arc-siphon", "Arc Siphon", "compendium/2026-01-01/Armor Mods#2"),
            Element("testing-combo-siphon", "Testing Combo Siphon", "compendium/2026-01-01/Armor Mods#2"),
            finisher);

        Assert.Empty(check.Problems);
        Assert.Equal(3, check.CheckedElements);
    }

    [Fact]
    public void A_citation_into_a_tab_without_a_parser_is_listed_as_not_checked()
    {
        var check = Check(NoAliases, Element("testing-orb", "Testing Orb", "compendium/2026-01-01/Game Mechanics#1"));

        Assert.Empty(check.Problems);
        Assert.Equal(0, check.CheckedElements);
        Assert.Equal("compendium/2026-01-01/Game Mechanics#1", Assert.Single(check.NotChecked).Citation);
    }

    [Fact]
    public void An_alias_for_an_element_the_rules_dont_have_or_a_number_it_doesnt_hold_is_stale()
    {
        var aliases = NoAliases with
        {
            Names = [new EntryName(ElementId.From("gone-mod"), "Siphon", "a mod since removed")],
            Worded = [new WordedNumber(ElementId.From("testing-punch"), 1m, "no longer in the rules")],
        };

        var check = Check(aliases, Punch("compendium/2026-01-01/Arc#4", cooldown: 50m, scalar: 0.9m));

        Assert.Equal(["names: no element has this id", "worded: the element holds no 1"], check.Problems.Select(problem => problem.Message));
    }

    private static CompendiumCheck Check(CompendiumAliases aliases, params BuildElement[] elements) =>
        CompendiumChecking.CheckCatalog(RepoCatalog.Catalog with { Elements = elements.ToImmutableDictionary(element => element.Id) }, Snapshot, aliases);

    private static BuildElement Punch(string source, decimal cooldown, decimal scalar) =>
        Element("testing-punch", "Testing Punch", source) with
        {
            Ability = Optional.Some(new AbilityProfile(AbilityKind.Melee, ChargeCount.From(1), new GameValue.Known(scalar), new GameValue.Known(cooldown))),
        };

    private static BuildElement Element(string id, string name, string source) =>
        TestCatalog.Element(id, ElementKind.Fragment, []) with
        {
            Name = name,
            Source = Loopsmith.Core.RuleParsing.GameNotationParsing.ParseProvenance(source).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure)),
        };
}

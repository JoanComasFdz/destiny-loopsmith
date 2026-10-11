using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.BuildExplanation;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;
using System.Collections.Immutable;

namespace Loopsmith.Core.Tests.BuildExplanation;

/// <summary>
/// What in a build interacts with a keyword, role by role, for its details ("In this build"), and a keyword named by
/// what it is in the game ("Arc buff", "Arc elemental pickup") rather than "Keyword".
/// </summary>
public sealed class KeywordInteractingTests
{
    private static readonly string RulesDirectory = RepoFiles.ToPath("rules");                                   // pure

    private static readonly Result<ImmutableArray<SourceText>, string> RuleFiles = FileSourceFetching.ReadRuleFiles(RulesDirectory);   // impure

    private static readonly RuleCatalog Catalog =
        RuleFiles.Bind(CatalogLoading.ParseCatalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    [Fact]
    public void Armor_Charge_is_given_by_Elemental_Charge_and_the_orb_used_up_by_Grenade_Kickstart_and_raised_by_Charged_Up()
    {
        var groups = Explain(new KeywordRef.Status(StatusId.From("armor-charge")));

        Assert.Equal(["Gives Armor Charge", "Uses up Armor Charge", "Changes Armor Charge"], groups.Select(group => group.Heading));
        Assert.Equal(["Elemental Charge", "Orb of Power"], groups[0].Interactions.Select(interaction => interaction.SourceName));
        Assert.Equal("Pick up Ionic Trace → +1 Armor Charge (chance)", groups[0].Interactions[0].Text);
        var kickstart = Assert.Single(groups[1].Interactions);
        Assert.Equal(("Grenade Kickstart", "Grenade thrown → spends Armor Charge → +?% grenade energy per stack"), (kickstart.SourceName, kickstart.Text));   // its "while Armor Charge" goes without saying here
        var chargedUp = Assert.Single(groups[2].Interactions);
        Assert.Equal(("Charged Up", "Armor Charge up to x4"), (chargedUp.SourceName, chargedUp.Text));   // one copy: its first cap
    }

    [Fact]
    public void Amplified_is_given_by_Flow_State_and_Flashover_and_switches_on_two_passives()
    {
        var groups = Explain(new KeywordRef.Status(StatusId.From("amplified")));

        Assert.Equal(["Gives Amplified", "While Amplified"], groups.Select(group => group.Heading));
        Assert.Equal(["Flow State", "Flashover"], groups[0].Interactions.Select(interaction => interaction.SourceName));
        Assert.Equal(["Spark of Frequency", "Ionic Overclock"], groups[1].Interactions.Select(interaction => interaction.SourceName));
        Assert.Equal(Optional.Some("No weapon in this build is one of them, so it does nothing here."), groups[1].Interactions[1].Note);
    }

    [Fact]
    public void A_keyword_leaves_out_its_own_rules_and_lists_who_reacts_to_it_and_who_changes_it()
    {
        var groups = Explain(new KeywordRef.Status(StatusId.From("bolt-charge")));

        Assert.Equal(["Gives Bolt Charge", "Reacts to Bolt Charge", "Changes Bolt Charge"], groups.Select(group => group.Heading));
        Assert.DoesNotContain(groups.SelectMany(group => group.Interactions), interaction => interaction.Source.Value == "bolt-charge");
        Assert.Contains(groups[1].Interactions, interaction => interaction.SourceName == "Shinobu's Vow" && interaction.Text == "Gain Bolt Charge → +?% grenade energy");
        Assert.Contains(groups[1].Interactions, interaction => interaction.SourceName == "Defibrillating Blast");   // its strike is Bolt Charge damage
        Assert.Equal(["Spark of Frequency", "Flashover"], groups[2].Interactions.Select(interaction => interaction.SourceName));
        Assert.Equal("+1 Bolt Charge per gain (while Amplified)", groups[2].Interactions[0].Text);
    }

    [Fact]
    public void A_pickup_is_created_by_some_elements_and_reacted_to_by_others()
    {
        var trace = Explain(new KeywordRef.Pickup(PickupId.From("ionic-trace")));
        var orb = Explain(new KeywordRef.Pickup(PickupId.From("orb-of-power")));

        Assert.Equal(["Creates Ionic Trace", "Reacts to Ionic Trace"], trace.Select(group => group.Heading));
        Assert.Equal(["Spark of Discharge", "Elemental Charge"], trace[1].Interactions.Select(interaction => interaction.SourceName));
        Assert.Equal(["Harmonic Siphon", "Strand Siphon", "Reaper"], orb[0].Interactions.Select(interaction => interaction.SourceName));
    }

    [Theory]
    [InlineData("bolt-charge", "Arc buff")]
    [InlineData("woven-mail", "Strand buff")]
    [InlineData("unraveling-rounds", "Strand weapon buff")]
    [InlineData("jolt", "Arc debuff")]
    [InlineData("ionic-trace", "Arc elemental pickup")]
    [InlineData("orb-of-power", "Pickup")]
    [InlineData("spark-of-shock", "Fragment")]
    public void An_element_is_named_by_what_it_is_in_the_game(string element, string type) =>
        Assert.Equal(type, LoopDesigning.DescribeElementType(StartOwnersDimDesign().Build, ElementId.From(element)));

    private static ImmutableArray<InteractionGroup> Explain(KeywordRef keyword) =>
        LoopDesigning.ExplainInteractions(StartOwnersDimDesign().Build, keyword);

    private static DesignSession StartOwnersDimDesign()
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        var file = RepoFiles.ToPath(path);                                                       // pure
        var json = File.ReadAllText(file);                                                       // impure
        return LoopDesigning.StartSavedDimDesign(Catalog, new SourceText(path, json))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
    }
}

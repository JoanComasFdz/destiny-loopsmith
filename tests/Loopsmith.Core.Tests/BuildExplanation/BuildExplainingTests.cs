using Loopsmith.Core.BuildExplanation;
using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Phrasing;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.BuildExplanation;

public class BuildExplainingTests
{
    [Fact]
    public void Rules_sharing_a_trigger_are_one_heading_with_a_line_per_outcome_and_its_source_aligned()
    {
        var shock = Element("spark-of-shock", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), new Outcome.DebuffTarget(Status("jolt"), Optional.None<Seconds>()))]);
        var vow = Element("shinobus-vow", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), Buff("bolt-charge"))]);
        var build = ValidateBuild([shock, vow]);

        var groups = BuildExplaining.ExplainBuild(build);
        var lines = BuildExplaining.RenderExplanation(build.Catalog.Glossary, groups).ToPlainText();

        Assert.Equal("""
            Grenade damage
              ├ Jolt target (4s)  [Spark Of Shock]
              └ +1 Bolt Charge    [Shinobus Vow]
            """, lines);
    }

    [Fact]
    public void Every_keyword_an_explanation_names_is_in_its_affinitys_tone_in_a_build_and_an_items_details()
    {
        var vow = Element("shinobus-vow", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), Buff("bolt-charge")) with { Reason = Optional.Some("Grenade hits give you Bolt Charge.") }]);
        var build = ValidateBuild([vow]);
        var groups = BuildExplaining.ExplainBuild(build);

        var tree = BuildExplaining.RenderExplanation(build.Catalog.Glossary, groups);
        var details = BuildExplaining.RenderElementExplanation(build.Catalog.Glossary, groups);

        Assert.Contains(tree[1].Spans, span => span == new StyledSpan("Bolt Charge", Tone.Arc));
        Assert.Equal(
            [new StyledSpan("Grenade hits give you ", Tone.Muted), new StyledSpan("Bolt Charge", Tone.Arc), new StyledSpan(".", Tone.Muted)],
            details[^1].Spans);                                                                    // the rule's reason
    }

    [Fact]
    public void Conditional_passives_are_grouped_under_their_state()
    {
        var frequency = Element("spark-of-frequency", ElementKind.Fragment, [],
            [new PassiveRule(RuleId.From("test-rule-1"), new Passive.ExtraStacks(Status("bolt-charge"), StackCount.From(1)), [new Condition.HasBuff(Status("amplified"))], Optional.None<string>())]);
        var build = ValidateBuild([frequency]);

        var groups = BuildExplaining.ExplainBuild(build);

        var group = Assert.Single(groups);
        Assert.Equal("While Amplified", group.Heading);
        Assert.Equal("+1 Bolt Charge per gain", group.Items.Single().Outcomes);
    }

    [Fact]
    public void Triggers_are_ordered_like_a_play_session()
    {
        var a = Element("a", ElementKind.Fragment, [On(new Trigger.StacksMaxed(Status("bolt-charge")), Buff("amplified"))]);
        var b = Element("b", ElementKind.Fragment, [On(new Trigger.PickUp(Pickup("ionic-trace")), Buff("bolt-charge"))]);
        var c = Element("c", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("amplified"))]);
        var build = ValidateBuild([a, b, c]);

        var headings = BuildExplaining.ExplainBuild(build).Select(g => g.Heading);

        Assert.Equal(["Class ability", "Pick up Ionic Trace", "Max Bolt Charge (x3)"], headings);   // a max names its cap
    }

    [Fact]
    public void Keywords_the_build_doesnt_name_are_left_out_of_its_explanation_yet_stay_equipped()
    {
        var vow = Element("shinobus-vow", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), Buff("bolt-charge"))]);
        var build = ValidateBuild([vow], keywords: [CreateBoltCharge(), Jolt]);

        var sources = BuildExplaining.ExplainBuild(build).SelectMany(group => group.Items).Select(item => item.SourceName).Distinct();

        Assert.Equal(["Shinobus Vow", "Bolt Charge"], sources);
        Assert.Contains(build.Equipped, equipped => equipped.Element.Id == ElementId.From("jolt"));   // step playing still fires it
    }

    [Fact]
    public void A_keyword_named_only_by_another_keyword_the_build_uses_is_listed_too()
    {
        var vow = Element("shinobus-vow", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), Buff("bolt-charge"))]);
        var build = ValidateBuild([vow], keywords: [CreateBoltCharge(Debuff("jolt")), Jolt]);

        var sources = BuildExplaining.ExplainBuild(build).SelectMany(group => group.Items).Select(item => item.SourceName).Distinct();

        Assert.Contains("Jolt", sources);
    }

    [Fact]
    public void A_keyword_that_gives_what_the_build_reads_is_listed()
    {
        var kickstart = Element("grenade-kickstart", ElementKind.Fragment,
            [OnWhen(new Trigger.AbilityCast(AbilityKind.Grenade), new Condition.HasBuff(Status("armor-charge")), Energy(AbilityKind.Grenade, new GameValue.Unknown()))]);
        var orbOfPower = Element("orb-of-power", ElementKind.Keyword, [On(new Trigger.PickUp(Pickup("orb-of-power")), Buff("armor-charge"))]);

        var reading = BuildExplaining.ExplainBuild(ValidateBuild([kickstart], keywords: [orbOfPower]));
        var notReading = BuildExplaining.ExplainBuild(ValidateBuild([], keywords: [orbOfPower]));

        Assert.Contains(reading.SelectMany(group => group.Items), item => item.SourceName == "Orb Of Power");
        Assert.Empty(notReading);
    }

    private static BuildElement CreateBoltCharge(params Outcome[] onGain) =>
        Element("bolt-charge", ElementKind.Keyword, [On(new Trigger.BuffGained(Status("bolt-charge")), [Energy(AbilityKind.Melee, new GameValue.Unknown()), .. onGain])]);

    private static readonly BuildElement Jolt =
        Element("jolt", ElementKind.Keyword, [On(new Trigger.DamageDebuffed(new DamageSource.AnyWeapon(), [Status("jolt")]), new Outcome.StrikeTarget(Status("jolt"), HitOutcome.Damage))]);
}

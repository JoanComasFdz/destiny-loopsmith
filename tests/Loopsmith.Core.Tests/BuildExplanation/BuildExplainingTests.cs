using Loopsmith.Core.BuildExplanation;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.BuildExplanation;

public class BuildExplainingTests
{
    [Fact]
    public void Rules_sharing_a_trigger_become_one_note_line_with_sources()
    {
        var shock = Element("spark-of-shock", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), new Outcome.DebuffTarget(Status("jolt"), Optional.None<Seconds>()))]);
        var vow = Element("shinobus-vow", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), Buff("bolt-charge"))]);
        var build = ValidateBuild([shock, vow]);

        var groups = BuildExplaining.ExplainBuild(build);
        var lines = BuildExplaining.RenderExplanation(groups, ExplanationStyle.Note).ToPlainText();

        Assert.Equal("Grenade damage -> Jolt target (4s) [Spark Of Shock] + +1 Bolt Charge [Shinobus Vow]", lines);
    }

    [Fact]
    public void Conditional_passives_are_grouped_under_their_state()
    {
        var frequency = Element("spark-of-frequency", ElementKind.Fragment, [],
            [new PassiveRule(new Passive.ExtraStacks(Status("bolt-charge"), StackCount.From(1)), [new Condition.HasBuff(Status("amplified"))], Optional.None<string>())]);
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

        Assert.Equal(["Class ability", "Pick up Ionic Trace", "Max Bolt Charge"], headings);
    }
}

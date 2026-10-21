using Loopsmith.Core.BuildExplanation;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Phrasing;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.BuildExplanation;

public class OutcomeExplainingTests
{
    [Fact]
    public void Every_way_to_an_outcome_is_one_row_under_it_with_its_trigger_and_its_source_aligned()
    {
        var shock = Element("spark-of-shock", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), Debuff("jolt"))]);
        var ascension = Element("ascension", ElementKind.Fragment,
            [On(new Trigger.AbilityCastAs(AbilityKind.ClassAbility, AbilityInput.Air), Debuff("jolt"), Buff("amplified"))]);
        var build = ValidateBuild([shock, ascension]);

        var lines = OutcomeExplaining.RenderOutcomes(build.Catalog.Glossary, OutcomeExplaining.ExplainOutcomes(build)).ToPlainText();

        Assert.Equal("""
            Amplified · Arc buff
              Is given by
                └ Class ability in the air → Amplified (10s)  [Ascension]
            Jolt · Arc debuff
              Is given by
                ├ Grenade damage → Jolt target (4s)            [Spark Of Shock]
                └ Class ability in the air → Jolt target (4s)  [Ascension]
            """, lines);
    }

    [Fact]
    public void Outcomes_are_ordered_by_group_then_by_name()
    {
        var a = Element("a", ElementKind.Fragment, [On(new Trigger.Finisher(), Energy(AbilityKind.Grenade, new GameValue.Known(0.1m)))]);
        var b = Element("b", ElementKind.Fragment, [On(new Trigger.Finisher(), new Outcome.Spawn(Pickup("orb-of-power"), 1))]);
        var c = Element("c", ElementKind.Fragment, [On(new Trigger.Finisher(), Debuff("jolt"))]);
        var d = Element("d", ElementKind.Fragment, [On(new Trigger.Finisher(), Buff("bolt-charge"), Buff("amplified"))]);
        var build = ValidateBuild([a, b, c, d]);

        var headings = OutcomeExplaining.ExplainOutcomes(build).Select(heading => heading.Name);

        Assert.Equal(["Amplified", "Bolt Charge", "Jolt", "Orb of Power", "Grenade energy"], headings);
    }

    [Fact]
    public void What_ends_or_uses_up_a_buff_is_under_it_and_stacks_spent_for_energy_are_under_the_energy_too()
    {
        var giver = Element("giver", ElementKind.Fragment, [On(new Trigger.Finisher(), Buff("armor-charge"))]);
        var kickstart = Element("kickstart", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), new Outcome.ConvertStacksToEnergy(Status("armor-charge"), AbilityKind.Grenade, new StackEnergy.PerStack(new GameValue.Unknown())))]);
        var build = ValidateBuild([giver, kickstart]);

        var headings = OutcomeExplaining.ExplainOutcomes(build);

        var charge = Assert.Single(headings, heading => heading.Name == "Armor Charge");
        Assert.Equal([OutcomeWayRole.GivenBy, OutcomeWayRole.EndedOrUsedUpBy], charge.Blocks.Select(block => block.Role));
        Assert.Equal("Kickstart", charge.Blocks[1].Ways.Single().SourceName);
        var energy = Assert.Single(headings, heading => heading.Name == "Grenade energy");
        Assert.Equal("Kickstart", energy.Blocks.Single().Ways.Single().SourceName);
    }

    [Fact]
    public void A_passive_is_a_way_to_what_it_gives_and_one_on_a_buffs_stacks_changes_that_buff()
    {
        var resistance = Element("spark-of-resistance", ElementKind.Fragment, [],
            [new PassiveRule(RuleId.From("test-rule-1"), new Passive.ResistDamage(new PveAndPvp(new GameValue.Known(0.25m), Optional.None<GameValue>())), [new Condition.HasBuff(Status("amplified"))], Optional.None<string>())]);
        var frequency = Element("spark-of-frequency", ElementKind.Fragment, [],
            [new PassiveRule(RuleId.From("test-rule-2"), new Passive.ExtraStacks(Status("bolt-charge"), StackCount.From(1)), [], Optional.None<string>())]);
        var build = ValidateBuild([resistance, frequency]);

        var lines = OutcomeExplaining.RenderOutcomes(build.Catalog.Glossary, OutcomeExplaining.ExplainOutcomes(build)).ToPlainText();

        Assert.Equal("""
            Bolt Charge · Arc buff
              Is changed by
                └ Always → +1 Bolt Charge per gain  [Spark Of Frequency]
            Damage resistance
              Is given by
                └ While Amplified → 25% damage resistance  [Spark Of Resistance]
            """, lines);
    }
}

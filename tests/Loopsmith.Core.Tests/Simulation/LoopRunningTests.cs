using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.Simulation;
using Loopsmith.Core.TraceRendering;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.Simulation;

/// <summary>docs/loop-format.md "Analysis" on the tiny hand-built catalog: the passes, the needs and their providers, the verdict.</summary>
public class LoopRunningTests
{
    private static readonly PlayerAction GrenadeKill = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One);
    private static readonly PlayerAction RifleKill = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One);
    private static readonly PlayerAction PowerKill = new PlayerAction.FireWeapon(WeaponSlot.Power, HitOutcome.Kill, TargetCount.One);
    private static readonly PlayerAction Dodge = new PlayerAction.UseClassAbility();
    private static readonly PlayerAction PickUpOrbs = new PlayerAction.CollectPickups(Pickup("orb-of-power"));
    private static readonly PlayerAction BoltChargeAtMax = new PlayerAction.Declare(new StateDeclaration.ReachMax(Status("bolt-charge")));
    private static readonly PlayerAction AmplifiedEnds = new PlayerAction.Declare(new StateDeclaration.EndStatus(Status("amplified")));

    private static ImmutableArray<LoopStep> ToSteps(params PlayerAction[] actions) =>
        [.. actions.Select(action => new LoopStep(action, Optional.None<string>()))];

    private static LoopReport RunLoop(ValidatedBuild build, params PlayerAction[] actions) =>
        LoopRunning.RunLoop(build, "Test loop", ToSteps(actions));

    /// <summary>A kill drops an Orb of Power.</summary>
    private static readonly BuildElement Spawner = Element("spawner", ElementKind.Fragment,
        [On(new Trigger.KillAny(new DamageSource.AnySource()), new Outcome.Spawn(Pickup("orb-of-power"), 1))]);

    /// <summary>Picking up an Orb of Power amplifies you.</summary>
    private static readonly BuildElement Collector = Element("collector", ElementKind.Fragment,
        [On(new Trigger.PickUp(Pickup("orb-of-power")), Buff("amplified"))]);

    private static ElementMention Mention(string id, Likelihood likelihood = Likelihood.Always) =>
        new(ElementId.From(id), string.Join(' ', id.Split('-').Select(DomainPhrasing.Capitalize)), Affinity.Arc, likelihood);

    private static ImmutableArray<string> DescribeNeeds(StepAnalysis step) => [.. step.Needs.Select(need => need.DescribeNeed())];

    [Fact]
    public void A_loop_of_abilities_repeats_from_its_first_pass()
    {
        var report = RunLoop(ValidateBuild([]), GrenadeKill, GrenadeKill, Dodge, GrenadeKill);

        Assert.Equal(new LoopVerdict.Repeats(Optional.None<BlockedStep>()), report.Verdict);
        Assert.Same(report.FirstPass, report.RepeatingPass);
        Assert.Empty(report.FirstPassDifferences);
        Assert.Equal([0, 1, 2, 3], report.Steps.Select(step => step.StepIndex));
        Assert.Equal(["grenade:kill", "grenade:kill", "class", "grenade:kill"], report.Steps.Select(step => step.Token));
        Assert.Equal(("Test loop", "Test build", 4), (report.LoopName, report.BuildName, report.StepLabels.Length));
    }

    [Fact]
    public void A_step_that_cannot_happen_in_the_repeating_pass_breaks_the_loop()
    {
        var report = RunLoop(ValidateBuild([]), GrenadeKill, PowerKill, RifleKill);

        var breaks = Assert.IsType<LoopVerdict.Breaks>(report.Verdict);
        Assert.Equal(new BlockedStep(1, report.StepLabels[1], "No weapon in the Power slot — nothing happens."), breaks.Blocked);
        Assert.True(report.Steps[1].Blocked.IsSome());
        Assert.Equal(3, report.RepeatingPass.Resolutions.Length);   // the pass is played to its end
    }

    [Fact]
    public void An_opener_the_first_pass_lacks_still_repeats_and_its_need_comes_from_the_previous_pass()
    {
        var report = RunLoop(ValidateBuild([Spawner, Collector]), PickUpOrbs, RifleKill);

        var repeats = Assert.IsType<LoopVerdict.Repeats>(report.Verdict);
        var blocked = Assert.IsType<Optional<BlockedStep>.Some>(repeats.FirstPassBlocked).Value;
        Assert.Equal((0, "No orb-of-power on the ground — nothing happens."), (blocked.StepIndex, blocked.Reason));
        var need = Assert.Single(report.Steps[0].Needs);
        var provider = Assert.IsType<NeedProvider.PreviousPass>(need.Provider);
        Assert.Equal(1, provider.StepIndex);
        Assert.Equal([Mention("spawner")], provider.Elements);
        Assert.Equal("Orb of Power ← previous pass #2 [Spawner]", need.DescribeNeed());
        Assert.Equal([Mention("collector")], report.Steps[0].SetsOff);
        Assert.Equal(0, report.RepeatingPass.Start.Step);   // each pass numbers its steps from #1

        var difference = Assert.Single(report.FirstPassDifferences);
        Assert.Equal((0, true), (difference.StepIndex, difference.BlockedOnFirstPass.IsSome()));
        Assert.Equal([Mention("collector")], difference.OnlyInRepeatingPass);
        Assert.Empty(difference.OnlyOnFirstPass);
    }

    [Fact]
    public void A_buff_nothing_ends_breaks_a_loop_whose_guard_needs_it_gone()
    {
        // A kill drops an orb only while you are not amplified, and the orb amplifies you: the second pass has no orb.
        var guardedSpawner = Element("spawner", ElementKind.Fragment,
            [OnWhen(new Trigger.KillAny(new DamageSource.AnySource()), new Condition.LacksBuff(Status("amplified")), new Outcome.Spawn(Pickup("orb-of-power"), 1))]);
        var build = ValidateBuild([guardedSpawner, Collector]);

        var report = RunLoop(build, RifleKill, PickUpOrbs);

        var breaks = Assert.IsType<LoopVerdict.Breaks>(report.Verdict);
        Assert.Equal(1, breaks.Blocked.StepIndex);
        Assert.Empty(report.Steps[0].SetsOff);
        Assert.Equal([Mention("spawner")], Assert.Single(report.FirstPassDifferences, d => d.StepIndex == 0).OnlyOnFirstPass);
    }

    [Fact]
    public void Declaring_the_buff_ended_lets_that_loop_repeat_and_the_ending_needs_the_buff()
    {
        var guardedSpawner = Element("spawner", ElementKind.Fragment,
            [OnWhen(new Trigger.KillAny(new DamageSource.AnySource()), new Condition.LacksBuff(Status("amplified")), new Outcome.Spawn(Pickup("orb-of-power"), 1))]);

        var report = RunLoop(ValidateBuild([guardedSpawner, Collector]), RifleKill, PickUpOrbs, AmplifiedEnds);

        Assert.Equal(new LoopVerdict.Repeats(Optional.None<BlockedStep>()), report.Verdict);
        Assert.Equal(["Orb of Power ← #1 [Spawner]"], DescribeNeeds(report.Steps[1]));
        Assert.Equal(["Amplified ← #2 [Collector]"], DescribeNeeds(report.Steps[2]));
    }

    [Fact]
    public void A_guard_needs_the_buff_an_earlier_step_gave()
    {
        var giver = Element("giver", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("amplified"))]);
        var guarded = Element("guarded", ElementKind.Fragment,
            [OnWhen(new Trigger.AbilityCast(AbilityKind.Grenade), new Condition.HasBuff(Status("amplified")), Buff("bolt-charge"))]);

        var report = RunLoop(ValidateBuild([giver, guarded]), Dodge, GrenadeKill);

        Assert.Empty(report.Steps[0].Needs);
        Assert.Equal(["Amplified ← #1 [Giver]"], DescribeNeeds(report.Steps[1]));
        Assert.Equal([Mention("guarded")], report.Steps[1].SetsOff);
    }

    [Fact]
    public void What_a_step_gives_itself_is_not_a_need()
    {
        // The grenade's cast amplifies you and its damage reads Amplified: one cascade, nothing needed from before.
        var selfGiver = Element("self-giver", ElementKind.Fragment,
        [
            On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("amplified")),
            OnWhen(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), new Condition.HasBuff(Status("amplified")), Buff("bolt-charge")),
        ]);

        var report = RunLoop(ValidateBuild([selfGiver]), GrenadeKill, AmplifiedEnds);

        Assert.Empty(report.Steps[0].Needs);
        Assert.Equal(2, report.RepeatingPass.Resolutions[0].Fired.Count(rule => rule.Source.Value == "self-giver"));
        Assert.Equal(["Amplified ← #1 [Self Giver]"], DescribeNeeds(report.Steps[1]));
    }

    [Fact]
    public void The_provider_is_a_step_where_it_always_arrives_and_only_since_it_was_last_used_up()
    {
        var sure = Element("sure", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), new Outcome.Spawn(Pickup("orb-of-power"), 1))]);
        var lucky = Element("lucky", ElementKind.Fragment,
            [new Rule(new Trigger.AbilityCast(AbilityKind.Grenade), [], [new Outcome.Spawn(Pickup("orb-of-power"), 1)], Optional.None<string>(), Likelihood.Chance, [])]);
        var build = ValidateBuild([sure, lucky]);

        var both = RunLoop(build, Dodge, GrenadeKill, PickUpOrbs);
        var pickedBetween = RunLoop(build, Dodge, PickUpOrbs, GrenadeKill, PickUpOrbs);

        Assert.Equal(["Orb of Power ← #1 [Sure]"], DescribeNeeds(both.Steps[2]));   // not the later chance drop
        Assert.Equal(["Orb of Power ← #3 [Lucky (chance)]"], DescribeNeeds(pickedBetween.Steps[3]));   // #1's orb was picked up at #2
    }

    [Fact]
    public void A_rule_that_reads_a_declared_maximum_needs_the_declaration()
    {
        var stacker = Element("stacker", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("bolt-charge"))]);
        var spender = Element("spender", ElementKind.Fragment,
        [
            OnWhen(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), new Condition.AtMax(Status("bolt-charge")),
                new Outcome.RemoveBuff(Status("bolt-charge"))),
        ]);

        var report = RunLoop(ValidateBuild([stacker, spender]), GrenadeKill, BoltChargeAtMax, GrenadeKill);

        Assert.Equal(new LoopVerdict.Repeats(Optional.None<BlockedStep>()), report.Verdict);
        Assert.Equal(["Bolt Charge ← #1 [Stacker]"], DescribeNeeds(report.Steps[1]));
        Assert.Equal(["Bolt Charge at max ← #2"], DescribeNeeds(report.Steps[2]));   // the Bolt Charge it spends, its own cast gave first
        Assert.Equal([Mention("stacker")], report.Steps[0].SetsOff);   // not at max yet: the spender stays quiet
        Assert.Equal([Mention("stacker"), Mention("spender")], report.Steps[2].SetsOff);
    }

    [Fact]
    public void A_declaration_that_does_not_hold_blocks_its_step()
    {
        var report = RunLoop(ValidateBuild([]), BoltChargeAtMax, GrenadeKill);

        var breaks = Assert.IsType<LoopVerdict.Breaks>(report.Verdict);
        Assert.Equal((0, "Bolt Charge isn't active — nothing to declare at max."), (breaks.Blocked.StepIndex, breaks.Blocked.Reason));
    }

    [Fact]
    public void A_rule_that_does_not_stack_is_wasted_and_not_set_off()
    {
        var giver = Element("giver", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("bolt-charge"))]);
        var yielder = Element("yielder", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("bolt-charge")).NotStackingWith("giver")]);

        var report = RunLoop(ValidateBuild([yielder, giver]), GrenadeKill);

        var step = Assert.Single(report.Steps);
        Assert.Equal([Mention("giver")], step.SetsOff);
        var wasted = Assert.Single(step.Wasted);
        Assert.Equal(new WastedMention(Mention("yielder"), "Giver"), wasted);
        Assert.Equal("Yielder — doesn't stack with Giver", wasted.DescribeWasted());
    }

    [Fact]
    public void An_element_is_chance_only_when_every_rule_of_it_that_fired_is_chance()
    {
        var lucky = Element("lucky", ElementKind.Fragment,
            [new Rule(new Trigger.AbilityCast(AbilityKind.Grenade), [], [Buff("amplified")], Optional.None<string>(), Likelihood.Chance, [])]);
        var mixed = Element("mixed", ElementKind.Fragment,
        [
            new Rule(new Trigger.AbilityCast(AbilityKind.Grenade), [], [Buff("bolt-charge")], Optional.None<string>(), Likelihood.Chance, []),
            On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), new Outcome.RestoreHealth(new GameValue.Unknown(), false)),
        ]);

        var report = RunLoop(ValidateBuild([lucky, mixed]), GrenadeKill);

        Assert.Equal([Mention("lucky", Likelihood.Chance), Mention("mixed")], report.Steps[0].SetsOff);
        Assert.Equal("Lucky (chance)", report.Steps[0].SetsOff[0].DescribeMention());
    }

    [Fact]
    public void An_empty_loop_has_no_steps_and_says_so()
    {
        var report = LoopRunning.RunLoop(ValidateBuild([]), "Empty", []);

        Assert.IsType<LoopVerdict.NoSteps>(report.Verdict);
        Assert.Empty(report.Steps);
        Assert.Empty(report.FirstPassDifferences);
        Assert.Contains("The loop has no steps.", LoopReportRendering.RenderLoopReport(report).ToPlainText());
    }

    [Fact]
    public void Running_a_design_uses_its_name_and_steps()
    {
        var design = new LoopDesign("Designed", Optional.None<string>(), Optional.None<string>(), Optional.None<CatalogVersion>(),
            new SourceText("b", ""), ToSteps(GrenadeKill, RifleKill));

        var report = LoopRunning.RunLoop(ValidateBuild([]), design);

        Assert.Equal(("Designed", 2), (report.LoopName, report.Steps.Length));
    }

    [Fact]
    public void The_report_reads_the_order_the_verdict_each_step_and_the_first_pass()
    {
        var report = RunLoop(ValidateBuild([Spawner, Collector]), PickUpOrbs, RifleKill);

        var lines = LoopReportRendering.RenderLoopReport(report).Select(line => line.ToPlainText()).ToImmutableArray();

        Assert.Equal("Test loop — Test build · 2 steps", lines[0]);
        Assert.Equal(string.Join(" → ", report.StepLabels), lines[1]);
        Assert.StartsWith("✓ Repeats — on the first pass, #1 (", lines[2]);
        Assert.Contains("   needs            Orb of Power ← previous pass #2 [Spawner]", lines);
        Assert.Contains("   sets off         Collector", lines);
        Assert.Contains("First pass (from a fresh spawn, where it differs)", lines);
        Assert.Contains("   doesn't set off  Collector", lines);
    }
}

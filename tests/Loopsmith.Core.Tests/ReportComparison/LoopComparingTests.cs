using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.ReportComparison;
using Loopsmith.Core.Simulation;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.ReportComparison;

/// <summary>docs/loop-format.md "Comparison": triggers matched by occurrence, and what each one's place changes.</summary>
public sealed class LoopComparingTests
{
    private static readonly PlayerAction GrenadeKill = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One);
    private static readonly PlayerAction RifleKill = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One);
    private static readonly PlayerAction Dodge = new PlayerAction.UseClassAbility();
    private static readonly PlayerAction AmplifiedEnds = new PlayerAction.Declare(new StateDeclaration.EndStatus(Status("amplified")));

    /// <summary>The dodge amplifies you; a grenade thrown while amplified gives Bolt Charge.</summary>
    private static readonly ValidatedBuild Build = ValidateBuild(
    [
        Element("giver", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("amplified"))]),
        Element("guarded", ElementKind.Fragment,
            [OnWhen(new Trigger.AbilityCast(AbilityKind.Grenade), new Condition.HasBuff(Status("amplified")), Buff("bolt-charge"))]),
    ]);

    private static LoopReport RunLoop(string name, params PlayerAction[] actions) =>
        LoopRunning.RunLoop(Build, name, [.. actions.Select(action => new LoopStep(action, Optional.None<string>()))]);

    private static int ReadStep(Optional<StepAnalysis> step) => step.Match(some => some.Value.StepIndex, _ => -1);

    [Fact]
    public void Triggers_are_matched_by_occurrence_left_first_then_what_only_the_right_has()
    {
        var left = RunLoop("A", Dodge, GrenadeKill, GrenadeKill);
        var right = RunLoop("B", GrenadeKill, Dodge, RifleKill);

        var comparison = LoopComparing.CompareLoops(left, right);

        Assert.Same(left, comparison.Left);
        Assert.Equal(
            [(0, 1), (1, 0), (2, -1), (-1, 2)],
            comparison.Triggers.Select(trigger => (ReadStep(trigger.Left), ReadStep(trigger.Right))));
        Assert.Equal([left.StepLabels[0], left.StepLabels[1], left.StepLabels[2], right.StepLabels[2]], comparison.Triggers.Select(t => t.Label));
    }

    [Fact]
    public void Dodge_then_grenade_sets_off_what_grenade_then_dodge_does_not()
    {
        var dodgeFirst = RunLoop("Dodge first", Dodge, GrenadeKill, AmplifiedEnds);
        var grenadeFirst = RunLoop("Grenade first", GrenadeKill, Dodge, AmplifiedEnds);

        var grenade = LoopComparing.CompareLoops(dodgeFirst, grenadeFirst).Triggers[1];

        Assert.Equal((1, 0), (ReadStep(grenade.Left), ReadStep(grenade.Right)));
        Assert.Equal(["Guarded"], grenade.OnlyLeft.Select(mention => mention.Name));
        Assert.Empty(grenade.OnlyRight);
        Assert.Equal(["Amplified ← #1"], dodgeFirst.Steps[1].Needs.Select(need => need.DescribeNeedBriefly()));
        Assert.Empty(grenadeFirst.Steps[0].Needs);
    }

    [Fact]
    public void A_trigger_only_one_loop_has_lists_no_differences()
    {
        var comparison = LoopComparing.CompareLoops(RunLoop("A", GrenadeKill), LoopRunning.RunLoop(Build, "Empty", []));

        var trigger = Assert.Single(comparison.Triggers);
        Assert.Equal((0, -1), (ReadStep(trigger.Left), ReadStep(trigger.Right)));
        Assert.Empty(trigger.OnlyLeft);
        Assert.Empty(trigger.OnlyRight);
    }

    [Fact]
    public void The_table_shows_each_order_then_trigger_by_trigger_where_its_needs_come_from()
    {
        var dodgeFirst = RunLoop("Dodge first", Dodge, GrenadeKill, AmplifiedEnds);
        var grenadeFirst = RunLoop("Grenade first", GrenadeKill, Dodge, AmplifiedEnds, RifleKill);

        var lines = ComparisonRendering.RenderComparison(LoopComparing.CompareLoops(dodgeFirst, grenadeFirst))
            .Select(line => line.ToPlainText())
            .ToImmutableArray();

        Assert.Equal(
            [
                $"A  Dodge first     {string.Join(" → ", dodgeFirst.StepLabels)}   ✓ Repeats   (Test build)",
                $"B  Grenade first   {string.Join(" → ", grenadeFirst.StepLabels)}   ✓ Repeats   (Test build)",
                "",
                $"{Pad(dodgeFirst.StepLabels[0])}A #1 · B #2: sets off the same",
                $"{Pad(dodgeFirst.StepLabels[1])}A #2 · B #1",
                "   A needs     Amplified ← #1",
                "   only in A   Guarded",
                $"{Pad(dodgeFirst.StepLabels[2])}A #3 · B #3: sets off the same",
                "   A needs     Amplified ← #1",
                "   B needs     Amplified ← #2",
                $"{Pad(grenadeFirst.StepLabels[3])}only in B (#4)",
            ],
            lines);

        string Pad(string label) => label.PadRight(dodgeFirst.StepLabels.Concat(grenadeFirst.StepLabels).Max(other => other.Length) + 2);
    }
}

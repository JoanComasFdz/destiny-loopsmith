using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.LoopAnalyzing;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.StepPlaying;
using Loopsmith.Core.TraceRendering;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.StepPlaying;

/// <summary>
/// ADRs D48: the grenade's, melee's and class ability's energy through a loop — a use spends a charge, a refund fills it
/// scaled by the receiving ability's chunk scalar at 70 in its stat, an unknown part reads as a floor or "?", the player
/// declares it recharged, and a step whose ability isn't ready is played and flagged.
/// </summary>
public sealed class EnergyTrackingTests
{
    private static readonly PlayerAction GrenadeKill = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One);
    private static readonly PlayerAction MeleeKill = new PlayerAction.CastAbility(OffensiveAbility.Melee, HitOutcome.Kill, TargetCount.One);
    private static readonly PlayerAction Dodge = new PlayerAction.UseClassAbility();
    private static readonly PlayerAction RechargeGrenade = new PlayerAction.Declare(new StateDeclaration.Recharged(AbilityKind.Grenade));

    /// <summary>A grenade with Skip Grenade's 0.75 chunk scalar.</summary>
    private static readonly BuildElement SkipLike = Element("test-grenade", ElementKind.Grenade, [],
        ability: Optional.Some(Profile(AbilityKind.Grenade, chunkScalar: new GameValue.Known(0.75m))));

    /// <summary>An Ionic Trace's 11.25% grenade energy on every grenade kill.</summary>
    private static readonly BuildElement Trace = Element("trace", ElementKind.Fragment,
        [On(new Trigger.KillAny(new DamageSource.AbilityOf(AbilityKind.Grenade)), Energy(AbilityKind.Grenade, new GameValue.Known(0.1125m)))]);

    [Fact]
    public void A_throw_spends_the_grenade_and_the_next_one_is_not_ready()
    {
        var build = ValidateBuild([]);

        var steps = ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), [GrenadeKill, GrenadeKill]);

        var energy = StepEnergyShaping.ShapeStepEnergy(build, steps[0]);
        Assert.Equal(
            [("Grenade", "100%", "0%"), ("Melee", "100%", "100%"), ("Class ability", "100%", "100%")],
            energy.Lines.Select(line => (line.Name, line.Before, line.After)));
        Assert.Equal("ready", ReadBadge(energy));
        var again = StepEnergyShaping.ShapeStepEnergy(build, steps[1]);
        Assert.Equal("Not ready", ReadBadge(again));
        Assert.Equal(Optional.Some<ReadinessFlag>(new ReadinessFlag.NotReady("Grenade not ready: 0% before #2")), again.Flag);
        Assert.False(steps[1].Blocked.IsSome());   // played all the same
    }

    [Theory]
    [InlineData(70, "8.44%")]    // 11.25% × 0.75, exactly, at 70 Grenade
    [InlineData(85, "≥ 8.44%")]  // more above 70, by how much Bungie doesn't say
    [InlineData(40, "?")]        // less below 70: no floor
    public void A_refund_is_scaled_by_the_chunk_scalar_at_70_in_its_stat(int grenade, string after)
    {
        var build = ValidateBuild([Trace], grenade: SkipLike, stats: Stats(grenade: grenade));

        var thrown = ActionResolution.ResolveAction(build, ActionResolution.CreateInitialState(), GrenadeKill);

        Assert.Equal(after, StepEnergyShaping.ShapeStepEnergy(build, thrown).Lines[0].After);
    }

    [Fact]
    public void Without_the_stat_a_refund_is_unknown_and_never_0()
    {
        var build = ValidateBuild([Trace], grenade: SkipLike);

        var thrown = ActionResolution.ResolveAction(build, ActionResolution.CreateInitialState(), GrenadeKill);
        var next = ActionResolution.ResolveAction(build, thrown.State, GrenadeKill);

        Assert.Equal("?", StepEnergyShaping.ShapeStepEnergy(build, thrown).Lines[0].After);
        Assert.Equal("May not be ready", ReadBadge(StepEnergyShaping.ShapeStepEnergy(build, next)));
        Assert.Equal(["Grenade may not be ready: ? before #2"], next.Notes);
    }

    [Fact]
    public void An_unknown_refund_keeps_the_known_part_as_a_floor_until_a_spend_makes_it_known()
    {
        var unknown = Element("unknown", ElementKind.Fragment,
            [On(new Trigger.KillAny(new DamageSource.AbilityOf(AbilityKind.Grenade)), Energy(AbilityKind.Grenade, new GameValue.Unknown()))]);
        var build = ValidateBuild([Trace, unknown], grenade: SkipLike, stats: Stats(grenade: 70));

        var steps = ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), [GrenadeKill, RechargeGrenade, GrenadeKill]);

        Assert.Equal("≥ 8.44%", StepEnergyShaping.ShapeStepEnergy(build, steps[0]).Lines[0].After);
        Assert.Equal(("≥ 8.44%", "100%"), (StepEnergyShaping.ShapeStepEnergy(build, steps[1]).Lines[0].Before, StepEnergyShaping.ShapeStepEnergy(build, steps[1]).Lines[0].After));
        Assert.Equal("ready", ReadBadge(StepEnergyShaping.ShapeStepEnergy(build, steps[2])));
    }

    [Fact]
    public void A_chance_rules_refund_may_not_come_so_it_moves_no_floor()
    {
        var chance = Element("chance", ElementKind.Fragment,
            [On(new Trigger.KillAny(new DamageSource.AbilityOf(AbilityKind.Grenade)), Energy(AbilityKind.Grenade, new GameValue.Known(0.5m))) with { Likelihood = Likelihood.Chance }]);
        var build = ValidateBuild([chance], grenade: SkipLike, stats: Stats(grenade: 70));

        var thrown = ActionResolution.ResolveAction(build, ActionResolution.CreateInitialState(), GrenadeKill);

        Assert.Equal([EnergyOf(AbilityKind.Grenade, 0, 0m, EnergyCertainty.AtLeast)], thrown.State.Energy);
    }

    [Fact]
    public void Gamblers_dodge_gives_2_percent_melee_energy_per_melee_point_unscaled()
    {
        var gamblers = Element("gamblers", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), new Outcome.GrantEnergy(AbilityKind.Melee, new EnergyGrant.PerStat(StatKind.Melee, new GameValue.Known(0.02m)), EnergyScaling.Unscaled))]);
        var melee = Element("test-melee", ElementKind.Melee, [], ability: Optional.Some(Profile(AbilityKind.Melee, chunkScalar: new GameValue.Known(0.5m))));
        var withStat = ValidateBuild([gamblers], melee: melee, stats: Stats(melee: 30));
        var without = ValidateBuild([gamblers], melee: melee);

        var punched = ActionResolution.ResolveAction(withStat, ActionResolution.CreateInitialState(), MeleeKill);
        var dodged = ActionResolution.ResolveAction(withStat, punched.State, Dodge);
        var unknown = ActionResolution.ResolveAction(without, ActionResolution.ResolveAction(without, ActionResolution.CreateInitialState(), MeleeKill).State, Dodge);

        Assert.Equal("60%", StepEnergyShaping.ShapeStepEnergy(withStat, dodged).Lines[1].After);   // 2% × 30, no chunk scalar
        Assert.Equal("?", StepEnergyShaping.ShapeStepEnergy(without, unknown).Lines[1].After);
        Assert.Contains("+2% melee energy per Melee point", DescribeOutcomes(withStat, dodged));
    }

    [Fact]
    public void With_two_charges_a_throw_spends_one_and_a_refund_fills_the_next()
    {
        var twoCharges = Element("test-grenade", ElementKind.Grenade, [],
            ability: Optional.Some(Profile(AbilityKind.Grenade, charges: 2, chunkScalar: new GameValue.Known(1m))));
        var refund = Element("refund", ElementKind.Fragment,
            [On(new Trigger.KillAny(new DamageSource.AbilityOf(AbilityKind.Grenade)), Energy(AbilityKind.Grenade, new GameValue.Known(0.25m)))]);
        var build = ValidateBuild([refund], grenade: twoCharges, stats: Stats(grenade: 70));

        var steps = ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), [GrenadeKill, GrenadeKill, GrenadeKill]);

        Assert.Equal(
            [("2 charges", "1 charge + 25%"), ("1 charge + 25%", "50%"), ("50%", "25%")],   // the third, played without a charge, empties it
            steps.Select(step => StepEnergyShaping.ShapeStepEnergy(build, step).Lines[0]).Select(line => (line.Before, line.After)));
        Assert.Equal(
            ["ready", "ready", "Not ready"],
            steps.Select(step => ReadBadge(StepEnergyShaping.ShapeStepEnergy(build, step))));
    }

    [Fact]
    public void Recharged_is_offered_while_an_ability_is_not_full_and_fills_every_charge()
    {
        var build = ValidateBuild([]);
        var thrown = ActionResolution.ResolveAction(build, ActionResolution.CreateInitialState(), GrenadeKill);

        var recharged = ActionResolution.ResolveAction(build, thrown.State, RechargeGrenade);

        Assert.DoesNotContain(RechargeGrenade, ActionResolution.ListAvailableActions(build, ActionResolution.CreateInitialState()));
        Assert.Contains(RechargeGrenade, thrown.NowAvailable);
        Assert.Empty(recharged.State.Energy);
        Assert.Equal("Grenade recharged", build.DescribeAction(RechargeGrenade));
        Assert.Equal("recharged:grenade", RechargeGrenade.ToActionToken());
        Assert.Equal(RechargeGrenade, Assert.IsType<Result<PlayerAction, string>.Ok>(ActionTokenParsing.ParseActionToken("recharged:grenade")).Value);
    }

    [Fact]
    public void Least_charged_energy_goes_where_that_is_sure_and_is_unknown_for_each_candidate_otherwise()
    {
        var restoration = Element("restoration", ElementKind.Fragment,
            [On(new Trigger.KillAny(new DamageSource.AnyWeapon()), new Outcome.GrantLeastChargedEnergy(new EnergyGrant.Fraction(new GameValue.Known(0.5m))))]);
        var grenade = Element("test-grenade", ElementKind.Grenade, [], ability: Optional.Some(Profile(AbilityKind.Grenade, chunkScalar: new GameValue.Known(1m))));
        var build = ValidateBuild([restoration], grenade: grenade, stats: Stats(grenade: 70, melee: 70));
        var rifleKill = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One);

        var sure = ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), [GrenadeKill, rifleKill]);
        var open = ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), [GrenadeKill, MeleeKill, rifleKill]);

        Assert.Equal([EnergyOf(AbilityKind.Grenade, 0, 0.5m, EnergyCertainty.Exact)], sure[^1].State.Energy);
        Assert.Equal(Optional.Some("to your grenade"), sure[^1].Fired.Single(fired => fired.Source.Value == "restoration").Outcomes[0].Caveat);
        Assert.Equal(
            [EnergyCertainty.AtLeast, EnergyCertainty.AtLeast],
            open[^1].State.Energy.Select(energy => energy.Certainty));   // a tie at 0%: which one gets it is open
    }

    [Fact]
    public void A_loop_whose_grenade_is_not_back_by_the_next_pass_breaks_there()
    {
        var build = ValidateBuild([]);

        var breaks = LoopAnalysis.AnalyzeLoop(build, "Test loop", LoopPlaying.PlayLoop(build, ToSteps([GrenadeKill])));
        var repeats = LoopAnalysis.AnalyzeLoop(build, "Test loop", LoopPlaying.PlayLoop(build, ToSteps([GrenadeKill, RechargeGrenade])));

        var broken = Assert.IsType<LoopVerdict.Breaks>(breaks.Verdict);
        Assert.Equal(new BlockedStep(0, breaks.StepLabels[0], "Grenade not ready: 0% before #1"), broken.Blocked);
        Assert.False(breaks.FirstPassRepeats);
        Assert.Equal(new LoopVerdict.Repeats(Optional.None<BlockedStep>()), repeats.Verdict);
    }

    [Fact]
    public void A_step_that_may_not_be_ready_is_a_caveat_not_a_break()
    {
        var build = ValidateBuild([Trace], grenade: SkipLike);

        var report = LoopAnalysis.AnalyzeLoop(build, "Test loop", LoopPlaying.PlayLoop(build, ToSteps([GrenadeKill])));

        Assert.IsType<LoopVerdict.Repeats>(report.Verdict);
        Assert.Equal(Optional.Some<ReadinessFlag>(new ReadinessFlag.MayNotBeReady("Grenade may not be ready: ? before #1")), report.Steps[0].Readiness);
    }

    [Theory]
    [InlineData(0, 0.084375, EnergyCertainty.Exact, 1, "8.44%")]
    [InlineData(0, 0.999999, EnergyCertainty.Exact, 1, "99.99%")]   // never rounded up to a full charge
    [InlineData(0, 0.5, EnergyCertainty.Approximate, 1, "~50%")]
    [InlineData(0, 0.0, EnergyCertainty.AtLeast, 1, "?")]
    [InlineData(1, 0.0, EnergyCertainty.AtLeast, 2, "≥ 1 charge")]
    [InlineData(1, 0.0, EnergyCertainty.Exact, 1, "100%")]
    [InlineData(2, 0.0, EnergyCertainty.Exact, 2, "2 charges")]
    public void Energy_reads_to_two_decimals_at_most_with_its_certainty(int charges, double progress, EnergyCertainty certainty, int total, string text) =>
        Assert.Equal(text, DomainPhrasing.DescribeEnergy(EnergyOf(AbilityKind.Grenade, charges, (decimal)progress, certainty), ChargeCount.From(total)));

    /// <summary>The step card's badge ("Not ready", "May not be ready"), or "ready" when it shows none.</summary>
    private static string ReadBadge(StepEnergy energy) =>
        energy.Badge.UnwrapOr("ready");

    private static ImmutableArray<LoopStep> ToSteps(ImmutableArray<PlayerAction> actions) =>
        [.. actions.Select(action => new LoopStep(action, Optional.None<string>()))];

    private static string DescribeOutcomes(ValidatedBuild build, Resolution resolution) =>
        string.Join("\n", resolution.Fired.SelectMany(fired => fired.Outcomes).Select(applied => build.Catalog.Glossary.DescribeOutcome(applied.Outcome, Optional.None<DamageType>())));
}

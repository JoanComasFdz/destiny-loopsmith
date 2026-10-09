using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.Simulation;
using Loopsmith.Core.TraceRendering;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.Simulation;

/// <summary>Every metric of docs/loop-format.md "Analysis", on the tiny hand-built catalog.</summary>
public class LoopRunningTests
{
    private static readonly PlayerAction GrenadeKill = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill);
    private static readonly PlayerAction RifleKill = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill);
    private static readonly PlayerAction RifleHit = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Damage);
    private static readonly PlayerAction Dodge = new PlayerAction.UseClassAbility();
    private static readonly PlayerAction PickUpOrbs = new PlayerAction.CollectPickups(Pickup("orb-of-power"));

    private static ImmutableArray<LoopStep> ToSteps(params PlayerAction[] actions) =>
        [.. actions.Select(action => new LoopStep(action, Optional.None<string>()))];

    /// <summary>Casting the ability refills it: a loop of that ability feeds itself.</summary>
    private static BuildElement CreateRefill(AbilityKind kind, string id) =>
        Element(id, ElementKind.Fragment, [On(new Trigger.AbilityCast(kind), new Outcome.GrantEnergy(kind, new EnergyGrant.Full()))]);

    private static readonly BuildElement GrenadeRefill = CreateRefill(AbilityKind.Grenade, "refill-grenade");

    private static LoopReport RunLoop(ValidatedBuild build, params PlayerAction[] actions) =>
        LoopRunning.RunLoop(build, "Test loop", ToSteps(actions), 10);

    [Fact]
    public void A_loop_that_refunds_itself_completes_every_cycle_and_is_sustainable()
    {
        var report = RunLoop(ValidateBuild([GrenadeRefill]), GrenadeKill);

        Assert.Equal(10, report.Cycles.Length);
        Assert.Equal(10, report.CompletedCycles);
        Assert.True(report.IsSustainable());
        Assert.All(report.Cycles, cycle => Assert.False(cycle.Blocked.IsSome()));
        Assert.Equal(1m, report.EnergyAtStart.Grenade.Value);
        Assert.Equal(new EnergyDelta(0m, 0m, 0m, 0m), report.ComputeNetEnergy());
        Assert.Equal(("Test loop", "Test build", 1), (report.LoopName, report.BuildName, report.StepCount));
    }

    [Fact]
    public void A_blocked_step_ends_the_run_after_its_cycle_is_played_to_the_end()
    {
        var report = RunLoop(ValidateBuild([]), GrenadeKill, GrenadeKill, RifleKill);

        var cycle = Assert.Single(report.Cycles);
        Assert.Equal(0, report.CompletedCycles);
        Assert.False(report.IsSustainable());
        Assert.Equal(3, cycle.Resolutions.Length);
        var blocked = Assert.IsType<Optional<BlockedStep>.Some>(cycle.Blocked).Value;
        Assert.Equal((1, GrenadeKill), (blocked.StepIndex, blocked.Action));
        Assert.Contains("Not enough Grenade energy", blocked.Reason);
        Assert.Equal(new OutcomeTally("Kills", 2), report.Outcomes[0]);   // the blocked grenade killed nothing
    }

    [Fact]
    public void Each_cycle_starts_where_the_previous_one_ended()
    {
        var grenade = Element("test-grenade", ElementKind.Grenade, [], ability: Optional.Some(Profile(AbilityKind.Grenade, charges: 2)));
        var halfRefund = Element("half-refund", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), Energy(AbilityKind.Grenade, new GameValue.Known(0.5m)))]);

        var report = RunLoop(ValidateBuild([halfRefund], grenade: grenade), GrenadeKill);

        Assert.Equal([1.5m, 1m, 0.5m, 0.5m], report.Cycles.Select(cycle => cycle.EnergyAtEnd.Grenade.Value));
        Assert.Equal(3, report.CompletedCycles);
        Assert.Equal(0, Assert.IsType<Optional<BlockedStep>.Some>(report.Cycles[3].Blocked).Value.StepIndex);
        Assert.Equal(2, report.Cycles[1].Resolutions[0].State.Step);
        Assert.Equal(3, report.FindSteadyCycle().Match(cycle => cycle.Value.Number, _ => 0));
        Assert.Equal(-0.5m, report.ComputeNetEnergy().Grenade);   // cycle 3 vs cycle 2
    }

    [Fact]
    public void With_one_completed_cycle_net_energy_is_measured_from_the_fresh_spawn()
    {
        var halfRefund = Element("half-refund", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), Energy(AbilityKind.Grenade, new GameValue.Known(0.5m)))]);

        var report = RunLoop(ValidateBuild([halfRefund]), GrenadeKill);

        Assert.Equal(1, report.CompletedCycles);
        Assert.Equal(2, report.Cycles.Length);
        Assert.Equal(new EnergyDelta(-0.5m, 0m, 0m, 0m), report.ComputeNetEnergy());
    }

    [Fact]
    public void When_no_cycle_completes_cycle_1_is_the_steady_state()
    {
        var counter = Element("counter", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("amplified"))]);

        var report = RunLoop(ValidateBuild([counter]), GrenadeKill, GrenadeKill);

        Assert.Equal(0, report.CompletedCycles);
        Assert.Equal(1, report.FindSteadyCycle().Match(cycle => cycle.Value.Number, _ => 0));
        Assert.Equal([("counter", 1)], report.Sources.Select(s => (s.Source.Value, s.Fired)));
        Assert.Equal(new EnergyDelta(-1m, 0m, 0m, 0m), report.ComputeNetEnergy());
    }

    [Fact]
    public void Kills_count_kill_actions_and_killing_strikes_and_maxed_statuses_count_once_per_event()
    {
        var charger = Element("charger", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("bolt-charge", 3))]);
        var striker = Element("striker", ElementKind.Fragment,
        [
            On(new Trigger.StacksMaxed(Status("bolt-charge")), new Outcome.RemoveBuff(Status("bolt-charge")), new Outcome.StrikeTarget(Status("bolt-charge"), HitOutcome.Kill)),
            On(new Trigger.StacksMaxed(Status("bolt-charge")), Buff("amplified")),
        ]);

        var report = RunLoop(ValidateBuild([charger, striker, GrenadeRefill]), GrenadeKill, RifleHit);

        Assert.True(report.IsSustainable());
        Assert.Equal([new OutcomeTally("Kills", 2), new OutcomeTally("Bolt Charge maxed", 1)], report.Outcomes);
    }

    [Fact]
    public void Spawned_pickups_are_counted_per_pickup_whether_collected_automatically_or_not()
    {
        var spawner = Element("spawner", ElementKind.Fragment,
            [On(new Trigger.KillAny(new DamageSource.AnySource()), new Outcome.Spawn(Pickup("orb-of-power"), 2), new Outcome.Spawn(Pickup("ionic-trace"), 1))]);

        var report = RunLoop(ValidateBuild([spawner]), RifleKill, PickUpOrbs);

        Assert.True(report.IsSustainable());
        Assert.Equal(
            [new OutcomeTally("Kills", 1), new OutcomeTally("Orb of Power spawned", 2), new OutcomeTally("Ionic Trace spawned", 1)],
            report.Outcomes);
    }

    [Fact]
    public void Uptime_is_the_number_of_steps_a_buff_was_active_after()
    {
        var dodge = Element("dodge-amp", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("amplified"), new Outcome.GrantEnergy(AbilityKind.ClassAbility, new EnergyGrant.Full()))]);
        var wait = new PlayerAction.Wait(Seconds.From(6m));

        var report = RunLoop(ValidateBuild([dodge]), Dodge, wait, wait, RifleKill);   // Amplified lasts 10s

        Assert.True(report.IsSustainable());
        Assert.Equal([new BuffUptime(Status("amplified"), "Amplified", Affinity.Arc, 2, 4)], report.Uptime);
        Assert.Equal(0.5m, report.Uptime[0].ComputeUptimeRatio());
    }

    [Fact]
    public void Unknown_values_and_chance_bullets_are_counted_in_the_steady_state()
    {
        var risky = Element("risky", ElementKind.Fragment,
        [
            new Rule(new Trigger.AbilityCast(AbilityKind.Grenade), [], [Energy(AbilityKind.Melee, new GameValue.Unknown())], Optional.None<string>(), Likelihood.Chance),
            On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), new Outcome.RestoreHealth(new GameValue.Unknown(), false)),
        ]);

        var report = RunLoop(ValidateBuild([risky, GrenadeRefill]), GrenadeKill);

        Assert.Equal(2, report.UnknownValues);
        Assert.Equal(1, report.ChanceRules);
    }

    [Fact]
    public void Sources_are_tallied_most_fired_first_then_in_order_of_appearance()
    {
        var once = Element("once", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("amplified"))]);
        var twice = Element("twice", ElementKind.Fragment,
        [
            On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("bolt-charge")),
            On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), Buff("bolt-charge")),
        ]);

        var report = RunLoop(ValidateBuild([once, twice, GrenadeRefill]), GrenadeKill);

        Assert.Equal([("twice", 2), ("once", 1), ("refill-grenade", 1)], report.Sources.Select(s => (s.Source.Value, s.Fired)));
        Assert.Equal("Twice", report.Sources[0].SourceName);
    }

    [Fact]
    public void An_empty_loop_runs_no_cycle_and_says_so()
    {
        var report = LoopRunning.RunLoop(ValidateBuild([]), "Empty", [], 10);

        Assert.Empty(report.Cycles);
        Assert.Equal((0, 10, 0), (report.CompletedCycles, report.MaxCycles, report.StepCount));
        Assert.False(report.IsSustainable());
        Assert.False(report.FindSteadyCycle().IsSome());
        Assert.Equal(new EnergyDelta(0m, 0m, 0m, 0m), report.ComputeNetEnergy());
        Assert.Empty(report.Sources);
        Assert.Empty(report.Uptime);
        Assert.Contains("no steps", LoopReportRendering.RenderLoopReport(report).ToPlainText());
    }

    [Fact]
    public void Fewer_than_one_cycle_runs_one()
    {
        var report = LoopRunning.RunLoop(ValidateBuild([GrenadeRefill]), "One", ToSteps(GrenadeKill), 0);

        Assert.Equal((1, 1), (report.MaxCycles, report.Cycles.Length));
        Assert.True(report.IsSustainable());
    }

    [Fact]
    public void Running_a_design_uses_its_name_and_steps()
    {
        var design = new LoopDesign("Designed", Optional.None<string>(), Optional.None<string>(), Optional.None<CatalogVersion>(),
            new SourceText("b", ""), ToSteps(GrenadeKill, RifleKill));

        var report = LoopRunning.RunLoop(ValidateBuild([GrenadeRefill]), design, 3);

        Assert.Equal(("Designed", 2, 3), (report.LoopName, report.StepCount, report.CompletedCycles));
    }
}

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
    private static readonly PlayerAction GrenadeKill = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One);
    private static readonly PlayerAction RifleKill = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One);
    private static readonly PlayerAction RifleHit = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Damage, TargetCount.One);
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

    private static readonly PlayerAction PowerKill = new PlayerAction.FireWeapon(WeaponSlot.Power, HitOutcome.Kill, TargetCount.One);

    [Fact]
    public void Abilities_never_run_out_so_a_loop_of_them_repeats_every_cycle()
    {
        var report = RunLoop(ValidateBuild([]), GrenadeKill, GrenadeKill, Dodge, GrenadeKill);

        Assert.Equal(10, report.Cycles.Length);
        Assert.Equal(10, report.CompletedCycles);
        Assert.True(report.IsRepeatable());
        Assert.All(report.Cycles, cycle => Assert.False(cycle.Blocked.IsSome()));
        Assert.Empty(report.Wasted);
        Assert.Equal(("Test loop", "Test build", 4), (report.LoopName, report.BuildName, report.StepCount));
    }

    [Fact]
    public void A_blocked_step_ends_the_run_after_its_cycle_is_played_to_the_end()
    {
        var report = RunLoop(ValidateBuild([]), GrenadeKill, PowerKill, RifleKill);

        var cycle = Assert.Single(report.Cycles);
        Assert.Equal(0, report.CompletedCycles);
        Assert.False(report.IsRepeatable());
        Assert.Equal(3, cycle.Resolutions.Length);
        var blocked = Assert.IsType<Optional<BlockedStep>.Some>(cycle.Blocked).Value;
        Assert.Equal((1, PowerKill), (blocked.StepIndex, blocked.Action));
        Assert.Contains("No weapon in the Power slot", blocked.Reason);
        Assert.Equal(new OutcomeTally("Kills", 2), report.Outcomes[0]);   // the blocked shot killed nothing
    }

    [Fact]
    public void Each_cycle_starts_where_the_previous_one_ended()
    {
        // A kill drops an orb only before the first pickup has amplified you, so cycle 2 has nothing to pick up.
        var spawner = Element("spawner", ElementKind.Fragment,
            [OnWhen(new Trigger.KillAny(new DamageSource.AnySource()), new Condition.LacksBuff(Status("amplified")), new Outcome.Spawn(Pickup("orb-of-power"), 1))]);
        var collector = Element("collector", ElementKind.Fragment, [On(new Trigger.PickUp(Pickup("orb-of-power")), Buff("amplified"))]);

        var report = RunLoop(ValidateBuild([spawner, collector]), RifleKill, PickUpOrbs);

        Assert.Equal(2, report.Cycles.Length);
        Assert.Equal(1, report.CompletedCycles);
        Assert.Equal(3, report.Cycles[1].Resolutions[0].State.Step);
        Assert.Equal(1, Assert.IsType<Optional<BlockedStep>.Some>(report.Cycles[1].Blocked).Value.StepIndex);
        Assert.Equal(1, report.FindSteadyCycle().Match(cycle => cycle.Value.Number, _ => 0));
    }

    [Fact]
    public void Pickups_left_on_the_ground_carry_over_into_the_next_cycle()
    {
        var spawner = Element("spawner", ElementKind.Fragment,
            [On(new Trigger.KillAny(new DamageSource.AnySource()), new Outcome.Spawn(Pickup("orb-of-power"), 1))]);
        var collector = Element("collector", ElementKind.Fragment, [On(new Trigger.PickUp(Pickup("orb-of-power")), Buff("bolt-charge"))]);

        var report = RunLoop(ValidateBuild([spawner, collector]), RifleKill, PickUpOrbs, RifleKill);

        Assert.True(report.IsRepeatable());
        Assert.Equal(1, report.Cycles[0].Resolutions[1].Fired.Count(f => f.Source.Value == "collector"));
        Assert.Equal([Pickup("orb-of-power")], report.Cycles[0].Resolutions[^1].State.Pickups);   // the last kill's orb carries over
        Assert.Equal(1, report.Cycles[1].Resolutions[1].Fired.Count(f => f.Source.Value == "collector"));   // one pickup step picks up one
    }

    [Fact]
    public void Rules_that_do_not_stack_are_tallied_as_wasted_and_not_as_fired()
    {
        var giver = Element("giver", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("bolt-charge"))]);
        var yielder = Element("yielder", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("bolt-charge")).NotStackingWith("giver")]);

        var report = RunLoop(ValidateBuild([yielder, giver]), GrenadeKill, GrenadeKill);

        Assert.Equal([new WastedTally(yielder.Id, "Yielder", Affinity.Arc, "Giver", 2)], report.Wasted);
        Assert.Equal(2, report.CountWasted());
        Assert.Equal([("giver", 2)], report.Sources.Select(s => (s.Source.Value, s.Fired)));
    }

    [Fact]
    public void When_no_cycle_completes_cycle_1_is_the_steady_state()
    {
        var counter = Element("counter", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("amplified"), Energy(AbilityKind.Grenade, new GameValue.Known(0.5m)))]);

        var report = RunLoop(ValidateBuild([counter]), GrenadeKill, PowerKill);

        Assert.Equal(0, report.CompletedCycles);
        Assert.Equal(1, report.FindSteadyCycle().Match(cycle => cycle.Value.Number, _ => 0));
        Assert.Equal([("counter", 1)], report.Sources.Select(s => (s.Source.Value, s.Fired)));
    }

    [Fact]
    public void Spawned_pickups_are_counted_per_pickup_whether_collected_automatically_or_not()
    {
        var spawner = Element("spawner", ElementKind.Fragment,
            [On(new Trigger.KillAny(new DamageSource.AnySource()), new Outcome.Spawn(Pickup("orb-of-power"), 2), new Outcome.Spawn(Pickup("ionic-trace"), 1))]);

        var report = RunLoop(ValidateBuild([spawner]), RifleKill, PickUpOrbs);

        Assert.True(report.IsRepeatable());
        Assert.Equal(
            [new OutcomeTally("Kills", 1), new OutcomeTally("Orb of Power spawned", 2), new OutcomeTally("Ionic Trace spawned", 1)],
            report.Outcomes);
    }

    [Fact]
    public void Unknown_values_and_chance_bullets_are_counted_in_the_steady_state()
    {
        var risky = Element("risky", ElementKind.Fragment,
        [
            new Rule(new Trigger.AbilityCast(AbilityKind.Grenade), [], [Energy(AbilityKind.Melee, new GameValue.Unknown())], Optional.None<string>(), Likelihood.Chance, []),
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
        Assert.False(report.IsRepeatable());
        Assert.False(report.FindSteadyCycle().IsSome());
        Assert.Empty(report.Wasted);
        Assert.Empty(report.Sources);
        Assert.Empty(report.Uptime);
        Assert.Contains("no steps", LoopReportRendering.RenderLoopReport(report).ToPlainText());
    }

    [Fact]
    public void Fewer_than_one_cycle_runs_one()
    {
        var report = LoopRunning.RunLoop(ValidateBuild([GrenadeRefill]), "One", ToSteps(GrenadeKill), 0);

        Assert.Equal((1, 1), (report.MaxCycles, report.Cycles.Length));
        Assert.True(report.IsRepeatable());
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

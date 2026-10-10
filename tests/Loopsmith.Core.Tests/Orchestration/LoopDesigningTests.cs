using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>The trigger palette of the designer: every ability always (ADRs D5), pickups when they land, target counts (D4).</summary>
public sealed class LoopDesigningTests
{
    private static readonly PlayerAction GrenadeKill = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One);
    private static readonly PlayerAction RifleKill = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One);

    private static DesignSession StartDesign(ValidatedBuild build) =>
        LoopDesigning.CreateSession(build, new LoopDesign(
            "Test", Optional.None<string>(), Optional.None<string>(), Optional.None<CatalogVersion>(), new SourceText("b", ""), []));

    private static DesignSession AppendSteps(DesignSession session, params PlayerAction[] actions) =>
        actions.Aggregate(session, (current, action) => LoopDesigning.AppendStep(current, action, Optional.None<string>()));

    [Fact]
    public void Every_ability_is_an_option_however_often_it_was_used()
    {
        var session = AppendSteps(StartDesign(ValidateBuild([])), GrenadeKill, GrenadeKill, GrenadeKill, new PlayerAction.UseClassAbility());

        var options = LoopDesigning.ListTriggerOptions(session);

        Assert.Equal(
            ["grenade:kill", "grenade", "melee:kill", "melee", "class", "super:kill", "super", "energy:kill", "energy", "wait:5"],
            options.Select(option => option.Token));
        Assert.All(options, option => Assert.False(option.IsNew));
        Assert.Equal("Grenade (kill)", options[0].Label);
    }

    [Fact]
    public void A_pickup_that_just_landed_is_new_and_only_then()
    {
        var spawner = Element("spawner", ElementKind.Fragment,
            [On(new Trigger.KillAny(new DamageSource.AnySource()), new Outcome.Spawn(Pickup("orb-of-power"), 2))]);
        var killed = AppendSteps(StartDesign(ValidateBuild([spawner])), RifleKill);
        var later = AppendSteps(killed, new PlayerAction.UseClassAbility());

        var justLanded = LoopDesigning.ListTriggerOptions(killed);
        var stillThere = LoopDesigning.ListTriggerOptions(later);

        var pickup = Assert.Single(justLanded, option => option.Group == TriggerGroup.Pickup);
        Assert.True(pickup.IsNew);
        Assert.Equal("pickup:orb-of-power", pickup.Token);
        Assert.Single(justLanded, option => option.IsNew);
        Assert.False(Assert.Single(stillThere, option => option.Group == TriggerGroup.Pickup).IsNew);
    }

    [Fact]
    public void Abilities_and_weapons_can_be_aimed_at_more_enemies_other_actions_cannot()
    {
        var five = TargetCount.From(5);

        var aimed = LoopDesigning.SetTargetCount(GrenadeKill, five);
        var shot = LoopDesigning.SetTargetCount(RifleKill, five);
        var dodge = LoopDesigning.SetTargetCount(new PlayerAction.UseClassAbility(), five);

        Assert.Equal(new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, five), aimed);
        Assert.Equal(new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, five), shot);
        Assert.Equal(new PlayerAction.UseClassAbility(), dodge);
        Assert.Equal(Optional.Some(five), LoopDesigning.ReadTargetCount(aimed));
        Assert.False(LoopDesigning.ReadTargetCount(dodge).IsSome());
    }

    [Fact]
    public void An_aimed_step_kills_every_target_in_the_analysis()
    {
        var session = AppendSteps(StartDesign(ValidateBuild([])), LoopDesigning.SetTargetCount(GrenadeKill, TargetCount.From(3)), RifleKill);

        var report = LoopDesigning.AnalyzeDesign(session, 2);

        Assert.Equal(new OutcomeTally("Kills", 4), report.Outcomes[0]);
        Assert.Contains("  - do: grenade:kill:3\n", LoopDesigning.ExportLoop(session));
    }
}

using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// The designer: the trigger palette (every ability always, ADRs D5; pickups when they land; target counts, D4) and
/// editing the steps (remove, move: the loop replays in its new order).
/// </summary>
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
            ["grenade:kill", "grenade", "melee:kill", "melee", "class", "super:kill", "super", "finisher", "energy:kill", "energy", "pack:new"],
            options.Select(option => option.Token));
        Assert.All(options, option => Assert.False(option.IsNew));
        Assert.Equal("Grenade (kill)", options[0].Label);
    }

    [Fact]
    public void A_finisher_is_offered_with_the_abilities_in_every_build()
    {
        var finisherMod = Element("finisher-mod", ElementKind.ArmorMod, [On(new Trigger.Finisher(), Buff("amplified"))]);
        var session = AppendSteps(StartDesign(ValidateBuild([], mods: [finisherMod])), GrenadeKill, new PlayerAction.PerformFinisher());

        var options = LoopDesigning.ListTriggerOptions(session);
        var without = LoopDesigning.ListTriggerOptions(StartDesign(ValidateBuild([])));

        var finisher = Assert.Single(options, option => option.Token == "finisher");
        Assert.Equal(("Finisher", TriggerGroup.Ability, false), (finisher.Label, finisher.Group, finisher.IsNew));
        Assert.Equal(Optional.None<TargetCount>(), LoopDesigning.ReadTargetCount(finisher.Action));
        var bare = Assert.Single(without, option => option.Token == "finisher");   // no rule reacts to it: still possible
        Assert.Equal(("Finisher", TriggerGroup.Ability), (bare.Label, bare.Group));
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
    public void An_aimed_step_keeps_its_target_count_in_the_analysis_and_the_file()
    {
        var session = AppendSteps(StartDesign(ValidateBuild([])), LoopDesigning.SetTargetCount(GrenadeKill, TargetCount.From(3)), RifleKill);

        var report = LoopDesigning.AnalyzeDesign(session);

        Assert.Equal(["grenade:kill:3", "energy:kill"], report.Steps.Select(step => step.Token));
        Assert.Contains("  - do: grenade:kill:3\n", LoopDesigning.ExportLoop(session));
    }

    [Fact]
    public void Removing_a_step_replays_the_rest_without_it()
    {
        var spawner = Element("spawner", ElementKind.Fragment,
            [On(new Trigger.KillAny(new DamageSource.AnySource()), new Outcome.Spawn(Pickup("orb-of-power"), 2))]);
        var collect = new PlayerAction.CollectPickups(PickupId.From("orb-of-power"));
        var session = LoopDesigning.AnnotateStep(AppendSteps(StartDesign(ValidateBuild([spawner])), RifleKill, collect, GrenadeKill), 2, Optional.Some("finish"));

        var removed = LoopDesigning.RemoveStep(session, 0);
        var outOfRange = LoopDesigning.RemoveStep(session, 3);

        Assert.Equal([collect, GrenadeKill], removed.Design.Steps.Select(step => step.Action));
        Assert.Equal(2, removed.Resolutions.Length);
        Assert.False(session.Resolutions[1].Blocked.IsSome());
        Assert.True(removed.Resolutions[0].Blocked.IsSome());                                    // no orb on the ground any more
        Assert.Equal(Optional.Some("finish"), removed.Design.Steps[1].Note);
        Assert.Same(session, outOfRange);
    }

    [Fact]
    public void Moving_a_step_replays_the_loop_in_its_new_order_with_its_note()
    {
        var spawner = Element("spawner", ElementKind.Fragment,
            [On(new Trigger.KillAny(new DamageSource.AnySource()), new Outcome.Spawn(Pickup("orb-of-power"), 2))]);
        var collect = new PlayerAction.CollectPickups(PickupId.From("orb-of-power"));
        var session = LoopDesigning.AnnotateStep(AppendSteps(StartDesign(ValidateBuild([spawner])), RifleKill, collect, GrenadeKill), 1, Optional.Some("grab it"));

        var early = LoopDesigning.MoveStep(session, 1, 0);
        var back = LoopDesigning.MoveStep(early, 0, 1);
        var last = LoopDesigning.MoveStep(session, 0, 2);

        Assert.Equal([collect, RifleKill, GrenadeKill], early.Design.Steps.Select(step => step.Action));
        Assert.True(early.Resolutions[0].Blocked.IsSome());                                      // nothing to collect before the kill
        Assert.Equal(Optional.Some("grab it"), early.Design.Steps[0].Note);
        Assert.Equal(session.Design.Steps, back.Design.Steps);
        Assert.False(back.Resolutions[1].Blocked.IsSome());
        Assert.Equal([collect, GrenadeKill, RifleKill], last.Design.Steps.Select(step => step.Action));
        Assert.Same(session, LoopDesigning.MoveStep(session, 1, 1));
        Assert.Same(session, LoopDesigning.MoveStep(session, 0, 3));
    }
}

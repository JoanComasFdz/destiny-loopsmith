using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Simulation;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.Simulation;

public class ActionResolutionTests
{
    private static readonly PlayerAction GrenadeKill = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill);

    private static Resolution ResolveOnce(ValidatedBuild build, PlayerAction action) =>
        ActionResolution.ResolveAction(build, ActionResolution.CreateInitialState(build), action);

    [Fact]
    public void Kill_sees_the_debuff_its_own_hit_applied()
    {
        var shock = Element("shock", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), new Outcome.DebuffTarget(Status("jolt"), Optional.None<Seconds>()))]);
        var flow = Element("flow", ElementKind.Fragment,
            [On(new Trigger.KillDebuffed(new DamageSource.AnySource(), [Status("jolt")]), Buff("amplified"))]);
        var build = ValidateBuild([shock, flow]);

        var resolution = ResolveOnce(build, GrenadeKill);

        Assert.Equal(["shock", "flow"], resolution.Fired.Select(f => f.Source.Value));
        Assert.Contains(resolution.State.Buffs, b => b.Status == Status("amplified"));
    }

    [Fact]
    public void Outcomes_of_one_event_apply_in_phase_order_not_equip_order()
    {
        var refund = Element("refund", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), Energy(AbilityKind.Melee, new GameValue.Known(0.1m)))]);
        var debuff = Element("debuff", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), new Outcome.DebuffTarget(Status("jolt"), Optional.None<Seconds>()))]);
        var build = ValidateBuild([refund, debuff]);

        var resolution = ResolveOnce(build, GrenadeKill);

        Assert.Equal(["debuff", "refund"], resolution.Fired.Select(f => f.Source.Value));
    }

    [Fact]
    public void Reaching_max_stacks_cascades_into_StacksMaxed()
    {
        var source = Element("source", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), Buff("bolt-charge", 3))]);
        var strike = Element("strike", ElementKind.Fragment,
            [On(new Trigger.StacksMaxed(Status("bolt-charge")), new Outcome.StrikeTarget(Status("bolt-charge"), HitOutcome.Damage), new Outcome.RemoveBuff(Status("bolt-charge")))]);
        var build = ValidateBuild([source, strike]);

        var resolution = ResolveOnce(build, GrenadeKill);

        var fired = Assert.Single(resolution.Fired, f => f.Source.Value == "strike");
        Assert.Equal(1, fired.Depth);
        Assert.DoesNotContain(resolution.State.Buffs, b => b.Status == Status("bolt-charge"));
    }

    [Fact]
    public void Auto_collected_pickups_cascade_and_manual_ones_wait_on_the_ground()
    {
        var spawner = Element("spawner", ElementKind.Fragment,
            [On(new Trigger.KillAny(new DamageSource.AnySource()), new Outcome.Spawn(Pickup("ionic-trace"), 1), new Outcome.Spawn(Pickup("orb-of-power"), 2))]);
        var collector = Element("collector", ElementKind.Fragment,
            [On(new Trigger.PickUp(Pickup("ionic-trace")), Buff("amplified")), On(new Trigger.PickUp(Pickup("orb-of-power")), Buff("bolt-charge"))]);
        var build = ValidateBuild([spawner, collector]);

        var kill = ResolveOnce(build, GrenadeKill);
        var pickup = ActionResolution.ResolveAction(build, kill.State, new PlayerAction.CollectPickups(Pickup("orb-of-power")));

        Assert.Contains(kill.State.Buffs, b => b.Status == Status("amplified"));
        Assert.Equal(2, kill.State.Pickups.Single().Count);
        Assert.Contains(new PlayerAction.CollectPickups(Pickup("orb-of-power")), kill.NowAvailable);
        Assert.Empty(pickup.State.Pickups);
        Assert.Equal(2, pickup.State.Buffs.Single(b => b.Status == Status("bolt-charge")).Stacks.Value);
    }

    [Fact]
    public void Unknown_amounts_are_never_applied_as_zero_or_anything_else()
    {
        var refund = Element("refund", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), Energy(AbilityKind.Grenade, new GameValue.Unknown()))]);
        var build = ValidateBuild([refund]);

        var resolution = ResolveOnce(build, GrenadeKill);

        var applied = resolution.Fired.Single().Outcomes.Single();
        Assert.Equal(Certainty.Unknown, applied.Certainty);
        Assert.Equal(0m, resolution.State.Abilities.Grenade.Energy.Value);
    }

    [Fact]
    public void Unknown_chunk_scalar_is_assumed_1x_and_flagged()
    {
        var refund = Element("refund", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), Energy(AbilityKind.Grenade, new GameValue.Known(0.25m)))]);
        var build = ValidateBuild([refund]);

        var resolution = ResolveOnce(build, GrenadeKill);

        Assert.Equal(Certainty.Assumed, resolution.Fired.Single().Outcomes.Single().Certainty);
        Assert.Equal(0.25m, resolution.State.Abilities.Grenade.Energy.Value);
    }

    [Fact]
    public void Known_chunk_scalar_scales_energy_by_ability_cost()
    {
        var grenade = Element("test-grenade", ElementKind.Grenade, [],
            ability: Optional.Some(Profile(AbilityKind.Grenade, chunkScalar: new GameValue.Known(0.5m))));
        var bomber = Element("bomber", ElementKind.ArmorMod,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), Energy(AbilityKind.Grenade, new GameValue.PerModCount([0.12m, 0.17m, 0.20m])))]);
        var build = ValidateBuild([], [bomber], [bomber.Id, bomber.Id, bomber.Id], grenade);

        var resolution = ResolveOnce(build, GrenadeKill);

        Assert.Equal(0.10m, resolution.State.Abilities.Grenade.Energy.Value);
        Assert.Equal(Certainty.Known, resolution.Fired.Single().Outcomes.Single().Certainty);
    }

    [Fact]
    public void Self_feeding_rules_terminate()
    {
        var loop = Element("loop", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AnySource()), new Outcome.StrikeTarget(Status("jolt"), HitOutcome.Damage))]);
        var build = ValidateBuild([loop]);

        var resolution = ResolveOnce(build, GrenadeKill);

        Assert.InRange(resolution.Fired.Length, 1, EventCascading.MaxCascadeDepth + 1);
    }

    [Fact]
    public void Conditional_passive_adds_extra_stacks_only_while_its_condition_holds()
    {
        var frequency = Element("frequency", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("amplified"))],
            [new PassiveRule(new Passive.ExtraStacks(Status("bolt-charge"), StackCount.From(1)), [new Condition.HasBuff(Status("amplified"))], Optional.None<string>())]);
        var source = Element("source", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("bolt-charge"))]);
        var build = ValidateBuild([frequency, source]);

        var plain = ResolveOnce(build, GrenadeKill);
        var amplified = ActionResolution.ResolveAction(build, ResolveOnce(build, new PlayerAction.UseClassAbility()).State, GrenadeKill);

        Assert.Equal(1, plain.State.Buffs.Single(b => b.Status == Status("bolt-charge")).Stacks.Value);
        Assert.Equal(2, amplified.State.Buffs.Single(b => b.Status == Status("bolt-charge")).Stacks.Value);
    }

    [Fact]
    public void Waiting_expires_timed_statuses()
    {
        var flow = Element("flow", ElementKind.Fragment,
            [On(new Trigger.KillAny(new DamageSource.AnySource()), Buff("amplified"), new Outcome.DebuffTarget(Status("jolt"), Optional.None<Seconds>()))]);
        var build = ValidateBuild([flow]);

        var kill = ResolveOnce(build, GrenadeKill);
        var short_ = ActionResolution.ResolveAction(build, kill.State, new PlayerAction.Wait(Seconds.From(5m)));
        var long_ = ActionResolution.ResolveAction(build, short_.State, new PlayerAction.Wait(Seconds.From(6m)));

        Assert.Contains(short_.State.Buffs, b => b.Status == Status("amplified"));
        Assert.Empty(short_.State.Target.Debuffs);
        Assert.Empty(long_.State.Buffs);
    }

    [Fact]
    public void Casting_without_energy_does_nothing_and_says_so()
    {
        var build = ValidateBuild([]);

        var first = ResolveOnce(build, GrenadeKill);
        var second = ActionResolution.ResolveAction(build, first.State, GrenadeKill);

        Assert.Empty(second.Fired);
        Assert.Contains(second.Notes, n => n.Contains("Not enough"));
        Assert.DoesNotContain(second.NowAvailable, a => a is PlayerAction.CastAbility { Kind: OffensiveAbility.Grenade });
    }

    [Fact]
    public void Same_build_state_and_action_give_the_same_resolution()
    {
        var shock = Element("shock", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AnySource()), new Outcome.DebuffTarget(Status("jolt"), Optional.None<Seconds>()), Buff("bolt-charge"))]);
        var build = ValidateBuild([shock]);
        var actions = ImmutableArray.Create<PlayerAction>(GrenadeKill, new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill));

        var first = ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(build), actions);
        var second = ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(build), actions);

        Assert.Equal(
            first.SelectMany(r => r.Fired).Select(f => $"{f.Source}@{f.Depth}"),
            second.SelectMany(r => r.Fired).Select(f => $"{f.Source}@{f.Depth}"));
        Assert.Equal(first[^1].State.Buffs, second[^1].State.Buffs);
    }
}

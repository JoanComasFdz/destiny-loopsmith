using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.Simulation;
using Loopsmith.Core.TraceRendering;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.Simulation;

public class ActionResolutionTests
{
    private static readonly PlayerAction GrenadeKill = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One);

    private static Resolution ResolveOnce(ValidatedBuild build, PlayerAction action) =>
        ActionResolution.ResolveAction(build, ActionResolution.CreateInitialState(), action);

    private static string DescribeTrace(ValidatedBuild build, Resolution resolution) =>
        string.Join("\n", TraceRenderer.RenderResolution(build, resolution, new TraceOptions(false, false, false)).Select(line => line.ToPlainText()));

    private static readonly BuildElement Giver =
        Element("giver", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("bolt-charge"))]);

    private static readonly BuildElement Yielder =
        Element("yielder", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("bolt-charge")).NotStackingWith("giver")]);

    private static readonly PlayerAction Dodge = new PlayerAction.UseClassAbility();

    private static readonly PlayerAction AirMove = new PlayerAction.UseClassAbility(Airborne: true);

    private static readonly BuildElement AirOnly = Element("air-only", ElementKind.Fragment,
        [On(new Trigger.AbilityCast(AbilityKind.ClassAbility, Airborne: true), Buff("amplified"))]);

    private static readonly BuildElement AnyUse = Element("any-use", ElementKind.Fragment,
        [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("bolt-charge"))]);

    [Fact]
    public void An_airborne_trigger_fires_only_on_the_air_move_and_a_plain_one_on_both()
    {
        var build = ValidateBuild([AirOnly, AnyUse]);

        var dodge = ResolveOnce(build, Dodge);
        var airMove = ResolveOnce(build, AirMove);

        Assert.Equal(["any-use"], dodge.Fired.Select(f => f.Source.Value));
        Assert.Equal(["air-only", "any-use"], airMove.Fired.Select(f => f.Source.Value).Order());
        Assert.Contains("Class ability in the air → Amplified (10s) [Air Only]", DescribeTrace(build, airMove));
    }

    [Fact]
    public void The_air_move_is_offered_only_when_an_equipped_rule_needs_it()
    {
        var initial = ActionResolution.CreateInitialState();

        Assert.Contains(AirMove, ActionResolution.ListAvailableActions(ValidateBuild([AirOnly]), initial));
        Assert.DoesNotContain(AirMove, ActionResolution.ListAvailableActions(ValidateBuild([AnyUse]), initial));
    }

    /// <summary>Reacts to every Bolt Charge grant, so the trace shows each <see cref="GameEvent.BuffGained"/>.</summary>
    private static readonly BuildElement GainWatcher = Element("gain-watcher", ElementKind.Fragment,
        [On(new Trigger.BuffGained(Status("bolt-charge")), Energy(AbilityKind.Melee, new GameValue.Known(0.025m)))]);

    private static PlayerAction DeclareMax(string status) => new PlayerAction.Declare(new StateDeclaration.ReachMax(Status(status)));

    private static PlayerAction DeclareEnd(string status) => new PlayerAction.Declare(new StateDeclaration.EndStatus(Status(status)));

    [Fact]
    public void A_grant_of_a_stacking_buff_raises_BuffGained_every_time_and_never_counts_to_a_maximum()
    {
        var charger = Element("charger", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("bolt-charge", 2))]);
        var maxed = Element("maxed", ElementKind.Fragment, [On(new Trigger.StacksMaxed(Status("bolt-charge")), Buff("amplified"))]);
        var build = ValidateBuild([charger, maxed, GainWatcher]);
        var actions = ImmutableArray.Create(GrenadeKill, GrenadeKill, GrenadeKill);

        var resolutions = ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), actions);

        Assert.All(resolutions, resolution => Assert.Single(resolution.Fired, f => f.Trigger is GameEvent.BuffGained));
        Assert.Equal([new ActiveBuff(Status("bolt-charge"), AtMax: false)], resolutions[^1].State.Buffs);
        Assert.DoesNotContain(resolutions.SelectMany(r => r.Fired), f => f.Source.Value == "maxed");
        Assert.Contains("+2 Bolt Charge [Charger]", DescribeTrace(build, resolutions[^1]));
    }

    [Fact]
    public void Declaring_a_buff_at_max_raises_StacksMaxed_and_the_atMax_guard_holds_until_it_is_consumed()
    {
        var charger = Element("charger", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("bolt-charge"))]);
        var maxed = Element("maxed", ElementKind.Fragment, [On(new Trigger.StacksMaxed(Status("bolt-charge")), Buff("amplified"))]);
        var discharge = Element("discharge", ElementKind.Fragment,
            [OnWhen(new Trigger.Damage(new DamageSource.AnyAbility()), new Condition.AtMax(Status("bolt-charge")),
                new Outcome.RemoveBuff(Status("bolt-charge")), new Outcome.StrikeTarget(Status("bolt-charge"), HitOutcome.Damage))]);
        var build = ValidateBuild([charger, maxed, discharge]);

        var charged = ResolveOnce(build, Dodge);
        var declared = ActionResolution.ResolveAction(build, charged.State, DeclareMax("bolt-charge"));
        var thrown = ActionResolution.ResolveAction(build, declared.State, GrenadeKill);

        Assert.False(declared.Blocked.IsSome());
        Assert.Equal([new GameEvent.StacksMaxed(Status("bolt-charge"))], declared.Fired.Select(f => f.Trigger));
        Assert.Contains(new ActiveBuff(Status("bolt-charge"), AtMax: true), declared.State.Buffs);
        Assert.Single(thrown.Fired, f => f.Source.Value == "discharge");
        Assert.DoesNotContain(thrown.State.Buffs, b => b.Status == Status("bolt-charge"));
        Assert.Equal("Bolt Charge at max", build.Catalog.Glossary.DescribeAction(DeclareMax("bolt-charge"), build.Build));
        Assert.Contains("Max Bolt Charge → Amplified (10s) [Maxed]", DescribeTrace(build, declared));
    }

    [Fact]
    public void A_gain_keeps_a_declared_maximum()
    {
        var charger = Element("charger", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("bolt-charge"))]);
        var build = ValidateBuild([charger, GainWatcher]);

        var charged = ResolveOnce(build, Dodge);
        var declared = ActionResolution.ResolveAction(build, charged.State, DeclareMax("bolt-charge"));
        var gained = ActionResolution.ResolveAction(build, declared.State, Dodge);

        Assert.Equal([new ActiveBuff(Status("bolt-charge"), AtMax: true)], gained.State.Buffs);
        Assert.Equal([new GameEvent.BuffGained(Status("bolt-charge"))], gained.Fired.Select(f => f.Trigger).OfType<GameEvent.BuffGained>());
    }

    [Fact]
    public void A_declaration_that_does_not_hold_is_a_blocked_step_checked_in_order()
    {
        var charger = Element("charger", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("bolt-charge"), Buff("amplified"))]);
        var build = ValidateBuild([charger]);
        var charged = ResolveOnce(build, Dodge);
        var declared = ActionResolution.ResolveAction(build, charged.State, DeclareMax("bolt-charge"));

        string ReadBlocked(GameState state, PlayerAction action) =>
            Assert.IsType<Optional<string>.Some>(ActionResolution.ResolveAction(build, state, action).Blocked).Value;

        Assert.Equal("No buff 'void-charge' in the rules — nothing to declare.", ReadBlocked(charged.State, DeclareMax("void-charge")));
        Assert.Equal("Jolt is a debuff — only a buff on you can be at max.", ReadBlocked(charged.State, DeclareMax("jolt")));
        Assert.Equal("Amplified doesn't stack — end it with end:amplified.", ReadBlocked(charged.State, DeclareMax("amplified")));
        Assert.Equal("Bolt Charge isn't active — nothing to declare at max.", ReadBlocked(ActionResolution.CreateInitialState(), DeclareMax("bolt-charge")));
        Assert.Equal("Bolt Charge is already at max.", ReadBlocked(declared.State, DeclareMax("bolt-charge")));
        Assert.Equal("Jolt isn't active — nothing ends.", ReadBlocked(charged.State, DeclareEnd("jolt")));
        Assert.Equal("No buff or debuff 'void-charge' in the rules — nothing ends.", ReadBlocked(charged.State, DeclareEnd("void-charge")));
    }

    [Fact]
    public void A_status_ends_only_when_a_rule_consumes_it_or_the_player_declares_it_ended()
    {
        var flow = Element("flow", ElementKind.Fragment,
            [On(new Trigger.KillAny(new DamageSource.AnySource()), Buff("amplified"), new Outcome.DebuffTarget(Status("jolt"), Optional.None<Seconds>()))]);
        var build = ValidateBuild([flow]);
        var actions = ImmutableArray.Create(GrenadeKill, Dodge, Dodge, Dodge, DeclareEnd("amplified"), DeclareEnd("jolt"));

        var resolutions = ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), actions);

        Assert.Contains(resolutions[3].State.Buffs, b => b.Status == Status("amplified"));
        Assert.Equal([Status("jolt")], resolutions[4].State.Target.Debuffs);
        Assert.Empty(resolutions[4].State.Buffs);
        Assert.Empty(resolutions[5].State.Target.Debuffs);
        Assert.All(resolutions[4..], resolution => Assert.Empty(resolution.Fired));
        Assert.Equal("Amplified ends", build.Catalog.Glossary.DescribeAction(DeclareEnd("amplified"), build.Build));
    }

    [Fact]
    public void The_declarations_offered_are_the_ones_that_hold()
    {
        var charger = Element("charger", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("bolt-charge"), Buff("amplified"), new Outcome.DebuffTarget(Status("jolt"), Optional.None<Seconds>()))]);
        var build = ValidateBuild([charger]);

        var charged = ResolveOnce(build, Dodge);
        var declared = ActionResolution.ResolveAction(build, charged.State, DeclareMax("bolt-charge"));

        Assert.Equal([new StateDeclaration.NewPack()], ActionResolution.ListDeclarations(build, ActionResolution.CreateInitialState()));
        Assert.Equal(
            ["pack:new", "max:bolt-charge", "end:bolt-charge", "end:amplified", "end:jolt"],
            ActionResolution.ListDeclarations(build, charged.State).Select(d => ((PlayerAction)new PlayerAction.Declare(d)).ToActionToken()));
        Assert.DoesNotContain(DeclareMax("bolt-charge"), declared.NowAvailable);
        Assert.Contains(DeclareEnd("bolt-charge"), declared.NowAvailable);
    }

    [Fact]
    public void A_new_pack_has_none_of_the_old_packs_debuffs_and_keeps_what_is_on_you_and_the_ground()
    {
        var charger = Element("charger", ElementKind.Fragment,
        [
            On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("amplified"), new Outcome.DebuffTarget(Status("jolt"), Optional.None<Seconds>()),
                new Outcome.Spawn(Pickup("orb-of-power"), 1)),
        ]);
        var build = ValidateBuild([charger]);
        var charged = ResolveOnce(build, Dodge);

        var fresh = ActionResolution.ResolveAction(build, charged.State, new PlayerAction.Declare(new StateDeclaration.NewPack()));
        var again = ActionResolution.ResolveAction(build, fresh.State, new PlayerAction.Declare(new StateDeclaration.NewPack()));

        Assert.Empty(fresh.State.Target.Debuffs);
        Assert.Equal([Status("amplified")], fresh.State.Buffs.Select(buff => buff.Status));
        Assert.Equal([Pickup("orb-of-power")], fresh.State.Pickups);
        Assert.Empty(fresh.Fired);
        Assert.False(again.Blocked.IsSome());   // a clean pack can be new again: it always holds
    }

    [Fact]
    public void A_rule_that_does_not_stack_gives_way_to_the_other_elements_rule_on_the_same_event()
    {
        var build = ValidateBuild([Yielder, Giver]);

        var resolution = ResolveOnce(build, GrenadeKill);

        var yielded = resolution.Fired.Single(f => f.Source.Value == "yielder");
        Assert.Equal(Optional.Some("Giver"), yielded.NotStackedWith);
        Assert.Empty(yielded.Outcomes);
        Assert.Equal([new ActiveBuff(Status("bolt-charge"), AtMax: false)], resolution.State.Buffs);
        Assert.Contains("+1 Bolt Charge [Giver] + doesn't stack with Giver [Yielder]", DescribeTrace(build, resolution));
    }

    [Fact]
    public void Without_the_other_element_a_rule_that_does_not_stack_applies_as_usual()
    {
        var build = ValidateBuild([Yielder]);

        var resolution = ResolveOnce(build, GrenadeKill);

        var fired = resolution.Fired.Single(f => f.Source.Value == "yielder");
        Assert.False(fired.NotStackedWith.IsSome());
        Assert.Single(fired.Outcomes);
        Assert.Contains(resolution.State.Buffs, b => b.Status == Status("bolt-charge"));
    }

    [Fact]
    public void The_build_check_warns_that_a_rule_which_does_not_stack_is_wasted()
    {
        var both = ValidateBuild([Yielder, Giver]);
        var alone = ValidateBuild([Yielder]);

        var warning = Assert.Single(both.Issues, issue => issue.Message.Contains("doesn't stack"));
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal("'Yielder': +1 Bolt Charge on \"Grenade thrown\" doesn't stack with 'Giver' — with both equipped, it is wasted.", warning.Message);
        Assert.DoesNotContain(alone.Issues, issue => issue.Message.Contains("doesn't stack"));
    }

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
    public void A_grant_never_raises_StacksMaxed_however_big_it_is()
    {
        var source = Element("source", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), Buff("bolt-charge", 3))]);
        var strike = Element("strike", ElementKind.Fragment,
            [On(new Trigger.StacksMaxed(Status("bolt-charge")), new Outcome.StrikeTarget(Status("bolt-charge"), HitOutcome.Damage), new Outcome.RemoveBuff(Status("bolt-charge")))]);
        var build = ValidateBuild([source, strike]);

        var resolution = ResolveOnce(build, GrenadeKill);

        Assert.DoesNotContain(resolution.Fired, f => f.Source.Value == "strike");
        Assert.Contains(resolution.State.Buffs, b => b.Status == Status("bolt-charge"));
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
        Assert.Equal([Pickup("orb-of-power")], kill.State.Pickups);
        Assert.Contains("2× Orb of Power [Spawner]", DescribeTrace(build, kill));
        Assert.Contains(new PlayerAction.CollectPickups(Pickup("orb-of-power")), kill.NowAvailable);
        Assert.Empty(pickup.State.Pickups);
        Assert.Single(pickup.Fired, f => f.Source.Value == "collector");
        Assert.Contains(pickup.State.Buffs, b => b.Status == Status("bolt-charge"));
    }

    [Fact]
    public void Energy_refunds_are_explained_with_their_amount_but_change_no_state()
    {
        var refund = Element("refund", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), Energy(AbilityKind.Grenade, new GameValue.Known(0.25m)), new Outcome.ResetCooldown(AbilityKind.Melee))]);
        var build = ValidateBuild([refund]);
        var initial = ActionResolution.CreateInitialState();

        var resolution = ActionResolution.ResolveAction(build, initial, GrenadeKill);

        var outcomes = resolution.Fired.Single().Outcomes;
        Assert.Equal([Certainty.Known, Certainty.Known], outcomes.Select(o => o.Certainty));
        Assert.All(outcomes, outcome => Assert.False(outcome.Caveat.IsSome()));
        Assert.Equal(initial with { Step = 1 }, resolution.State with { Target = initial.Target });
    }

    [Fact]
    public void Unknown_refunds_stay_unknown_never_zero()
    {
        var refund = Element("refund", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), Energy(AbilityKind.Grenade, new GameValue.Unknown()))]);
        var build = ValidateBuild([refund]);

        var resolution = ResolveOnce(build, GrenadeKill);

        var applied = resolution.Fired.Single().Outcomes.Single();
        Assert.Equal(Certainty.Unknown, applied.Certainty);
        Assert.Equal(Optional.Some("amount unknown"), applied.Caveat);
    }

    [Fact]
    public void Refunds_resolve_for_the_copies_equipped_and_ignore_chunk_scalars()
    {
        var grenade = Element("test-grenade", ElementKind.Grenade, [],
            ability: Optional.Some(Profile(AbilityKind.Grenade, chunkScalar: new GameValue.Known(0.5m))));
        var bomber = Element("bomber", ElementKind.ArmorMod,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), Energy(AbilityKind.Grenade, new GameValue.PerModCount([0.12m, 0.17m, 0.20m])))]);
        var build = ValidateBuild([], [bomber], [bomber.Id, bomber.Id, bomber.Id], grenade);

        var resolution = ResolveOnce(build, GrenadeKill);

        var applied = resolution.Fired.Single().Outcomes.Single();
        Assert.Equal(Certainty.Known, applied.Certainty);
        Assert.Contains("+20% grenade energy", DescribeTrace(build, resolution));
    }

    [Fact]
    public void Converting_stacks_consumes_the_buff_and_shows_the_energy_per_stack_as_a_fact()
    {
        var charger = Element("charger", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("bolt-charge", 2))]);
        var kickstart = Element("kickstart", ElementKind.ArmorMod,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), new Outcome.ConvertStacksToEnergy(Status("bolt-charge"), AbilityKind.Grenade, new GameValue.Approximate(0.1m)))]);
        var build = ValidateBuild([charger], [kickstart]);

        var charged = ResolveOnce(build, new PlayerAction.UseClassAbility());
        var thrown = ActionResolution.ResolveAction(build, charged.State, GrenadeKill);
        var again = ActionResolution.ResolveAction(build, thrown.State, GrenadeKill);

        var applied = thrown.Fired.Single(f => f.Source.Value == "kickstart").Outcomes.Single();
        Assert.DoesNotContain(thrown.State.Buffs, b => b.Status == Status("bolt-charge"));
        Assert.Equal(Certainty.Approximate, applied.Certainty);
        Assert.False(applied.Caveat.IsSome());
        Assert.Contains("spends Bolt Charge → +~10% grenade energy per stack [Kickstart]", DescribeTrace(build, thrown));
        Assert.Equal(Optional.Some("nothing to consume"), again.Fired.Single(f => f.Source.Value == "kickstart").Outcomes.Single().Caveat);
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
    public void A_conditional_passive_names_its_extra_stacks_on_the_grant_while_its_condition_holds()
    {
        var frequency = Element("frequency", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("amplified"))],
            [new PassiveRule(new Passive.ExtraStacks(Status("bolt-charge"), StackCount.From(1)), [new Condition.HasBuff(Status("amplified"))], Optional.None<string>())]);
        var source = Element("source", ElementKind.Fragment,
            [On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("bolt-charge"))]);
        var build = ValidateBuild([frequency, source]);

        var plain = ResolveOnce(build, GrenadeKill);
        var amplified = ActionResolution.ResolveAction(build, ResolveOnce(build, new PlayerAction.UseClassAbility()).State, GrenadeKill);

        Assert.False(plain.Fired.Single(f => f.Source.Value == "source").Outcomes.Single().Caveat.IsSome());
        Assert.Equal(Optional.Some("+1 from Frequency"), amplified.Fired.Single(f => f.Source.Value == "source").Outcomes.Single().Caveat);
        Assert.Equal(plain.State.Buffs, amplified.State.Buffs.Where(b => b.Status == Status("bolt-charge")).ToImmutableArray());
    }

    [Fact]
    public void Abilities_can_always_be_used_there_is_no_energy_to_run_out_of()
    {
        var counter = Element("counter", ElementKind.Fragment, [On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("bolt-charge"))]);
        var build = ValidateBuild([counter]);
        var actions = ImmutableArray.Create(GrenadeKill, GrenadeKill, GrenadeKill, new PlayerAction.UseClassAbility(), new PlayerAction.UseClassAbility());

        var resolutions = ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), actions);

        Assert.All(resolutions, resolution => Assert.False(resolution.Blocked.IsSome()));
        Assert.All(resolutions, resolution => Assert.Empty(resolution.Notes));
        Assert.Contains(resolutions[^1].State.Buffs, b => b.Status == Status("bolt-charge"));
        Assert.Contains(GrenadeKill, resolutions[^1].NowAvailable);
        Assert.Contains(new PlayerAction.UseClassAbility(), resolutions[^1].NowAvailable);
    }

    [Fact]
    public void Only_a_missing_pickup_an_empty_weapon_slot_or_a_declaration_that_does_not_hold_blocks_a_step()
    {
        var build = ValidateBuild([]);

        var noOrb = ResolveOnce(build, new PlayerAction.CollectPickups(Pickup("orb-of-power")));
        var noPowerWeapon = ResolveOnce(build, new PlayerAction.FireWeapon(WeaponSlot.Power, HitOutcome.Kill, TargetCount.One));

        Assert.Contains("No orb-of-power on the ground", Assert.IsType<Optional<string>.Some>(noOrb.Blocked).Value);
        Assert.Contains("No weapon in the Power slot", Assert.IsType<Optional<string>.Some>(noPowerWeapon.Blocked).Value);
    }

    // ── N targets (ADRs D4) ───────────────────────────────────────────────────────

    /// <summary>One For All: "hitting three separate targets … grants increased damage" — inline, not from the rules.</summary>
    private static readonly BuildElement OneForAll = Element("one-for-all", ElementKind.Fragment,   // a weapon perk in game; the test catalog equips fragments
        [On(new Trigger.DamageMultiple(new DamageSource.AnyWeapon(), TargetCount.From(3)), Buff("amplified"))]);

    private static PlayerAction RifleAt(HitOutcome hit, int targets) =>
        new PlayerAction.FireWeapon(WeaponSlot.Energy, hit, TargetCount.From(targets));

    [Fact]
    public void An_action_against_N_enemies_hits_each_then_kills_each_then_counts_them_once()
    {
        var probe = Element("probe", ElementKind.Fragment,
        [
            On(new Trigger.AbilityCast(AbilityKind.Grenade), Buff("amplified")),
            On(new Trigger.Damage(new DamageSource.AnySource()), Buff("amplified")),
            On(new Trigger.KillAny(new DamageSource.AnySource()), Buff("amplified")),
            On(new Trigger.DamageMultiple(new DamageSource.AnySource(), TargetCount.One), Buff("amplified")),
        ]);
        var build = ValidateBuild([probe]);

        var resolution = ResolveOnce(build, new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.From(3)));

        Assert.Equal(
            ["cast", "damage", "damage", "damage", "kill", "kill", "kill", "3 targets Kill"],
            resolution.Fired.Select(fired => fired.Trigger switch
            {
                GameEvent.AbilityCast => "cast",
                GameEvent.Damaged => "damage",
                GameEvent.Killed => "kill",
                GameEvent.TargetsHit struck => $"{struck.Targets.Value} targets {struck.Hit}",
                _ => "other",
            }));
        Assert.All(resolution.Fired, fired => Assert.Equal(0, fired.Depth));
    }

    [Fact]
    public void Kill_actions_count_every_target_in_the_label_and_the_token()
    {
        var build = ValidateBuild([]);
        var killThree = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.From(3));
        var hitFive = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Damage, TargetCount.From(5));

        Assert.Equal("Grenade (kill 3)", build.Catalog.Glossary.DescribeAction(killThree, build.Build));
        Assert.Equal("Test Rifle (hit 5)", build.Catalog.Glossary.DescribeAction(hitFive, build.Build));
        Assert.Equal("Grenade (kill)", build.Catalog.Glossary.DescribeAction(GrenadeKill, build.Build));
        Assert.Equal(["grenade:kill:3", "energy:hit:5", "grenade:kill"], new PlayerAction[] { killThree, hitFive, GrenadeKill }.Select(a => a.ToActionToken()));
    }

    [Fact]
    public void Each_enemy_cascades_fully_so_later_hits_see_earlier_debuffs()
    {
        var shock = Element("shock", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), new Outcome.DebuffTarget(Status("jolt"), Optional.None<Seconds>()))]);
        var flow = Element("flow", ElementKind.Fragment,
            [On(new Trigger.KillDebuffed(new DamageSource.AnySource(), [Status("jolt")]), Buff("bolt-charge"))]);
        var build = ValidateBuild([shock, flow, GainWatcher]);

        var resolution = ResolveOnce(build, new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.From(3)));

        Assert.Equal(3, resolution.Fired.Count(f => f.Source.Value == "shock"));
        Assert.Equal(3, resolution.Fired.Count(f => f.Source.Value == "flow"));
        Assert.Equal(3, resolution.Fired.Count(f => f.Trigger is GameEvent.BuffGained));
    }

    [Fact]
    public void A_hit_at_least_N_trigger_fires_once_per_action_that_hits_enough_enemies()
    {
        var build = ValidateBuild([OneForAll]);

        var two = ResolveOnce(build, RifleAt(HitOutcome.Damage, 2));
        var three = ResolveOnce(build, RifleAt(HitOutcome.Damage, 3));
        var fiveKills = ResolveOnce(build, RifleAt(HitOutcome.Kill, 5));
        var grenade = ResolveOnce(build, new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage, TargetCount.From(3)));

        Assert.DoesNotContain(two.Fired, f => f.Source.Value == "one-for-all");
        var fired = Assert.Single(three.Fired, f => f.Source.Value == "one-for-all");
        Assert.Equal(new GameEvent.TargetsHit(new DamageOrigin.Weapon(WeaponSlot.Energy, DamageType.Arc), TargetCount.From(3), HitOutcome.Damage), fired.Trigger);
        Assert.Single(fiveKills.Fired, f => f.Source.Value == "one-for-all");   // a kill hits too
        Assert.DoesNotContain(grenade.Fired, f => f.Source.Value == "one-for-all");   // not a weapon
        Assert.Equal("Hit 3+ enemies with weapon", build.Catalog.Glossary.DescribeTrigger(OneForAll.Rules[0].On));
        Assert.Contains(
            "  Test Rifle hit 3 enemies → Amplified (10s) [One For All]",
            TraceRenderer.RenderResolution(build, three, new TraceOptions(false, false, false)).Select(line => line.ToPlainText()));
    }

    [Fact]
    public void A_kill_at_least_N_trigger_needs_a_kill_action()
    {
        var multikill = Element("multikill", ElementKind.Fragment,
            [On(new Trigger.KillMultiple(new DamageSource.AbilityOf(AbilityKind.Grenade), TargetCount.From(2)), new Outcome.Spawn(Pickup("orb-of-power"), 1))]);
        var build = ValidateBuild([multikill]);

        var hitTwo = ResolveOnce(build, new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage, TargetCount.From(2)));
        var killOne = ResolveOnce(build, GrenadeKill);
        var killTwo = ResolveOnce(build, new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.From(2)));

        Assert.Empty(hitTwo.Fired);
        Assert.Empty(killOne.Fired);
        Assert.Single(killTwo.Fired);
        Assert.Equal([Pickup("orb-of-power")], killTwo.State.Pickups);
    }

    [Fact]
    public void Same_build_state_and_action_give_the_same_resolution()
    {
        var shock = Element("shock", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AnySource()), new Outcome.DebuffTarget(Status("jolt"), Optional.None<Seconds>()), Buff("bolt-charge"))]);
        var build = ValidateBuild([shock]);
        var actions = ImmutableArray.Create<PlayerAction>(GrenadeKill, new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One));

        var first = ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), actions);
        var second = ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), actions);

        Assert.Equal(
            first.SelectMany(r => r.Fired).Select(f => $"{f.Source}@{f.Depth}"),
            second.SelectMany(r => r.Fired).Select(f => $"{f.Source}@{f.Depth}"));
        Assert.Equal(first[^1].State.Buffs, second[^1].State.Buffs);
    }
}

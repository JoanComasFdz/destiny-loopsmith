using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.Simulation;
using Loopsmith.Core.TraceRendering;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.Simulation;

/// <summary>
/// ADRs D33: Armor Charge is counted. Sure stacks come from rules that always fire, chance stacks from chance rules (and
/// what they set off); the cap holds; a spend takes them off; a rule that needs more than the loop holds doesn't fire, and
/// one that has them only with chance stacks fires as a chance.
/// </summary>
public sealed class StackCountingTests
{
    private static readonly StatusId ArmorCharge = Status("armor-charge");
    private static readonly PickupId Orb = Pickup("orb-of-power");
    private static readonly PlayerAction Dodge = new PlayerAction.UseClassAbility();
    private static readonly PlayerAction PickUpOrb = new PlayerAction.CollectPickups(Pickup("orb-of-power"));
    private static readonly PlayerAction Finisher = new PlayerAction.PerformFinisher();
    private static readonly PlayerAction GrenadeKill = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One);

    private static PlayerAction RifleKills(int kills) =>
        new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.From(kills));

    private static BuildElement Mod(string id, ImmutableArray<Rule> rules, ImmutableArray<PassiveRule> passives = default) =>
        Element(id, ElementKind.ArmorMod, rules, passives) with { Affinity = Affinity.Neutral };

    /// <summary>The keyword every build has: an Orb of Power picked up gives one Armor Charge.</summary>
    private static readonly BuildElement OrbOfPower = Element("orb-of-power", ElementKind.Keyword,
        [On(new Trigger.PickUp(Pickup("orb-of-power")), Buff("armor-charge"))]) with { Affinity = Affinity.Neutral };

    /// <summary>Reaper: a dodge arms it, and the next weapon kill drops an orb — for sure.</summary>
    private static readonly BuildElement Reaper = Mod("reaper",
    [
        On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("amplified")),
        OnWhen(new Trigger.KillAny(new DamageSource.AnyWeapon()), new Condition.HasBuff(Status("amplified")),
            new Outcome.RemoveBuff(Status("amplified")), new Outcome.Spawn(Orb, 1)),
    ]);

    /// <summary>A Siphon: two kills at once drop an orb for sure, one kill as a chance.</summary>
    private static readonly BuildElement Siphon = Mod("arc-siphon",
    [
        On(new Trigger.KillMultiple(new DamageSource.WeaponOfType(DamageType.Arc), TargetCount.From(2), Optional.Some(Seconds.From(3m))), new Outcome.Spawn(Orb, 1)),
        On(new Trigger.KillFewer(new DamageSource.WeaponOfType(DamageType.Arc), TargetCount.From(2)), new Outcome.Spawn(Orb, 1)) with { Likelihood = Likelihood.Chance },
    ]);

    private static readonly BuildElement StacksOnStacks = Mod("stacks-on-stacks", [],
        [new PassiveRule(new Passive.ExtraStacks(ArmorCharge, StackCount.From(1)), [], Optional.None<string>())]);

    private static readonly BuildElement ChargedUp = Mod("charged-up", [],
        [new PassiveRule(new Passive.RaiseMaxStacks(ArmorCharge, new GameValue.PerModCount([4m, 5m, 6m])), [], Optional.None<string>())]);

    private static readonly StackTable KickstartTable = new(StackCount.From(2), [0.162m, 0.217m, 0.284m, 0.344m, 0.384m, 0.413m, 0.434m, 0.45m]);

    private static readonly BuildElement Kickstart = Mod("grenade-kickstart",
    [
        OnWhen(new Trigger.AbilityCast(AbilityKind.Grenade), new Condition.HasBuff(ArmorCharge),
            new Outcome.ConvertStacksToEnergy(ArmorCharge, AbilityKind.Grenade, new StackEnergy.ByCharges(KickstartTable))),
    ]);

    private static readonly BuildElement Bulwark = Mod("bulwark-finisher",
    [
        OnWhen(new Trigger.Finisher(), new Condition.Holds(ArmorCharge, StackCount.From(3)),
            new Outcome.RemoveBuff(ArmorCharge),
            new Outcome.ApplyBuff(Status("amplified"), new BuffDuration.BySpent(new StackTable(StackCount.From(3), [8m, 10m, 12m, 14m])), StackCount.From(1))),
    ]);

    private static readonly BuildElement Healthy = Mod("healthy-finisher",
        [OnWhen(new Trigger.Finisher(), new Condition.HasBuff(ArmorCharge), new Outcome.SpendStacks(ArmorCharge, StackCount.From(2)))]);

    private static readonly BuildElement Empowered = Mod("empowered-finish",
        [OnWhen(new Trigger.Finisher(), new Condition.LacksBuff(ArmorCharge), Buff("bolt-charge"))]);

    private static readonly BuildElement Font = Mod("grenade-font", [],
        [new PassiveRule(new Passive.ModifyStat(StatKind.Grenade, new GameValue.Known(20m)), [new Condition.HasBuff(ArmorCharge)], Optional.None<string>())]);

    private static ImmutableArray<Resolution> Play(ImmutableArray<BuildElement> mods, params PlayerAction[] actions) =>
        ActionResolution.ResolveSequence(ValidateBuild([], mods, keywords: [OrbOfPower]), ActionResolution.CreateInitialState(), [.. actions]);

    private static ValidatedBuild BuildOf(ImmutableArray<BuildElement> mods) =>
        ValidateBuild([], mods, keywords: [OrbOfPower]);

    private static (int Sure, int Chance) ReadCount(Resolution resolution) =>
        resolution.State.ReadTally(ArmorCharge).Match(tally => (tally.Value.Sure.Value, tally.Value.Chance.Value), _ => (0, 0));

    [Fact]
    public void Dodge_for_Reaper_a_weapon_kill_and_the_orb_picked_up_make_one_sure_Armor_Charge_and_Stacks_on_Stacks_two()
    {
        var plain = Play([Reaper], Dodge, RifleKills(1), PickUpOrb);
        var stacked = Play([Reaper, StacksOnStacks], Dodge, RifleKills(1), PickUpOrb);

        Assert.Equal((1, 0), ReadCount(plain[^1]));
        Assert.Equal((2, 0), ReadCount(stacked[^1]));
        Assert.Contains("Armor Charge x1", StepStateShaping.ShapeStepState(BuildOf([Reaper]), plain[^1].State, plain[^1].ActivePassives).Buffs.Select(entry => entry.Text));
    }

    [Fact]
    public void A_Siphon_orb_from_one_kill_is_a_chance_and_from_two_kills_at_once_is_sure()
    {
        var one = Play([Siphon], RifleKills(1), PickUpOrb);
        var two = Play([Siphon], RifleKills(2), PickUpOrb);

        Assert.Equal((0, 1), ReadCount(one[^1]));
        Assert.Equal(Likelihood.Chance, Assert.Single(one[^1].Fired, fired => fired.Source.Value == "orb-of-power").Likelihood);
        Assert.Contains("Orb of Power (chance)", StepStateShaping.ShapeStepState(BuildOf([Siphon]), one[0].State, one[0].ActivePassives).Ground.Select(entry => entry.Text));
        Assert.Equal((1, 0), ReadCount(two[^1]));
        Assert.Equal(Likelihood.Always, Assert.Single(two[^1].Fired, fired => fired.Source.Value == "orb-of-power").Likelihood);
    }

    [Fact]
    public void A_sure_orb_on_the_ground_stays_sure_when_a_chance_rule_drops_one_too()
    {
        var steps = Play([Reaper, Siphon], Dodge, RifleKills(1), PickUpOrb);

        Assert.Equal((1, 0), ReadCount(steps[^1]));
    }

    [Fact]
    public void The_count_stops_at_the_cap_the_rest_is_wasted_and_sure_stacks_at_the_cap_are_at_max()
    {
        var steps = Play([Reaper], Dodge, RifleKills(1), PickUpOrb, Dodge, RifleKills(1), PickUpOrb, Dodge, RifleKills(1), PickUpOrb, Dodge, RifleKills(1), PickUpOrb);

        Assert.Equal((3, 0), ReadCount(steps[8]));
        Assert.True(steps[8].State.IsAtMax(ArmorCharge));
        Assert.Equal((3, 0), ReadCount(steps[^1]));
        var orb = Assert.Single(steps[^1].Fired, fired => fired.Source.Value == "orb-of-power");
        Assert.Equal(Optional.Some("at cap: +1 wasted"), orb.Outcomes[0].Caveat);
        Assert.DoesNotContain(ActionResolution.ListDeclarations(BuildOf([Reaper]), steps[^1].State), declaration => declaration is StateDeclaration.ReachMax);
    }

    [Fact]
    public void Charged_Up_raises_the_cap_the_count_stops_at()
    {
        var steps = Play([Reaper, StacksOnStacks, ChargedUp], Dodge, RifleKills(1), PickUpOrb, Dodge, RifleKills(1), PickUpOrb);

        Assert.Equal((4, 0), ReadCount(steps[^1]));
    }

    [Fact]
    public void Declaring_it_at_max_puts_the_sure_count_at_the_cap_and_ending_it_takes_every_stack()
    {
        var build = BuildOf([Siphon]);
        var declared = ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(),
            [RifleKills(1), PickUpOrb, new PlayerAction.Declare(new StateDeclaration.ReachMax(ArmorCharge))]);
        var ended = ActionResolution.ResolveAction(build, declared[^1].State, new PlayerAction.Declare(new StateDeclaration.EndStatus(ArmorCharge)));

        Assert.Equal((3, 0), ReadCount(declared[^1]));
        Assert.False(ended.State.HasBuff(ArmorCharge));
    }

    [Fact]
    public void A_Kickstart_spends_every_stack_and_gives_its_tables_energy_for_the_copies_and_the_stacks()
    {
        var build = ValidateBuild([], [Reaper, Kickstart, Kickstart, Siphon], keywords: [OrbOfPower]);
        var steps = ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(),
            [Dodge, RifleKills(1), PickUpOrb, Dodge, RifleKills(1), PickUpOrb, RifleKills(1), PickUpOrb, GrenadeKill]);

        var kickstart = Assert.Single(steps[^1].Fired, fired => fired.Source.Value == "grenade-kickstart");
        var spend = kickstart.Outcomes[0].Spend.UnwrapOr(null!);

        Assert.Equal((2, 1), ReadCount(steps[^2]));   // two Reaper orbs, then a Siphon's chance orb
        Assert.Equal((2, 1), (spend.Sure.Value, spend.Chance.Value));
        Assert.Equal(Optional.Some<GameValue>(new GameValue.Known(0.284m)), spend.AtSure);       // 2 copies + x2 = 4 charges
        Assert.Equal(Optional.Some<GameValue>(new GameValue.Known(0.344m)), spend.WithChance);   // and the chance stack: 5
        Assert.Equal(Likelihood.Always, kickstart.Likelihood);
        Assert.False(steps[^1].State.HasBuff(ArmorCharge));
        Assert.Contains(
            "spends Armor Charge x2 · +1 (chance) → +28.4% grenade energy (34.4% with the chance stack) [Grenade Kickstart]",
            TraceRenderer.RenderResolution(build, steps[^1], new TraceOptions(false, false, false)).Select(line => line.ToPlainText()).Single(line => line.Contains("Kickstart", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_finisher_short_of_its_stacks_doesnt_fire_and_says_so_with_chance_stacks_it_is_a_chance_and_with_sure_ones_it_fires()
    {
        var short_ = Play([Reaper, Bulwark], Dodge, RifleKills(1), PickUpOrb, Finisher);
        var byChance = Play([Reaper, Siphon, Bulwark], Dodge, RifleKills(1), PickUpOrb, RifleKills(1), PickUpOrb, RifleKills(1), PickUpOrb, Finisher);
        var sure = Play([Reaper, StacksOnStacks, Bulwark], Dodge, RifleKills(1), PickUpOrb, Dodge, RifleKills(1), PickUpOrb, Finisher);

        Assert.DoesNotContain(short_[^1].Fired, fired => fired.Source.Value == "bulwark-finisher");
        Assert.Contains("Bulwark Finisher needs x3 Armor Charge, holds x1 — it doesn't fire.", short_[^1].Notes);
        Assert.Equal((1, 2), ReadCount(byChance[^2]));
        Assert.Equal(Likelihood.Chance, Assert.Single(byChance[^1].Fired, fired => fired.Source.Value == "bulwark-finisher").Likelihood);
        var bulwark = Assert.Single(sure[^1].Fired, fired => fired.Source.Value == "bulwark-finisher");
        Assert.Equal(Likelihood.Always, bulwark.Likelihood);
        Assert.Equal(new BuffDuration.Fixed(Seconds.From(8m)), Assert.IsType<Outcome.ApplyBuff>(bulwark.Outcomes[1].Outcome).Duration);   // x3 spent: 8 s
        Assert.False(sure[^1].State.HasBuff(ArmorCharge));
    }

    [Fact]
    public void A_fixed_spend_takes_sure_stacks_first_then_chance_ones_and_the_buff_ends_with_none_left()
    {
        var steps = Play([Reaper, Siphon, Healthy], Dodge, RifleKills(1), PickUpOrb, RifleKills(1), PickUpOrb, RifleKills(1), PickUpOrb, Finisher);
        var spent = Assert.Single(steps[^1].Fired, fired => fired.Source.Value == "healthy-finisher").Outcomes[0].Spend.UnwrapOr(null!);
        var again = ActionResolution.ResolveAction(BuildOf([Reaper, Siphon, Healthy]), steps[^1].State, Finisher);

        Assert.Equal((1, 1), (spent.Sure.Value, spent.Chance.Value));
        Assert.Equal((0, 1), ReadCount(steps[^1]));
        Assert.False(again.State.HasBuff(ArmorCharge));
    }

    [Fact]
    public void Without_Armor_Charge_holds_for_sure_with_none_and_only_by_chance_with_chance_stacks()
    {
        var none = Play([Empowered], Finisher);
        var chance = Play([Siphon, Empowered], RifleKills(1), PickUpOrb, Finisher);

        Assert.Equal(Likelihood.Always, Assert.Single(none[^1].Fired, fired => fired.Source.Value == "empowered-finish").Likelihood);
        Assert.Equal(Likelihood.Chance, Assert.Single(chance[^1].Fired, fired => fired.Source.Value == "empowered-finish").Likelihood);
    }

    [Fact]
    public void A_passive_held_only_by_chance_stacks_is_a_chance()
    {
        var sure = Play([Reaper, Font], Dodge, RifleKills(1), PickUpOrb);
        var chance = Play([Siphon, Font], RifleKills(1), PickUpOrb);

        Assert.Equal(Likelihood.Always, Assert.Single(sure[^1].ActivePassives).Likelihood);
        Assert.Equal(Likelihood.Chance, Assert.Single(chance[^1].ActivePassives).Likelihood);
    }

    [Fact]
    public void The_state_lists_each_change_with_its_step_and_the_next_pass_starts_from_what_this_one_ended_with()
    {
        var build = BuildOf([Reaper, Siphon]);
        var steps = ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), [Dodge, RifleKills(1), PickUpOrb, RifleKills(1), PickUpOrb]);
        var count = Assert.Single(StepStateShaping.ShapeStepState(build, steps[^1].State, steps[^1].ActivePassives).Counts);
        var carried = steps[^1].State.StartNextPass().ReadTally(ArmorCharge).UnwrapOr(null!);

        Assert.Equal("x1 · +1 (chance)", count.Count);
        Assert.Equal(["#3 Orb Of Power: +1 → x1", "#5 Orb Of Power: +1 (chance) → x1 · +1 (chance)"], count.Changes);
        Assert.Equal(Optional.Some("1 stack every 10 s"), count.Decay);
        Assert.Equal((1, 1), (carried.Sure.Value, carried.Chance.Value));
        Assert.Equal(["the pass before: x1 · +1 (chance)"], carried.Changes.Select(DomainPhrasing.DescribeStackChange));
    }

    [Fact]
    public void An_enemy_count_says_what_needs_it_only_on_the_weapon_or_ability_it_comes_from_and_nothing_filling_ends_at_it()
    {
        var build = BuildOf([Siphon]);

        var rifle = ActionResolution.ListAvailableActions(build, ActionResolution.CreateInitialState()).OfType<PlayerAction.FireWeapon>().First();
        var grenade = ActionResolution.ListAvailableActions(build, ActionResolution.CreateInitialState()).OfType<PlayerAction.CastAbility>().First();

        var initial = ActionResolution.CreateInitialState();
        var glossary = build.Catalog.Glossary;

        Assert.Equal(["1 enemy", "2 in 3 s · Arc Siphon"], TargetCountListing.ListTargetCountChoices(build, initial, rifle).Select(choice => glossary.DescribeTargetCountChoice(choice)));
        Assert.Equal(["1 enemy"], TargetCountListing.ListTargetCountChoices(build, initial, grenade).Select(choice => glossary.DescribeTargetCountChoice(choice)));
    }

    [Fact]
    public void The_counts_go_up_to_the_one_that_fills_a_buff_to_its_cap_and_keep_the_actions_own_count()
    {
        var charger = Element("charger", ElementKind.Fragment, [On(new Trigger.KillAny(new DamageSource.AnyWeapon()), Buff("bolt-charge"))]);
        var build = ValidateBuild([charger]);
        var glossary = build.Catalog.Glossary;
        var initial = ActionResolution.CreateInitialState();

        Assert.Equal(["1 enemy", "2 enemies", "3 enemies · Bolt Charge x3"], TargetCountListing.ListTargetCountChoices(build, initial, RifleKills(1)).Select(choice => glossary.DescribeTargetCountChoice(choice)));
        Assert.Equal(7, TargetCountListing.ListTargetCountChoices(build, initial, RifleKills(7))[^1].Count.Value);   // a loop file's count stays
    }

    [Fact]
    public void Armor_Charge_already_at_its_cap_fills_nothing_so_one_enemy_is_all_a_step_offers()
    {
        var charger = Mod("charger", [On(new Trigger.KillAny(new DamageSource.AnyWeapon()), Buff("armor-charge"))]);
        var build = BuildOf([charger]);
        var glossary = build.Catalog.Glossary;
        var full = ActionResolution.ResolveAction(build, ActionResolution.CreateInitialState(), RifleKills(3)).State;

        Assert.Equal(["1 enemy", "2 enemies", "3 enemies · Armor Charge x3"], TargetCountListing.ListTargetCountChoices(build, ActionResolution.CreateInitialState(), RifleKills(1)).Select(choice => glossary.DescribeTargetCountChoice(choice)));
        Assert.Equal(["1 enemy"], TargetCountListing.ListTargetCountChoices(build, full, RifleKills(1)).Select(choice => glossary.DescribeTargetCountChoice(choice)));
    }
}

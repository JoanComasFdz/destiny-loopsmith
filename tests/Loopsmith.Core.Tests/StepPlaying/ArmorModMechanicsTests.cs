using System.Collections.Immutable;
using Loopsmith.Core.BuildExplanation;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.StepPlaying;
using Loopsmith.Core.TraceRendering;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.StepPlaying;

/// <summary>
/// What the armor mods need of the engine: a finisher (it kills one enemy of the pack, and only "any source" kills react
/// to it), a stat change while a buff holds (a fact, never added up), energy to the least-charged ability (a fact) and a
/// weapon of your Super's element.
/// </summary>
public sealed class ArmorModMechanicsTests
{
    private static readonly PlayerAction Finisher = new PlayerAction.PerformFinisher();
    private static readonly PlayerAction GrenadeHit = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage, TargetCount.One);
    private static readonly PlayerAction Dodge = new PlayerAction.UseClassAbility();
    private static readonly PlayerAction RifleKill = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One);

    private static Resolution ResolveOnce(ValidatedBuild build, PlayerAction action) =>
        ActionResolution.ResolveAction(build, ActionResolution.CreateInitialState(), action);

    private static ImmutableArray<Resolution> ResolveSteps(ValidatedBuild build, params PlayerAction[] actions) =>
        ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), [.. actions]);

    private static string DescribeTrace(ValidatedBuild build, Resolution resolution) =>
        string.Join("\n", TraceRenderer.RenderResolution(build, resolution, new TraceOptions(false, false, false)).Select(line => line.ToPlainText()));

    private static string DescribeExplanation(ValidatedBuild build) =>
        BuildExplaining.RenderExplanation(build.Catalog.Glossary, BuildExplaining.ExplainBuild(build), ExplanationStyle.Note).ToPlainText();

    private static BuildElement Mod(string id, ImmutableArray<Rule> rules, ImmutableArray<PassiveRule> passives = default) =>
        Element(id, ElementKind.ArmorMod, rules, passives) with { Affinity = Affinity.Neutral };

    // ── Finisher ─────────────────────────────────────────────────────────────────────────

    private static readonly BuildElement Jolter = Element("jolter", ElementKind.Fragment,
        [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), new Outcome.DebuffTarget(Status("jolt"), Optional.None<Seconds>()))]);

    private static readonly BuildElement FinisherMod = Mod("finisher-mod", [On(new Trigger.Finisher(), Buff("amplified"))]);

    private static readonly BuildElement JoltedKill = Element("jolted-kill", ElementKind.Fragment,
        [On(new Trigger.KillDebuffed(new DamageSource.AnySource(), [Status("jolt")]), Buff("bolt-charge"))]);

    private static readonly BuildElement MeleeKill = Mod("melee-kill",
        [On(new Trigger.KillAny(new DamageSource.AbilityOf(AbilityKind.Melee)), Buff("bolt-charge"))]);

    private static readonly BuildElement ArcKill = Mod("arc-kill",
        [On(new Trigger.KillAny(new DamageSource.OfType(DamageType.Arc)), Buff("bolt-charge"))]);

    private static readonly BuildElement KineticKill = Mod("kinetic-kill",
        [On(new Trigger.KillAny(new DamageSource.OfType(DamageType.Kinetic)), Buff("bolt-charge"))]);

    [Fact]
    public void A_finisher_fires_the_finisher_rules_then_kills_one_enemy_of_the_pack_which_only_any_source_matches()
    {
        var build = ValidateBuild([Jolter, JoltedKill], mods: [FinisherMod, MeleeKill, ArcKill, KineticKill]);

        var steps = ResolveSteps(build, GrenadeHit, Finisher);
        var finisher = steps[1];

        Assert.False(finisher.Blocked.IsSome());
        Assert.Equal(["finisher-mod", "jolted-kill"], finisher.Fired.Select(f => f.Source.Value));
        Assert.Equal(new GameEvent.Finisher(), finisher.Fired[0].Trigger);
        var kill = Assert.IsType<GameEvent.Killed>(finisher.Fired[1].Trigger);
        Assert.Equal(new DamageOrigin.Finisher(), kill.Origin);
        Assert.Equal(EnemyTier.Minor, kill.Tier);
        Assert.Equal([Status("jolt")], kill.TargetHas);
        Assert.Equal(
            "#2 Finisher\n  Finisher → Amplified (10s) [Finisher Mod]\n  Finisher 💀 kill on Jolted target → +1 Bolt Charge [Jolted Kill]",
            DescribeTrace(build, finisher));
    }

    [Fact]
    public void A_finisher_is_a_token_a_label_and_a_trigger_named_Finisher()
    {
        var build = ValidateBuild([], mods: [FinisherMod]);

        Assert.Equal("finisher", Finisher.ToActionToken());
        Assert.Equal(new Result<PlayerAction, string>.Ok(Finisher), ActionTokenParsing.ParseActionToken("Finisher"));
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("finisher:kill"));
        Assert.Equal("Finisher", build.DescribeAction(Finisher));
        Assert.Equal("Finisher", build.Catalog.Glossary.DescribeTrigger(new Trigger.Finisher(), build.ReadSuperType()));
        Assert.Contains("Finisher -> Amplified (10s) [Finisher Mod]", DescribeExplanation(build));
    }

    [Fact]
    public void A_finisher_is_offered_in_every_build_and_always_resolves()
    {
        var initial = ActionResolution.CreateInitialState();
        var without = ValidateBuild([JoltedKill]);

        Assert.Contains(Finisher, ActionResolution.ListAvailableActions(ValidateBuild([], mods: [FinisherMod]), initial));
        Assert.Contains(Finisher, ActionResolution.ListAvailableActions(without, initial));   // always possible in the game
        Assert.False(ResolveOnce(without, Finisher).Blocked.IsSome());
    }

    // ── Critical health ──────────────────────────────────────────────────────────────────

    private static readonly PlayerAction CriticalHealth = new PlayerAction.Declare(new StateDeclaration.CriticalHealth());

    private static readonly PlayerAction ArmorChargeAtMax = new PlayerAction.Declare(new StateDeclaration.ReachMax(Status("armor-charge")));

    /// <summary>Emergency Reinforcement's shape: for sure at the declared max, a chance below it.</summary>
    private static readonly BuildElement Reinforcement = Mod("reinforcement",
    [
        OnWhen(new Trigger.CriticalHealth(), new Condition.AtMax(Status("armor-charge")), new Outcome.RemoveBuff(Status("armor-charge")), Buff("amplified")),
        OnWhen(new Trigger.CriticalHealth(), new Condition.BelowMax(Status("armor-charge")), new Outcome.RemoveBuff(Status("armor-charge")), Buff("amplified"))
            with { Likelihood = Likelihood.Chance },
    ]);

    [Fact]
    public void Critical_health_is_a_token_a_label_and_a_trigger_named_Critical_health()
    {
        var build = ValidateBuild([], mods: [Reinforcement]);

        Assert.Equal("health:critical", CriticalHealth.ToActionToken());
        Assert.Equal(new Result<PlayerAction, string>.Ok(CriticalHealth), ActionTokenParsing.ParseActionToken("Health:Critical"));
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("health"));
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("health:low"));
        Assert.Equal("Critical health", build.DescribeAction(CriticalHealth));
        Assert.Equal("Critical health", build.Catalog.Glossary.DescribeTrigger(new Trigger.CriticalHealth(), build.ReadSuperType()));
        Assert.Equal("while Armor Charge below max", build.Catalog.Glossary.DescribeCondition(new Condition.BelowMax(Status("armor-charge"))));
        Assert.Contains("Critical health -> consumes Armor Charge and Amplified (10s) (while Armor Charge at max) [Reinforcement]", DescribeExplanation(build));
    }

    [Fact]
    public void Critical_health_is_offered_only_when_a_rule_reacts_to_it_and_always_resolves()
    {
        var initial = ActionResolution.CreateInitialState();
        var without = ValidateBuild([Charger]);

        Assert.Contains(CriticalHealth, ActionResolution.ListAvailableActions(ValidateBuild([], mods: [Reinforcement]), initial));
        Assert.DoesNotContain(CriticalHealth, ActionResolution.ListAvailableActions(without, initial));
        var nothing = ResolveOnce(without, CriticalHealth);
        Assert.False(nothing.Blocked.IsSome());
        Assert.Empty(nothing.Fired);
    }

    [Fact]
    public void Critical_health_spends_the_buff_for_sure_at_its_declared_max_as_a_chance_below_it_and_does_nothing_without_it()
    {
        var build = ValidateBuild([Charger], mods: [Reinforcement]);

        var bare = ResolveOnce(build, CriticalHealth);
        var below = ResolveSteps(build, Dodge, CriticalHealth)[1];
        var atMax = ResolveSteps(build, Dodge, ArmorChargeAtMax, CriticalHealth)[2];

        Assert.Empty(bare.Fired);
        var chance = Assert.Single(below.Fired);
        Assert.Equal((Likelihood.Chance, (GameEvent)new GameEvent.CriticalHealth()), (chance.Likelihood, chance.Trigger));
        Assert.Equal(Likelihood.Always, Assert.Single(atMax.Fired).Likelihood);
        Assert.Equal([Status("critical-health"), Status("amplified")], below.State.Buffs.Select(buff => buff.Status));    // the Armor Charge is spent
        Assert.Equal([Status("critical-health"), Status("amplified")], atMax.State.Buffs.Select(buff => buff.Status));
        Assert.Equal("#2 Critical health\n  Critical health → consumes Armor Charge x1 and Amplified (10s) [Reinforcement] (chance)", DescribeTrace(build, below));
    }

    private static readonly PlayerAction HitAtCriticalHealth = new PlayerAction.Declare(new StateDeclaration.HitAtCriticalHealth());

    private static readonly PlayerAction EndCriticalHealth = new PlayerAction.Declare(new StateDeclaration.EndStatus(Status("critical-health")));

    /// <summary>Spark of Instinct's shape: a hit at critical health jolts the pack.</summary>
    private static readonly BuildElement Instinct = Element("instinct", ElementKind.Fragment,
        [On(new Trigger.HitAtCriticalHealth(), new Outcome.DebuffTarget(Status("jolt"), Optional.None<Seconds>()))]);

    [Fact]
    public void Hit_at_critical_health_is_a_token_a_label_and_a_trigger_named_Hit_at_critical_health()
    {
        var build = ValidateBuild([Instinct]);

        Assert.Equal("health:hit", HitAtCriticalHealth.ToActionToken());
        Assert.Equal(new Result<PlayerAction, string>.Ok(HitAtCriticalHealth), ActionTokenParsing.ParseActionToken("Health:Hit"));
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("health:hit:2"));
        Assert.Equal("Hit at critical health", build.DescribeAction(HitAtCriticalHealth));
        Assert.Equal("Critical health ends", build.DescribeAction(EndCriticalHealth));
        Assert.Equal("Hit at critical health", build.Catalog.Glossary.DescribeTrigger(new Trigger.HitAtCriticalHealth(), build.ReadSuperType()));
    }

    [Fact]
    public void Critical_health_stays_on_you_until_declared_ended_and_offers_the_hit_while_it_lasts()
    {
        var build = ValidateBuild([Instinct]);
        var initial = ActionResolution.CreateInitialState();
        var steps = ResolveSteps(build, CriticalHealth, EndCriticalHealth);

        Assert.Contains(CriticalHealth, ActionResolution.ListAvailableActions(build, initial));   // a rule reacts to a hit at it
        Assert.DoesNotContain(HitAtCriticalHealth, ActionResolution.ListAvailableActions(build, initial));
        Assert.Equal([Status("critical-health")], steps[0].State.Buffs.Select(buff => buff.Status));
        Assert.Contains(HitAtCriticalHealth, steps[0].NowAvailable);
        Assert.Contains(EndCriticalHealth, steps[0].NowAvailable);
        Assert.DoesNotContain(CriticalHealth, steps[0].NowAvailable);
        Assert.Empty(steps[1].State.Buffs);
        Assert.Contains(CriticalHealth, steps[1].NowAvailable);
        Assert.DoesNotContain(HitAtCriticalHealth, ActionResolution.ListAvailableActions(ValidateBuild([], mods: [Reinforcement]), steps[0].State));   // nothing reacts to a hit
    }

    [Fact]
    public void A_hit_at_critical_health_fires_its_rules_only_after_a_step_reached_critical_health()
    {
        var build = ValidateBuild([Instinct]);

        var alone = ResolveOnce(build, HitAtCriticalHealth);
        var steps = ResolveSteps(build, CriticalHealth, HitAtCriticalHealth, HitAtCriticalHealth, CriticalHealth);

        Assert.Equal("Not at critical health — declare Critical health (health:critical) first.", alone.Blocked.Match(some => some.Value, _ => ""));
        Assert.Empty(alone.Fired);
        Assert.Empty(steps[0].Fired);
        var burst = Assert.Single(steps[1].Fired);
        Assert.Equal((Likelihood.Always, (GameEvent)new GameEvent.HitAtCriticalHealth()), (burst.Likelihood, burst.Trigger));
        Assert.Equal([Status("jolt")], steps[1].State.Target.Debuffs);
        Assert.Single(steps[2].Fired);   // every hit while at it
        Assert.Equal("Already at critical health — declare it ended first (end:critical-health).", steps[3].Blocked.Match(some => some.Value, _ => ""));
        Assert.Equal("#2 Hit at critical health\n  Hit at critical health → Jolt target (4s) [Instinct]", DescribeTrace(build, steps[1]));
    }

    // ── Breaking a shield (ADRs D36) ─────────────────────────────────────────────────────

    private static readonly PlayerAction ShieldKill = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.From(2), ShieldBreak.Breaks);

    /// <summary>Shield Break Charge's shape: x1 | x2 Armor Charge by copies.</summary>
    private static readonly BuildElement ShieldBreaker = Mod("shield-breaker",
    [
        On(new Trigger.ShieldBroken(new DamageSource.AnySource()),
            new Outcome.ApplyBuff(Status("armor-charge"), new BuffDuration.Unstated(), new StackGrant.PerModCount([StackCount.From(1), StackCount.From(2)]))),
    ]);

    [Fact]
    public void Breaking_a_shield_is_a_weapon_steps_option_with_its_token_label_and_trigger()
    {
        var build = ValidateBuild([], mods: [ShieldBreaker]);

        Assert.Equal("energy:kill:2:shield", ShieldKill.ToActionToken());
        Assert.Equal(new Result<PlayerAction, string>.Ok(ShieldKill), ActionTokenParsing.ParseActionToken("Energy:Kill:2:Shield"));
        Assert.Equal(new Result<PlayerAction, string>.Ok(new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Damage, TargetCount.One, ShieldBreak.Breaks)),
            ActionTokenParsing.ParseActionToken("kinetic:shield"));
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("grenade:kill:shield"));   // a weapon's option only
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("energy:shield:kill"));    // the option goes last
        Assert.Equal("Test Rifle (💀 kill 2 · 🛡 shield)", build.DescribeAction(ShieldKill));
        Assert.Equal("Break a shield", build.Catalog.Glossary.DescribeTrigger(new Trigger.ShieldBroken(new DamageSource.AnySource()), build.ReadSuperType()));
        Assert.Contains("Break a shield -> +1 Armor Charge [Shield Breaker]", DescribeExplanation(build));
    }

    [Fact]
    public void A_weapon_step_that_breaks_a_shield_breaks_one_after_its_first_hit_and_gives_Armor_Charge_by_copies()
    {
        var one = ResolveOnce(ValidateBuild([], mods: [ShieldBreaker]), ShieldKill);
        var two = ResolveOnce(ValidateBuild([], mods: [ShieldBreaker, ShieldBreaker]), ShieldKill);
        var plain = ResolveOnce(ValidateBuild([], mods: [ShieldBreaker]), RifleKill);

        var fired = Assert.Single(one.Fired);
        Assert.Equal(new GameEvent.ShieldBroken(new DamageOrigin.Weapon(WeaponSlot.Energy, DamageType.Arc, Optional.None<AmmoType>())), fired.Trigger);
        Assert.Equal(1, one.State.ReadTally(Status("armor-charge")).Match(tally => tally.Value.Sure.Value, _ => 0));
        Assert.Equal(2, two.State.ReadTally(Status("armor-charge")).Match(tally => tally.Value.Sure.Value, _ => 0));
        Assert.Empty(plain.Fired);
        Assert.StartsWith("#1 Test Rifle (💀 kill 2 · 🛡 shield)\n  Test Rifle 🛡 broke a shield → +2 Armor Charge [Shield Breaker]", DescribeTrace(ValidateBuild([], mods: [ShieldBreaker, ShieldBreaker]), two));
    }

    // ── An ability's energy fully expended (ADRs D36) ────────────────────────────────────

    /// <summary>A Kickstart's shape: the last grenade charge spends every Armor Charge stack.</summary>
    private static readonly BuildElement Kickstart = Mod("kickstart",
        [OnWhen(new Trigger.EnergyExpended(AbilityKind.Grenade), new Condition.HasBuff(Status("armor-charge")), new Outcome.RemoveBuff(Status("armor-charge")))]);

    /// <summary>Shinobu's Vow's second grenade charge.</summary>
    private static readonly BuildElement SecondGrenade = Mod("second-grenade", [],
        [new PassiveRule(RuleId.From("test-rule-1"), new Passive.ExtraCharges(AbilityKind.Grenade, ChargeCount.One), [], Optional.None<string>())]);

    private static readonly PlayerAction LastGrenadeHit = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage, TargetCount.One, AbilityInput.Press, ChargeUse.Last);

    [Fact]
    public void The_last_charge_is_an_ability_steps_option_with_its_token_label_and_trigger()
    {
        var build = ValidateBuild([], mods: [Kickstart, SecondGrenade]);
        var lastDodge = new PlayerAction.UseClassAbility(AbilityInput.Press, ChargeUse.Last);

        Assert.Equal("grenade:last", LastGrenadeHit.ToActionToken());
        Assert.Equal("grenade:kill:3:last", new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.From(3), AbilityInput.Press, ChargeUse.Last).ToActionToken());
        Assert.Equal("class:last", lastDodge.ToActionToken());
        Assert.Equal(new Result<PlayerAction, string>.Ok(LastGrenadeHit), ActionTokenParsing.ParseActionToken("Grenade:Last"));
        Assert.Equal(new Result<PlayerAction, string>.Ok(new PlayerAction.CastAbility(OffensiveAbility.Melee, HitOutcome.Kill, TargetCount.One, AbilityInput.Slide, ChargeUse.Last)),
            ActionTokenParsing.ParseActionToken("melee:slide:kill:last"));
        Assert.Equal(new Result<PlayerAction, string>.Ok(lastDodge), ActionTokenParsing.ParseActionToken("class:last"));
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("super:last"));        // a Super has one charge
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("kinetic:kill:last"));  // an ability's option only
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("grenade:last:kill"));  // the option goes last
        Assert.Equal("Grenade (🥊 hit · ⬜ spend fully)", build.DescribeAction(LastGrenadeHit));
        Assert.Equal("Class ability (⬜ spend fully)", build.DescribeAction(lastDodge));
        Assert.Equal("Grenade energy fully expended", build.Catalog.Glossary.DescribeTrigger(new Trigger.EnergyExpended(AbilityKind.Grenade), build.ReadSuperType()));
        Assert.Equal(2, build.CountCharges(AbilityKind.Grenade).Value);
        Assert.Equal(1, build.CountCharges(AbilityKind.Melee).Value);
    }

    [Fact]
    public void With_one_charge_every_use_expends_the_ability()
    {
        var build = ValidateBuild([Charger], mods: [Kickstart]);

        var steps = ResolveSteps(build, Dodge, GrenadeHit);

        var fired = Assert.Single(steps[1].Fired, rule => rule.Source == ElementId.From("kickstart"));
        Assert.Equal(new GameEvent.EnergyExpended(AbilityKind.Grenade), fired.Trigger);
        Assert.False(steps[1].State.HasBuff(Status("armor-charge")));
    }

    [Fact]
    public void With_more_charges_only_the_use_marked_as_the_last_one_expends_the_ability()
    {
        var build = ValidateBuild([Charger], mods: [Kickstart, SecondGrenade]);

        var plain = ResolveSteps(build, Dodge, GrenadeHit)[1];
        var last = ResolveSteps(build, Dodge, LastGrenadeHit)[1];

        Assert.DoesNotContain(plain.Fired, rule => rule.Source == ElementId.From("kickstart"));
        Assert.True(plain.State.HasBuff(Status("armor-charge")));
        Assert.Contains(last.Fired, rule => rule.Source == ElementId.From("kickstart"));
        Assert.False(last.State.HasBuff(Status("armor-charge")));
    }

    // ── A stat change while a buff holds ─────────────────────────────────────────────────

    private static readonly BuildElement Charger = Element("charger", ElementKind.Fragment,
        [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("armor-charge"))]);

    private static readonly BuildElement GrenadeFont = Mod("grenade-font", [],
        [new PassiveRule(RuleId.From("test-rule-1"), new Passive.ModifyStat(StatKind.Grenade, new GameValue.PerModCount([20m, 40m, 50m])), [new Condition.HasBuff(Status("armor-charge"))], Optional.None<string>())]);

    [Fact]
    public void A_stat_change_holds_while_its_buff_does_and_shows_with_the_buff_never_added_to_the_stats()
    {
        var build = ValidateBuild([Charger], mods: [GrenadeFont]);

        var charged = ResolveOnce(build, Dodge);
        var plain = ResolveOnce(build, GrenadeHit);
        var state = StepStateShaping.ShapeStepState(build, charged.State, charged.ActivePassives);

        Assert.Equal(["grenade-font"], charged.ActivePassives.Select(passive => passive.Source.Value));
        Assert.Empty(plain.ActivePassives);
        var armorCharge = Assert.Single(state.Buffs);
        Assert.Equal("Armor Charge x1", armorCharge.Text);   // counted (ADRs D33)
        Assert.Equal(["+20 Grenade"], armorCharge.SwitchesOn.Select(effect => effect.Text));
        Assert.False(build.Build.Stats.Grenade.IsSome());   // a fact shown, never added to the build's stats (ADRs D1)
        Assert.Contains("While Armor Charge -> +20 Grenade [Grenade Font]", DescribeExplanation(build));
    }

    [Fact]
    public void A_passive_shows_the_value_for_the_copies_equipped()
    {
        var surge = Mod("weapon-surge", [],
            [new PassiveRule(RuleId.From("test-rule-1"), new Passive.ModifyDamage(new DamageSource.WeaponOfType(DamageType.Arc), new GameValue.PerModCount([0.10m, 0.17m, 0.22m, 0.25m])), [], Optional.None<string>())]);
        var build = ValidateBuild([Charger], mods: [GrenadeFont, surge],
            modOrder: [ElementId.From("grenade-font"), ElementId.From("grenade-font"), ElementId.From("weapon-surge"), ElementId.From("weapon-surge")]);

        var charged = ResolveOnce(build, Dodge);
        var state = StepStateShaping.ShapeStepState(build, charged.State, charged.ActivePassives);
        var explanation = DescribeExplanation(build);

        Assert.Equal(["+40 Grenade"], Assert.Single(state.Buffs).SwitchesOn.Select(effect => effect.Text));
        Assert.Contains("+40 Grenade [Grenade Font]", explanation);
        Assert.Contains("+17% Arc weapon damage [Weapon Surge]", explanation);
    }

    [Fact]
    public void A_stat_change_that_lowers_or_is_unknown_reads_as_such()
    {
        var glossary = ValidateBuild([]).Catalog.Glossary;

        Assert.Equal("-10 Health", glossary.DescribePassive(new Passive.ModifyStat(StatKind.Health, new GameValue.Known(-10m)), CopyCount.One, Optional.None<DamageType>()));
        Assert.Equal("+? Melee", glossary.DescribePassive(new Passive.ModifyStat(StatKind.Melee, new GameValue.Unknown()), CopyCount.One, Optional.None<DamageType>()));
    }

    // ── Energy to the least-charged ability ──────────────────────────────────────────────

    [Fact]
    public void Energy_to_the_least_charged_ability_is_a_fact_by_copies_that_changes_nothing()
    {
        var restoration = Mod("orbs-of-restoration",
            [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), new Outcome.GrantLeastChargedEnergy(new EnergyGrant.Fraction(new GameValue.PerModCount([0.10m, 0.13m, 0.15m]))))]);
        var unknown = Mod("unknown-restoration",
            [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), new Outcome.GrantLeastChargedEnergy(new EnergyGrant.Fraction(new GameValue.Unknown())))]);
        var build = ValidateBuild([], mods: [restoration, unknown],
            modOrder: [ElementId.From("orbs-of-restoration"), ElementId.From("orbs-of-restoration"), ElementId.From("unknown-restoration")]);

        var dodge = ResolveOnce(build, Dodge);

        Assert.Equal(ActionResolution.CreateInitialState() with { Step = 1, Casting = Optional.Some(AbilityKind.ClassAbility) }, dodge.State);
        Assert.Equal(
            "#1 Class ability\n  Class ability → +13% energy to your least-charged ability [Orbs Of Restoration] + +?% energy to your least-charged ability [Unknown Restoration]",
            DescribeTrace(build, dodge));
        var applied = dodge.Fired[1].Outcomes[0];
        Assert.Equal(Certainty.Unknown, applied.Certainty);
        Assert.Equal(Optional.Some("amount unknown"), applied.Caveat);
        Assert.Contains("Class ability -> +13% energy to your least-charged ability [Orbs Of Restoration]", DescribeExplanation(build));
    }

    // ── A weapon of your Super's element ─────────────────────────────────────────────────

    private static readonly BuildElement HarmonicSiphon = Mod("harmonic-siphon",
        [On(new Trigger.KillAny(new DamageSource.WeaponOfSuperType()), new Outcome.Spawn(Pickup("orb-of-power"), 1))]);

    [Fact]
    public void A_weapon_of_your_supers_element_matches_an_Arc_weapon_with_an_Arc_super_only()
    {
        var arc = ValidateBuild([], mods: [HarmonicSiphon]);
        var solar = ValidateBuild([], mods: [HarmonicSiphon], subclass: Subclass.Prismatic, super: Optional.Some(Affinity.Solar));
        var unknown = ValidateBuild([], mods: [HarmonicSiphon], super: Optional.None<Affinity>());

        Assert.Single(ResolveOnce(arc, RifleKill).Fired, f => f.Source.Value == "harmonic-siphon");
        Assert.DoesNotContain(ResolveOnce(solar, RifleKill).Fired, f => f.Source.Value == "harmonic-siphon");
        Assert.DoesNotContain(ResolveOnce(unknown, RifleKill).Fired, f => f.Source.Value == "harmonic-siphon");
        Assert.DoesNotContain(ResolveOnce(arc, new PlayerAction.CastAbility(OffensiveAbility.Super, HitOutcome.Kill, TargetCount.One)).Fired,
            f => f.Source.Value == "harmonic-siphon");   // a weapon's, not the Super's own damage
        Assert.Equal(Optional.Some(DamageType.Arc), arc.ReadSuperType());
        Assert.Equal(Optional.Some(DamageType.Solar), solar.ReadSuperType());
        Assert.Equal(Optional.None<DamageType>(), unknown.ReadSuperType());
    }

    [Theory]
    [InlineData(DamageType.Solar, DamageType.Solar, true)]
    [InlineData(DamageType.Arc, DamageType.Solar, false)]
    [InlineData(DamageType.Strand, DamageType.Strand, true)]
    public void A_weapon_of_your_supers_element_is_a_weapon_of_the_supers_type(DamageType weapon, DamageType super, bool matches)
    {
        var source = new DamageSource.WeaponOfSuperType();

        Assert.Equal(matches, source.IsMatchedBy(new DamageOrigin.Weapon(WeaponSlot.Kinetic, weapon, Optional.None<AmmoType>()), Optional.Some(super)));
        Assert.False(source.IsMatchedBy(new DamageOrigin.Weapon(WeaponSlot.Kinetic, weapon, Optional.None<AmmoType>()), Optional.None<DamageType>()));
        Assert.False(source.IsMatchedBy(new DamageOrigin.Ability(AbilityKind.Grenade, super), Optional.Some(super)));
        Assert.False(source.IsMatchedBy(new DamageOrigin.Finisher(), Optional.Some(super)));
    }

    [Fact]
    public void A_weapon_of_your_supers_element_names_the_supers_element_when_it_is_known()
    {
        var arc = ValidateBuild([], mods: [HarmonicSiphon]);
        var unknown = ValidateBuild([], mods: [HarmonicSiphon], super: Optional.None<Affinity>());
        var glossary = arc.Catalog.Glossary;
        var damage = new Trigger.Damage(new DamageSource.WeaponOfSuperType());
        var boost = new Passive.ModifyDamage(new DamageSource.WeaponOfSuperType(), new GameValue.Known(0.2m));

        Assert.Contains("Kill target with Arc weapon (your Super's element) -> Orb of Power [Harmonic Siphon]", DescribeExplanation(arc));
        Assert.Contains("Kill target with weapon of your Super's element -> Orb of Power [Harmonic Siphon]", DescribeExplanation(unknown));
        Assert.Equal("Damage target with Arc weapon (your Super's element)", glossary.DescribeTrigger(damage, arc.ReadSuperType()));
        Assert.Equal("+20% damage with weapon of your Super's element", glossary.DescribePassive(boost, CopyCount.One, unknown.ReadSuperType()));
    }
}

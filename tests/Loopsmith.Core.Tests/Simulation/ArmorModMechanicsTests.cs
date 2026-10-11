using System.Collections.Immutable;
using Loopsmith.Core.BuildExplanation;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.LoopGraphing;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.Simulation;
using Loopsmith.Core.TraceRendering;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.Simulation;

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
            "#2 Finisher\n  Finisher → Amplified (10s) [Finisher Mod]\n  Finisher kill on Jolted target → +1 Bolt Charge [Jolted Kill]",
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
    public void A_finisher_is_offered_only_where_a_rule_reacts_to_one_and_always_resolves()
    {
        var initial = ActionResolution.CreateInitialState();
        var without = ValidateBuild([JoltedKill]);

        Assert.Contains(Finisher, ActionResolution.ListAvailableActions(ValidateBuild([], mods: [FinisherMod]), initial));
        Assert.DoesNotContain(Finisher, ActionResolution.ListAvailableActions(without, initial));
        Assert.False(ResolveOnce(without, Finisher).Blocked.IsSome());   // a token still plays anywhere
    }

    [Fact]
    public void The_loop_graph_draws_a_finisher_action_only_where_a_rule_reacts_to_one()
    {
        var with = LoopGraphBuilding.BuildLoopGraph(ValidateBuild([JoltedKill], mods: [FinisherMod]));
        var without = LoopGraphBuilding.BuildLoopGraph(ValidateBuild([JoltedKill]));

        Assert.Contains(with.Nodes, node => node.Key == "a:Finisher" && node.Label == "Finisher" && node.Kind == NodeKind.Action);
        Assert.Contains(with.Edges, edge => edge.From == "a:Finisher" && edge.To == "t:Finisher");
        Assert.DoesNotContain(without.Nodes, node => node.Key == "a:Finisher");
    }

    // ── A stat change while a buff holds ─────────────────────────────────────────────────

    private static readonly BuildElement Charger = Element("charger", ElementKind.Fragment,
        [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("armor-charge"))]);

    private static readonly BuildElement GrenadeFont = Mod("grenade-font", [],
        [new PassiveRule(new Passive.ModifyStat(StatKind.Grenade, new GameValue.PerModCount([20m, 40m, 50m])), [new Condition.HasBuff(Status("armor-charge"))], Optional.None<string>())]);

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
        Assert.Equal("Armor Charge", armorCharge.Text);
        Assert.Equal(["+20 Grenade"], armorCharge.SwitchesOn.Select(effect => effect.Text));
        Assert.False(build.Build.Stats.Grenade.IsSome());   // a fact shown, never added to the build's stats (ADRs D1)
        Assert.Contains("While Armor Charge -> +20 Grenade [Grenade Font]", DescribeExplanation(build));
    }

    [Fact]
    public void A_passive_shows_the_value_for_the_copies_equipped()
    {
        var surge = Mod("weapon-surge", [],
            [new PassiveRule(new Passive.ModifyDamage(new DamageSource.WeaponOfType(DamageType.Arc), new GameValue.PerModCount([0.10m, 0.17m, 0.22m, 0.25m])), [], Optional.None<string>())]);
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

        Assert.Equal("-10 Health", glossary.DescribePassive(new Passive.ModifyStat(StatKind.Health, new GameValue.Known(-10m)), 1, Optional.None<DamageType>()));
        Assert.Equal("+? Melee", glossary.DescribePassive(new Passive.ModifyStat(StatKind.Melee, new GameValue.Unknown()), 1, Optional.None<DamageType>()));
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

        Assert.Equal(ActionResolution.CreateInitialState() with { Step = 1 }, dodge.State);
        Assert.Equal(
            "#1 Class ability\n  Class ability → +13% energy to your least-charged ability [Orbs Of Restoration] + +?% energy to your least-charged ability [Unknown Restoration]",
            DescribeTrace(build, dodge));
        var applied = dodge.Fired[1].Outcomes[0];
        Assert.Equal(Certainty.Unknown, applied.Certainty);
        Assert.Equal(Optional.Some("amount unknown"), applied.Caveat);
        Assert.Contains("Class ability -> +13% energy to your least-charged ability [Orbs Of Restoration]", DescribeExplanation(build));
        Assert.DoesNotContain(LoopGraphBuilding.BuildLoopGraph(build).Edges, edge => edge.To.StartsWith("e:", StringComparison.Ordinal) && edge.Kind == EdgeKind.Rule);
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

        Assert.Equal(matches, source.IsMatchedBy(new DamageOrigin.Weapon(WeaponSlot.Kinetic, weapon), Optional.Some(super)));
        Assert.False(source.IsMatchedBy(new DamageOrigin.Weapon(WeaponSlot.Kinetic, weapon), Optional.None<DamageType>()));
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
        Assert.Equal("+20% damage with weapon of your Super's element", glossary.DescribePassive(boost, 1, unknown.ReadSuperType()));
    }
}

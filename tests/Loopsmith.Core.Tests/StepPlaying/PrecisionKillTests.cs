using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.StepPlaying;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.StepPlaying;

/// <summary>
/// A weapon step's precision kills (ADRs D36): the player says them with <c>:precision</c>, and a kill trigger that needs
/// one (<c>hit: precision</c>, Spark of Brilliance's) fires on them and on no other kill, one that needs a body kill
/// (<c>hit: body</c>, Hatchling's chance) on every other kill only.
/// </summary>
public sealed class PrecisionKillTests
{
    private static readonly PlayerAction BodyKill = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One);
    private static readonly PlayerAction PrecisionKill = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.From(2), Zone: HitZone.Precision);
    private static readonly PlayerAction ShieldPrecisionKill = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One, ShieldBreak.Breaks, HitZone.Precision);
    private static readonly PlayerAction JoltingGrenade = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage, TargetCount.One);

    /// <summary>Spark of Brilliance's shape: a precision kill of a jolted enemy jolts the enemies around it.</summary>
    private static readonly BuildElement Brilliance = Element("brilliance", ElementKind.Fragment,
        [On(new Trigger.KillDebuffed(new DamageSource.AnySource(), [Status("jolt")], new ZoneNeed.PrecisionOnly()), Debuff("jolt"))]);

    /// <summary>A grenade that jolts what it hits, so the next kill is of a jolted enemy.</summary>
    private static readonly BuildElement JoltGrenade = Element("jolt-grenade", ElementKind.Grenade,
        [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), Debuff("jolt"))],
        ability: Optional.Some(Profile(AbilityKind.Grenade)));

    private static ValidatedBuild BrillianceBuild => ValidateBuild([Brilliance], grenade: JoltGrenade);

    private static ImmutableArray<Resolution> Play(params PlayerAction[] actions) =>
        ActionResolution.ResolveSequence(BrillianceBuild, ActionResolution.CreateInitialState(), [.. actions]);

    [Fact]
    public void Precision_is_a_weapon_steps_option_with_its_token_and_label()
    {
        Assert.Equal("energy:kill:2:precision", PrecisionKill.ToActionToken());
        Assert.Equal("energy:kill:shield:precision", ShieldPrecisionKill.ToActionToken());
        Assert.Equal(new Result<PlayerAction, string>.Ok(PrecisionKill), ActionTokenParsing.ParseActionToken("Energy:Kill:2:Precision"));
        Assert.Equal(new Result<PlayerAction, string>.Ok(ShieldPrecisionKill), ActionTokenParsing.ParseActionToken("energy:kill:shield:precision"));
        Assert.Equal(new Result<PlayerAction, string>.Ok(ShieldPrecisionKill), ActionTokenParsing.ParseActionToken("energy:kill:precision:shield"));
        Assert.Equal(new Result<PlayerAction, string>.Ok(new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Damage, TargetCount.From(2), Zone: HitZone.Precision)),
            ActionTokenParsing.ParseActionToken("kinetic:hit:2:precision"));
        Assert.Equal("Test Rifle (💀 kill 2 · 🎯 precision)", BrillianceBuild.DescribeAction(PrecisionKill));
        Assert.Equal("Test Rifle (💀 kill · 🛡 shield · 🎯 precision)", BrillianceBuild.DescribeAction(ShieldPrecisionKill));
    }

    [Theory]
    [InlineData("grenade:kill:precision")]          // a weapon's option only
    [InlineData("super:kill:precision")]
    [InlineData("class:precision")]
    [InlineData("energy:reload:precision")]         // a reload takes no option
    [InlineData("energy:precision:kill")]           // the option goes last
    [InlineData("energy:kill:precision:precision")] // each option once
    [InlineData("energy:kill:last:precision")]      // :last is an ability's
    public void A_precision_option_off_a_weapon_hit_or_kill_is_an_unknown_action(string token) =>
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken(token));

    [Fact]
    public void A_precision_kill_of_a_jolted_enemy_fires_the_precision_rule_for_sure_and_a_body_kill_fires_none()
    {
        var precise = Play(JoltingGrenade, PrecisionKill)[1];
        var body = Play(JoltingGrenade, BodyKill)[1];

        var fired = precise.Fired.Where(rule => rule.Source.Value == "brilliance").ToImmutableArray();
        Assert.Equal(2, fired.Length);   // both kills are precision kills
        Assert.All(fired, rule => Assert.Equal(Likelihood.Always, rule.Likelihood));
        Assert.All(fired, rule => Assert.Equal(HitZone.Precision, Assert.IsType<GameEvent.Killed>(rule.Trigger).Zone));
        Assert.DoesNotContain(body.Fired, rule => rule.Source.Value == "brilliance");
    }

    [Fact]
    public void A_precision_kill_fires_the_kill_rules_that_need_none()
    {
        var anyKill = Element("any-kill", ElementKind.Fragment, [On(new Trigger.KillAny(new DamageSource.AnyWeapon()), Buff("amplified"))]);

        var resolution = ActionResolution.ResolveAction(ValidateBuild([anyKill]), ActionResolution.CreateInitialState(), PrecisionKill);

        Assert.Equal(2, resolution.Fired.Count(rule => rule.Source.Value == "any-kill"));
    }

    /// <summary>Hatchling's shape: fewer than 3 kills hatch a Threadling for sure on a precision kill, by chance on a body kill.</summary>
    private static readonly BuildElement Hatchling = Element("hatchling", ElementKind.Fragment,
    [
        On(new Trigger.KillFewer(new DamageSource.AnyWeapon(), TargetCount.From(3), new ZoneNeed.PrecisionOnly()), Buff("amplified")),
        On(new Trigger.KillFewer(new DamageSource.AnyWeapon(), TargetCount.From(3), new ZoneNeed.BodyOnly()), Buff("amplified")) with { Likelihood = Likelihood.Chance },
    ]);

    [Fact]
    public void A_body_kill_rule_fires_on_a_body_kill_step_and_never_on_a_precision_kill_step()
    {
        var build = ValidateBuild([Hatchling]);
        var body = ActionResolution.ResolveAction(build, ActionResolution.CreateInitialState(), BodyKill);
        var precise = ActionResolution.ResolveAction(build, ActionResolution.CreateInitialState(), PrecisionKill);

        Assert.Equal([Likelihood.Chance], body.Fired.Where(rule => rule.Source.Value == "hatchling").Select(rule => rule.Likelihood));
        Assert.Equal([Likelihood.Always], precise.Fired.Where(rule => rule.Source.Value == "hatchling").Select(rule => rule.Likelihood));
    }

    [Fact]
    public void The_designer_offers_precision_on_a_weapon_when_a_rule_needs_a_body_kill()
    {
        var bodyOnly = Element("body-only", ElementKind.Fragment, [On(new Trigger.KillAny(new DamageSource.AnyWeapon(), new ZoneNeed.BodyOnly()), Buff("amplified"))]);

        Assert.Equal([StepOption.Precision], LoopDesigning.ListStepOptions(ValidateBuild([bodyOnly]), BodyKill));
    }

    [Fact]
    public void A_body_kill_trigger_reads_as_one()
    {
        var trigger = new Trigger.KillFewer(new DamageSource.AnySource(), TargetCount.From(3), new ZoneNeed.BodyOnly());

        Assert.EndsWith(" (body)", Glossary.DescribeTrigger(trigger, Optional.None<DamageType>()));
    }

    [Fact]
    public void The_designer_offers_precision_on_a_weapon_only_when_a_rule_needs_a_precision_kill()
    {
        Assert.Equal([StepOption.Precision], LoopDesigning.ListStepOptions(BrillianceBuild, BodyKill));
        Assert.Empty(LoopDesigning.ListStepOptions(BrillianceBuild, JoltingGrenade));
        Assert.Empty(LoopDesigning.ListStepOptions(ValidateBuild([]), BodyKill));
        var precise = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One, Zone: HitZone.Precision);
        Assert.Equal(precise, LoopDesigning.SetStepOption(BodyKill, StepOption.Precision, true));
        Assert.Equal(BodyKill, LoopDesigning.SetStepOption(precise, StepOption.Precision, false));
    }

    [Fact]
    public void A_precision_kill_trigger_reads_as_one()
    {
        var trigger = new Trigger.KillDebuffed(new DamageSource.AnySource(), [Status("jolt")], new ZoneNeed.PrecisionOnly());

        Assert.Equal("Kill Jolted target (precision)", Glossary.DescribeTrigger(trigger, Optional.None<DamageType>()));
    }
}

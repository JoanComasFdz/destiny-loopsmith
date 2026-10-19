using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.StepPlaying;
using Loopsmith.Core.TraceRendering;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.StepPlaying;

/// <summary>
/// A slide over an ammo brick (Spark of Momentum): a step the player declares with the weapon in hand, a <c>slideOverAmmo</c>
/// trigger that names a weapon like a reload's, and a refill that is no reload (Compendium Weapon Perks#250: a refill sets
/// off no on-reload perk). No ammo is counted, so whether the brick's ammo isn't full is the player's call.
/// </summary>
public sealed class AmmoSlideTests
{
    private static readonly PlayerAction SlideEnergy = new PlayerAction.SlideOverAmmo(WeaponSlot.Energy);
    private static readonly PlayerAction SlideKinetic = new PlayerAction.SlideOverAmmo(WeaponSlot.Kinetic);

    private static BuildElement CreateFragment(string id, DamageSource via) =>
        Element(id, ElementKind.Fragment, [On(new Trigger.SlideOverAmmo(via), Buff("amplified"))]);

    private static DesignSession StartDesign(ValidatedBuild build) =>
        LoopDesigning.CreateSession(build, new LoopDesign(
            "Test", Optional.None<string>(), Optional.None<string>(), Optional.None<CatalogVersion>(), new SourceText("b", ""), []));

    private static ImmutableArray<Resolution> ResolveSteps(ValidatedBuild build, params PlayerAction[] actions) =>
        ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), [.. actions]);

    private static string DescribeTrace(ValidatedBuild build, Resolution resolution) =>
        string.Join("\n", TraceRenderer.RenderResolution(build, resolution, new TraceOptions(false, false, false)).Select(line => line.ToPlainText()));

    [Fact]
    public void A_slide_over_an_ammo_brick_fires_the_slide_rules_of_the_weapon_in_hand_and_not_its_reload_rules()
    {
        var reloadPerk = Element("reload-mod", ElementKind.ArmorMod, [On(new Trigger.Reload(new DamageSource.AnySource()), Buff("amplified"))]);
        var build = ValidateBuild([CreateFragment("momentum-probe", new DamageSource.AnySource()), CreateFragment("solar-probe", new DamageSource.WeaponOfType(DamageType.Solar))], mods: [reloadPerk]);

        var step = Assert.Single(ResolveSteps(build, SlideEnergy));

        var fired = Assert.Single(step.Fired);   // not the Solar weapon's, and no reload: a refill is no reload
        Assert.Equal(ElementId.From("momentum-probe"), fired.Source);
        Assert.Equal(new GameEvent.SlidOverAmmo(new DamageOrigin.Weapon(WeaponSlot.Energy, DamageType.Arc, Optional.None<AmmoType>())), fired.Trigger);
        Assert.Equal("Slide over an ammo brick with Test Rifle", build.DescribeAction(SlideEnergy));
        Assert.StartsWith("#1 Slide over an ammo brick with Test Rifle\n  Slid over an ammo brick with Test Rifle → Amplified", DescribeTrace(build, step));
    }

    [Fact]
    public void A_slide_over_an_ammo_brick_with_no_weapon_in_the_slot_is_blocked()
    {
        var step = Assert.Single(ResolveSteps(ValidateBuild([CreateFragment("momentum-probe", new DamageSource.AnySource())]), SlideKinetic));

        Assert.Equal(Optional.Some("No weapon in the Kinetic slot — nothing happens."), step.Blocked);
    }

    [Fact]
    public void A_slide_over_an_ammo_brick_is_offered_on_every_weapon_and_enabled_only_when_a_rule_reacts_to_that_weapon()
    {
        var none = LoopDesigning.ListTriggerOptions(StartDesign(ValidateBuild([])));
        var solar = LoopDesigning.ListTriggerOptions(StartDesign(ValidateBuild([CreateFragment("solar-probe", new DamageSource.WeaponOfType(DamageType.Solar))])));
        var any = LoopDesigning.ListTriggerOptions(StartDesign(ValidateBuild([CreateFragment("momentum-probe", new DamageSource.AnySource())])));

        Assert.False(Assert.Single(none, option => option.Action == SlideEnergy).IsEnabled);
        Assert.False(Assert.Single(solar, option => option.Action == SlideEnergy).IsEnabled);
        Assert.True(Assert.Single(any, option => option.Action == SlideEnergy).IsEnabled);
    }

    [Fact]
    public void A_slide_over_an_ammo_brick_token_round_trips_and_takes_no_count_or_option()
    {
        Assert.Equal(SlideEnergy, Assert.IsType<Result<PlayerAction, string>.Ok>(ActionTokenParsing.ParseActionToken("energy:ammo")).Value);
        Assert.Equal("power:ammo", new PlayerAction.SlideOverAmmo(WeaponSlot.Power).ToActionToken());
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("energy:ammo:3"));
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("energy:ammo:shield"));
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("grenade:ammo"));
    }

    [Fact]
    public void A_slide_over_an_ammo_brick_trigger_reads_as_the_player_says_it()
    {
        Assert.Equal("Slide over an ammo brick", Glossary.DescribeTrigger(new Trigger.SlideOverAmmo(new DamageSource.AnySource()), Optional.None<DamageType>()));
        Assert.Equal("Slide over an ammo brick with Arc weapon", Glossary.DescribeTrigger(new Trigger.SlideOverAmmo(new DamageSource.WeaponOfType(DamageType.Arc)), Optional.None<DamageType>()));
    }
}

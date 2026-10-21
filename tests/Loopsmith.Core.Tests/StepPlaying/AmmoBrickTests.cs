using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.StepPlaying;
using Loopsmith.Core.TraceRendering;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.StepPlaying;

/// <summary>
/// An ammo brick picked up (ADRs D43): a step the player declares, a Special or a Heavy brick, walked or slid over
/// (<c>ammo:heavy:slide</c>), always available since bricks drop from enemies; an <c>ammoPickedUp</c> trigger on any brick, one
/// brick, or only a slide (Spark of Momentum). No ammo is counted, and the refill some perks give is no reload.
/// </summary>
public sealed class AmmoBrickTests
{
    private static readonly PlayerAction PickUpSpecial = new PlayerAction.PickUpAmmo(AmmoBrick.Special);
    private static readonly PlayerAction PickUpHeavy = new PlayerAction.PickUpAmmo(AmmoBrick.Heavy);
    private static readonly PlayerAction SlideOverSpecial = new PlayerAction.PickUpAmmo(AmmoBrick.Special, BrickPickup.Slide);

    private static BuildElement CreateProbe(string id, Trigger.PickUpAmmo on) =>
        Element(id, ElementKind.Fragment, [On(on, Buff("amplified"))]);

    private static readonly BuildElement AnyBrick = CreateProbe("any-brick", new Trigger.PickUpAmmo(Optional.None<AmmoBrick>(), BrickNeed.AnyPickup));
    private static readonly BuildElement HeavyBrick = CreateProbe("heavy-brick", new Trigger.PickUpAmmo(Optional.Some(AmmoBrick.Heavy), BrickNeed.AnyPickup));
    private static readonly BuildElement SlideOnly = CreateProbe("slide-only", new Trigger.PickUpAmmo(Optional.None<AmmoBrick>(), BrickNeed.Slide));

    private static DesignSession StartDesign(ValidatedBuild build) =>
        LoopDesigning.CreateSession(build, new LoopDesign(
            "Test", Optional.None<string>(), Optional.None<string>(), Optional.None<CatalogVersion>(), new SourceText("b", ""), []));

    private static ImmutableArray<Resolution> ResolveSteps(ValidatedBuild build, params PlayerAction[] actions) =>
        ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), [.. actions]);

    private static ImmutableArray<string> ListFiredSources(ValidatedBuild build, PlayerAction action) =>
        [.. Assert.Single(ResolveSteps(build, action)).Fired.Select(fired => fired.Source.Value)];

    private static string DescribeTrace(ValidatedBuild build, Resolution resolution) =>
        string.Join("\n", TraceRenderer.RenderResolution(build, resolution, new TraceOptions(false, false, false)).Select(line => line.ToPlainText()));

    [Fact]
    public void A_plain_pickup_fires_the_pickup_rules_of_its_brick_but_not_the_slide_rules()
    {
        var build = ValidateBuild([AnyBrick, HeavyBrick, SlideOnly]);

        Assert.Equal(["any-brick"], ListFiredSources(build, PickUpSpecial));
        Assert.Equal(["any-brick", "heavy-brick"], ListFiredSources(build, PickUpHeavy));
    }

    [Fact]
    public void A_slide_over_a_brick_is_a_pickup_too_and_fires_the_slide_rules()
    {
        var build = ValidateBuild([AnyBrick, HeavyBrick, SlideOnly]);

        Assert.Equal(["any-brick", "slide-only"], ListFiredSources(build, SlideOverSpecial));
    }

    [Fact]
    public void A_pickup_is_never_blocked_fires_no_reload_and_reads_as_the_player_says_it()
    {
        var reloadPerk = Element("reload-mod", ElementKind.ArmorMod, [On(new Trigger.Reload(new DamageSource.AnySource()), Buff("amplified"))]);
        var build = ValidateBuild([SlideOnly], mods: [reloadPerk]);

        var step = Assert.Single(ResolveSteps(build, SlideOverSpecial));

        Assert.Equal(Optional.None<string>(), step.Blocked);
        var fired = Assert.Single(step.Fired);   // a refill is no reload: no reload rule fires
        Assert.Equal(new GameEvent.AmmoPickedUp(AmmoBrick.Special, BrickPickup.Slide), fired.Trigger);
        Assert.Equal("Slide over a Special ammo brick", build.DescribeAction(SlideOverSpecial));
        Assert.Equal("Pick up a Heavy ammo brick", build.DescribeAction(PickUpHeavy));
        Assert.StartsWith("#1 Slide over a Special ammo brick\n  Slid over a Special ammo brick → Amplified", DescribeTrace(build, step));
    }

    [Fact]
    public void Both_bricks_are_always_offered_and_the_slide_option_only_when_a_rule_reacts_to_a_slide()
    {
        var bare = ValidateBuild([AnyBrick]);
        var sliding = ValidateBuild([SlideOnly]);
        var offered = LoopDesigning.ListTriggerOptions(StartDesign(ValidateBuild([])))
            .Where(option => option.Group == TriggerGroup.AmmoBrick)
            .Select(option => option.Action);

        Assert.Equal([PickUpSpecial, PickUpHeavy], offered);
        Assert.Empty(LoopDesigning.ListStepOptions(bare, PickUpSpecial));
        Assert.Equal([StepOption.Slide], LoopDesigning.ListStepOptions(sliding, PickUpSpecial));
        Assert.Equal(SlideOverSpecial, LoopDesigning.SetStepOption(PickUpSpecial, StepOption.Slide, true));
        Assert.Equal(PickUpSpecial, LoopDesigning.SetStepOption(SlideOverSpecial, StepOption.Slide, false));
    }

    [Theory]
    [InlineData("ammo:special")]
    [InlineData("ammo:heavy")]
    [InlineData("ammo:heavy:slide")]
    public void An_ammo_brick_token_round_trips(string token) =>
        Assert.Equal(token, Assert.IsType<Result<PlayerAction, string>.Ok>(ActionTokenParsing.ParseActionToken(token)).Value.ToActionToken());

    [Theory]
    [InlineData("ammo")]
    [InlineData("ammo:primary")]
    [InlineData("ammo:heavy:3")]
    [InlineData("ammo:heavy:slide:slide")]
    [InlineData("energy:ammo")]
    public void An_ammo_brick_token_takes_a_brick_and_only_the_slide(string token) =>
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken(token));

    [Fact]
    public void An_ammo_brick_trigger_reads_as_the_player_says_it()
    {
        Assert.Equal("Pick up an ammo brick", Glossary.DescribeTrigger(new Trigger.PickUpAmmo(Optional.None<AmmoBrick>(), BrickNeed.AnyPickup), Optional.None<DamageType>()));
        Assert.Equal("Pick up a Heavy ammo brick", Glossary.DescribeTrigger(new Trigger.PickUpAmmo(Optional.Some(AmmoBrick.Heavy), BrickNeed.AnyPickup), Optional.None<DamageType>()));
        Assert.Equal("Slide over an ammo brick", Glossary.DescribeTrigger(new Trigger.PickUpAmmo(Optional.None<AmmoBrick>(), BrickNeed.Slide), Optional.None<DamageType>()));
    }
}

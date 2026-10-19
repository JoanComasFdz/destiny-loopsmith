using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.StepPlaying;
using Loopsmith.Core.TraceRendering;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.StepPlaying;

/// <summary>
/// Readying a weapon (Mechaneer's Tricksleeves, ADRs D42): a step the player declares for a weapon, a <c>ready</c> trigger
/// that names a weapon like a reload's, and a damage source by weapon type (<c>weapon:sidearm</c>). Which weapon is in hand
/// isn't kept.
/// </summary>
public sealed class ReadyWeaponTests
{
    private static readonly PlayerAction ReadyEnergy = new PlayerAction.ReadyWeapon(WeaponSlot.Energy);
    private static readonly PlayerAction ReadyKinetic = new PlayerAction.ReadyWeapon(WeaponSlot.Kinetic);
    private static readonly DamageSource Sidearm = new DamageSource.WeaponOfArchetype("sidearm");

    private static BuildElement CreateFragment(string id, DamageSource via) =>
        Element(id, ElementKind.Fragment, [On(new Trigger.ReadyWeapon(via), Buff("amplified"))]);

    /// <summary>The test build with its one weapon (Test Rifle, Energy) of that archetype; none: the manifest doesn't say.</summary>
    private static ValidatedBuild ValidateWithArchetype(ImmutableArray<BuildElement> fragments, Optional<string> archetype)
    {
        var build = ValidateBuild(fragments);
        var weapons = build.Build.Weapons.Select(weapon => weapon with { Archetype = archetype }).ToImmutableArray();
        return BuildValidation.ValidateBuild(build.Build with { Weapons = weapons }, build.Catalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
    }

    private static DesignSession StartDesign(ValidatedBuild build) =>
        LoopDesigning.CreateSession(build, new LoopDesign(
            "Test", Optional.None<string>(), Optional.None<string>(), Optional.None<CatalogVersion>(), new SourceText("b", ""), []));

    private static ImmutableArray<Resolution> ResolveSteps(ValidatedBuild build, params PlayerAction[] actions) =>
        ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), [.. actions]);

    private static string DescribeTrace(ValidatedBuild build, Resolution resolution) =>
        string.Join("\n", TraceRenderer.RenderResolution(build, resolution, new TraceOptions(false, false, false)).Select(line => line.ToPlainText()));

    [Fact]
    public void Readying_a_weapon_fires_the_ready_rules_of_that_weapon_only()
    {
        var build = ValidateWithArchetype([CreateFragment("ready-probe", new DamageSource.AnySource()), CreateFragment("solar-probe", new DamageSource.WeaponOfType(DamageType.Solar))], Optional.Some("sidearm"));

        var step = Assert.Single(ResolveSteps(build, ReadyEnergy));

        var fired = Assert.Single(step.Fired);   // not the Solar weapon's
        Assert.Equal(ElementId.From("ready-probe"), fired.Source);
        Assert.Equal(new GameEvent.WeaponReadied(new DamageOrigin.Weapon(WeaponSlot.Energy, DamageType.Arc, Optional.None<AmmoType>(), Optional.Some("sidearm"))), fired.Trigger);
        Assert.Equal("Ready Test Rifle", build.DescribeAction(ReadyEnergy));
        Assert.StartsWith("#1 Ready Test Rifle\n  Readied Test Rifle → Amplified", DescribeTrace(build, step));
    }

    [Theory]
    [InlineData("sidearm", true)]
    [InlineData("hand-cannon", false)]
    [InlineData(null, false)]   // the manifest doesn't say: it is of no weapon type
    public void A_sidearm_source_matches_only_a_weapon_of_that_archetype(string? archetype, bool fires)
    {
        var build = ValidateWithArchetype([CreateFragment("sidearm-probe", Sidearm)], archetype is null ? Optional.None<string>() : Optional.Some(archetype));

        var step = Assert.Single(ResolveSteps(build, ReadyEnergy));

        Assert.Equal(fires, !step.Fired.IsEmpty);
    }

    [Fact]
    public void Readying_a_weapon_with_no_weapon_in_the_slot_is_blocked()
    {
        var step = Assert.Single(ResolveSteps(ValidateBuild([CreateFragment("ready-probe", new DamageSource.AnySource())]), ReadyKinetic));

        Assert.Equal(Optional.Some("No weapon in the Kinetic slot — nothing happens."), step.Blocked);
    }

    [Fact]
    public void Ready_is_offered_on_every_weapon_and_enabled_only_when_a_rule_reacts_to_that_weapon()
    {
        var none = LoopDesigning.ListTriggerOptions(StartDesign(ValidateBuild([])));
        var handCannon = LoopDesigning.ListTriggerOptions(StartDesign(ValidateWithArchetype([CreateFragment("sidearm-probe", Sidearm)], Optional.Some("hand-cannon"))));
        var sidearm = LoopDesigning.ListTriggerOptions(StartDesign(ValidateWithArchetype([CreateFragment("sidearm-probe", Sidearm)], Optional.Some("sidearm"))));

        Assert.False(Assert.Single(none, option => option.Action == ReadyEnergy).IsEnabled);
        Assert.False(Assert.Single(handCannon, option => option.Action == ReadyEnergy).IsEnabled);
        Assert.True(Assert.Single(sidearm, option => option.Action == ReadyEnergy).IsEnabled);
    }

    [Fact]
    public void A_ready_token_round_trips_and_takes_no_count_or_option()
    {
        Assert.Equal(ReadyEnergy, Assert.IsType<Result<PlayerAction, string>.Ok>(ActionTokenParsing.ParseActionToken("energy:ready")).Value);
        Assert.Equal("power:ready", new PlayerAction.ReadyWeapon(WeaponSlot.Power).ToActionToken());
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("energy:ready:3"));
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("energy:ready:shield"));
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("grenade:ready"));
    }

    [Fact]
    public void A_ready_trigger_reads_as_the_player_says_it()
    {
        Assert.Equal("Ready a weapon", Glossary.DescribeTrigger(new Trigger.ReadyWeapon(new DamageSource.AnySource()), Optional.None<DamageType>()));
        Assert.Equal("Ready Sidearm", Glossary.DescribeTrigger(new Trigger.ReadyWeapon(Sidearm), Optional.None<DamageType>()));
    }
}

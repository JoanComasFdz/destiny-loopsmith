using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.StepPlaying;
using Loopsmith.Core.TraceRendering;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.StepPlaying;

/// <summary>
/// A reload (ADRs D37): the player's Reload step, a <c>reload</c> trigger for one weapon, and an outcome that reloads a
/// weapon as the step would, so that weapon's reload rules fire too. No ammo is counted. And an energy grant by the
/// archetype of the weapon holding the element (Demolitionist).
/// </summary>
public sealed class ReloadingTests
{
    private static readonly PlayerAction ReloadEnergy = new PlayerAction.ReloadWeapon(WeaponSlot.Energy);
    private static readonly PlayerAction ReloadKinetic = new PlayerAction.ReloadWeapon(WeaponSlot.Kinetic);
    private static readonly PlayerAction GrenadeHit = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage, TargetCount.One);

    private static WeaponLoadout CreateWeapon(WeaponSlot slot, string name, DamageType type, Optional<string> archetype, params string[] perks) =>
        new(slot, name, type, archetype, [.. perks.Select(ElementId.From)], Optional.None<ItemHash>(), [], false, Optional.None<ItemInstanceId>(), Optional.None<ItemHash>());

    /// <summary>The test build with these weapons, and these perks in the catalog for them to roll.</summary>
    private static ValidatedBuild ValidateWithWeapons(ValidatedBuild build, ImmutableArray<BuildElement> perks, params WeaponLoadout[] weapons)
    {
        var catalog = build.Catalog with { Elements = build.Catalog.Elements.SetItems(perks.Select(perk => KeyValuePair.Create(perk.Id, perk))) };
        return BuildValidation.ValidateBuild(build.Build with { Weapons = [.. weapons] }, catalog).Match(
            ok => ok.Value,
            error => throw new InvalidOperationException(error.Failure));
    }

    private static ImmutableArray<Resolution> ResolveSteps(ValidatedBuild build, params PlayerAction[] actions) =>
        ActionResolution.ResolveSequence(build, ActionResolution.CreateInitialState(), [.. actions]);

    private static string DescribeTrace(ValidatedBuild build, Resolution resolution) =>
        string.Join("\n", TraceRenderer.RenderResolution(build, resolution, new TraceOptions(false, false, false)).Select(line => line.ToPlainText()));

    private static BuildElement CreatePerk(string id, params Rule[] rules) =>
        Element(id, ElementKind.WeaponPerk, [.. rules]) with { Affinity = Affinity.Neutral };

    private static readonly BuildElement ReloadPerk = CreatePerk("reload-perk", On(new Trigger.Reload(new DamageSource.ThisWeapon()), Buff("amplified")));

    private static readonly BuildElement GrenadeReloads = CreatePerk("grenade-reloads", On(new Trigger.AbilityCast(AbilityKind.Grenade), new Outcome.ReloadWeapon(new DamageSource.ThisWeapon())));

    [Fact]
    public void A_reload_step_fires_the_reload_rules_of_its_weapon_and_not_of_another()
    {
        var build = ValidateWithWeapons(ValidateBuild([]), [ReloadPerk],
            CreateWeapon(WeaponSlot.Kinetic, "Pistol", DamageType.Kinetic, Optional.None<string>()),
            CreateWeapon(WeaponSlot.Energy, "Rifle", DamageType.Arc, Optional.None<string>(), "reload-perk"));

        var steps = ResolveSteps(build, ReloadKinetic, ReloadEnergy);

        Assert.Empty(steps[0].Fired);
        var fired = Assert.Single(steps[1].Fired);
        Assert.Equal(ElementId.From("reload-perk"), fired.Source);
        Assert.Equal(new GameEvent.Reloaded(new DamageOrigin.Weapon(WeaponSlot.Energy, DamageType.Arc)), fired.Trigger);
        Assert.Equal("Reload Rifle", build.DescribeAction(ReloadEnergy));
        Assert.StartsWith("#2 Reload Rifle\n  Rifle reloaded → Amplified", DescribeTrace(build, steps[1]));
    }

    [Fact]
    public void A_reload_step_with_no_weapon_in_the_slot_is_blocked()
    {
        var step = Assert.Single(ResolveSteps(ValidateBuild([]), ReloadKinetic));

        Assert.Equal(Optional.Some("No weapon in the Kinetic slot — nothing happens."), step.Blocked);
    }

    [Fact]
    public void A_reload_outcome_reloads_the_weapon_holding_the_element_and_fires_its_reload_rules()
    {
        var build = ValidateWithWeapons(ValidateBuild([]), [ReloadPerk, GrenadeReloads],
            CreateWeapon(WeaponSlot.Kinetic, "Pistol", DamageType.Kinetic, Optional.None<string>(), "reload-perk"),
            CreateWeapon(WeaponSlot.Energy, "Rifle", DamageType.Arc, Optional.None<string>(), "grenade-reloads", "reload-perk"));

        var step = Assert.Single(ResolveSteps(build, GrenadeHit));

        var reload = Assert.Single(step.Fired, rule => rule.Source == ElementId.From("grenade-reloads"));
        Assert.Equal(new Outcome.ReloadWeapon(new DamageSource.WeaponInSlot(WeaponSlot.Energy)), Assert.Single(reload.Outcomes).Outcome);
        var followed = Assert.Single(step.Fired, rule => rule.Source == ElementId.From("reload-perk"));
        Assert.Equal(new GameEvent.Reloaded(new DamageOrigin.Weapon(WeaponSlot.Energy, DamageType.Arc)), followed.Trigger);   // the Rifle's copy, not the Pistol's
        Assert.Equal(
            "#1 Grenade (🥊 hit)\n  Grenade thrown → reloads your energy weapon [Grenade Reloads]\n    ↳ Rifle reloaded → Amplified (10s) [Reload Perk]",
            DescribeTrace(build, step));
    }

    [Fact]
    public void This_weapons_reload_trigger_and_outcome_bind_to_the_weapon_holding_it()
    {
        var weapon = CreateWeapon(WeaponSlot.Power, "Machine Gun", DamageType.Arc, Optional.None<string>());

        var trigger = Assert.Single(ReloadPerk.BindToWeapon(weapon).Rules).On;
        var outcome = Assert.Single(Assert.Single(GrenadeReloads.BindToWeapon(weapon).Rules).Then);

        Assert.Equal(new Trigger.Reload(new DamageSource.WeaponInSlot(WeaponSlot.Power)), trigger);
        Assert.Equal(new Outcome.ReloadWeapon(new DamageSource.WeaponInSlot(WeaponSlot.Power)), outcome);
        Assert.Equal("Reload this weapon", Glossary.DescribeTrigger(ReloadPerk.Rules[0].On, Optional.None<DamageType>()));
        Assert.Equal("reloads this weapon", Glossary.DescribeOutcome(GrenadeReloads.Rules[0].Then[0], Optional.None<DamageType>()));
    }

    [Fact]
    public void An_energy_grant_by_archetype_takes_the_weapons_amount_and_reads_both_when_its_archetype_is_unknown()
    {
        var perk = CreatePerk("demolition", On(
            new Trigger.KillAny(new DamageSource.ThisWeapon()),
            new Outcome.GrantEnergy(AbilityKind.Grenade, new EnergyGrant.ByArchetype(new GameValue.Known(0.10m), ["shotgun", "sniper-rifle"], new GameValue.Known(0.20m)))));

        var amounts = new[] { Optional.Some("shotgun"), Optional.Some("hand-cannon"), Optional.None<string>() }
            .Select(archetype => Assert.IsType<Outcome.GrantEnergy>(Assert.Single(Assert.Single(perk.BindToWeapon(CreateWeapon(WeaponSlot.Energy, "Gun", DamageType.Arc, archetype)).Rules).Then)))
            .Select(grant => Glossary.DescribeOutcome(grant, Optional.None<DamageType>()))
            .ToArray();

        Assert.Equal(["+20% grenade energy", "+10% grenade energy", "+10% grenade energy (20% on Shotguns and Sniper Rifles)"], amounts);
    }

    [Fact]
    public void A_reload_token_round_trips_and_takes_no_count_or_option()
    {
        Assert.Equal(ReloadEnergy, Assert.IsType<Result<PlayerAction, string>.Ok>(ActionTokenParsing.ParseActionToken("energy:reload")).Value);
        Assert.Equal("power:reload", new PlayerAction.ReloadWeapon(WeaponSlot.Power).ToActionToken());
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("energy:reload:3"));
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("energy:reload:shield"));
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken("grenade:reload"));
    }
}

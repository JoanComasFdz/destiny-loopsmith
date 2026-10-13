using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Core.Tests.TraceRendering;

/// <summary>
/// The state at the end of a step, laid out like the game's HUD: buffs on you, buffs on your weapons (a glossary
/// <c>weaponBuff</c>: Slice, Unraveling Rounds), debuffs on the pack, pickups on the ground, the passives that hold.
/// </summary>
public sealed class StepStateShapingTests
{
    private static readonly string RulesDirectory = RepoFiles.ToPath("rules");                                   // pure

    private static readonly Result<ImmutableArray<SourceText>, string> RuleFiles = FileSourceFetching.ReadRuleFiles(RulesDirectory);   // impure

    private static readonly RuleCatalog Catalog =
        RuleFiles.Bind(CatalogLoading.ParseCatalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    [Fact]
    public void A_weapon_buff_sits_with_the_weapons_and_names_the_weapon_whose_perk_gives_it()
    {
        var played = Play(
            new PlayerAction.UseClassAbility(),
            new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One));

        var state = LoopDesigning.ShapeStepState(played, played.Resolutions[1]);

        Assert.Equal(["Slice"], state.Weapons.Select(entry => entry.Text));
        Assert.DoesNotContain(state.Buffs, entry => entry.Text == "Slice");
        Assert.Contains(state.Debuffs, entry => entry.Text == "Sever" && entry.Affinity == Affinity.Strand);
        Assert.Contains(state.Ground, entry => entry.Keyword == new KeywordRef.Pickup(PickupId.From("orb-of-power")));
        Assert.Equal([0], LoopDesigning.ListWeaponsGiving(played.Build, StatusId.From("slice")));             // Festival Flight rolls Slice
        Assert.Empty(LoopDesigning.ListWeaponsGiving(played.Build, StatusId.From("unraveling-rounds")));     // an artifact perk gives it
    }

    [Fact]
    public void An_entry_carries_its_element_and_the_glossarys_facts_and_a_buffs_own_effect_is_not_one_it_switches_on()
    {
        var played = Play(
            new PlayerAction.UseClassAbility(),
            new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One));

        var state = LoopDesigning.ShapeStepState(played, played.Resolutions[1]);

        var slice = Assert.Single(state.Weapons);
        Assert.Equal((Optional.Some(ElementId.From("slice")), Optional.Some("up to x5 · 8s")), (slice.Element, slice.Facts));
        var woven = Assert.Single(state.Buffs, entry => entry.Text == "Woven Mail");
        Assert.Equal(Optional.Some(ElementId.From("woven-mail")), woven.Element);
        Assert.Empty(woven.SwitchesOn);                                                          // its 45% resistance is Woven Mail's own: in its details
    }

    [Fact]
    public void Amplified_switches_on_Spark_of_Frequency_and_Ionic_Overclock_does_nothing_without_a_weapon_of_its_types()
    {
        var played = Play(
            new PlayerAction.UseClassAbility(),
            new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One));
        var amplified = StatusId.From("amplified");

        var state = LoopDesigning.ShapeStepState(played, played.Resolutions[1]);
        var details = Assert.Single(LoopDesigning.ExplainInteractions(played.Build, new KeywordRef.Status(amplified)), group => group.Heading == "While Amplified").Interactions;

        var entry = Assert.Single(state.Buffs, buff => buff.Text == "Amplified");
        Assert.Equal(["Spark of Frequency"], entry.SwitchesOn.Select(effect => effect.SourceName));
        Assert.Equal("+1 Bolt Charge per gain", entry.SwitchesOn[0].Text);
        Assert.Empty(state.WeaponEffects);
        Assert.Equal(["Flow State", "Spark of Frequency", "Ionic Overclock"], details.Select(line => line.SourceName));
        Assert.Equal(
            ("Linear Fusion Rifles, Fusion Rifles and Heat Weapons: +? Handling, +? Reload", Optional.Some("No weapon in this build is one of them, so it does nothing here.")),
            (details[2].Text, details[2].Note));
        Assert.Contains(played.Build.Issues, issue => issue.Severity == Severity.Info
            && issue.Message == "Ionic Overclock changes Linear Fusion Rifles, Fusion Rifles and Heat Weapons, and no weapon in this build is one, so it does nothing here.");
    }

    [Fact]
    public void A_weapon_of_its_types_shows_Ionic_Overclock_with_that_weapon_while_Amplified()
    {
        var owners = Play().Build;
        var fusion = owners.Build.Weapons[2] with { Archetype = Optional.Some("fusion-rifle") };
        var build = BuildValidation.ValidateBuild(owners.Build with { Weapons = owners.Build.Weapons.SetItem(2, fusion) }, Catalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        var played = PlayOn(build,
            new PlayerAction.UseClassAbility(),
            new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One));

        var state = LoopDesigning.ShapeStepState(played, played.Resolutions[1]);

        var effect = Assert.Single(state.WeaponEffects);
        Assert.Equal("Ionic Overclock", effect.SourceName);
        Assert.Equal([2], effect.Weapons);
        Assert.Contains(state.Buffs.Single(buff => buff.Text == "Amplified").SwitchesOn, switched => switched.SourceName == "Ionic Overclock");
        var whileAmplified = Assert.Single(LoopDesigning.ExplainInteractions(build, new KeywordRef.Status(StatusId.From("amplified"))), group => group.Heading == "While Amplified");
        Assert.Equal(Optional.Some($"On {fusion.Name}."), whileAmplified.Interactions[2].Note);
        Assert.DoesNotContain(build.Issues, issue => issue.Message.StartsWith("Ionic Overclock", StringComparison.Ordinal));
        Assert.Empty(LoopDesigning.ShapeStepState(played, played.Resolutions[0]).WeaponEffects);    // not Amplified yet
    }

    [Fact]
    public void A_weapon_buff_is_a_buff_to_the_engine_it_can_be_declared_at_max_and_ended()
    {
        var played = Play(
            new PlayerAction.UseClassAbility(),
            new PlayerAction.Declare(new StateDeclaration.ReachMax(StatusId.From("slice"))),
            new PlayerAction.UseClassAbility(),
            new PlayerAction.Declare(new StateDeclaration.EndStatus(StatusId.From("slice"))));

        Assert.All(played.Resolutions, resolution => Assert.False(resolution.Blocked.IsSome()));
        Assert.Contains(played.Resolutions[1].Fired, fired => fired.Source.Value == "slice");                 // its 5 hits done: Slice ends
        Assert.Empty(LoopDesigning.ShapeStepState(played, played.Resolutions[3]).Weapons);
    }

    [Fact]
    public void The_CLIs_state_lines_follow_the_same_layout()
    {
        var played = Play(new PlayerAction.UseClassAbility());

        var lines = TraceRenderer.RenderState(played.Build, played.Resolutions[0].State, played.Resolutions[0].ActivePassives).Select(line => line.ToPlainText().Trim());

        Assert.Equal(
            [
                "Buffs         Reaper", "Weapons       Slice", "Debuffs       none", "Ground        none",
                "Resistance    PvE 50.9% · PvP 10%  (Guardian 34.5% × Spark of Resistance 25% [PvP 10%])",
                "against Arc combatants  PvE 58.2%  (× Harmonic Resistance 15%)",
                "against combatants' area, blast and explosive damage  PvE 58.2%  (× Concussive Dampener 15%)",
            ],
            lines);
    }

    [Fact]
    public void The_damage_resistance_multiplies_its_sources_PvE_and_PvP_and_a_resistance_against_some_combatants_has_its_own_line()
    {
        var played = Play(
            new PlayerAction.UseClassAbility(),
            new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One));   // To Shreds: Woven Mail

        var resistance = Assert.IsType<Optional<DamageResistance>.Some>(LoopDesigning.ShapeStepState(played, played.Resolutions[1]).Resistance).Value;

        var all = resistance.AllDamage;
        Assert.Equal(["Guardian", "Spark of Resistance", "Woven Mail"], all.Sources.Select(source => source.Name));
        Assert.Equal(new GameValue.Known(1m - (0.655m * 0.75m * 0.55m)), all.Pve);                       // 73%
        Assert.Equal(new GameValue.Known(1m - (0.9m * 0.75m)), all.Pvp);                                 // 32.5%: the base is against combatants only
        Assert.Equal(Optional.None<GameValue>(), all.Sources[0].Pvp);
        Assert.Equal("1 − (1 − 34.5%) × (1 − 25%) × (1 − 45%) = 73%", all.PveFormula);
        Assert.Equal("1 − (1 − 10%) × (1 − 25%) = 32.5%", all.PvpFormula);
        var arc = resistance.AgainstSome[0];
        Assert.Equal("Arc combatants", arc.Against);                                                     // Harmonic: the Super is Arc
        Assert.Equal(["Harmonic Resistance"], arc.Sources.Select(source => source.Name));
        Assert.Equal(new GameValue.Known(1m - (0.655m * 0.75m * 0.55m * 0.85m)), arc.Pve);
        Assert.Equal("combatants' area, blast and explosive damage", resistance.AgainstSome[1].Against);
    }

    [Fact]
    public void The_damage_resistance_drops_back_when_a_buff_that_gives_it_ends()
    {
        var played = Play(
            new PlayerAction.UseClassAbility(),
            new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One),
            new PlayerAction.Declare(new StateDeclaration.EndStatus(StatusId.From("woven-mail"))));

        var all = Assert.IsType<Optional<DamageResistance>.Some>(LoopDesigning.ShapeStepState(played, played.Resolutions[2]).Resistance).Value.AllDamage;

        Assert.Equal(["Guardian", "Spark of Resistance"], all.Sources.Select(source => source.Name));
        Assert.Equal(new GameValue.Known(1m - (0.655m * 0.75m)), all.Pve);
    }

    [Theory]
    [InlineData("arc-staff", "Arc Staff", 0.53)]
    [InlineData("gathering-storm", "Gathering Storm", 0.49)]
    [InlineData("storms-edge", "Storm's Edge", 0.45)]
    public void Casting_the_Super_puts_you_in_it_and_its_resistance_counts_until_the_player_ends_it(string super, string name, decimal pvp)
    {
        var owners = Play().Build;
        var build = Validate(owners.Build with { Abilities = owners.Build.Abilities with { Super = new AbilityPlug.Known(ElementId.From(super)) } });
        var played = PlayOn(build,
            new PlayerAction.CastAbility(OffensiveAbility.Super, HitOutcome.Kill, TargetCount.One),
            new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One),
            new PlayerAction.Declare(new StateDeclaration.EndStatus(StatusId.From("super"))));

        var cast = ReadAllDamage(played, 0);
        var later = ReadAllDamage(played, 1);
        var ended = ReadAllDamage(played, 2);

        var source = Assert.Single(cast.Sources, source => source.Name == name);
        Assert.Equal((new GameValue.Known(0.9m), Optional.Some<GameValue>(new GameValue.Known(pvp))), (source.Pve, source.Pvp));
        Assert.Contains(played.Resolutions[0].State.Buffs, buff => buff.Status == StatusId.From("super"));
        Assert.Contains(later.Sources, source => source.Name == name);                          // still in your Super
        Assert.DoesNotContain(ended.Sources, source => source.Name == name);                    // end:super
    }

    [Fact]
    public void Flow_States_resistance_holds_on_a_class_ability_step_while_Amplified_and_on_no_other()
    {
        var played = Play(
            new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One),   // Spark of Shock jolts, Flow State: Amplified
            new PlayerAction.UseClassAbility(),
            new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One),
            new PlayerAction.Declare(new StateDeclaration.EndStatus(StatusId.From("amplified"))),
            new PlayerAction.UseClassAbility());

        var dodge = Assert.Single(ReadAllDamage(played, 1).Sources, source => source.Name == "Flow State");

        Assert.Equal((new GameValue.Known(0.66m), Optional.Some<GameValue>(new GameValue.Known(0.32m))), (dodge.Pve, dodge.Pvp));
        Assert.DoesNotContain(ReadAllDamage(played, 0).Sources, source => source.Name == "Flow State");   // Amplified, no dodge
        Assert.DoesNotContain(ReadAllDamage(played, 2).Sources, source => source.Name == "Flow State");   // Amplified, a weapon
        Assert.DoesNotContain(ReadAllDamage(played, 4).Sources, source => source.Name == "Flow State");   // a dodge, not Amplified
    }

    [Fact]
    public void Ascensions_air_move_is_a_dodge_for_Flow_State()
    {
        var owners = Play().Build;
        var build = Validate(owners.Build with { Aspects = [ElementId.From("flow-state"), ElementId.From("ascension")] });
        var played = PlayOn(build, new PlayerAction.UseClassAbility(AbilityInput.Air));            // Ascension: Amplified

        Assert.Contains(ReadAllDamage(played, 0).Sources, source => source.Name == "Flow State");
    }

    [Fact]
    public void Amplified_resists_combatants_15_percent_in_PvE_only()
    {
        var played = Play(new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One));

        var all = ReadAllDamage(played, 0);

        var amplified = Assert.Single(all.Sources, source => source.Name == "Amplified");
        Assert.Equal((new GameValue.Known(0.15m), Optional.None<GameValue>()), (amplified.Pve, amplified.Pvp));
        Assert.Equal(new GameValue.Known(1m - (0.85m * 0.655m * 0.75m)), all.Pve);
        Assert.Equal(new GameValue.Known(0.1m), all.Pvp);                                        // Spark of Resistance's alone
    }

    [Fact]
    public void The_CLI_says_what_a_status_switches_on_under_its_state()
    {
        var played = Play(
            new PlayerAction.UseClassAbility(),
            new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One));

        var lines = TraceRenderer.RenderState(played.Build, played.Resolutions[1].State, played.Resolutions[1].ActivePassives).Select(line => line.ToPlainText().Trim()).ToArray();

        Assert.Contains("While Amplified  +1 Bolt Charge per gain [Spark of Frequency]", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("Active", StringComparison.Ordinal));
    }

    private static AllDamageResistance ReadAllDamage(DesignSession played, int step) =>
        Assert.IsType<Optional<DamageResistance>.Some>(LoopDesigning.ShapeStepState(played, played.Resolutions[step]).Resistance).Value.AllDamage;

    private static ValidatedBuild Validate(Build build) =>
        BuildValidation.ValidateBuild(build, Catalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static DesignSession PlayOn(ValidatedBuild build, params PlayerAction[] actions) =>
        actions.Aggregate(
            LoopDesigning.CreateSession(build, new LoopDesign("Test", Optional.None<string>(), Optional.None<string>(), Optional.None<CatalogVersion>(), new SourceText("b", ""), [])),
            (played, action) => LoopDesigning.AppendStep(played, action, Optional.None<string>()));

    private static DesignSession Play(params PlayerAction[] actions)
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        var file = RepoFiles.ToPath(path);                                                       // pure
        var json = File.ReadAllText(file);                                                       // impure
        var session = LoopDesigning.StartSavedDimDesign(Catalog, new SourceText(path, json))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        return actions.Aggregate(session, (played, action) => LoopDesigning.AppendStep(played, action, Optional.None<string>()));
    }
}

using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.TraceRendering;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// The weapon perks that need several enemies in one action, in the real rules (ADRs D4, D31): each fires for sure when
/// one step of its weapon reaches its count, as a chance on fewer when earlier steps may have counted, and nothing with
/// another weapon; the weapon's enemy-count selector names the count with its window ("3 in 3 s · One for All").
/// </summary>
public sealed class MultiEnemyWeaponPerksCatalogTests
{
    private static readonly Result<ImmutableArray<SourceText>, string> RuleFiles = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"));   // impure

    private static readonly RuleCatalog Catalog =
        RuleFiles.Bind(CatalogLoading.ParseCatalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    [Fact]
    public void One_for_all_powers_up_on_three_hits_for_sure_on_fewer_as_a_chance_and_never_from_another_weapon()
    {
        var session = StartWithKineticPerk("one-for-all");

        var three = Hit(session, WeaponSlot.Kinetic, 3);
        var two = Hit(session, WeaponSlot.Kinetic, 2);
        var other = Hit(session, WeaponSlot.Energy, 3);

        Assert.Contains(three.Fired, fired => fired.Source.Value == "one-for-all" && fired.Likelihood == Likelihood.Always);
        Assert.Contains(three.State.Buffs, buff => buff.Status.Value == "one-for-all");
        Assert.Contains(
            three.Fired.Where(fired => fired.Source.Value == "one-for-all").SelectMany(fired => fired.Outcomes),
            applied => applied.Outcome is Outcome.ModifyDamage { Change: GameValue.Known { Value: 0.35m } });
        Assert.Contains(two.Fired, fired => fired.Source.Value == "one-for-all" && fired.Likelihood == Likelihood.Chance);
        Assert.DoesNotContain(two.Fired, fired => fired.Source.Value == "one-for-all" && fired.Likelihood == Likelihood.Always);
        Assert.DoesNotContain(other.Fired, fired => fired.Source.Value == "one-for-all");
    }

    [Fact]
    public void The_enemy_count_selector_names_each_perks_count_with_its_window()
    {
        Assert.Contains("3 in 3 s · One for All", ListCountTexts("one-for-all"));
        Assert.Contains("3 in 3 s · Stats for All", ListCountTexts("stats-for-all"));
        Assert.Equal(   // the build's Strand Siphon needs 2 kills from this Strand weapon too; at 4 the buff is at its cap
            ["2 · Strand Siphon in 3 s, Reaper's Tithe", "3 · Reaper's Tithe", "4 · Reaper's Tithe · Reaper's Tithe x4"],
            ListCountTexts("reapers-tithe").Where(text => text.Contains("Reaper's Tithe")));
        Assert.Contains("3 in 3 s · Hatchling", ListCountTexts("hatchling"));
        Assert.Contains("3 in 3 s · Veteran's Wisdom", ListCountTexts("veterans-wisdom"));
        Assert.Contains("3 in 5 s · Unrelenting", ListCountTexts("unrelenting"));
        Assert.Contains("3 in 8 s · Radiolaria Transposer", ListCountTexts("radiolaria-transposer"));
        Assert.DoesNotContain(ListCountTexts("one-for-all", WeaponSlot.Energy), text => text.Contains("One for All"));
    }

    [Fact]
    public void Stats_for_all_shows_its_buff_and_this_weapons_stats_while_it_is_up()
    {
        var session = StartWithKineticPerk("stats-for-all");

        var three = Hit(session, WeaponSlot.Kinetic, 3);

        Assert.Contains(three.State.Buffs, buff => buff.Status.Value == "stats-for-all");
        var passive = Assert.Single(three.ActivePassives, passive => passive.Source.Value == "stats-for-all");
        Assert.Equal(
            "Kinetic weapon: +10 Range, +10 Stability, +35 Handling, +35 Reload Speed",
            Catalog.Glossary.DescribePassive(passive.Passive.Modifier, CopyCount.One, Optional.None<DamageType>()));
        var shown = StepStateShaping.ShapeStepState(session.Build, three.State, three.ActivePassives);
        var buff = Assert.Single(shown.Weapons);                                                     // one chip: the buff, with its stats
        Assert.Equal(["Kinetic weapon: +10 Range, +10 Stability, +35 Handling, +35 Reload Speed"], buff.OwnEffects);
        Assert.Empty(shown.WeaponEffects);
    }

    [Fact]
    public void The_enhanced_stats_for_all_shows_one_chip_with_its_own_duration_and_stats()
    {
        var session = StartWithKineticPerk("stats-for-all-enhanced");

        var three = Hit(session, WeaponSlot.Kinetic, 3);

        var shown = StepStateShaping.ShapeStepState(session.Build, three.State, three.ActivePassives);
        var buff = Assert.Single(shown.Weapons);                                                     // one chip, the enhanced trait's
        Assert.Equal((Optional.Some(ElementId.From("stats-for-all-enhanced")), Optional.Some("11s")), (buff.Element, buff.Facts));
        Assert.Equal(["Kinetic weapon: +10 Range, +10 Stability, +35 Handling, +35 Reload Speed"], buff.OwnEffects);
        Assert.Empty(buff.SwitchesOn);
        Assert.Empty(shown.WeaponEffects);
    }

    [Fact]
    public void With_both_copies_of_stats_for_all_the_buff_shows_the_copy_on_the_weapon_that_shot()
    {
        var session = StartWithPerks("stats-for-all", "stats-for-all-enhanced");

        var enhanced = Hit(session, WeaponSlot.Energy, 3);
        var shownEnhanced = Assert.Single(StepStateShaping.ShapeStepState(session.Build, enhanced.State, enhanced.ActivePassives).Weapons);
        var thenBase = Hit(session, WeaponSlot.Kinetic, 3);
        var shown = StepStateShaping.ShapeStepState(session.Build, thenBase.State, thenBase.ActivePassives);
        var shownBase = Assert.Single(shown.Weapons);

        Assert.Equal((Optional.Some(ElementId.From("stats-for-all-enhanced")), Optional.Some("11s")), (shownEnhanced.Element, shownEnhanced.Facts));
        Assert.Equal(["Energy weapon: +10 Range, +10 Stability, +35 Handling, +35 Reload Speed"], shownEnhanced.OwnEffects);
        Assert.Equal([1], LoopDesigning.ListWeaponsHolding(session.Build, ElementId.From("stats-for-all-enhanced")));
        Assert.Equal((Optional.Some(ElementId.From("stats-for-all")), Optional.Some("10s")), (shownBase.Element, shownBase.Facts));
        Assert.Equal(["Kinetic weapon: +10 Range, +10 Stability, +35 Handling, +35 Reload Speed"], shownBase.OwnEffects);
        Assert.Empty(shown.WeaponEffects);                                                           // the other copy's stats show nowhere
    }

    [Theory]
    [InlineData("stats-for-all", "Stats for All (10s)", "stats-for-all")]
    [InlineData("stats-for-all-enhanced", "Stats for All (11s)", "stats-for-all-enhanced")]
    [InlineData("one-for-all", "One for All (10s)", "one-for-all")]
    [InlineData("one-for-all-enhanced", "One for All (11s)", "one-for-all-enhanced")]
    [InlineData("reapers-tithe", "+2 Reaper's Tithe (15s)", "reapers-tithe")]
    [InlineData("reapers-tithe-enhanced", "+2 Reaper's Tithe (17s)", "reapers-tithe-enhanced")]
    [InlineData("slice", "+1 Slice (8s)", "slice")]
    [InlineData("slice-enhanced", "+1 Slice (9s)", "slice-enhanced")]
    [InlineData("all-star", "All-Star (6s)", "all-star")]
    [InlineData("all-star-enhanced", "All-Star (7s)", "all-star-enhanced")]
    public void The_buff_a_perk_fires_opens_the_perk_that_fired_it_its_enhanced_trait_too(string perk, string line, string opens)
    {
        var session = StartWithKineticPerk(perk);

        var resolution = PlayGivingStep(session, perk);

        var fired = StepOutcomeShaping.ShapeStepOutcomes(session.Build, resolution, new TraceOptions(false, false, false)).Events
            .SelectMany(shaped => shaped.Sources)
            .Where(source => source.Source.Value == perk)
            .SelectMany(source => source.Outcomes);
        var keyword = Assert.Single(Assert.Single(fired, outcome => outcome.Text == line).Parts, part => part.Keyword.IsSome());
        Assert.Equal(Optional.Some(ElementId.From(opens)), keyword.Keyword.Bind(found => found.Element));
    }

    /// <summary>The step that gives a perk's buff: 3 hits, 2 kills at once, a dodge (it arms Slice) or an ammo brick picked up.</summary>
    private static Resolution PlayGivingStep(DesignSession session, string perk) =>
        perk.Replace("-enhanced", "", StringComparison.Ordinal) switch
        {
            "reapers-tithe" => Kill(session, WeaponSlot.Kinetic, 2),
            "slice" => LoopDesigning.AppendStep(session, new PlayerAction.UseClassAbility(), Optional.None<string>()).Resolutions[^1],
            "all-star" => LoopDesigning.AppendStep(session, new PlayerAction.PickUpAmmo(AmmoBrick.Heavy), Optional.None<string>()).Resolutions[^1],
            _ => Hit(session, WeaponSlot.Kinetic, 3),
        };

    [Theory]
    [InlineData(1, null)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    [InlineData(6, 4)]
    public void Reapers_tithe_gives_the_stacks_of_the_enemies_killed_at_once_and_nothing_for_one(int killed, int? stacks)
    {
        var resolution = Kill(StartWithKineticPerk("reapers-tithe"), WeaponSlot.Kinetic, killed);

        var grants = resolution.Fired
            .Where(fired => fired.Source.Value == "reapers-tithe")
            .SelectMany(fired => fired.Outcomes.Select(applied => applied.Outcome).OfType<Outcome.ApplyBuff>())
            .Select(apply => ((StackGrant.Fixed)apply.Stacks).Count.Value)
            .ToImmutableArray();

        Assert.Equal(stacks is { } some ? [some] : ImmutableArray<int>.Empty, grants);
        Assert.All(resolution.Fired.Where(fired => fired.Source.Value == "reapers-tithe"), fired => Assert.Equal(Likelihood.Always, fired.Likelihood));
    }

    [Theory]
    [InlineData("hatchling")]
    [InlineData("veterans-wisdom")]
    [InlineData("unrelenting")]
    [InlineData("radiolaria-transposer")]
    public void A_kill_count_perk_fires_for_sure_on_three_kills_and_as_a_chance_on_fewer(string perk)
    {
        var session = StartWithKineticPerk(perk);

        var three = Kill(session, WeaponSlot.Kinetic, 3);
        var one = Kill(session, WeaponSlot.Kinetic, 1);

        Assert.Contains(three.Fired, fired => fired.Source.Value == perk && fired.Likelihood == Likelihood.Always);
        Assert.Contains(one.Fired, fired => fired.Source.Value == perk && fired.Likelihood == Likelihood.Chance);
        Assert.DoesNotContain(one.Fired, fired => fired.Source.Value == perk && fired.Likelihood == Likelihood.Always);
    }

    [Theory]
    [InlineData("hatchling")]
    [InlineData("veterans-wisdom")]
    public void A_precision_kill_perk_fires_for_sure_on_fewer_than_three_precision_kills_and_never_as_a_chance(string perk)
    {
        var session = StartWithKineticPerk(perk);

        var precise = LoopDesigning.AppendStep(session, new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One, Zone: HitZone.Precision), Optional.None<string>()).Resolutions[^1];

        Assert.Equal([Likelihood.Always], precise.Fired.Where(fired => fired.Source.Value == perk).Select(fired => fired.Likelihood));
        Assert.Contains(StepOption.Precision, LoopDesigning.ListStepOptions(session.Build, new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One)));
    }

    [Fact]
    public void Hatchlings_enhanced_trait_may_hatch_a_second_threadling()
    {
        var three = Kill(StartWithKineticPerk("hatchling-enhanced"), WeaponSlot.Kinetic, 3);

        var summons = three.Fired
            .Where(fired => fired.Source.Value == "hatchling-enhanced")
            .SelectMany(fired => fired.Outcomes.Where(applied => applied.Outcome is Outcome.SpawnSummon).Select(_ => fired.Likelihood))
            .ToImmutableArray();

        Assert.Equal([Likelihood.Always, Likelihood.Chance], summons);
    }

    [Fact]
    public void Veterans_wisdoms_shockwave_exhausts_the_pack_and_strikes_as_this_weapon_so_slice_severs()
    {
        var armed = LoopDesigning.AppendStep(StartWithKineticPerk("slice, veterans-wisdom"), new PlayerAction.UseClassAbility(), Optional.None<string>());

        var three = Kill(armed, WeaponSlot.Kinetic, 3);

        Assert.Contains(three.State.Target.Debuffs, debuff => debuff.Value == "exhaust");
        var strike = Assert.Single(
            three.Fired.Where(fired => fired.Source.Value == "veterans-wisdom").SelectMany(fired => fired.Outcomes),
            applied => applied.Outcome is Outcome.WeaponStrike);
        Assert.Equal(Optional.Some(DamageType.Kinetic), ((Outcome.WeaponStrike)strike.Outcome).Type);
        Assert.Contains(three.Fired, fired => fired.Source.Value == "slice" && fired.Depth > 0);   // the shockwave's hit, as Festival Flight's
    }

    [Fact]
    public void Radiolaria_transposers_explosion_and_pool_strike_as_this_weapon_with_arc_damage()
    {
        var three = Kill(StartWithKineticPerk("radiolaria-transposer"), WeaponSlot.Kinetic, 3);

        var strikes = three.Fired
            .Where(fired => fired.Source.Value == "radiolaria-transposer")
            .SelectMany(fired => fired.Outcomes.Select(applied => applied.Outcome).OfType<Outcome.WeaponStrike>())
            .Select(strike => strike.Type)
            .ToImmutableArray();

        Assert.Equal([Optional.Some(DamageType.Arc), Optional.Some(DamageType.Arc)], strikes);
        Assert.False(Catalog.Elements[ElementId.From("radiolaria-transposer")].Inert.IsSome());
    }

    [Theory]
    [InlineData("stats-for-all", "stats-for-all")]
    [InlineData("stats-for-all-enhanced", "stats-for-all")]
    [InlineData("all-star", "all-star")]
    [InlineData("all-star-enhanced", "all-star")]
    public void A_perks_status_lists_nothing_of_the_perk_in_this_build_enhanced_or_not(string perk, string status) =>
        Assert.Empty(LoopDesigning.ExplainInteractions(StartWithKineticPerk(perk).Build, new KeywordRef.Status(StatusId.From(status))));

    [Theory]
    [InlineData("stats-for-all", "", WeaponSlot.Kinetic, "stats-for-all")]
    [InlineData("stats-for-all-enhanced", "", WeaponSlot.Kinetic, "stats-for-all-enhanced")]
    [InlineData("stats-for-all", "stats-for-all-enhanced", WeaponSlot.Energy, "stats-for-all-enhanced")]
    [InlineData("stats-for-all", "stats-for-all-enhanced", WeaponSlot.Kinetic, "stats-for-all")]
    public void Stats_for_all_named_in_a_step_opens_the_copy_that_gave_it(string kinetic, string energy, WeaponSlot slot, string opened)
    {
        var session = LoopDesigning.AppendStep(StartWithPerks(kinetic, energy), new PlayerAction.FireWeapon(slot, HitOutcome.Damage, TargetCount.From(3)), Optional.None<string>());

        var shaped = LoopDesigning.ShapeStepOutcomes(session, session.Resolutions[^1], new TraceOptions(false, false, false));

        var named = shaped.Events.SelectMany(shown => shown.Sources).SelectMany(source => source.Outcomes).SelectMany(line => line.Parts)
            .Where(part => part.Text == "Stats for All")
            .Select(part => part.Keyword.Bind(keyword => keyword.Element));
        Assert.Equal([Optional.Some(ElementId.From(opened))], named);
    }

    [Theory]
    [InlineData("unrelenting", 65)]
    [InlineData("unrelenting-enhanced", 70)]
    public void Unrelenting_heals_its_hp(string perk, int hp)
    {
        var three = Kill(StartWithKineticPerk(perk), WeaponSlot.Kinetic, 3);

        var heal = Assert.Single(
            three.Fired.Where(fired => fired.Source.Value == perk).SelectMany(fired => fired.Outcomes.Select(applied => applied.Outcome)).OfType<Outcome.RestoreHealth>());
        Assert.Equal(new HealAmount.Points(new GameValue.Known(hp)), heal.Amount);
        Assert.Equal($"heals you {hp} HP", Catalog.Glossary.DescribeOutcome(heal, Optional.None<DamageType>()));
    }

    private static Resolution Hit(DesignSession session, WeaponSlot slot, int count) =>
        LoopDesigning.AppendStep(session, new PlayerAction.FireWeapon(slot, HitOutcome.Damage, TargetCount.From(count)), Optional.None<string>()).Resolutions[^1];

    private static Resolution Kill(DesignSession session, WeaponSlot slot, int count) =>
        LoopDesigning.AppendStep(session, new PlayerAction.FireWeapon(slot, HitOutcome.Kill, TargetCount.From(count)), Optional.None<string>()).Resolutions[^1];

    private static ImmutableArray<string> ListCountTexts(string perk, WeaponSlot slot = WeaponSlot.Kinetic)
    {
        var session = StartWithKineticPerk(perk);
        var choices = LoopDesigning.ListTargetCountChoices(session.Build, session.Current, new PlayerAction.FireWeapon(slot, HitOutcome.Kill, TargetCount.One));
        return [.. choices.Select(choice => Catalog.Glossary.DescribeTargetCountChoice(choice))];
    }

    /// <summary>
    /// An Arc Hunter with <paramref name="perk"/> (one perk id, or several comma-separated) on its Kinetic weapon (Festival
    /// Flight, Strand), an Arc Energy weapon (Crisis Inverted) with none, Gambler's Dodge (it arms Slice) and Strand Siphon.
    /// </summary>
    private static DesignSession StartWithKineticPerk(string perk) =>
        StartWithPerks(perk, "");

    /// <summary>Festival Flight (Kinetic) with one perk, Crisis Inverted (Energy) with another, or none for "".</summary>
    private static DesignSession StartWithPerks(string kinetic, string energy)
    {
        var text = $$"""
            name: Test
            class: hunter
            subclass: arc
            classAbility: gamblers-dodge
            armorMods: [strand-siphon]
            weapons:
              - { hash: 4019651319, perks: [{{kinetic}}] } # Festival Flight
              - { hash: 2888266564, perks: [{{energy}}] } # Crisis Inverted
            """;
        return LoopDesigning.StartDesign(Catalog, new SourceText("build.yaml", text), "Test")
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
    }
}

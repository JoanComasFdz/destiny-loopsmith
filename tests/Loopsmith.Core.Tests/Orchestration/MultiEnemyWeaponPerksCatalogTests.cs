using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// The weapon perks that need several enemies in one action, in the real rules (ADRs D4, D31): each fires for sure when
/// one step of its weapon reaches its count, as a chance on fewer when earlier steps may have counted, and nothing with
/// another weapon; the weapon's enemy-count selector names the count with its window ("3 in 3 s · One for All").
/// </summary>
public sealed class MultiEnemyWeaponPerksCatalogTests
{
    private const string BuildPath = "builds/skip-grenade-hunter/build.yaml";

    private static readonly Result<ImmutableArray<SourceText>, string> RuleFiles = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"));   // impure

    private static readonly string BuildText = File.ReadAllText(RepoFiles.ToPath(BuildPath));                                                    // impure

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
            Catalog.Glossary.DescribePassive(passive.Passive.Modifier, 1, Optional.None<DamageType>()));
    }

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
    public void A_kill_count_perk_fires_for_sure_on_three_kills_and_as_a_chance_on_fewer(string perk)
    {
        var session = StartWithKineticPerk(perk);

        var three = Kill(session, WeaponSlot.Kinetic, 3);
        var one = Kill(session, WeaponSlot.Kinetic, 1);

        Assert.Contains(three.Fired, fired => fired.Source.Value == perk && fired.Likelihood == Likelihood.Always);
        Assert.Contains(one.Fired, fired => fired.Source.Value == perk && fired.Likelihood == Likelihood.Chance);
        Assert.DoesNotContain(one.Fired, fired => fired.Source.Value == perk && fired.Likelihood == Likelihood.Always);
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
    public void Veterans_wisdom_exhausts_the_pack_and_radiolaria_transposer_says_why_it_sets_nothing_off()
    {
        var three = Kill(StartWithKineticPerk("veterans-wisdom"), WeaponSlot.Kinetic, 3);

        Assert.Contains(three.State.Target.Debuffs, debuff => debuff.Value == "exhaust");
        Assert.True(Catalog.Elements[ElementId.From("radiolaria-transposer")].Inert.IsSome());
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

    /// <summary>The creator's build with only <paramref name="perk"/> on its Kinetic weapon (Festival Flight).</summary>
    private static DesignSession StartWithKineticPerk(string perk)
    {
        var text = BuildText.Replace("perks: [slice, attrition-orbs]", $"perks: [{perk}]", StringComparison.Ordinal);
        return LoopDesigning.StartDesign(Catalog, new SourceText(BuildPath, text), "Test")
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
    }
}

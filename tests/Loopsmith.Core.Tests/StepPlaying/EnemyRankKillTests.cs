using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.StepPlaying;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.StepPlaying;

/// <summary>
/// The rank of the enemy a kill step kills (ADRs D36): the pack is minor (ADRs D7), and the player says with
/// <c>:elite</c>, <c>:miniboss</c>, <c>:champion</c> or <c>:boss</c> that a kill step or a finisher kills a stronger one. A kill
/// trigger's <c>tier:</c> needs one rank (<c>boss</c>) or that rank or stronger (<c>elite+</c>: Ascendant Escape's "Elite+").
/// </summary>
public sealed class EnemyRankKillTests
{
    private static readonly PlayerAction MinorKill = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One);
    private static readonly PlayerAction EliteKill = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One, Rank: EnemyTier.Elite);

    /// <summary>Ascendant Escape's shape: an Elite+ kill from any source gives a buff.</summary>
    private static readonly BuildElement ElitePlus = Element("elite-plus", ElementKind.Fragment,
        [On(new Trigger.KillOfTier(new DamageSource.AnySource(), new TierNeed.AtLeast(EnemyTier.Elite)), Buff("amplified"))]);

    private static readonly BuildElement BossOnly = Element("boss-only", ElementKind.Fragment,
        [On(new Trigger.KillOfTier(new DamageSource.AnySource(), new TierNeed.Exactly(EnemyTier.Boss)), Buff("amplified"))]);

    private static ImmutableArray<string> ListFired(ValidatedBuild build, PlayerAction action) =>
        [.. ActionResolution.ResolveAction(build, ActionResolution.CreateInitialState(), action).Fired.Select(rule => rule.Source.Value)];

    [Theory]
    [InlineData("energy:kill:elite")]
    [InlineData("kinetic:kill:3:boss")]
    [InlineData("power:kill:shield:precision:champion")]
    [InlineData("grenade:kill:miniboss")]
    [InlineData("grenade:kill:last:elite")]
    [InlineData("melee:slide:kill:boss")]
    [InlineData("super:kill:2:elite")]
    [InlineData("finisher:elite")]
    public void A_rank_on_a_kill_or_a_finisher_round_trips(string token)
    {
        var action = Assert.IsType<Result<PlayerAction, string>.Ok>(ActionTokenParsing.ParseActionToken(token)).Value;

        Assert.Equal(token, action.ToActionToken());
    }

    [Fact]
    public void A_rank_reads_in_any_order_with_the_other_options()
    {
        var expected = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One, ShieldBreak.Breaks, HitZone.Precision, EnemyTier.Boss);

        Assert.Equal(new Result<PlayerAction, string>.Ok(expected), ActionTokenParsing.ParseActionToken("Energy:Kill:Boss:Precision:Shield"));
    }

    [Theory]
    [InlineData("energy:hit:elite")]          // a hit kills no one
    [InlineData("grenade:elite")]
    [InlineData("class:elite")]
    [InlineData("energy:reload:elite")]
    [InlineData("energy:ammo:boss")]
    [InlineData("energy:kill:elite:boss")]    // one rank
    [InlineData("energy:kill:minor")]         // a minor is said by no option
    [InlineData("energy:kill:major")]
    [InlineData("pack:elite")]
    public void A_rank_off_a_kill_or_a_finisher_is_an_unknown_action(string token) =>
        Assert.IsType<Result<PlayerAction, string>.Error>(ActionTokenParsing.ParseActionToken(token));

    [Fact]
    public void A_rank_reads_in_the_steps_label()
    {
        var build = ValidateBuild([ElitePlus]);

        Assert.Equal("Test Rifle (💀 kill · Elite)", build.DescribeAction(EliteKill));
        Assert.Equal("Finisher (Boss)", build.DescribeAction(new PlayerAction.PerformFinisher(EnemyTier.Boss)));
        Assert.Equal("Finisher", build.DescribeAction(new PlayerAction.PerformFinisher()));
    }

    [Theory]
    [InlineData(EnemyTier.Elite, true)]
    [InlineData(EnemyTier.Miniboss, true)]
    [InlineData(EnemyTier.Champion, true)]
    [InlineData(EnemyTier.Boss, true)]
    [InlineData(EnemyTier.Minor, false)]
    public void An_elite_plus_rule_fires_on_an_elite_or_stronger_kill(EnemyTier rank, bool fires)
    {
        var kill = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One, Rank: rank);

        Assert.Equal(fires, ListFired(ValidateBuild([ElitePlus]), kill).Contains("elite-plus"));
    }

    [Fact]
    public void A_rule_for_one_rank_fires_on_that_rank_only()
    {
        var build = ValidateBuild([BossOnly]);

        Assert.Contains("boss-only", ListFired(build, new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One, Rank: EnemyTier.Boss)));
        Assert.DoesNotContain("boss-only", ListFired(build, EliteKill));
    }

    [Fact]
    public void A_kill_step_of_several_kills_one_enemy_of_its_rank_and_minors_after_it()
    {
        var anyKill = Element("any-kill", ElementKind.Fragment, [On(new Trigger.KillAny(new DamageSource.AnySource()), Buff("amplified"))]);
        var build = ValidateBuild([ElitePlus, anyKill]);

        var fired = ListFired(build, new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.From(3), Rank: EnemyTier.Elite));

        Assert.Equal(1, fired.Count(source => source == "elite-plus"));
        Assert.Equal(3, fired.Count(source => source == "any-kill"));
    }

    [Fact]
    public void An_ability_kill_and_a_finisher_kill_of_their_rank()
    {
        var build = ValidateBuild([ElitePlus]);

        Assert.Contains("elite-plus", ListFired(build, new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One, Rank: EnemyTier.Champion)));
        Assert.Contains("elite-plus", ListFired(build, new PlayerAction.PerformFinisher(EnemyTier.Elite)));
        Assert.DoesNotContain("elite-plus", ListFired(build, new PlayerAction.PerformFinisher()));
    }

    [Fact]
    public void The_designer_says_whether_a_rank_sets_something_off_and_sets_it_on_kills_only()
    {
        var hit = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Damage, TargetCount.One);

        Assert.True(LoopDesigning.ReactsToRank(ValidateBuild([ElitePlus])));
        Assert.False(LoopDesigning.ReactsToRank(ValidateBuild([])));
        Assert.Equal(EliteKill, LoopDesigning.SetKillRank(MinorKill, EnemyTier.Elite));
        Assert.Equal(hit, LoopDesigning.SetKillRank(hit, EnemyTier.Elite));
        Assert.Equal(new PlayerAction.PerformFinisher(EnemyTier.Boss), LoopDesigning.SetKillRank(new PlayerAction.PerformFinisher(), EnemyTier.Boss));
        Assert.Equal([EnemyTier.Minor, EnemyTier.Elite, EnemyTier.Miniboss, EnemyTier.Champion, EnemyTier.Boss], LoopDesigning.ListRanks());
    }

    [Fact]
    public void A_tier_kill_trigger_reads_with_its_rank()
    {
        var elitePlus = new Trigger.KillOfTier(new DamageSource.AnySource(), new TierNeed.AtLeast(EnemyTier.Elite));
        var boss = new Trigger.KillOfTier(new DamageSource.AnySource(), new TierNeed.Exactly(EnemyTier.Boss));

        Assert.EndsWith("(elite+)", Glossary.DescribeTrigger(elitePlus, Optional.None<DamageType>()));
        Assert.EndsWith("(boss)", Glossary.DescribeTrigger(boss, Optional.None<DamageType>()));
    }
}

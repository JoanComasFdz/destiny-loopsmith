using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// The rules that need several kills in one action, in the real rules (ADRs D31, D33): Spark of Discharge's counter fills
/// on three Arc weapon kills, Spark of Amplitude's two kills within 4 s and the Siphons' two within 3 s happen at once
/// in one step, and the enemy-count selector names each of them on the abilities and weapons they come from.
/// </summary>
public sealed class KillCountsCatalogTests
{
    private static readonly Result<ImmutableArray<SourceText>, string> RuleFiles = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"));   // impure

    private static readonly RuleCatalog Catalog =
        RuleFiles.Bind(CatalogLoading.ParseCatalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private const string BuildText = """
        name: Kill Counts Hunter
        class: hunter
        subclass: arc
        super: gathering-storm
        grenade: skip-grenade
        melee: combination-blow
        classAbility: gamblers-dodge
        aspects: [flow-state]
        fragments: [spark-of-shock, spark-of-discharge, spark-of-amplitude]
        exoticArmor: shinobus-vow
        armorSetBonuses: [luminopotent-4pc]
        armorMods: [harmonic-siphon, charged-up, elemental-charge]
        artifactPerks: [dielectric]
        weapons:
          - {hash: 3325463374} # Thunderlord
        """;

    [Fact]
    public void Three_Arc_weapon_kills_at_once_drop_Spark_of_Discharges_trace_for_sure_and_fewer_as_a_chance()
    {
        var session = StartDesign();
        var arc = FindArcWeapon(session.Build);

        var three = LoopDesigning.AppendStep(session, new PlayerAction.FireWeapon(arc, HitOutcome.Kill, TargetCount.From(3)), Optional.None<string>()).Resolutions[^1];
        var two = LoopDesigning.AppendStep(session, new PlayerAction.FireWeapon(arc, HitOutcome.Kill, TargetCount.From(2)), Optional.None<string>()).Resolutions[^1];

        Assert.Contains(three.Fired, fired => fired.Source.Value == "spark-of-discharge" && fired.On is Trigger.KillMultiple && fired.Likelihood == Likelihood.Always);
        Assert.DoesNotContain(two.Fired, fired => fired.Source.Value == "spark-of-discharge" && fired.On is Trigger.KillMultiple);
        Assert.Contains(two.Fired, fired => fired.Source.Value == "spark-of-discharge" && fired.On is Trigger.KillFewer && fired.Likelihood == Likelihood.Chance);
    }

    [Fact]
    public void The_count_selector_goes_up_to_what_fills_and_names_the_rules_that_need_a_count()
    {
        var session = StartDesign();
        var build = session.Build;
        var glossary = build.Catalog.Glossary;
        var arc = LoopDesigning.ListTargetCountChoices(build, session.Current, new PlayerAction.FireWeapon(FindArcWeapon(build), HitOutcome.Kill, TargetCount.One));
        var grenade = LoopDesigning.ListTargetCountChoices(build, session.Current, new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One));

        var arcTexts = arc.Select(choice => glossary.DescribeTargetCountChoice(choice)).ToImmutableArray();

        // A Thunderlord kill is one blow: Lightning Rounds' every 10th hit needs a hit step (ADRs D34), so Reign Havoc's
        // strike is the kill's one Bolt Charge.
        Assert.Equal(["1 enemy", "2 · Spark of Amplitude in 4 s, Harmonic Siphon in 3 s", "3 · Spark of Discharge", "4 enemies", "5 enemies"], arcTexts.Take(5));
        Assert.Equal("9 enemies · Bolt Charge x10", arcTexts[8]);
        Assert.Equal("20 enemies", arcTexts[^1]);   // each Bolt Charge gained gives 2.5% melee energy, at the cap too (Arc#5): still filling at the most a step offers
        Assert.Equal(
            ["1 enemy", "2 in 4 s · Spark of Amplitude", "3 enemies", "4 enemies · Armor Charge x4, Bolt Charge x10", "5 enemies", "6 enemies · melee full", "7 enemies · grenade full", "8 enemies · class ability full"],
            grenade.Select(choice => glossary.DescribeTargetCountChoice(choice)));
    }

    [Fact]
    public void Two_kills_at_once_while_Amplified_drop_Spark_of_Amplitudes_orb_for_sure_and_one_as_a_chance()
    {
        var amplified = LoopDesigning.AppendStep(StartDesign(), new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One), Optional.None<string>());

        var two = LoopDesigning.AppendStep(amplified, new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.From(2)), Optional.None<string>()).Resolutions[^1];
        var one = LoopDesigning.AppendStep(amplified, new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One), Optional.None<string>()).Resolutions[^1];

        Assert.Contains(two.Fired, fired => fired.Source.Value == "spark-of-amplitude" && fired.Likelihood == Likelihood.Always);
        Assert.Contains(one.Fired, fired => fired.Source.Value == "spark-of-amplitude" && fired.Likelihood == Likelihood.Chance);
        Assert.DoesNotContain(one.Fired, fired => fired.Source.Value == "spark-of-amplitude" && fired.Likelihood == Likelihood.Always);
    }

    private static WeaponSlot FindArcWeapon(ValidatedBuild build) =>
        build.Build.Weapons.First(weapon => weapon.Type == DamageType.Arc).Slot;

    /// <summary>
    /// An Arc Hunter with Spark of Shock (a grenade jolts, and Flow State makes a jolted kill Amplified), Spark of
    /// Discharge and Spark of Amplitude as its fragments, Thunderlord its Arc weapon, and what fills on a step's kills:
    /// Harmonic Siphon (two Arc weapon kills in 3 s), Elemental Charge's Armor Charge on an Ionic Trace (Charged Up's cap),
    /// the Luminopotent 4-piece bonus's Ionic Trace and Dielectric's Bolt Charge on a Jolted kill, and Shinobu's Vow.
    /// </summary>
    private static DesignSession StartDesign() =>
        LoopDesigning.StartDesign(Catalog, new SourceText("kill-counts.build.yaml", BuildText), "Kill counts loop")
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}

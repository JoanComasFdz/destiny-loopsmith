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

    [Fact]
    public void Three_Arc_weapon_kills_at_once_drop_Spark_of_Discharges_trace_for_sure_and_fewer_as_a_chance()
    {
        var session = StartOwnersDesign();
        var arc = FindArcWeapon(session.Build);

        var three = LoopDesigning.AppendStep(session, new PlayerAction.FireWeapon(arc, HitOutcome.Kill, TargetCount.From(3)), Optional.None<string>()).Resolutions[^1];
        var two = LoopDesigning.AppendStep(session, new PlayerAction.FireWeapon(arc, HitOutcome.Kill, TargetCount.From(2)), Optional.None<string>()).Resolutions[^1];

        Assert.Contains(three.Fired, fired => fired.Source.Value == "spark-of-discharge" && fired.On is Trigger.KillMultiple && fired.Likelihood == Likelihood.Always);
        Assert.DoesNotContain(two.Fired, fired => fired.Source.Value == "spark-of-discharge" && fired.On is Trigger.KillMultiple);
        Assert.Contains(two.Fired, fired => fired.Source.Value == "spark-of-discharge" && fired.On is Trigger.KillFewer && fired.Likelihood == Likelihood.Chance);
    }

    [Fact]
    public void The_count_selector_names_the_rules_that_need_that_many_from_that_weapon_or_ability()
    {
        var session = StartOwnersDesign();
        var build = session.Build;
        var arc = LoopDesigning.ListTargetCountChoices(build, new PlayerAction.FireWeapon(FindArcWeapon(build), HitOutcome.Kill, TargetCount.One));
        var grenade = LoopDesigning.ListTargetCountChoices(build, new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One));

        Assert.Equal(
            ["1 enemy", "2 · Spark of Amplitude in 4 s, Harmonic Siphon in 3 s", "3 · Spark of Discharge", "4 enemies"],
            arc.Take(4).Select(DomainPhrasing.DescribeTargetCountChoice));
        Assert.Equal(
            ["1 enemy", "2 in 4 s · Spark of Amplitude", "3 enemies"],
            grenade.Take(3).Select(DomainPhrasing.DescribeTargetCountChoice));
    }

    [Fact]
    public void Two_kills_at_once_while_Amplified_drop_Spark_of_Amplitudes_orb_for_sure_and_one_as_a_chance()
    {
        var amplified = LoopDesigning.AppendStep(StartOwnersDesign(), new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One), Optional.None<string>());

        var two = LoopDesigning.AppendStep(amplified, new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.From(2)), Optional.None<string>()).Resolutions[^1];
        var one = LoopDesigning.AppendStep(amplified, new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One), Optional.None<string>()).Resolutions[^1];

        Assert.Contains(two.Fired, fired => fired.Source.Value == "spark-of-amplitude" && fired.Likelihood == Likelihood.Always);
        Assert.Contains(one.Fired, fired => fired.Source.Value == "spark-of-amplitude" && fired.Likelihood == Likelihood.Chance);
        Assert.DoesNotContain(one.Fired, fired => fired.Source.Value == "spark-of-amplitude" && fired.Likelihood == Likelihood.Always);
    }

    private static WeaponSlot FindArcWeapon(ValidatedBuild build) =>
        build.Build.Weapons.First(weapon => weapon.Type == DamageType.Arc).Slot;

    /// <summary>
    /// The owner's DIM build with Spark of Shock (a grenade jolts, and Flow State makes a jolted kill Amplified), Spark of
    /// Discharge and Spark of Amplitude as its fragments.
    /// </summary>
    private static DesignSession StartOwnersDesign()
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        var json = File.ReadAllText(RepoFiles.ToPath(path));                                     // impure
        var session = LoopDesigning.StartSavedDimDesign(Catalog, new SourceText(path, json))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        ImmutableArray<ElementId> fragments = [ElementId.From("spark-of-shock"), ElementId.From("spark-of-discharge"), ElementId.From("spark-of-amplitude")];
        var build = session.Build.Build with { Fragments = fragments };
        var wearing = BuildValidation.ValidateBuild(build, Catalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        return LoopDesigning.CreateSession(wearing, session.Design);
    }
}

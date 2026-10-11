using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// The artifact's perks are part of the loop: the owner's DIM build (its share's artifact unlocks) equips the seven
/// Tablet of Ruin perks the rules know, and each fires on its trigger like any other element.
/// </summary>
public sealed class ArtifactPerkLoopTests
{
    private static readonly RuleCatalog Catalog =
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    [Fact]
    public void Each_of_the_owners_artifact_perks_fires_in_a_loop()
    {
        var steps = new (PlayerAction Action, string Fires)[]
        {
            (new PlayerAction.UseClassAbility(), ""),                                                              // arms Slice
            (new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Damage, TargetCount.One), ""),             // Severs
            (new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One), "to-shreds"),      // a weapon kill on a Severed target
            (new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One), "photonic-flare"),  // an Arc kill on a Severed target
            (new PlayerAction.Declare(new StateDeclaration.ReachMax(StatusId.From("bolt-charge"))), "flashover"),
            (new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage, TargetCount.One), "defibrillating-blast"),   // the Bolt Charge strike
            (new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One), "dielectric"),   // a kill on a Jolted target
            (new PlayerAction.CollectPickups(PickupId.From("orb-of-power")), "unraveling-orbs"),
            (new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Damage, TargetCount.One), "horde-shuttle"),   // a weapon hit on an Unraveled target
        };

        var played = steps.Aggregate(StartOwnersDimDesign(), (session, step) => LoopDesigning.AppendStep(session, step.Action, Optional.None<string>()));

        Assert.Equal(7, played.Build.Equipped.Count(equipped => equipped.Element.Kind == ElementKind.ArtifactPerk));
        Assert.All(
            steps.Select((step, index) => (step.Fires, Fired: played.Resolutions[index].Fired)).Where(step => step.Fires.Length > 0),
            step => Assert.Contains(step.Fired, fired => fired.Source.Value == step.Fires && fired.SourceKind == ElementKind.ArtifactPerk));
    }

    private static DesignSession StartOwnersDimDesign()
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        return LoopDesigning.StartSavedDimDesign(Catalog, new SourceText(path, File.ReadAllText(RepoFiles.ToPath(path))))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
    }
}

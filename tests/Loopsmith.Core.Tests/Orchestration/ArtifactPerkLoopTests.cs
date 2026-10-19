using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// The artifact's perks are part of the loop: an Arc Hunter that equips the seven Tablet of Ruin perks the rules know
/// plays each of them on its trigger like any other element. Slice on its Strand grenade launcher severs, Spark of
/// Discharge's Ionic Trace (an Arc hand cannon's kill) gives the Bolt Charge, and Reaper drops the orb.
/// </summary>
public sealed class ArtifactPerkLoopTests
{
    private static readonly Result<ImmutableArray<SourceText>, string> RuleFiles = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"));   // impure

    private static readonly RuleCatalog Catalog =
        RuleFiles.Bind(CatalogLoading.ParseCatalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private const string BuildText = """
        name: Tablet of Ruin Hunter
        class: hunter
        subclass: arc
        grenade: skip-grenade
        classAbility: gamblers-dodge
        fragments: [spark-of-discharge]
        armorMods: [reaper]
        artifactPerks: [defibrillating-blast, flashover, dielectric, to-shreds, horde-shuttle, unraveling-orbs, photonic-flare]
        weapons:
          - {hash: 4019651319, perks: [slice]} # Festival Flight
          - {hash: 2888266564} # Crisis Inverted
        """;

    [Fact]
    public void Each_of_the_seven_artifact_perks_fires_in_a_loop()
    {
        var none = Optional.None<string>();
        var steps = new (PlayerAction Action, Optional<string> Fires)[]
        {
            (new PlayerAction.UseClassAbility(), none),                                                              // arms Slice
            (new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Damage, TargetCount.One), none),             // Severs
            (new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Damage, TargetCount.One), Optional.Some("to-shreds")),    // weapon damage kept up on a Severed target unravels it
            (new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One), Optional.Some("to-shreds")),      // a weapon kill on a Severed target; Reaper's orb lands
            (new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One), Optional.Some("photonic-flare")),  // an Arc kill on a Severed target; the Ionic Trace's Bolt Charge
            (new PlayerAction.Declare(new StateDeclaration.ReachMax(StatusId.From("bolt-charge"))), Optional.Some("flashover")),
            (new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage, TargetCount.One), Optional.Some("defibrillating-blast")),   // the Bolt Charge strike
            (new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One), Optional.Some("dielectric")),   // a kill on a Jolted target
            (new PlayerAction.CollectPickups(PickupId.From("orb-of-power")), Optional.Some("unraveling-orbs")),
            (new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Damage, TargetCount.One), Optional.Some("horde-shuttle")),   // a weapon hit on an Unraveled target
        };

        var played = steps.Aggregate(StartDesign(), (session, step) => LoopDesigning.AppendStep(session, step.Action, Optional.None<string>()));

        Assert.Equal(7, played.Build.Equipped.Count(equipped => equipped.Element.Kind == ElementKind.ArtifactPerk));
        Assert.All(
            steps.Select((step, index) => (step.Fires, Fired: played.Resolutions[index].Fired)).Where(step => step.Fires.IsSome()),
            step => Assert.Contains(step.Fired, fired => Optional.Some(fired.Source.Value) == step.Fires && fired.SourceKind == ElementKind.ArtifactPerk));
    }

    private static DesignSession StartDesign() =>
        LoopDesigning.StartDesign(Catalog, new SourceText("tablet-of-ruin.build.yaml", BuildText), "Tablet of Ruin loop")
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}

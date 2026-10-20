using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Weapons;

/// <summary>
/// Synergy's rules (rules/mods/weapons.yaml), one scenario per rule claim: for each damage type, 12 kills at once with
/// the weapon holding it fill its counter and spawn its pickup for sure, and fewer spawn it as a chance. An Ionic Trace
/// homes in on you, so the Arc scenarios' card picks it up and they don't check the ground.
/// </summary>
public sealed class SynergyTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Synergy's twelve kills with a Kinetic weapon spawn an Orb of Power
          # Rule mods-weapons-synergy-1: This weapon 12+ kills ➜ Synergy equipped ➜ Orb of Power
          Given the build "synergy-battle-scar" is imported, with no steps
          When I tap "Battle Scar" in the trigger panel
          And I select "12 · Synergy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Battle Scar (💀 kill 12)"
          And it reads "Battle Scar 💀 killed 12 enemies"
          And "Synergy" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task SynergysTwelveKillsWithAKineticWeaponSpawnAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-battle-scar.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BattleScar());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BattleScar());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("12 · Synergy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Battle Scar (💀 kill 12)")
            .Reads("Battle Scar 💀 killed 12 enemies")
            .Fires(new WeaponMod.Synergy(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Synergy's single kill with a Kinetic weapon may spawn an Orb of Power
          # Rule mods-weapons-synergy-2: This weapon fewer than 12 kills ➜ Synergy equipped ➜ Orb of Power
          Given the build "synergy-battle-scar" is imported, with no steps
          When I tap "Battle Scar" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Battle Scar (💀 kill)"
          And it reads "Battle Scar 💀 killed 1 enemy"
          # A chance: the kills of earlier steps may have filled the counter.
          And "Synergy" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task SynergysSingleKillWithAKineticWeaponMaySpawnAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-battle-scar.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BattleScar());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BattleScar());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Battle Scar (💀 kill)")
            .Reads("Battle Scar 💀 killed 1 enemy")
            .Fires(new WeaponMod.Synergy(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Synergy's twelve kills with an Arc weapon spawn an Ionic Trace
          # Rule mods-weapons-synergy-3: This weapon 12+ kills ➜ Synergy equipped ➜ Ionic Trace
          Given the build "synergy-matador-64" is imported, with no steps
          When I tap "Matador 64" in the trigger panel
          And I select "12 · Synergy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Matador 64 (💀 kill 12)"
          And it reads "Matador 64 💀 killed 12 enemies"
          And "Synergy" fires "Ionic Trace"
          # An Ionic Trace homes in on you: the same card picks it up, so it never lies on the ground.
        """)]
    public async Task SynergysTwelveKillsWithAnArcWeaponSpawnAnIonicTrace(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-matador-64.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Matador64());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Matador64());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("12 · Synergy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Matador 64 (💀 kill 12)")
            .Reads("Matador 64 💀 killed 12 enemies")
            .Fires(new WeaponMod.Synergy(), "Ionic Trace");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Synergy's single kill with an Arc weapon may spawn an Ionic Trace
          # Rule mods-weapons-synergy-4: This weapon fewer than 12 kills ➜ Synergy equipped ➜ Ionic Trace
          Given the build "synergy-matador-64" is imported, with no steps
          When I tap "Matador 64" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Matador 64 (💀 kill)"
          And it reads "Matador 64 💀 killed 1 enemy"
          # A chance: the kills of earlier steps may have filled the counter.
          And "Synergy" fires "Ionic Trace (chance)"
          # An Ionic Trace homes in on you: the same card picks it up, so it never lies on the ground.
        """)]
    public async Task SynergysSingleKillWithAnArcWeaponMaySpawnAnIonicTrace(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-matador-64.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Matador64());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Matador64());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Matador 64 (💀 kill)")
            .Reads("Matador 64 💀 killed 1 enemy")
            .Fires(new WeaponMod.Synergy(), "Ionic Trace (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Synergy's twelve kills with a Solar weapon spawn a Firesprite
          # Rule mods-weapons-synergy-5: This weapon 12+ kills ➜ Synergy equipped ➜ Firesprite
          Given the build "synergy-speleologist" is imported, with no steps
          When I tap "Speleologist" in the trigger panel
          And I select "12 · Synergy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Speleologist (💀 kill 12)"
          And it reads "Speleologist 💀 killed 12 enemies"
          And "Synergy" fires "Firesprite"
          And its GROUND shows only "Firesprite"
        """)]
    public async Task SynergysTwelveKillsWithASolarWeaponSpawnAFiresprite(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-speleologist.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("12 · Synergy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Speleologist (💀 kill 12)")
            .Reads("Speleologist 💀 killed 12 enemies")
            .Fires(new WeaponMod.Synergy(), "Firesprite")
            .Shows(new StateGroup.Ground(), "Firesprite");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Synergy's single kill with a Solar weapon may spawn a Firesprite
          # Rule mods-weapons-synergy-6: This weapon fewer than 12 kills ➜ Synergy equipped ➜ Firesprite
          Given the build "synergy-speleologist" is imported, with no steps
          When I tap "Speleologist" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Speleologist (💀 kill)"
          And it reads "Speleologist 💀 killed 1 enemy"
          # A chance: the kills of earlier steps may have filled the counter.
          And "Synergy" fires "Firesprite (chance)"
          And its GROUND shows only "Firesprite (chance)"
        """)]
    public async Task SynergysSingleKillWithASolarWeaponMaySpawnAFiresprite(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-speleologist.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Speleologist (💀 kill)")
            .Reads("Speleologist 💀 killed 1 enemy")
            .Fires(new WeaponMod.Synergy(), "Firesprite (chance)")
            .Shows(new StateGroup.Ground(), "Firesprite (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Synergy's twelve kills with a Void weapon spawn a Void Breach
          # Rule mods-weapons-synergy-7: This weapon 12+ kills ➜ Synergy equipped ➜ Void Breach
          Given the build "synergy-bellowing-giant" is imported, with no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "12 · Synergy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill 12)"
          And it reads "Bellowing Giant 💀 killed 12 enemies"
          And "Synergy" fires "Void Breach"
          And its GROUND shows only "Void Breach"
        """)]
    public async Task SynergysTwelveKillsWithAVoidWeaponSpawnAVoidBreach(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-bellowing-giant.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("12 · Synergy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Bellowing Giant (💀 kill 12)")
            .Reads("Bellowing Giant 💀 killed 12 enemies")
            .Fires(new WeaponMod.Synergy(), "Void Breach")
            .Shows(new StateGroup.Ground(), "Void Breach");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Synergy's single kill with a Void weapon may spawn a Void Breach
          # Rule mods-weapons-synergy-8: This weapon fewer than 12 kills ➜ Synergy equipped ➜ Void Breach
          Given the build "synergy-bellowing-giant" is imported, with no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill)"
          And it reads "Bellowing Giant 💀 killed 1 enemy"
          # A chance: the kills of earlier steps may have filled the counter.
          And "Synergy" fires "Void Breach (chance)"
          And its GROUND shows only "Void Breach (chance)"
        """)]
    public async Task SynergysSingleKillWithAVoidWeaponMaySpawnAVoidBreach(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-bellowing-giant.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Bellowing Giant (💀 kill)")
            .Reads("Bellowing Giant 💀 killed 1 enemy")
            .Fires(new WeaponMod.Synergy(), "Void Breach (chance)")
            .Shows(new StateGroup.Ground(), "Void Breach (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Synergy's twelve kills with a Stasis weapon spawn a Stasis Shard
          # Rule mods-weapons-synergy-9: This weapon 12+ kills ➜ Synergy equipped ➜ Stasis Shard
          Given the build "synergy-forensic-nightmare" is imported, with no steps
          When I tap "Forensic Nightmare" in the trigger panel
          And I select "12 · Synergy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Forensic Nightmare (💀 kill 12)"
          And it reads "Forensic Nightmare 💀 killed 12 enemies"
          And "Synergy" fires "Stasis Shard"
          And its GROUND shows only "Stasis Shard"
        """)]
    public async Task SynergysTwelveKillsWithAStasisWeaponSpawnAStasisShard(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-forensic-nightmare.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.ForensicNightmare());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.ForensicNightmare());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("12 · Synergy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Forensic Nightmare (💀 kill 12)")
            .Reads("Forensic Nightmare 💀 killed 12 enemies")
            .Fires(new WeaponMod.Synergy(), "Stasis Shard")
            .Shows(new StateGroup.Ground(), "Stasis Shard");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Synergy's single kill with a Stasis weapon may spawn a Stasis Shard
          # Rule mods-weapons-synergy-10: This weapon fewer than 12 kills ➜ Synergy equipped ➜ Stasis Shard
          Given the build "synergy-forensic-nightmare" is imported, with no steps
          When I tap "Forensic Nightmare" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Forensic Nightmare (💀 kill)"
          And it reads "Forensic Nightmare 💀 killed 1 enemy"
          # A chance: the kills of earlier steps may have filled the counter.
          And "Synergy" fires "Stasis Shard (chance)"
          And its GROUND shows only "Stasis Shard (chance)"
        """)]
    public async Task SynergysSingleKillWithAStasisWeaponMaySpawnAStasisShard(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-forensic-nightmare.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.ForensicNightmare());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.ForensicNightmare());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Forensic Nightmare (💀 kill)")
            .Reads("Forensic Nightmare 💀 killed 1 enemy")
            .Fires(new WeaponMod.Synergy(), "Stasis Shard (chance)")
            .Shows(new StateGroup.Ground(), "Stasis Shard (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Synergy's twelve kills with a Strand weapon spawn a Tangle
          # Rule mods-weapons-synergy-11: This weapon 12+ kills ➜ Synergy equipped ➜ Tangle
          Given the build "synergy-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "12 · Synergy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill 12)"
          And it reads "Festival Flight 💀 killed 12 enemies"
          And "Synergy" fires "Tangle"
          And its GROUND shows only "Tangle"
        """)]
    public async Task SynergysTwelveKillsWithAStrandWeaponSpawnATangle(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("12 · Synergy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Festival Flight (💀 kill 12)")
            .Reads("Festival Flight 💀 killed 12 enemies")
            .Fires(new WeaponMod.Synergy(), "Tangle")
            .Shows(new StateGroup.Ground(), "Tangle");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Synergy's single kill with a Strand weapon may spawn a Tangle
          # Rule mods-weapons-synergy-12: This weapon fewer than 12 kills ➜ Synergy equipped ➜ Tangle
          Given the build "synergy-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill)"
          And it reads "Festival Flight 💀 killed 1 enemy"
          # A chance: the kills of earlier steps may have filled the counter.
          And "Synergy" fires "Tangle (chance)"
          And its GROUND shows only "Tangle (chance)"
        """)]
    public async Task SynergysSingleKillWithAStrandWeaponMaySpawnATangle(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Festival Flight (💀 kill)")
            .Reads("Festival Flight 💀 killed 1 enemy")
            .Fires(new WeaponMod.Synergy(), "Tangle (chance)")
            .Shows(new StateGroup.Ground(), "Tangle (chance)");
    }
}

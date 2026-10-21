using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Weapons;

/// <summary>
/// Synergy (enhanced)'s rules (rules/mods/weapons.yaml), one scenario per rule claim: for each damage type, 8 kills at once with
/// the weapon holding it fill its counter and spawn its pickup for sure, and fewer spawn it as a chance. An Ionic Trace
/// homes in on you, so the Arc scenarios' card picks it up and they don't check the ground.
/// </summary>
public sealed class SynergyEnhancedTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Synergy's eight kills with a Kinetic weapon spawn an Orb of Power
          # Rule mods-weapons-synergy-enhanced-1: This weapon (Kinetic) 8+ kills ➜ Synergy (enhanced) equipped ➜ Orb of Power
          Given the build "synergy-enhanced-battle-scar" is imported, with no steps
          When I tap "Battle Scar" in the trigger panel
          And I select "8 · Synergy (enhanced)"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Battle Scar (💀 kill 8)"
          And it reads "Battle Scar 💀 killed 8 enemies"
          And "Synergy (enhanced)" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task EnhancedSynergysEightKillsWithAKineticWeaponSpawnAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-enhanced-battle-scar.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BattleScar());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BattleScar());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("8 · Synergy (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Battle Scar (💀 kill 8)")
            .Reads("Battle Scar 💀 killed 8 enemies")
            .Fires(new WeaponMod.SynergyEnhanced(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Synergy's single kill with a Kinetic weapon may spawn an Orb of Power
          # Rule mods-weapons-synergy-enhanced-2: This weapon (Kinetic) fewer than 8 kills ➜ Synergy (enhanced) equipped ➜ Orb of Power
          Given the build "synergy-enhanced-battle-scar" is imported, with no steps
          When I tap "Battle Scar" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Battle Scar (💀 kill)"
          And it reads "Battle Scar 💀 killed 1 enemy"
          # A chance: the kills of earlier steps may have filled the counter.
          And "Synergy (enhanced)" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task EnhancedSynergysSingleKillWithAKineticWeaponMaySpawnAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-enhanced-battle-scar.build.yaml");
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
            .Fires(new WeaponMod.SynergyEnhanced(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Synergy's eight kills with an Arc weapon spawn an Ionic Trace
          # Rule mods-weapons-synergy-enhanced-3: This weapon (Arc) 8+ kills ➜ Synergy (enhanced) equipped ➜ Ionic Trace
          Given the build "synergy-enhanced-matador-64" is imported, with no steps
          When I tap "Matador 64" in the trigger panel
          And I select "8 · Synergy (enhanced)"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Matador 64 (💀 kill 8)"
          And it reads "Matador 64 💀 killed 8 enemies"
          And "Synergy (enhanced)" fires "Ionic Trace"
          # An Ionic Trace homes in on you: the same card picks it up, so it never lies on the ground.
        """)]
    public async Task EnhancedSynergysEightKillsWithAnArcWeaponSpawnAnIonicTrace(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-enhanced-matador-64.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Matador64());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Matador64());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("8 · Synergy (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Matador 64 (💀 kill 8)")
            .Reads("Matador 64 💀 killed 8 enemies")
            .Fires(new WeaponMod.SynergyEnhanced(), "Ionic Trace");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Synergy's single kill with an Arc weapon may spawn an Ionic Trace
          # Rule mods-weapons-synergy-enhanced-4: This weapon (Arc) fewer than 8 kills ➜ Synergy (enhanced) equipped ➜ Ionic Trace
          Given the build "synergy-enhanced-matador-64" is imported, with no steps
          When I tap "Matador 64" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Matador 64 (💀 kill)"
          And it reads "Matador 64 💀 killed 1 enemy"
          # A chance: the kills of earlier steps may have filled the counter.
          And "Synergy (enhanced)" fires "Ionic Trace (chance)"
          # An Ionic Trace homes in on you: the same card picks it up, so it never lies on the ground.
        """)]
    public async Task EnhancedSynergysSingleKillWithAnArcWeaponMaySpawnAnIonicTrace(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-enhanced-matador-64.build.yaml");
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
            .Fires(new WeaponMod.SynergyEnhanced(), "Ionic Trace (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Synergy's eight kills with a Solar weapon spawn a Firesprite
          # Rule mods-weapons-synergy-enhanced-5: This weapon (Solar) 8+ kills ➜ Synergy (enhanced) equipped ➜ Firesprite
          Given the build "synergy-enhanced-speleologist" is imported, with no steps
          When I tap "Speleologist" in the trigger panel
          And I select "8 · Synergy (enhanced)"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Speleologist (💀 kill 8)"
          And it reads "Speleologist 💀 killed 8 enemies"
          And "Synergy (enhanced)" fires "Firesprite"
          And its GROUND shows only "Firesprite"
        """)]
    public async Task EnhancedSynergysEightKillsWithASolarWeaponSpawnAFiresprite(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-enhanced-speleologist.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("8 · Synergy (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Speleologist (💀 kill 8)")
            .Reads("Speleologist 💀 killed 8 enemies")
            .Fires(new WeaponMod.SynergyEnhanced(), "Firesprite")
            .Shows(new StateGroup.Ground(), "Firesprite");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Synergy's single kill with a Solar weapon may spawn a Firesprite
          # Rule mods-weapons-synergy-enhanced-6: This weapon (Solar) fewer than 8 kills ➜ Synergy (enhanced) equipped ➜ Firesprite
          Given the build "synergy-enhanced-speleologist" is imported, with no steps
          When I tap "Speleologist" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Speleologist (💀 kill)"
          And it reads "Speleologist 💀 killed 1 enemy"
          # A chance: the kills of earlier steps may have filled the counter.
          And "Synergy (enhanced)" fires "Firesprite (chance)"
          And its GROUND shows only "Firesprite (chance)"
        """)]
    public async Task EnhancedSynergysSingleKillWithASolarWeaponMaySpawnAFiresprite(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-enhanced-speleologist.build.yaml");
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
            .Fires(new WeaponMod.SynergyEnhanced(), "Firesprite (chance)")
            .Shows(new StateGroup.Ground(), "Firesprite (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Synergy's eight kills with a Void weapon spawn a Void Breach
          # Rule mods-weapons-synergy-enhanced-7: This weapon (Void) 8+ kills ➜ Synergy (enhanced) equipped ➜ Void Breach
          Given the build "synergy-enhanced-bellowing-giant" is imported, with no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "8 · Synergy (enhanced)"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill 8)"
          And it reads "Bellowing Giant 💀 killed 8 enemies"
          And "Synergy (enhanced)" fires "Void Breach"
          And its GROUND shows only "Void Breach"
        """)]
    public async Task EnhancedSynergysEightKillsWithAVoidWeaponSpawnAVoidBreach(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-enhanced-bellowing-giant.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("8 · Synergy (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Bellowing Giant (💀 kill 8)")
            .Reads("Bellowing Giant 💀 killed 8 enemies")
            .Fires(new WeaponMod.SynergyEnhanced(), "Void Breach")
            .Shows(new StateGroup.Ground(), "Void Breach");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Synergy's single kill with a Void weapon may spawn a Void Breach
          # Rule mods-weapons-synergy-enhanced-8: This weapon (Void) fewer than 8 kills ➜ Synergy (enhanced) equipped ➜ Void Breach
          Given the build "synergy-enhanced-bellowing-giant" is imported, with no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill)"
          And it reads "Bellowing Giant 💀 killed 1 enemy"
          # A chance: the kills of earlier steps may have filled the counter.
          And "Synergy (enhanced)" fires "Void Breach (chance)"
          And its GROUND shows only "Void Breach (chance)"
        """)]
    public async Task EnhancedSynergysSingleKillWithAVoidWeaponMaySpawnAVoidBreach(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-enhanced-bellowing-giant.build.yaml");
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
            .Fires(new WeaponMod.SynergyEnhanced(), "Void Breach (chance)")
            .Shows(new StateGroup.Ground(), "Void Breach (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Synergy's eight kills with a Stasis weapon spawn a Stasis Shard
          # Rule mods-weapons-synergy-enhanced-9: This weapon (Stasis) 8+ kills ➜ Synergy (enhanced) equipped ➜ Stasis Shard
          Given the build "synergy-enhanced-forensic-nightmare" is imported, with no steps
          When I tap "Forensic Nightmare" in the trigger panel
          And I select "8 · Synergy (enhanced)"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Forensic Nightmare (💀 kill 8)"
          And it reads "Forensic Nightmare 💀 killed 8 enemies"
          And "Synergy (enhanced)" fires "Stasis Shard"
          And its GROUND shows only "Stasis Shard"
        """)]
    public async Task EnhancedSynergysEightKillsWithAStasisWeaponSpawnAStasisShard(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-enhanced-forensic-nightmare.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.ForensicNightmare());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.ForensicNightmare());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("8 · Synergy (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Forensic Nightmare (💀 kill 8)")
            .Reads("Forensic Nightmare 💀 killed 8 enemies")
            .Fires(new WeaponMod.SynergyEnhanced(), "Stasis Shard")
            .Shows(new StateGroup.Ground(), "Stasis Shard");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Synergy's single kill with a Stasis weapon may spawn a Stasis Shard
          # Rule mods-weapons-synergy-enhanced-10: This weapon (Stasis) fewer than 8 kills ➜ Synergy (enhanced) equipped ➜ Stasis Shard
          Given the build "synergy-enhanced-forensic-nightmare" is imported, with no steps
          When I tap "Forensic Nightmare" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Forensic Nightmare (💀 kill)"
          And it reads "Forensic Nightmare 💀 killed 1 enemy"
          # A chance: the kills of earlier steps may have filled the counter.
          And "Synergy (enhanced)" fires "Stasis Shard (chance)"
          And its GROUND shows only "Stasis Shard (chance)"
        """)]
    public async Task EnhancedSynergysSingleKillWithAStasisWeaponMaySpawnAStasisShard(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-enhanced-forensic-nightmare.build.yaml");
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
            .Fires(new WeaponMod.SynergyEnhanced(), "Stasis Shard (chance)")
            .Shows(new StateGroup.Ground(), "Stasis Shard (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Synergy's eight kills with a Strand weapon spawn a Tangle
          # Rule mods-weapons-synergy-enhanced-11: This weapon (Strand) 8+ kills ➜ Synergy (enhanced) equipped ➜ Tangle
          Given the build "synergy-enhanced-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "8 · Synergy (enhanced)"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill 8)"
          And it reads "Festival Flight 💀 killed 8 enemies"
          And "Synergy (enhanced)" fires "Tangle"
          And its GROUND shows only "Tangle"
        """)]
    public async Task EnhancedSynergysEightKillsWithAStrandWeaponSpawnATangle(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-enhanced-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("8 · Synergy (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Festival Flight (💀 kill 8)")
            .Reads("Festival Flight 💀 killed 8 enemies")
            .Fires(new WeaponMod.SynergyEnhanced(), "Tangle")
            .Shows(new StateGroup.Ground(), "Tangle");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Synergy's single kill with a Strand weapon may spawn a Tangle
          # Rule mods-weapons-synergy-enhanced-12: This weapon (Strand) fewer than 8 kills ➜ Synergy (enhanced) equipped ➜ Tangle
          Given the build "synergy-enhanced-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill)"
          And it reads "Festival Flight 💀 killed 1 enemy"
          # A chance: the kills of earlier steps may have filled the counter.
          And "Synergy (enhanced)" fires "Tangle (chance)"
          And its GROUND shows only "Tangle (chance)"
        """)]
    public async Task EnhancedSynergysSingleKillWithAStrandWeaponMaySpawnATangle(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("synergy-enhanced-festival-flight.build.yaml");
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
            .Fires(new WeaponMod.SynergyEnhanced(), "Tangle (chance)")
            .Shows(new StateGroup.Ground(), "Tangle (chance)");
    }
}

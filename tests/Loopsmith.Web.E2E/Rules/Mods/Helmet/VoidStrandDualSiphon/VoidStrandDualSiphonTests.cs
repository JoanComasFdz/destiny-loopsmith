using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Helmet;

/// <summary>Void/Strand Dual Siphon's rules (rules/mods/helmet.yaml), one scenario per rule claim.</summary>
public sealed class VoidStrandDualSiphonTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Void/Strand Dual Siphon's two Void weapon kills at once drop an Orb of Power
          # Rule mods-helmet-void-strand-dual-siphon-1: Void weapon 2+ kills within 3s ➜ Void/Strand Dual Siphon equipped ➜ Orb of Power
          Given the build "void-strand-dual-siphon-bellowing-giant-festival-flight" is imported, with no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "2 in 3 s · Void/Strand Dual Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill 2)"
          And it reads "Bellowing Giant 💀 killed 2 enemies in 3 s"
          And "Void/Strand Dual Siphon" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task VoidStrandDualSiphonsTwoVoidWeaponKillsAtOnceDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("void-strand-dual-siphon-bellowing-giant-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Void/Strand Dual Siphon");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Bellowing Giant (💀 kill 2)")
            .Reads("Bellowing Giant 💀 killed 2 enemies in 3 s")
            .Fires(new ArmorMod.VoidStrandDualSiphon(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Void/Strand Dual Siphon's single Void weapon kill may drop an Orb of Power
          # Rule mods-helmet-void-strand-dual-siphon-2: Void weapon fewer than 2 kills ➜ Void/Strand Dual Siphon equipped ➜ Orb of Power (chance)
          Given the build "void-strand-dual-siphon-bellowing-giant-festival-flight" is imported, with no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill)"
          And it reads "Bellowing Giant 💀 killed 1 enemy"
          And "Void/Strand Dual Siphon" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task VoidStrandDualSiphonsSingleVoidWeaponKillMayDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("void-strand-dual-siphon-bellowing-giant-festival-flight.build.yaml");
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
            .Fires(new ArmorMod.VoidStrandDualSiphon(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Void/Strand Dual Siphon's two Strand weapon kills at once drop an Orb of Power
          # Rule mods-helmet-void-strand-dual-siphon-3: Strand weapon 2+ kills within 3s ➜ Void/Strand Dual Siphon equipped ➜ Orb of Power
          Given the build "void-strand-dual-siphon-bellowing-giant-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "2 in 3 s · Void/Strand Dual Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill 2)"
          And it reads "Festival Flight 💀 killed 2 enemies in 3 s"
          And "Void/Strand Dual Siphon" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task VoidStrandDualSiphonsTwoStrandWeaponKillsAtOnceDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("void-strand-dual-siphon-bellowing-giant-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Void/Strand Dual Siphon");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Festival Flight (💀 kill 2)")
            .Reads("Festival Flight 💀 killed 2 enemies in 3 s")
            .Fires(new ArmorMod.VoidStrandDualSiphon(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Void/Strand Dual Siphon's single Strand weapon kill may drop an Orb of Power
          # Rule mods-helmet-void-strand-dual-siphon-4: Strand weapon fewer than 2 kills ➜ Void/Strand Dual Siphon equipped ➜ Orb of Power (chance)
          Given the build "void-strand-dual-siphon-bellowing-giant-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill)"
          And it reads "Festival Flight 💀 killed 1 enemy"
          And "Void/Strand Dual Siphon" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task VoidStrandDualSiphonsSingleStrandWeaponKillMayDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("void-strand-dual-siphon-bellowing-giant-festival-flight.build.yaml");
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
            .Fires(new ArmorMod.VoidStrandDualSiphon(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }
}

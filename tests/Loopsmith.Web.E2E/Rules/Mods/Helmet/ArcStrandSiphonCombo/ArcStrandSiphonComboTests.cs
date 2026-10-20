using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Helmet;

/// <summary>Arc/Strand Siphon Combo's rules (rules/mods/helmet.yaml), one scenario per rule claim.</summary>
public sealed class ArcStrandSiphonComboTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Arc/Strand Siphon Combo's two Arc weapon kills at once drop an Orb of Power
          # Rule mods-helmet-arc-strand-siphon-combo-1: Arc weapon 2+ kills within 3s ➜ Arc/Strand Siphon Combo equipped ➜ Orb of Power
          Given the build "arc-strand-siphon-combo-crisis-inverted-festival-flight" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "2 in 3 s · Arc/Strand Siphon Combo"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill 2)"
          And it reads "Crisis Inverted 💀 killed 2 enemies in 3 s"
          And "Arc/Strand Siphon Combo" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task ArcStrandSiphonCombosTwoArcWeaponKillsAtOnceDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("arc-strand-siphon-combo-crisis-inverted-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Arc/Strand Siphon Combo");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Crisis Inverted (💀 kill 2)")
            .Reads("Crisis Inverted 💀 killed 2 enemies in 3 s")
            .Fires(new ArmorMod.ArcStrandSiphonCombo(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Arc/Strand Siphon Combo's single Arc weapon kill may drop an Orb of Power
          # Rule mods-helmet-arc-strand-siphon-combo-2: Arc weapon fewer than 2 kills ➜ Arc/Strand Siphon Combo equipped ➜ Orb of Power (chance)
          Given the build "arc-strand-siphon-combo-crisis-inverted-festival-flight" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill)"
          And it reads "Crisis Inverted 💀 killed 1 enemy"
          And "Arc/Strand Siphon Combo" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task ArcStrandSiphonCombosSingleArcWeaponKillMayDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("arc-strand-siphon-combo-crisis-inverted-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Crisis Inverted (💀 kill)")
            .Reads("Crisis Inverted 💀 killed 1 enemy")
            .Fires(new ArmorMod.ArcStrandSiphonCombo(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Arc/Strand Siphon Combo's two Strand weapon kills at once drop an Orb of Power
          # Rule mods-helmet-arc-strand-siphon-combo-3: Strand weapon 2+ kills within 3s ➜ Arc/Strand Siphon Combo equipped ➜ Orb of Power
          Given the build "arc-strand-siphon-combo-crisis-inverted-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "2 in 3 s · Arc/Strand Siphon Combo"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill 2)"
          And it reads "Festival Flight 💀 killed 2 enemies in 3 s"
          And "Arc/Strand Siphon Combo" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task ArcStrandSiphonCombosTwoStrandWeaponKillsAtOnceDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("arc-strand-siphon-combo-crisis-inverted-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Arc/Strand Siphon Combo");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Festival Flight (💀 kill 2)")
            .Reads("Festival Flight 💀 killed 2 enemies in 3 s")
            .Fires(new ArmorMod.ArcStrandSiphonCombo(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Arc/Strand Siphon Combo's single Strand weapon kill may drop an Orb of Power
          # Rule mods-helmet-arc-strand-siphon-combo-4: Strand weapon fewer than 2 kills ➜ Arc/Strand Siphon Combo equipped ➜ Orb of Power (chance)
          Given the build "arc-strand-siphon-combo-crisis-inverted-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill)"
          And it reads "Festival Flight 💀 killed 1 enemy"
          And "Arc/Strand Siphon Combo" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task ArcStrandSiphonCombosSingleStrandWeaponKillMayDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("arc-strand-siphon-combo-crisis-inverted-festival-flight.build.yaml");
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
            .Fires(new ArmorMod.ArcStrandSiphonCombo(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }
}

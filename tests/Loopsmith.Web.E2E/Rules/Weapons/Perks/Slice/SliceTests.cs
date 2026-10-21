using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Weapons.Perks;

/// <summary>Slice's rules (rules/weapons/perks.yaml), one scenario per rule claim: Marksman's Dodge (no rules of its own) arms it.</summary>
public sealed class SliceTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Slice's dodge arms Slice
          # Rule weapons-perks-slice-1: Class ability ➜ Slice equipped ➜ Slice
          Given the build "slice-festival-flight" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And "Slice" fires "+1 Slice (8s)"
          And its WEAPONS show only "Slice · Festival Flight"
        """)]
    public async Task SlicesDodgeArmsSlice(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("slice-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#1 Class ability")
            .Fires(new WeaponPerk.Slice(), "+1 Slice (8s)")
            .Shows(new StateGroup.Weapons(), "Slice · Festival Flight");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Slice's armed hit severs the enemy
          # Rule weapons-perks-slice-2: This weapon hit ➜ Slice equipped and Slice on you ➜ Slice and Sever
          Given the build "slice-festival-flight" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And its WEAPONS show only "Slice · Festival Flight"
          # keeping step #1: its Slice is what #2's hit needs
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Festival Flight (🥊 hit)"
          And "Slice" fires "Sever target (10s)" and "+1 Slice (8s)"
          And its DEBUFFS show only "Sever"
          And its WEAPONS show only "Slice · Festival Flight"
        """)]
    public async Task SlicesArmedHitSeversTheEnemy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("slice-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#1 Class ability")
            .Shows(new StateGroup.Weapons(), "Slice · Festival Flight");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var hit = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#2 Festival Flight (🥊 hit)")
            .Fires(new WeaponPerk.Slice(), "Sever target (10s)", "+1 Slice (8s)")
            .Shows(new StateGroup.Debuffs(), "Sever")
            .Shows(new StateGroup.Weapons(), "Slice · Festival Flight");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Slice's 5 hits declared at max end it
          # Rule weapons-perks-slice-3: Slice at max ➜ Slice equipped ➜ Slice ends
          Given the build "slice-festival-flight" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And its WEAPONS show only "Slice · Festival Flight"
          # keeping step #1: its Slice is what #2 declares at max
          When I tap "at max (x5)" on "Slice" under "Your weapons" in the trigger panel
          Then card #2's heading is "#2 Slice at max (x5)"
          And "Slice" fires "consumes Slice"
          And its WEAPONS show none
        """)]
    public async Task Slices5HitsDeclaredAtMaxEndIt(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("slice-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#1 Class ability")
            .Shows(new StateGroup.Weapons(), "Slice · Festival Flight");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new WeaponPerk.Slice(), "at max (x5)");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var declared = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(declared)
            .HasHeading("#2 Slice at max (x5)")
            .Fires(new WeaponPerk.Slice(), "consumes Slice")
            .ShowsNone(new StateGroup.Weapons());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Slice's dodge arms Slice for 9 s
          # Rule weapons-perks-slice-1: Class ability ➜ Slice (enhanced) equipped ➜ Slice
          Given the build "slice-enhanced-festival-flight" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And "Slice (enhanced)" fires "+1 Slice (9s)"
          And its WEAPONS show only "Slice · Festival Flight"
          # keeping step #1: its Slice is what the chip shows
          When I tap the "Slice · Festival Flight" chip in card #1's WEAPONS
          Then the item details popup opens, titled "Slice (enhanced)"
          And its "Right then, after #1" card reads only "up to x5 · 9s"
          When I close the item details popup
          And I tap "Slice" in "+1 Slice (9s)", which "Slice (enhanced)" fires in card #1
          Then the item details popup opens, titled "Slice (enhanced)"
        """)]
    public async Task EnhancedSlicesDodgeArmsSliceFor9S(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("slice-enhanced-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#1 Class ability")
            .Fires(new WeaponPerk.SliceEnhanced(), "+1 Slice (9s)")
            .Shows(new StateGroup.Weapons(), "Slice · Festival Flight");

        // When
        await Frontend.Designer.Loop.Step(1).ClickStateChipAsync(new StateGroup.Weapons(), "Slice · Festival Flight");
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details)
            .IsTitled(new WeaponPerk.SliceEnhanced())
            .HasRightThen("Right then, after #1", "up to x5 · 9s");

        // When
        await Frontend.Designer.ItemDetails.ClickCloseAsync();
        await Frontend.Designer.ItemDetails.WaitUntilClosedAsync();
        await Frontend.Designer.Loop.Step(1).ClickFiredKeywordAsync(new WeaponPerk.Slice(), "+1 Slice (9s)", new WeaponPerk.SliceEnhanced());
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var keywordDetails = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(keywordDetails)
            .IsTitled(new WeaponPerk.SliceEnhanced());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Slice's armed hit severs the enemy
          # Rule weapons-perks-slice-2: This weapon hit ➜ Slice (enhanced) equipped and Slice on you ➜ Slice and Sever
          Given the build "slice-enhanced-festival-flight" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And its WEAPONS show only "Slice · Festival Flight"
          # keeping step #1: its Slice is what #2's hit needs
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Festival Flight (🥊 hit)"
          And "Slice (enhanced)" fires "Sever target (10s)" and "+1 Slice (9s)"
          And its DEBUFFS show only "Sever"
          And its WEAPONS show only "Slice · Festival Flight"
        """)]
    public async Task EnhancedSlicesArmedHitSeversTheEnemy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("slice-enhanced-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#1 Class ability")
            .Shows(new StateGroup.Weapons(), "Slice · Festival Flight");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var hit = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#2 Festival Flight (🥊 hit)")
            .Fires(new WeaponPerk.SliceEnhanced(), "Sever target (10s)", "+1 Slice (9s)")
            .Shows(new StateGroup.Debuffs(), "Sever")
            .Shows(new StateGroup.Weapons(), "Slice · Festival Flight");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Slice's 5 hits declared at max end it
          # Rule weapons-perks-slice-3: Slice at max ➜ Slice (enhanced) equipped ➜ Slice ends
          Given the build "slice-enhanced-festival-flight" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And its WEAPONS show only "Slice · Festival Flight"
          # keeping step #1: its Slice is what #2 declares at max
          When I tap "at max (x5)" on "Slice" under "Your weapons" in the trigger panel
          Then card #2's heading is "#2 Slice at max (x5)"
          And "Slice (enhanced)" fires "consumes Slice"
          And its WEAPONS show none
        """)]
    public async Task EnhancedSlices5HitsDeclaredAtMaxEndIt(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("slice-enhanced-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#1 Class ability")
            .Shows(new StateGroup.Weapons(), "Slice · Festival Flight");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new WeaponPerk.Slice(), "at max (x5)");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var declared = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(declared)
            .HasHeading("#2 Slice at max (x5)")
            .Fires(new WeaponPerk.SliceEnhanced(), "consumes Slice")
            .ShowsNone(new StateGroup.Weapons());
    }
}

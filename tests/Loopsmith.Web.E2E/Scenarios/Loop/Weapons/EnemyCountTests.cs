using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Weapons;

/// <summary>The "×" enemy count of a weapon whose perks need several enemies in one action: what it offers, and which count fires a perk for sure, as a chance or not at all.</summary>
public sealed class EnemyCountTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: One for All needs 3 hits or kills from its own weapon
          Given the build "festival-flight-one-for-all-ogma-pr6" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          Then the "×" enemy count offers "1 enemy" and "3 in 3 s · One for All"
          When I tap "Ogma PR6" in the trigger panel
          Then the "×" enemy count offers only "1 enemy"
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Festival Flight (🥊 hit)"
          And it reads "Festival Flight 🥊 hit 1 enemy"
          And "One for All" fires "One for All (10s) (chance)" and "+35% kinetic weapon damage (chance)"
          And its WEAPONS show only "One for All · Festival Flight"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "3 in 3 s · One for All"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Festival Flight (🥊 hit 3)"
          And it reads "Festival Flight 🥊 hit 3 enemies in 3 s"
          And "One for All" fires "One for All (10s)" and "+35% kinetic weapon damage"
          And its WEAPONS show only "One for All · Festival Flight"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "3 in 3 s · One for All"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill 3)"
          And it reads "Festival Flight 💀 killed 3 enemies in 3 s"
          And "One for All" fires "One for All (10s)" and "+35% kinetic weapon damage"
          And its WEAPONS show only "One for All · Festival Flight"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "1 enemy"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Ogma PR6 (🥊 hit)"
          And it reads "nothing triggers"
          And its WEAPONS show none
        """)]
    public async Task OneForAllNeeds3HitsOrKillsFromItsOwnWeapon(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("festival-flight-one-for-all-ogma-pr6.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());

        // Then
        var festivalFlightRow = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(festivalFlightRow).OffersCounts("1 enemy", "3 in 3 s · One for All");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());

        // Then
        var ogmaRow = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(ogmaRow).OffersCounts("1 enemy");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var oneHit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(oneHit)
            .HasHeading("#1 Festival Flight (🥊 hit)")
            .Reads("Festival Flight 🥊 hit 1 enemy")
            .Fires(new WeaponPerk.OneForAll(), "One for All (10s) (chance)", "+35% kinetic weapon damage (chance)")
            .Shows(new StateGroup.Weapons(), "One for All · Festival Flight");

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterOneHit = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterOneHit).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · One for All");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var threeHits = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(threeHits)
            .HasHeading("#1 Festival Flight (🥊 hit 3)")
            .Reads("Festival Flight 🥊 hit 3 enemies in 3 s")
            .Fires(new WeaponPerk.OneForAll(), "One for All (10s)", "+35% kinetic weapon damage")
            .Shows(new StateGroup.Weapons(), "One for All · Festival Flight");

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterThreeHits = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterThreeHits).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · One for All");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var threeKills = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(threeKills)
            .HasHeading("#1 Festival Flight (💀 kill 3)")
            .Reads("Festival Flight 💀 killed 3 enemies in 3 s")
            .Fires(new WeaponPerk.OneForAll(), "One for All (10s)", "+35% kinetic weapon damage")
            .Shows(new StateGroup.Weapons(), "One for All · Festival Flight");

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterThreeKills = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterThreeKills).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var anotherWeapon = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(anotherWeapon)
            .HasHeading("#1 Ogma PR6 (🥊 hit)")
            .ReadsNothingTriggers()
            .ShowsNone(new StateGroup.Weapons());
    }

    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: Stats for All shows its stats in one chip
          # Desktop only: what it checks is the chip's tooltip, which shows on hover, and a phone has none.
          Given the build "ogma-pr6-stats-for-all" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          Then the "×" enemy count offers "1 enemy" and "3 in 3 s · Stats for All"
          When I tap "Ogma PR6" in the trigger panel
          And I select "1 enemy"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Ogma PR6 (🥊 hit)"
          And it reads "Ogma PR6 🥊 hit 1 enemy"
          And "Stats for All" fires "Stats for All (10s) (chance)"
          And its WEAPONS show only "Stats for All · 1 effect · Ogma PR6"
          And the "Stats for All · 1 effect · Ogma PR6" chip's tooltip reads "10s · Energy weapon: +10 Range, +10 Stability, +35 Handling, +35 Reload Speed"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "3 in 3 s · Stats for All"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Ogma PR6 (🥊 hit 3)"
          And it reads "Ogma PR6 🥊 hit 3 enemies in 3 s"
          And "Stats for All" fires "Stats for All (10s)"
          And its WEAPONS show only "Stats for All · 1 effect · Ogma PR6"
          And the "Stats for All · 1 effect · Ogma PR6" chip's tooltip reads "10s · Energy weapon: +10 Range, +10 Stability, +35 Handling, +35 Reload Speed"
        """)]
    public async Task StatsForAllShowsItsStatsInOneChip(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("ogma-pr6-stats-for-all.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row).OffersCounts("1 enemy", "3 in 3 s · Stats for All");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var oneHit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(oneHit)
            .HasHeading("#1 Ogma PR6 (🥊 hit)")
            .Reads("Ogma PR6 🥊 hit 1 enemy")
            .Fires(new WeaponPerk.StatsForAll(), "Stats for All (10s) (chance)")
            .Shows(new StateGroup.Weapons(), "Stats for All · 1 effect · Ogma PR6")
            .HasTooltip(new StateGroup.Weapons(), "Stats for All · 1 effect · Ogma PR6", "10s · Energy weapon: +10 Range, +10 Stability, +35 Handling, +35 Reload Speed");

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterOneHit = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterOneHit).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · Stats for All");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var threeHits = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(threeHits)
            .HasHeading("#1 Ogma PR6 (🥊 hit 3)")
            .Reads("Ogma PR6 🥊 hit 3 enemies in 3 s")
            .Fires(new WeaponPerk.StatsForAll(), "Stats for All (10s)")
            .Shows(new StateGroup.Weapons(), "Stats for All · 1 effect · Ogma PR6")
            .HasTooltip(new StateGroup.Weapons(), "Stats for All · 1 effect · Ogma PR6", "10s · Energy weapon: +10 Range, +10 Stability, +35 Handling, +35 Reload Speed");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Reaper's Tithe gives a stack per enemy killed at once
          Given the build "bellowing-giant-reapers-tithe" is imported, with no steps
          When I tap "Bellowing Giant" in the trigger panel
          Then the "×" enemy count offers "1 enemy", "2 · Reaper's Tithe", "3 · Reaper's Tithe" and "4 · Reaper's Tithe · Reaper's Tithe x4"
          # "· Reaper's Tithe x4": the buff reaches its cap at 4 kills.
          When I tap "Bellowing Giant" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill)"
          # No "(chance)" either: kills at once can't come from the steps before.
          And it reads "nothing triggers"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "2 · Reaper's Tithe"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill 2)"
          And it reads "Bellowing Giant 💀 killed 2 enemies"
          And "Reaper's Tithe" fires "+2 Reaper's Tithe (15s)" and "+20% heavy weapon damage"
          And its WEAPONS show only "Reaper's Tithe · Bellowing Giant"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "3 · Reaper's Tithe"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill 3)"
          And it reads "Bellowing Giant 💀 killed 3 enemies"
          And "Reaper's Tithe" fires "+3 Reaper's Tithe (15s)" and "+30% heavy weapon damage"
          And its WEAPONS show only "Reaper's Tithe · Bellowing Giant"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "4 · Reaper's Tithe · Reaper's Tithe x4"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill 4)"
          And it reads "Bellowing Giant 💀 killed 4 enemies"
          And "Reaper's Tithe" fires "+4 Reaper's Tithe (15s)" and "+40% heavy weapon damage"
          And its WEAPONS show only "Reaper's Tithe · Bellowing Giant"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "2 · Reaper's Tithe"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Bellowing Giant (🥊 hit 2)"
          # Reaper's Tithe needs kills.
          And it reads "nothing triggers"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "4 · Reaper's Tithe · Reaper's Tithe x4"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Bellowing Giant (🥊 hit 4)"
          And it reads "nothing triggers"
        """)]
    public async Task ReapersTitheGivesAStackPerEnemyKilledAtOnce(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("bellowing-giant-reapers-tithe.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row).OffersCounts("1 enemy", "2 · Reaper's Tithe", "3 · Reaper's Tithe", "4 · Reaper's Tithe · Reaper's Tithe x4");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // No "(chance)" either: kills at once can't come from the steps before.
        var oneKill = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(oneKill)
            .HasHeading("#1 Bellowing Giant (💀 kill)")
            .ReadsNothingTriggers();

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterOneKill = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterOneKill).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 · Reaper's Tithe");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var twoKills = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(twoKills)
            .HasHeading("#1 Bellowing Giant (💀 kill 2)")
            .Reads("Bellowing Giant 💀 killed 2 enemies")
            .Fires(new WeaponPerk.ReapersTithe(), "+2 Reaper's Tithe (15s)", "+20% heavy weapon damage")
            .Shows(new StateGroup.Weapons(), "Reaper's Tithe · Bellowing Giant");

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterTwoKills = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterTwoKills).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 · Reaper's Tithe");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var threeKills = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(threeKills)
            .HasHeading("#1 Bellowing Giant (💀 kill 3)")
            .Reads("Bellowing Giant 💀 killed 3 enemies")
            .Fires(new WeaponPerk.ReapersTithe(), "+3 Reaper's Tithe (15s)", "+30% heavy weapon damage")
            .Shows(new StateGroup.Weapons(), "Reaper's Tithe · Bellowing Giant");

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterThreeKills = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterThreeKills).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("4 · Reaper's Tithe · Reaper's Tithe x4");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var fourKills = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(fourKills)
            .HasHeading("#1 Bellowing Giant (💀 kill 4)")
            .Reads("Bellowing Giant 💀 killed 4 enemies")
            .Fires(new WeaponPerk.ReapersTithe(), "+4 Reaper's Tithe (15s)", "+40% heavy weapon damage")
            .Shows(new StateGroup.Weapons(), "Reaper's Tithe · Bellowing Giant");

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterFourKills = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterFourKills).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 · Reaper's Tithe");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // Reaper's Tithe needs kills.
        var twoHits = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(twoHits)
            .HasHeading("#1 Bellowing Giant (🥊 hit 2)")
            .ReadsNothingTriggers();

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterTwoHits = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterTwoHits).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("4 · Reaper's Tithe · Reaper's Tithe x4");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var fourHits = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(fourHits)
            .HasHeading("#1 Bellowing Giant (🥊 hit 4)")
            .ReadsNothingTriggers();
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: One count names both Strand Siphon and Reaper's Tithe
          Given the build "crux-celeritas-reapers-tithe-strand-siphon" is imported, with no steps
          When I tap "Crux Celeritas IV" in the trigger panel
          Then the "×" enemy count offers "1 enemy", "2 · Strand Siphon in 3 s, Reaper's Tithe", "3 · Reaper's Tithe" and "4 · Reaper's Tithe · Reaper's Tithe x4"
          When I tap "Crux Celeritas IV" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crux Celeritas IV (💀 kill)"
          And it reads "Crux Celeritas IV 💀 killed 1 enemy"
          And "Strand Siphon" fires "Orb of Power (chance)"
          And "Reaper's Tithe" fires nothing
          And its GROUND show only "Orb of Power (chance)"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Crux Celeritas IV" in the trigger panel
          And I select "2 · Strand Siphon in 3 s, Reaper's Tithe"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crux Celeritas IV (💀 kill 2)"
          And it reads "Crux Celeritas IV 💀 killed 2 enemies in 3 s"
          And "Reaper's Tithe" fires "+2 Reaper's Tithe (15s)" and "+20% heavy weapon damage"
          And "Strand Siphon" fires "Orb of Power"
          And no line says "(chance)"
          And its WEAPONS show only "Reaper's Tithe · Crux Celeritas IV"
          And its GROUND show only "Orb of Power"
        """)]
    public async Task OneCountNamesBothStrandSiphonAndReapersTithe(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("crux-celeritas-reapers-tithe-strand-siphon.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CruxCeleritasIv());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CruxCeleritasIv());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row).OffersCounts("1 enemy", "2 · Strand Siphon in 3 s, Reaper's Tithe", "3 · Reaper's Tithe", "4 · Reaper's Tithe · Reaper's Tithe x4");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CruxCeleritasIv());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CruxCeleritasIv());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var oneKill = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(oneKill)
            .HasHeading("#1 Crux Celeritas IV (💀 kill)")
            .Reads("Crux Celeritas IV 💀 killed 1 enemy")
            .Fires(new ArmorMod.StrandSiphon(), "Orb of Power (chance)")
            .DoesNotFire(new WeaponPerk.ReapersTithe())
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterOneKill = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterOneKill).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CruxCeleritasIv());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CruxCeleritasIv());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 · Strand Siphon in 3 s, Reaper's Tithe");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var twoKills = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(twoKills)
            .HasHeading("#1 Crux Celeritas IV (💀 kill 2)")
            .Reads("Crux Celeritas IV 💀 killed 2 enemies in 3 s")
            .Fires(new WeaponPerk.ReapersTithe(), "+2 Reaper's Tithe (15s)", "+20% heavy weapon damage")
            .Fires(new ArmorMod.StrandSiphon(), "Orb of Power")
            .HasNoChance()
            .Shows(new StateGroup.Weapons(), "Reaper's Tithe · Crux Celeritas IV")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Hatchling hatches a Threadling on kills, never on hits
          Given the build "festival-flight-hatchling" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          Then the "×" enemy count offers "1 enemy" and "3 in 3 s · Hatchling"
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Festival Flight (🥊 hit)"
          And it reads "nothing triggers"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "3 in 3 s · Hatchling"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Festival Flight (🥊 hit 3)"
          And it reads "nothing triggers"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill)"
          And it reads "Festival Flight 💀 killed 1 enemy"
          And "Hatchling" fires "Threadling (chance)"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "3 in 3 s · Hatchling"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill 3)"
          And it reads "Festival Flight 💀 killed 3 enemies in 3 s"
          And "Hatchling" fires "Threadling"
        """)]
    public async Task HatchlingHatchesAThreadlingOnKillsNeverOnHits(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("festival-flight-hatchling.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row).OffersCounts("1 enemy", "3 in 3 s · Hatchling");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var oneHit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(oneHit)
            .HasHeading("#1 Festival Flight (🥊 hit)")
            .ReadsNothingTriggers();

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterOneHit = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterOneHit).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · Hatchling");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var threeHits = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(threeHits)
            .HasHeading("#1 Festival Flight (🥊 hit 3)")
            .ReadsNothingTriggers();

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterThreeHits = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterThreeHits).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var oneKill = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(oneKill)
            .HasHeading("#1 Festival Flight (💀 kill)")
            .Reads("Festival Flight 💀 killed 1 enemy")
            .Fires(new WeaponPerk.Hatchling(), "Threadling (chance)");

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterOneKill = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterOneKill).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · Hatchling");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var threeKills = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(threeKills)
            .HasHeading("#1 Festival Flight (💀 kill 3)")
            .Reads("Festival Flight 💀 killed 3 enemies in 3 s")
            .Fires(new WeaponPerk.Hatchling(), "Threadling");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Hatchling may hatch a second Threadling
          Given the build "festival-flight-hatchling-enhanced" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          Then the "×" enemy count offers "1 enemy" and "3 in 3 s · Hatchling (enhanced)"
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill)"
          And it reads "Festival Flight 💀 killed 1 enemy"
          # Two Threadlings: the one every Hatchling gives, and the extra one only the enhanced trait gives.
          And "Hatchling (enhanced)" fires "Threadling (chance)" and "Threadling (chance)"
          When I tick "why" above the loop
          Then "Hatchling (enhanced)" gives the reasons "why: Fewer than three kills in this step: a precision kill, or the kills of the steps before, may hatch it." and "why: When it hatches one, the enhanced trait may hatch a second."
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "3 in 3 s · Hatchling (enhanced)"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill 3)"
          And it reads "Festival Flight 💀 killed 3 enemies in 3 s"
          And "Hatchling (enhanced)" fires "Threadling" and "Threadling (chance)"
          # "why" is still ticked from the round before.
          And "Hatchling (enhanced)" gives the reasons "why: Three quick kills with this weapon hatch a Threadling." and "why: When it hatches one, the enhanced trait may hatch a second."
        """)]
    public async Task EnhancedHatchlingMayHatchASecondThreadling(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("festival-flight-hatchling-enhanced.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row).OffersCounts("1 enemy", "3 in 3 s · Hatchling (enhanced)");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // Two Threadlings: the one every Hatchling gives, and the extra one only the enhanced trait gives.
        var oneKill = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(oneKill)
            .HasHeading("#1 Festival Flight (💀 kill)")
            .Reads("Festival Flight 💀 killed 1 enemy")
            .Fires(new WeaponPerk.HatchlingEnhanced(), "Threadling (chance)", "Threadling (chance)");

        // When
        await Frontend.Designer.TickWhyAsync();
        await Frontend.Designer.Loop.Step(1).WaitUntilReasonsShownAsync();

        // Then
        var oneKillWhy = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(oneKillWhy)
            .GivesReasons(new WeaponPerk.HatchlingEnhanced(), "why: Fewer than three kills in this step: a precision kill, or the kills of the steps before, may hatch it.", "why: When it hatches one, the enhanced trait may hatch a second.");

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterOneKill = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterOneKill).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · Hatchling (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // "why" is still ticked from the round before.
        var threeKills = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(threeKills)
            .HasHeading("#1 Festival Flight (💀 kill 3)")
            .Reads("Festival Flight 💀 killed 3 enemies in 3 s")
            .Fires(new WeaponPerk.HatchlingEnhanced(), "Threadling", "Threadling (chance)")
            .GivesReasons(new WeaponPerk.HatchlingEnhanced(), "why: Three quick kills with this weapon hatch a Threadling.", "why: When it hatches one, the enhanced trait may hatch a second.");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Unrelenting heals for sure on 3 quick kills
          Given the build "ogma-pr6-unrelenting" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          Then the "×" enemy count offers "1 enemy" and "3 in 5 s · Unrelenting"
          When I tap "Ogma PR6" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Ogma PR6 (💀 kill)"
          And it reads "Ogma PR6 💀 killed 1 enemy"
          And "Unrelenting" fires "heals you 65 HP (chance)"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "3 in 5 s · Unrelenting"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Ogma PR6 (💀 kill 3)"
          And it reads "Ogma PR6 💀 killed 3 enemies in 5 s"
          And "Unrelenting" fires "heals you 65 HP"
        """)]
    public async Task UnrelentingHealsForSureOn3QuickKills(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("ogma-pr6-unrelenting.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row).OffersCounts("1 enemy", "3 in 5 s · Unrelenting");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var oneKill = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(oneKill)
            .HasHeading("#1 Ogma PR6 (💀 kill)")
            .Reads("Ogma PR6 💀 killed 1 enemy")
            .Fires(new WeaponPerk.Unrelenting(), "heals you 65 HP (chance)");

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterOneKill = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterOneKill).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 5 s · Unrelenting");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var threeKills = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(threeKills)
            .HasHeading("#1 Ogma PR6 (💀 kill 3)")
            .Reads("Ogma PR6 💀 killed 3 enemies in 5 s")
            .Fires(new WeaponPerk.Unrelenting(), "heals you 65 HP");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Veteran's Wisdom strikes for sure on 3 quick kills
          Given the build "scathelocke-veterans-wisdom" is imported, with no steps
          When I tap "Scathelocke" in the trigger panel
          Then the "×" enemy count offers "1 enemy" and "3 in 3 s · Veteran's Wisdom"
          When I tap "Scathelocke" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Scathelocke (💀 kill)"
          And it reads "Scathelocke 💀 killed 1 enemy"
          And "Veteran's Wisdom" fires "Exhaust target (10s) (chance)" and "kinetic weapon strike, Kinetic damage (chance)"
          And its DEBUFFS show only "Exhaust"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Scathelocke" in the trigger panel
          And I select "3 in 3 s · Veteran's Wisdom"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Scathelocke (💀 kill 3)"
          And it reads "Scathelocke 💀 killed 3 enemies in 3 s"
          And "Veteran's Wisdom" fires "Exhaust target (10s)" and "kinetic weapon strike, Kinetic damage"
          And its DEBUFFS show only "Exhaust"
        """)]
    public async Task VeteransWisdomStrikesForSureOn3QuickKills(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("scathelocke-veterans-wisdom.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Scathelocke());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row).OffersCounts("1 enemy", "3 in 3 s · Veteran's Wisdom");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var oneKill = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(oneKill)
            .HasHeading("#1 Scathelocke (💀 kill)")
            .Reads("Scathelocke 💀 killed 1 enemy")
            .Fires(new WeaponPerk.VeteransWisdom(), "Exhaust target (10s) (chance)", "kinetic weapon strike, Kinetic damage (chance)")
            .Shows(new StateGroup.Debuffs(), "Exhaust");

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterOneKill = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterOneKill).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · Veteran's Wisdom");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var threeKills = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(threeKills)
            .HasHeading("#1 Scathelocke (💀 kill 3)")
            .Reads("Scathelocke 💀 killed 3 enemies in 3 s")
            .Fires(new WeaponPerk.VeteransWisdom(), "Exhaust target (10s)", "kinetic weapon strike, Kinetic damage")
            .Shows(new StateGroup.Debuffs(), "Exhaust");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Radiolaria Transposer makes both Arc strikes for sure on 3 kills
          Given the build "speleologist-radiolaria-transposer" is imported, with no steps
          When I tap "Speleologist" in the trigger panel
          Then the "×" enemy count offers "1 enemy" and "3 in 8 s · Radiolaria Transposer"
          When I tap "Speleologist" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Speleologist (💀 kill)"
          And it reads "Speleologist 💀 killed 1 enemy"
          # Two strikes: the explosion, then the pool.
          And "Radiolaria Transposer" fires "heavy weapon strike, Arc damage (chance)" and "heavy weapon strike, Arc damage (chance)"
          When I remove step #1 with the "✕" on its card
          Then the loop has no steps
          When I tap "Speleologist" in the trigger panel
          And I select "3 in 8 s · Radiolaria Transposer"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Speleologist (💀 kill 3)"
          And it reads "Speleologist 💀 killed 3 enemies in 8 s"
          And "Radiolaria Transposer" fires "heavy weapon strike, Arc damage" and "heavy weapon strike, Arc damage"
        """)]
    public async Task RadiolariaTransposerMakesBothArcStrikesForSureOn3Kills(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("speleologist-radiolaria-transposer.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Speleologist());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row).OffersCounts("1 enemy", "3 in 8 s · Radiolaria Transposer");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // Two strikes: the explosion, then the pool.
        var oneKill = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(oneKill)
            .HasHeading("#1 Speleologist (💀 kill)")
            .Reads("Speleologist 💀 killed 1 enemy")
            .Fires(new WeaponPerk.RadiolariaTransposer(), "heavy weapon strike, Arc damage (chance)", "heavy weapon strike, Arc damage (chance)");

        // When
        await Frontend.Designer.Loop.ClickRemoveStepAsync(1);
        await Frontend.Designer.Loop.WaitUntilNoStepsAsync();

        // Then
        var afterOneKill = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(afterOneKill).HasNoSteps();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 8 s · Radiolaria Transposer");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var threeKills = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(threeKills)
            .HasHeading("#1 Speleologist (💀 kill 3)")
            .Reads("Speleologist 💀 killed 3 enemies in 8 s")
            .Fires(new WeaponPerk.RadiolariaTransposer(), "heavy weapon strike, Arc damage", "heavy weapon strike, Arc damage");
    }
}

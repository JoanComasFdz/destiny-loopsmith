using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Keywords.Strand;

/// <summary>Woven Mail's rules (rules/keywords/strand.yaml), one scenario per rule claim.</summary>
public sealed class WovenMailTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Woven Mail gives damage resistance
          # Rule keywords-strand-woven-mail-2: Always ➜ Woven Mail on you ➜ Damage resistance (45% [PvP 25%])
          Given the build "woven-mail-to-shreds-slice-storms-edge" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And its WEAPONS show only "Slice · Festival Flight"
          # keeping step #1: its Slice is what #2's hit severs with
          When I tap "Festival Flight" in the trigger panel
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Festival Flight (🥊 hit)"
          And its DEBUFFS show only "Sever"
          # keeping step #2: its severed target is what #3's kill needs for To Shreds' Woven Mail
          When I tap "Festival Flight" in the trigger panel
          And I tap "💀 kill"
          And I tap "▸ DAMAGE RESISTANCE" on card #3
          Then card #3's heading is "#3 Festival Flight (💀 kill)"
          And its BUFFS show only "Woven Mail"
          # 34.5% is every Guardian's own against combatants; Woven Mail's 45% adds to it, 1 − (1 − 34.5%) × (1 − 45%).
          And its "All damage" resistance reads "63.98%" PvE and "25%" PvP
          And "Woven Mail" gives "45% · PvP 25%" of it
        """)]
    public async Task WovenMailGivesDamageResistance(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("woven-mail-to-shreds-slice-storms-edge.build.yaml");
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
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var hit = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#2 Festival Flight (🥊 hit)")
            .Shows(new StateGroup.Debuffs(), "Sever");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);
        await Frontend.Designer.Loop.Step(3).ClickDamageResistanceAsync();
        await Frontend.Designer.Loop.Step(3).WaitUntilResistanceSourcesShownAsync();

        // Then
        // 34.5% is every Guardian's own against combatants; Woven Mail's 45% adds to it, 1 − (1 − 34.5%) × (1 − 45%).
        var killed = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#3 Festival Flight (💀 kill)")
            .Shows(new StateGroup.Buffs(), "Woven Mail")
            .HasResistance("All damage", "63.98%", "25%")
            .HasResistanceFrom(new Keyword.WovenMail(), "45% · PvP 25%");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Woven Mail ends when a Super is cast
          # Rule keywords-strand-woven-mail-1: Super cast ➜ Woven Mail on you ➜ ends Woven Mail
          Given the build "woven-mail-to-shreds-slice-storms-edge" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And its WEAPONS show only "Slice · Festival Flight"
          # keeping step #1: its Slice is what #2's hit severs with
          When I tap "Festival Flight" in the trigger panel
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Festival Flight (🥊 hit)"
          And its DEBUFFS show only "Sever"
          # keeping step #2: its severed target is what #3's kill needs for To Shreds' Woven Mail
          When I tap "Festival Flight" in the trigger panel
          And I tap "💀 kill"
          Then card #3's heading is "#3 Festival Flight (💀 kill)"
          And its BUFFS show only "Woven Mail"
          # keeping step #3: its Woven Mail is what #4's Super cast ends
          When I tap "Storm's Edge" in the trigger panel
          And I tap "🥊 hit"
          Then card #4's heading is "#4 Super (🥊 hit)"
          # A Super's first hit casts it: the card reads the cast first.
          And it reads "Super cast"
          And "Woven Mail" fires "consumes Woven Mail"
          And its BUFFS show only "Super · 1 effect"
        """)]
    public async Task WovenMailEndsWhenASuperIsCast(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("woven-mail-to-shreds-slice-storms-edge.build.yaml");
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
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var hit = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#2 Festival Flight (🥊 hit)")
            .Shows(new StateGroup.Debuffs(), "Sever");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        var killed = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#3 Festival Flight (💀 kill)")
            .Shows(new StateGroup.Buffs(), "Woven Mail");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.StormsEdge());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.StormsEdge());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(4);

        // Then
        // A Super's first hit casts it: the card reads the cast first.
        var cast = await Frontend.Designer.Loop.Step(4).ReadAsync();
        Asserting.That(cast)
            .HasHeading("#4 Super (🥊 hit)")
            .Reads("Super cast")
            .Fires(new Keyword.WovenMail(), "consumes Woven Mail")
            .Shows(new StateGroup.Buffs(), "Super · 1 effect");
    }
}

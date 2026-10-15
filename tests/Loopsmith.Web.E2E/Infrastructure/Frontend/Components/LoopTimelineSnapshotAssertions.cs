using JoanComasFdz.AssertingThat;
using Loopsmith.Core.Domain;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;
using CoreWeaponSlot = Loopsmith.Core.Domain.WeaponSlot;
using WeaponSlot = Loopsmith.Web.E2E.Infrastructure.Vocabulary.WeaponSlot;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the loop.</summary>
public static class LoopTimelineSnapshotAssertions
{
    /// <summary>The loop has no step after the fresh spawn.</summary>
    public static AssertingThat<LoopTimelineSnapshot> HasNoSteps(this AssertingThat<LoopTimelineSnapshot> that)
    {
        var shown = that.InstanceToAssert.Steps.Length;
        Assert.True(shown == 0, $"The loop shows {shown} step{(shown == 1 ? "" : "s")}, but none was expected.");
        return that;
    }

    /// <summary>Step <paramref name="number"/> reloads the weapon in <paramref name="slot"/>, with no enemy count.</summary>
    public static AssertingThat<LoopTimelineSnapshot> HasReloadAt(this AssertingThat<LoopTimelineSnapshot> that, int number, WeaponSlot slot) =>
        that.HasStepAt(number, new PlayerAction.ReloadWeapon(ToCoreSlot(slot)), $"a reload of the {slot.ToShownText()} weapon");

    /// <summary>Step <paramref name="number"/> hits <paramref name="enemies"/> enemies with the weapon in <paramref name="slot"/>.</summary>
    public static AssertingThat<LoopTimelineSnapshot> HasHitAt(this AssertingThat<LoopTimelineSnapshot> that, int number, WeaponSlot slot, int enemies) =>
        that.HasStepAt(number, new PlayerAction.FireWeapon(ToCoreSlot(slot), HitOutcome.Damage, TargetCount.From(enemies)), $"a hit of {enemies} enemies with the {slot.ToShownText()} weapon");

    private static AssertingThat<LoopTimelineSnapshot> HasStepAt(this AssertingThat<LoopTimelineSnapshot> that, int number, PlayerAction expected, string described)
    {
        var steps = that.InstanceToAssert.Steps;
        Assert.True(steps.Length >= number, $"The loop shows {steps.Length} steps, but step #{number} was expected to be {described}.");
        var step = steps[number - 1];
        Assert.True(Equals(step.Action, expected), $"Step #{number} shows \"{step.Token}\", but {described} was expected.");
        return that;
    }

    private static CoreWeaponSlot ToCoreSlot(WeaponSlot slot) =>
        slot.Match(
            kinetic => CoreWeaponSlot.Kinetic,
            energy => CoreWeaponSlot.Energy,
            power => CoreWeaponSlot.Power,
            unlisted => throw new ArgumentException($"No weapon slot is named \"{unlisted.Shown}\".", nameof(slot)));
}

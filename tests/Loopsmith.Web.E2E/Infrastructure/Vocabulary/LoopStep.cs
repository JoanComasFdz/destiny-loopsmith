using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using CoreWeaponSlot = Loopsmith.Core.Domain.WeaponSlot;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A step of the loop, as its card's token names it ("kinetic:reload", "ammo:heavy:slide"). A case is added when a test needs it.</summary>
[Union]
public partial record LoopStep
{
    public partial record Reload(WeaponSlot Slot);
    public partial record PickUpAmmo(Brick Ammo);      // ammo:heavy (ADRs D42)
    public partial record SlideOverAmmo(Brick Ammo);   // ammo:heavy:slide
    public partial record Hit(WeaponSlot Slot, int Enemies);

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="LoopStep"/>: the only place those strings live.</summary>
public static class LoopStepShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>: "kinetic:reload", "ammo:heavy", "ammo:special:slide", "kinetic:hit:3".</summary>
    public static string ToShownText(this LoopStep value) =>
        value.Match(
            reload => $"{ToTokenSlot(reload.Slot)}:reload",
            pickUp => $"ammo:{pickUp.Ammo.ToShownText()}",
            slide => $"ammo:{slide.Ammo.ToShownText()}:slide",
            hit => hit.Enemies == 1 ? $"{ToTokenSlot(hit.Slot)}:hit" : $"{ToTokenSlot(hit.Slot)}:hit:{hit.Enemies}",
            unlisted => unlisted.Shown);

    /// <summary>
    /// The case the UI's <paramref name="shown"/> token names, or <see cref="LoopStep.Unlisted"/>. A token names a step with
    /// numbers in it, so it is read with Core's own parser and checked against <see cref="ToShownText"/>.
    /// </summary>
    public static LoopStep ReadLoopStep(string shown)
    {
        var read = ActionTokenParsing.ParseActionToken(shown) is Result<PlayerAction, string>.Ok parsed ? ToLoopStep(parsed.Value) : null;
        return read is not null && read.ToShownText() == shown ? read : new LoopStep.Unlisted(shown);
    }

    private static LoopStep? ToLoopStep(PlayerAction action) =>
        action switch
        {
            PlayerAction.ReloadWeapon reload => new LoopStep.Reload(ToSlot(reload.Slot)),
            PlayerAction.PickUpAmmo { Pickup: BrickPickup.Plain } pickUp => new LoopStep.PickUpAmmo(ToBrick(pickUp.Brick)),
            PlayerAction.PickUpAmmo slide => new LoopStep.SlideOverAmmo(ToBrick(slide.Brick)),
            PlayerAction.FireWeapon { Hit: HitOutcome.Damage, Shield: ShieldBreak.None } hit => new LoopStep.Hit(ToSlot(hit.Slot), hit.Targets.Value),
            _ => null,
        };

    private static Brick ToBrick(AmmoBrick brick) =>
        brick == AmmoBrick.Special ? new Brick.Special() : new Brick.Heavy();

    private static WeaponSlot ToSlot(CoreWeaponSlot slot) =>
        slot switch
        {
            CoreWeaponSlot.Kinetic => new WeaponSlot.Kinetic(),
            CoreWeaponSlot.Energy => new WeaponSlot.Energy(),
            _ => new WeaponSlot.Power(),
        };

    private static string ToTokenSlot(WeaponSlot slot) =>
        slot.Match(
            kinetic => "kinetic",
            energy => "energy",
            power => "power",
            unlisted => unlisted.Shown);
}

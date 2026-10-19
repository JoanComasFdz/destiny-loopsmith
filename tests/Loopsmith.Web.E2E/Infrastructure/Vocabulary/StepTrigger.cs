using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A button of the trigger palette's picked row, as its caption names it. A case is added when a test needs it.</summary>
[Union]
public partial record StepTrigger
{
    public partial record Hit;
    public partial record Kill;
    public partial record Reload;
    public partial record PickUp(Brick Ammo);   // an ammo brick's button: "special", "heavy" (ADRs D43)

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="StepTrigger"/>: the only place those strings live.</summary>
public static class StepTriggerShowing
{
    private static readonly StepTrigger[] Listed = [new StepTrigger.Hit(), new StepTrigger.Kill(), new StepTrigger.Reload(), new StepTrigger.PickUp(new Brick.Special()), new StepTrigger.PickUp(new Brick.Heavy())];

    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this StepTrigger value) =>
        value.Match(
            hit => "🥊 hit",
            kill => "💀 kill",
            reload => "🔄 reload",
            pickUp => pickUp.Ammo.ToShownText(),
            unlisted => unlisted.Shown);

    /// <summary>The case the UI's <paramref name="shown"/> text names, or <see cref="StepTrigger.Unlisted"/>.</summary>
    public static StepTrigger ReadStepTrigger(string shown) =>
        Listed.FirstOrDefault(value => value.ToShownText() == shown) ?? new StepTrigger.Unlisted(shown);
}

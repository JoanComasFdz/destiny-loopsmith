using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>
/// An ability whose energy a loop keeps (ADRs D48), as a step card's ENERGY names it ("Grenade") and the trigger panel's
/// "On you" declares it recharged ("Grenade recharged").
/// </summary>
[Union]
public partial record AbilitySlot
{
    public partial record Grenade;
    public partial record Melee;
    public partial record ClassAbility;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="AbilitySlot"/>: the only place those strings live.</summary>
public static class AbilitySlotShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this AbilitySlot value) =>
        value.Match(
            grenade => "Grenade",
            melee => "Melee",
            classAbility => "Class ability",
            unlisted => unlisted.Shown);

    /// <summary>The "On you" button that declares <paramref name="value"/> recharged: "Grenade recharged".</summary>
    public static string ToRechargedText(this AbilitySlot value) =>
        $"{value.ToShownText()} recharged";
}

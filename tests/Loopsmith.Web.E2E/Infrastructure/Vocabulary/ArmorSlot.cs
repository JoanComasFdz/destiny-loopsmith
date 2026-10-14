using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>An armor slot, as the designer's Armor box names it. A case is added when a test needs it.</summary>
[Union]
public partial record ArmorSlot
{
    public partial record Helmet;
    public partial record Arms;
    public partial record Chest;
    public partial record Legs;
    public partial record ClassItem;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="ArmorSlot"/>: the only place those strings live.</summary>
public static class ArmorSlotShowing
{
    private static readonly ArmorSlot[] Listed = [new ArmorSlot.Helmet(), new ArmorSlot.Arms(), new ArmorSlot.Chest(), new ArmorSlot.Legs(), new ArmorSlot.ClassItem()];

    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this ArmorSlot value) =>
        value.Match(
            helmet => "Helmet",
            arms => "Arms",
            chest => "Chest",
            legs => "Legs",
            classItem => "ClassItem",
            unlisted => unlisted.Shown);

    /// <summary>The case the UI's <paramref name="shown"/> text names, or <see cref="ArmorSlot.Unlisted"/>.</summary>
    public static ArmorSlot ReadArmorSlot(string shown) =>
        Listed.FirstOrDefault(value => value.ToShownText() == shown) ?? new ArmorSlot.Unlisted(shown);
}

using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A damage type, as a weapon's square in the Weapons box names it. A case is added when a test needs it.</summary>
[Union]
public partial record DamageType
{
    public partial record Solar;
    public partial record Strand;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="DamageType"/>: the only place those strings live.</summary>
public static class DamageTypeShowing
{
    private static readonly DamageType[] Listed = [new DamageType.Solar(), new DamageType.Strand()];

    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this DamageType value) =>
        value.Match(
            solar => "Solar",
            strand => "Strand",
            unlisted => unlisted.Shown);

    /// <summary>The case the UI's <paramref name="shown"/> text names, or <see cref="DamageType.Unlisted"/>.</summary>
    public static DamageType ReadDamageType(string shown) =>
        Listed.FirstOrDefault(value => value.ToShownText() == shown) ?? new DamageType.Unlisted(shown);
}

using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A Guardian's class, as the designer's Subclass box names it. A case is added when a test needs it.</summary>
[Union]
public partial record GuardianClass
{
    public partial record Hunter;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="GuardianClass"/>: the only place those strings live.</summary>
public static class GuardianClassShowing
{
    private static readonly GuardianClass[] Listed = [new GuardianClass.Hunter()];

    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this GuardianClass value) =>
        value.Match(
            hunter => "Hunter",
            unlisted => unlisted.Shown);

    /// <summary>The case the UI's <paramref name="shown"/> text names, or <see cref="GuardianClass.Unlisted"/>.</summary>
    public static GuardianClass ReadGuardianClass(string shown) =>
        Listed.FirstOrDefault(value => value.ToShownText() == shown) ?? new GuardianClass.Unlisted(shown);
}

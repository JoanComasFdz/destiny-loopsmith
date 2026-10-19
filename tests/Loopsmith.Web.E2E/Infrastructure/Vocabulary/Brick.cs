using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>An ammo brick the player picks up (ADRs D42), as its button in the trigger panel's row names it. A case is added when a test needs it.</summary>
[Union]
public partial record Brick
{
    public partial record Special;
    public partial record Heavy;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="Brick"/>: the only place those strings live.</summary>
public static class BrickShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>: "special", "heavy" (the row's buttons are lowercase, as "hit" and "kill").</summary>
    public static string ToShownText(this Brick value) =>
        value.Match(
            special => "special",
            heavy => "heavy",
            unlisted => unlisted.Shown);
}

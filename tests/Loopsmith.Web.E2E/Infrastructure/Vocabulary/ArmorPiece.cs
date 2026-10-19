using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>An armor piece, as a step card names what it fired. A case is added when a test needs it.</summary>
[Union]
public partial record ArmorPiece
{
    public partial record MechaneersTricksleeves;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="ArmorPiece"/>: the only place those strings live.</summary>
public static class ArmorPieceShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this ArmorPiece value) =>
        value.Match(
            mechaneersTricksleeves => "Mechaneer's Tricksleeves",
            unlisted => unlisted.Shown);
}

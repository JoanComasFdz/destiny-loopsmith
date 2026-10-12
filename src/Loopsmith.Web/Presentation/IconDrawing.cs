using System.Collections.Immutable;
using Loopsmith.Core.Domain;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// Pure: the shapes the game draws that Bungie's data has no icon file for (<see cref="DrawnIcon"/>, the glossary's
/// <c>drawnIcon</c>), drawn here as SVG data addresses in the keyword's affinity — Loopsmith's drawing of the game's
/// shape, not the game's file. Rounds: a triangle around a round, as the game shows Unraveling Rounds in your buffs.
/// </summary>
public static class IconDrawing
{
    /// <summary>A drawn icon's address (an SVG data address), in its affinity's colour.</summary>
    public static string DrawIcon(DrawnIcon shape, Affinity affinity) =>
        shape switch
        {
            _ => DrawRounds(ToColor(affinity)),   // Rounds
        };

    /// <summary>A triangle in the colour (filled faintly) around a round in that colour.</summary>
    private static string DrawRounds(string color) =>
        ToDataAddress(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>"
            + $"<path d='M16 3.5 L29.5 28.5 H2.5 Z' fill='{color}' fill-opacity='.22' stroke='{color}' stroke-width='2.2' stroke-linejoin='round'/>"
            + $"<rect x='12.5' y='14' width='7' height='11' rx='1.2' fill='{color}'/>"
            + "<rect x='14.6' y='16.5' width='2.8' height='6' rx='.6' fill='#0b0f17' fill-opacity='.7'/>"
            + "</svg>");

    private static string ToDataAddress(string svg) => "data:image/svg+xml," + Uri.EscapeDataString(svg);

    private static string ToColor(Affinity affinity) =>
        Palette.Where(entry => entry.Affinity == affinity).Select(entry => entry.Color).DefaultIfEmpty(KineticColor).First();

    // The affinities' colours as app.css has them (--arc, --solar…); a neutral or kinetic keyword in --kinetic's. An <img>
    // can't take the page's CSS colours, so they are written here too: change both together.
    private const string KineticColor = "#c9ced8";

    private static readonly ImmutableArray<(Affinity Affinity, string Color)> Palette =
    [
        (Affinity.Arc, "#6fe3ff"), (Affinity.Solar, "#ff9a3c"), (Affinity.Void, "#b07cff"),
        (Affinity.Stasis, "#5aa0ff"), (Affinity.Strand, "#4ee28a"), (Affinity.Prismatic, "#ff5fa2"),
    ];
}

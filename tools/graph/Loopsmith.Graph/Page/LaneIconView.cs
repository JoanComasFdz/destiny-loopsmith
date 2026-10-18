using Loopsmith.Core.DependencyGraphing;

namespace Loopsmith.Graph.Page;

/// <summary>
/// A box's icon: the picture; a buff's picture clipped to a diamond inside the buffs' green diamond border; an emoji (two
/// or more shrink to fit); or an empty dashed square.
/// </summary>
public static class LaneIconView
{
    public static string RenderIcon(LaneIcon icon) =>
        icon.Match(
            picture => $"<img src=\"{LaneBoxView.Encode(picture.Url)}\" alt=\"\">",
            masked => $"<span class=\"bmask\"><img src=\"{LaneBoxView.Encode(masked.Url)}\" alt=\"\">"
                + "<svg viewBox=\"0 0 30 30\"><polygon points=\"15,2 28,15 15,28 2,15\"/></svg></span>",
            emoji => $"<em class=\"emo{(CountSymbols(emoji.Text) > 1 ? " many" : "")}\">{LaneBoxView.Encode(emoji.Text)}</em>",
            _ => "<i></i>");

    /// <summary>A group heading's icon: the same, but nothing when there is none.</summary>
    public static string RenderHeadingIcon(LaneIcon icon) =>
        icon is LaneIcon.Blank ? "" : RenderIcon(icon);

    private static int CountSymbols(string text) => System.Globalization.StringInfo.ParseCombiningCharacters(text).Length;
}

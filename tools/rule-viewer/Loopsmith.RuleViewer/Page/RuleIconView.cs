using Loopsmith.RuleViewer.Core;

namespace Loopsmith.RuleViewer.Page;

/// <summary>
/// A box's icon: the picture; a buff's picture clipped to a diamond inside the buffs' green diamond border; an emoji (two
/// or more shrink to fit); or an empty dashed square.
/// </summary>
public static class RuleIconView
{
    public static string RenderIcon(RuleIcon icon) =>
        icon.Match(
            picture => $"<img src=\"{RuleBoxView.Encode(picture.Url)}\" alt=\"\">",
            masked => $"<span class=\"bmask\"><img src=\"{RuleBoxView.Encode(masked.Url)}\" alt=\"\">"
                + "<svg viewBox=\"0 0 30 30\"><polygon points=\"15,2 28,15 15,28 2,15\"/></svg></span>",
            emoji => $"<em class=\"emo{(CountSymbols(emoji.Text) > 1 ? " many" : "")}\">{RuleBoxView.Encode(emoji.Text)}</em>",
            _ => "<i></i>");

    /// <summary>A group heading's icon: the same, but nothing when there is none.</summary>
    public static string RenderHeadingIcon(RuleIcon icon) =>
        icon is RuleIcon.Blank ? "" : RenderIcon(icon);

    private static int CountSymbols(string text) => System.Globalization.StringInfo.ParseCombiningCharacters(text).Length;
}

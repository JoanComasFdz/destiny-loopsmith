using System.Net;
using Loopsmith.RuleViewer.Core;

namespace Loopsmith.RuleViewer.Page;

/// <summary>A lane box: its icon, its kind title in its colour, its name and its caption; a box the lane ends has a dashed border.</summary>
public static class LaneBoxView
{
    public static string RenderBox(LaneBox box) =>
        $"<div class=\"lbox{ToRoleClass(box.Role)}\" style=\"--c:var(--{ToToneName(box.Tone)})\">"
        + LaneIconView.RenderIcon(box.Icon)
        + $"<div><b>{Encode(box.Title)}</b><span>{Encode(box.Name)}</span>"
        + (box.Caption.Length == 0 ? "" : $"<small>{Encode(box.Caption)}</small>")
        + "</div></div>";

    private static string ToRoleClass(BoxRole role) =>
        role switch
        {
            BoxRole.Trigger => " trig",
            BoxRole.Ends => " ends",
            _ => "",
        };

    /// <summary>The page's colour variable of a tone (<c>--buff</c>, <c>--outside</c>).</summary>
    private static string ToToneName(BoxTone tone) => tone.ToString().ToLowerInvariant();

    public static string Encode(string text) => WebUtility.HtmlEncode(text);
}

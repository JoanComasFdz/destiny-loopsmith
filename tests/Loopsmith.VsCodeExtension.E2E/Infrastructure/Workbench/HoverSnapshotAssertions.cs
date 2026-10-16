using JoanComasFdz.AssertingThat;

namespace Loopsmith.VsCodeExtension.E2E.Infrastructure.Workbench;

/// <summary>What a test checks on the hover.</summary>
public static class HoverSnapshotAssertions
{
    /// <summary>The hover's title (its bold first line) is <paramref name="title"/>.</summary>
    public static AssertingThat<HoverSnapshot> HasTitle(this AssertingThat<HoverSnapshot> that, string title)
    {
        Assert.True(that.InstanceToAssert.Title == title, $"The hover's title is \"{that.InstanceToAssert.Title}\", but \"{title}\" was expected.");
        return that;
    }

    /// <summary>A line of the hover reads <paramref name="line"/>.</summary>
    public static AssertingThat<HoverSnapshot> Reads(this AssertingThat<HoverSnapshot> that, string line)
    {
        Assert.True(
            that.InstanceToAssert.Lines.Contains(line),
            $"The hover reads {string.Join(", ", that.InstanceToAssert.Lines.Select(shown => $"\"{shown}\""))}, but no line reads \"{line}\".");
        return that;
    }

    /// <summary>The hover shows exactly these GIFs, each labelled, side by side in this order (same top, left to right).</summary>
    public static AssertingThat<HoverSnapshot> ShowsGifsSideBySide(this AssertingThat<HoverSnapshot> that, params (string Asset, string Label)[] gifs)
    {
        var shown = that.InstanceToAssert.Gifs;
        Assert.True(
            shown.Select(gif => (gif.Asset, gif.Label)).SequenceEqual(gifs),
            $"The hover shows {string.Join(", ", shown.Select(gif => $"\"{gif.Asset}\" labelled \"{gif.Label}\""))}, but {string.Join(", ", gifs.Select(gif => $"\"{gif.Asset}\" labelled \"{gif.Label}\""))} was expected.");
        for (var at = 1; at < shown.Length; at++)
        {
            var (left, right) = (shown[at - 1].Box, shown[at].Box);
            Assert.True(
                Math.Abs(left.Top - right.Top) < 1 && right.Left >= left.Right - 1,
                $"The hover's \"{shown[at].Label}\" GIF isn't beside its \"{shown[at - 1].Label}\" GIF: it is at ({right.Left}, {right.Top}), that one ends at ({left.Right}, {left.Top}).");
        }

        return that;
    }

    /// <summary>The hover's links are exactly <paramref name="links"/>, in this order.</summary>
    public static AssertingThat<HoverSnapshot> Links(this AssertingThat<HoverSnapshot> that, params string[] links)
    {
        var shown = that.InstanceToAssert.Links.Select(link => link.Text).ToArray();
        Assert.True(
            shown.SequenceEqual(links),
            $"The hover links {string.Join(", ", shown.Select(link => $"\"{link}\""))}, but {string.Join(", ", links.Select(link => $"\"{link}\""))} was expected.");
        return that;
    }

    /// <summary>The hover's link <paramref name="link"/> goes to <paramref name="target"/>.</summary>
    public static AssertingThat<HoverSnapshot> LinksTo(this AssertingThat<HoverSnapshot> that, string link, string target)
    {
        var shown = that.InstanceToAssert.Links.FirstOrDefault(found => found.Text == link);
        Assert.True(shown is not null, $"The hover has no \"{link}\" link.");
        Assert.True(shown.Target == target, $"The hover's \"{link}\" goes to \"{shown.Target}\", but \"{target}\" was expected.");
        return that;
    }

    /// <summary>The hover has no link at all (none plays an MP4, none opens the player).</summary>
    public static AssertingThat<HoverSnapshot> HasNoLinks(this AssertingThat<HoverSnapshot> that) => that.Links();
}

namespace Loopsmith.RuleViewer.E2E.Infrastructure.RulesPage;

/// <summary>A box on the screen, in CSS pixels from the window's top left corner.</summary>
public sealed record ShownBox(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;

    /// <summary>Whether this box and <paramref name="other"/> cover any of the same pixels.</summary>
    public bool Overlaps(ShownBox other) => Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;
}

/// <summary>Where the tree and the flows are, and how wide the page is against the window it shows in.</summary>
public sealed record RulesPageLayout(ShownBox Tree, ShownBox Flows, double WindowWidth, double PageWidth);

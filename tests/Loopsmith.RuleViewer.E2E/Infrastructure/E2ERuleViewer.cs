namespace Loopsmith.RuleViewer.E2E.Infrastructure;

/// <summary>
/// Where the run's page and recordings are, from the environment: <c>E2E_RULE_VIEWER_URL</c> (the rules page the rule
/// viewer drew from Fixtures/rules, served: tools/e2e/record.sh serves it; without it every scenario is skipped),
/// <c>E2E_VIDEO_DIR</c> (where each scenario's video and its record go; without it nothing is recorded) and
/// <c>E2E_ACTION_GAP</c> (the least time between two steps of a recorded scenario, in seconds) and <c>E2E_PAGE_CACHE</c>
/// (the page's icons and fonts, outside the repository).
/// </summary>
public static class E2ERuleViewer
{
    /// <summary>The rules page's address, or null when <c>E2E_RULE_VIEWER_URL</c> isn't set.</summary>
    public static string? Url { get; } = ReadNonEmpty("E2E_RULE_VIEWER_URL");

    /// <summary>Where videos go, or null when the run records none.</summary>
    public static string? VideoDirectory { get; } = ReadNonEmpty("E2E_VIDEO_DIR") is { } dir ? Path.GetFullPath(dir) : null;

    /// <summary>The least time between the end of one step of a recorded scenario and the start of the next (1 s by default).</summary>
    public static TimeSpan ActionGap { get; } = ReadNonEmpty("E2E_ACTION_GAP") is { } gap
        ? TimeSpan.FromSeconds(double.Parse(gap, System.Globalization.CultureInfo.InvariantCulture))
        : TimeSpan.FromSeconds(1);

    /// <summary>
    /// Where the page's icons and fonts are cached (<see cref="OutsideRouting"/>): <c>E2E_PAGE_CACHE</c>, by default
    /// <c>~/.cache/loopsmith/rule-viewer-page</c>. Bungie's and Google's files: never committed.
    /// </summary>
    public static string PageCache { get; } = ReadNonEmpty("E2E_PAGE_CACHE") is { } cache
        ? Path.GetFullPath(cache)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "loopsmith", "rule-viewer-page");

    private static string? ReadNonEmpty(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
}

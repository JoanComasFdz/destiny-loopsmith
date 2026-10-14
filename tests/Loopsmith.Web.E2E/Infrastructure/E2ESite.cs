namespace Loopsmith.Web.E2E.Infrastructure;

/// <summary>
/// Where the run's site, recordings and manifest cache are, from the environment: <c>E2E_URL</c> (the running site's
/// Home; without it every scenario is skipped), <c>E2E_VIDEO_DIR</c> (where each scenario's video and its record go;
/// without it nothing is recorded) and <c>E2E_MANIFEST_CACHE</c> (Bungie's manifest files, outside the repository).
/// </summary>
public static class E2ESite
{
    /// <summary>The running site's Home, ending in "/", or null when <c>E2E_URL</c> isn't set.</summary>
    public static string? Url { get; } = ReadUrl();

    /// <summary>Where videos go, or null when the run records none.</summary>
    public static string? VideoDirectory { get; } = ReadNonEmpty("E2E_VIDEO_DIR") is { } dir ? Path.GetFullPath(dir) : null;

    /// <summary>The local copy of Bungie's manifest files: Bungie's data, never copied into the repository.</summary>
    public static string ManifestCache { get; } = ReadNonEmpty("E2E_MANIFEST_CACHE") is { } dir
        ? Path.GetFullPath(dir)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "loopsmith", "bungie-manifest");

    /// <summary>Where every request that left for bungie.net, or was refused, is logged (one line each).</summary>
    public static string? BungieLog => VideoDirectory is { } dir ? Path.Combine(dir, "bungie-requests.log") : null;

    private static string? ReadUrl() =>
        ReadNonEmpty("E2E_URL") is { } url ? (url.EndsWith('/') ? url : url + "/") : null;

    private static string? ReadNonEmpty(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
}

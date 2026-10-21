using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Core;

/// <summary>The parts of an asset name <c>pr-&lt;n&gt;-v&lt;version&gt;-&lt;run&gt;</c>: its PR, its version and its run, with the extension.</summary>
public sealed record AssetName(int Pr, int Version, string Run);

/// <summary>
/// Where a run's recordings go and what they are called there. A rule scenario's (its GIF under <c>Rules/&lt;…&gt;/</c>) go to
/// its rule group's release, <c>rules-&lt;the folders under Rules/, kebab-cased, joined by dashes&gt;-resources</c>
/// (Rules/Hunter/Arc/Melees → rules-hunter-arc-melees-resources, one per rule file: rules/hunter/arc/melees.yaml); any
/// other run's to <c>prs-resources</c>. The VS Code extension names them the same way (its <c>findRelease</c> and
/// <c>nameAsset</c>; tests/Loopsmith.VsCodeExtension.E2E's AssetNaming).
/// </summary>
public static partial class ReleaseNaming
{
    public const string PrsRelease = "prs-resources";
    public const string RulesFolder = "Rules";

    /// <summary>The longest asset name the workflow writes; a longer one keeps its start and ends with a hash.</summary>
    public const int MaxName = 200;

    [GeneratedRegex(@"^rules-[a-z0-9]+(?:-[a-z0-9]+)*-resources$")]
    private static partial Regex RuleReleasePattern();

    [GeneratedRegex(@"^pr-([0-9]+)-v([0-9]+)-(.+)$")]
    private static partial Regex AssetPattern();

    [GeneratedRegex(@"-([0-9a-f]{8})(\.[a-z0-9]+)$")]
    private static partial Regex HashedPattern();

    [GeneratedRegex(@"([A-Z]+)([A-Z][a-z])")]
    private static partial Regex CapitalRunPattern();

    [GeneratedRegex(@"([a-z\d])([A-Z])")]
    private static partial Regex WordStartPattern();

    [GeneratedRegex(@"[-\s]")]
    private static partial Regex SeparatorPattern();

    /// <summary>"AllClasses" → "all-classes": Humanizer's Kebaberize, as ScenarioNaming.ToGifName and the extension's toKebabCase.</summary>
    public static string ToKebabCase(string name)
    {
        var split = CapitalRunPattern().Replace(name, "$1_$2");
        var words = WordStartPattern().Replace(split, "$1_$2");
        return SeparatorPattern().Replace(words, "_").ToLowerInvariant().Replace('_', '-');
    }

    /// <summary>A run's release, from its GIF's path (<c>&lt;domain folder&gt;/&lt;GIF name&gt;.gif</c>).</summary>
    public static string FindRelease(string gif)
    {
        var folders = gif.Split('/')[..^1];
        return folders.Length < 2 || folders[0] != RulesFolder
            ? PrsRelease
            : $"rules-{string.Join('-', folders[1..].Select(ToKebabCase))}-resources";
    }

    public static bool IsRuleRelease(string tag) => RuleReleasePattern().IsMatch(tag);

    /// <summary>
    /// <c>pr-&lt;n&gt;-v&lt;version&gt;-&lt;GIF name&gt;&lt;extension&gt;</c>; past 200 characters, its start plus a hash of the GIF's path (a
    /// method and its screen). A run's .gif and .mp4 share their stem.
    /// </summary>
    public static string NameAsset(int pr, int version, string gif, string extension)
    {
        var stem = $"pr-{pr}-v{version}-{RemoveExtension(gif.Split('/')[^1])}";
        if (stem.Length + extension.Length <= MaxName)
        {
            return stem + extension;
        }

        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(gif)))[..8];
        var keep = MaxName - $"-{digest}{extension}".Length;
        return $"{stem[..keep].TrimEnd('-')}-{digest}{extension}";
    }

    /// <summary>
    /// <c>pr-&lt;n&gt;-v&lt;version&gt;-&lt;rule name&gt;.png</c>, the flow of the rule a run names; none without one. A rule's scenarios
    /// and screens share it, and the cleanup keeps each rule's newest like a run's.
    /// </summary>
    public static Optional<string> NameFlow(int pr, int version, Optional<string> flow) =>
        flow.Map(path => $"pr-{pr}-v{version}-{path.Split('/')[^1]}");

    /// <summary>
    /// The parts of a name <c>pr-&lt;n&gt;-v&lt;version&gt;-&lt;run&gt;</c>; none for any other name. A name shortened to 200
    /// characters keeps less of its run the longer its PR number and version are, so its run is its hash and extension,
    /// the same for every PR and version.
    /// </summary>
    public static Optional<AssetName> ReadAsset(string name)
    {
        var found = AssetPattern().Match(name);
        if (!found.Success)
        {
            return Optional.None<AssetName>();
        }

        var hashed = name.Length == MaxName ? HashedPattern().Match(name) : Match.Empty;
        var run = hashed.Success ? $"#{hashed.Groups[1].Value}{hashed.Groups[2].Value}" : found.Groups[3].Value;
        return Optional.Some(new AssetName(int.Parse(found.Groups[1].Value), int.Parse(found.Groups[2].Value), run));
    }

    /// <summary>A file name without its last extension ("hits-desktop.gif" → "hits-desktop"); a leading dot is no extension.</summary>
    private static string RemoveExtension(string file)
    {
        var dot = file.LastIndexOf('.');
        return dot > 0 && file[..dot].TrimStart('.').Length > 0 ? file[..dot] : file;
    }
}

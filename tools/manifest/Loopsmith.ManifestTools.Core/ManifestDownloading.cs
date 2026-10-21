using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Loopsmith.Functional;
using static Loopsmith.ManifestTools.Core.DefinitionReading;

namespace Loopsmith.ManifestTools.Core;

/// <summary>A manifest version: its version string (written into the excerpt) and its content id (in each component's file name).</summary>
public sealed record ManifestVersion(string Version, string ContentId);

/// <summary>A component to download: its file in the manifest folder, and where bungie.net serves it.</summary>
public sealed record ComponentDownload(string File, string Url);

/// <summary>What the download needs: the manifest index, or the version given in its place, and the components' addresses.</summary>
public static partial class ManifestDownloading
{
    /// <summary>The manifest index; it is sent with the API key (<c>X-API-Key</c>) when there is one.</summary>
    public const string IndexUrl = "https://www.bungie.net/Platform/Destiny2/Manifest/";

    /// <summary>
    /// The version the environment gives in place of the index (<c>MANIFEST_ID</c>, and <c>MANIFEST_VERSION</c>, "unknown"
    /// when it is missing); none without a content id: the index is read then.
    /// </summary>
    public static Optional<ManifestVersion> ReadGivenVersion(Optional<string> contentId, Optional<string> version) =>
        contentId
            .Bind(id => id.Length > 0 ? Optional.Some(id) : Optional.None<string>())
            .Map(id => new ManifestVersion(version.Bind(given => given.Length > 0 ? Optional.Some(given) : Optional.None<string>()).UnwrapOr("unknown"), id));

    /// <summary>
    /// The manifest index → its version and its English content id (the part of DestinyInventoryItemDefinition's path
    /// after "Definition-"); an error saying what the index lacks.
    /// </summary>
    public static Result<ManifestVersion, string> ReadIndex(string json) =>
        ParseRoot(json)
            .Bind(root => ReadObject(root, "Response"))
            .ToResult(() => "The manifest index isn't JSON with a Response: " + Shorten(json))
            .Bind(response =>
                ReadString(response, "version").ToResult(() => "The manifest index has no version.").Bind(version =>
                ReadObject(response, "jsonWorldComponentContentPaths")
                    .Bind(paths => ReadObject(paths, "en"))
                    .Bind(english => ReadString(english, "DestinyInventoryItemDefinition"))
                    .Bind(path => FindContentId().Match(path) is { Success: true } found ? Optional.Some(found.Groups[1].Value) : Optional.None<string>())
                    .ToResult(() => "The manifest index has no English DestinyInventoryItemDefinition path.")
                    .Map(contentId => new ManifestVersion(version, contentId))));

    /// <summary>The components to download, each to <c>&lt;name&gt;.json</c>.</summary>
    public static ImmutableArray<ComponentDownload> ListDownloads(string contentId) =>
        [.. ManifestTableParsing.ComponentNames.Select(name => new ComponentDownload(
            $"{name}.json", $"https://www.bungie.net/common/destiny2_content/json/en/{name}-{contentId}.json"))];

    /// <summary>version.txt's text: the version, one line.</summary>
    public static string WriteVersionFile(ManifestVersion version) => version.Version + "\n";

    /// <summary>The line the download ends with.</summary>
    public static string DescribeDownload(ManifestVersion version, string folder) =>
        $"manifest {version.Version} ({version.ContentId}) in {folder}";

    /// <summary>version.txt's text → the version (its first line, trimmed); none when it is empty.</summary>
    public static Optional<string> ReadVersionFile(string text) =>
        text.Split('\n')[0].Trim() is { Length: > 0 } version ? Optional.Some(version) : Optional.None<string>();

    private static Optional<JsonElement> ParseRoot(string json)
    {
        try
        {
            return Optional.Some(JsonDocument.Parse(json).RootElement);
        }
        catch (JsonException)
        {
            return Optional.None<JsonElement>();
        }
    }

    private static string Shorten(string text) => text.Length > 200 ? text[..200] + "…" : text;

    [GeneratedRegex(@"Definition-(.+)\.json$")]
    private static partial Regex FindContentId();
}

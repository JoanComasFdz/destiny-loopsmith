using System.Net;
using Loopsmith.Functional;
using Loopsmith.ManifestTools.Core;

namespace Loopsmith.ManifestTools.Hosting;

/// <summary>
/// <c>download</c>: the manifest index (or the version the environment gives) → the twelve English components and
/// version.txt in the folder. The index is sent with <c>BUNGIE_API_KEY</c> as <c>X-API-Key</c> when it is set; the
/// component files need no key. Where the index can't be read (a cloud session got bungie.net's 500 error page for it),
/// give the version instead: <c>MANIFEST_ID=&lt;content id&gt; MANIFEST_VERSION=&lt;version&gt;</c>.
/// </summary>
public static class ManifestDownload
{
    public static async Task<Result<Unit, string>> DownloadManifest(ManifestCommand.Download command)
    {
        var contentId = Environment.GetEnvironmentVariable("MANIFEST_ID");   // impure
        var versionName = Environment.GetEnvironmentVariable("MANIFEST_VERSION");   // impure
        var apiKey = Environment.GetEnvironmentVariable("BUNGIE_API_KEY");   // impure
        var given = ManifestDownloading.ReadGivenVersion(Optional.FromNullable(contentId), Optional.FromNullable(versionName));   // pure
        var created = ManifestFiles.CreateFolder(command.Folder);   // impure
        if (created is Result<Unit, string>.Error)
        {
            return created;
        }

        using var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });   // impure
        var version = given is Optional<ManifestVersion>.Some known
            ? new Result<ManifestVersion, string>.Ok(known.Value)
            : await ReadIndex(client, command.Folder, Optional.FromNullable(apiKey));   // impure
        if (version is not Result<ManifestVersion, string>.Ok found)
        {
            return version.Map(Unit (_) => new Unit.Value());
        }

        var downloads = ManifestDownloading.ListDownloads(found.Value.ContentId);   // pure
        foreach (var download in downloads)
        {
            var saved = await DownloadFile(client, download.Url, Path.Combine(command.Folder, download.File));   // impure
            if (saved is Result<Unit, string>.Error)
            {
                return saved;
            }
        }

        var versionFile = ManifestDownloading.WriteVersionFile(found.Value);   // pure
        var written = ManifestFiles.WriteText(Path.Combine(command.Folder, "version.txt"), versionFile);   // impure
        var described = ManifestDownloading.DescribeDownload(found.Value, command.Folder);   // pure
        ManifestFiles.Report(described);   // impure
        return written;
    }

    /// <summary>The index, saved as index.json in the folder, → the version.</summary>
    private static async Task<Result<ManifestVersion, string>> ReadIndex(HttpClient client, string folder, Optional<string> apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ManifestDownloading.IndexUrl);
        if (apiKey is Optional<string>.Some { Value.Length: > 0 } key)
        {
            request.Headers.Add("X-API-Key", key.Value);
        }

        try
        {
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return new Result<ManifestVersion, string>.Error(
                    $"The manifest index answered {(int)response.StatusCode}: give MANIFEST_ID and MANIFEST_VERSION instead (tools/manifest/README.md).");
            }

            await File.WriteAllTextAsync(Path.Combine(folder, "index.json"), text);
            return ManifestDownloading.ReadIndex(text);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException or TaskCanceledException)
        {
            return new Result<ManifestVersion, string>.Error($"Cannot read the manifest index: {exception.Message}");
        }
    }

    private static async Task<Result<Unit, string>> DownloadFile(HttpClient client, string url, string path)
    {
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return new Result<Unit, string>.Error($"{url} answered {(int)response.StatusCode}.");
            }

            await using var file = File.Create(path);
            await response.Content.CopyToAsync(file);
            return new Result<Unit, string>.Ok(new Unit.Value());
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException or TaskCanceledException)
        {
            return new Result<Unit, string>.Error($"Cannot download {url}: {exception.Message}");
        }
    }
}

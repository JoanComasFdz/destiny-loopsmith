using System.Collections.Immutable;
using Loopsmith.Core.Functional;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace Loopsmith.Web.Hosting;

/// <summary>
/// Impure: every browser side effect the web host performs (download, clipboard, confirm, URL hash, reading a
/// picked file). Nothing is stored in the browser. Components call these on their own lines, never nested in a
/// pure call.
/// </summary>
public sealed class BrowserInterop(IJSRuntime js)
{
    private const long MaxFileBytes = 2 * 1024 * 1024;

    public ValueTask DownloadTextAsync(string fileName, string text) =>
        js.InvokeVoidAsync("loopsmith.downloadText", fileName, text, "application/yaml");

    public ValueTask<bool> CopyTextAsync(string text) =>
        js.InvokeAsync<bool>("loopsmith.copyText", text);

    public ValueTask<bool> ConfirmAsync(string message) =>
        js.InvokeAsync<bool>("loopsmith.confirmAction", message);

    /// <summary>Scrolls the element into view on wide screens (on a phone the palette stays where the thumb is).</summary>
    public ValueTask RevealElementAsync(string elementId) =>
        js.InvokeVoidAsync("loopsmith.revealElement", elementId);

    public ValueTask ClearLocationHashAsync() =>
        js.InvokeVoidAsync("loopsmith.clearHash");

    // ── Bungie's manifest (wwwroot/js/manifest.js holds the downloaded components; Core reads their entries) ──

    /// <summary>The manifest's index, or none when bungie.net doesn't answer it.</summary>
    public async Task<Optional<string>> ReadManifestIndexAsync(string url, string apiKey)
    {
        var index = await js.InvokeAsync<string?>("loopsmithManifest.readIndex", url, apiKey);
        return Optional.FromNullable(index);
    }

    // JS interop serializes plain dictionaries and arrays (not immutable collections), so each call copies into them.

    /// <summary>Downloads (once per tab) and indexes each component; why it couldn't, or "" when every one is in.</summary>
    public ValueTask<string> LoadManifestTablesAsync(IReadOnlyDictionary<string, string> urls) =>
        js.InvokeAsync<string>("loopsmithManifest.loadTables", new Dictionary<string, string>(urls));

    /// <summary>The raw JSON of the entries asked for, per component (<c>{ component: { hash: entry } }</c>).</summary>
    public ValueTask<string> ReadManifestEntriesAsync(IReadOnlyDictionary<string, ImmutableArray<uint>> request) =>
        js.InvokeAsync<string>("loopsmithManifest.readEntries", request.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray()));

    /// <summary>The item component's plug items of each plug category (<c>{ category: [hashes] }</c>).</summary>
    public ValueTask<string> FindManifestPlugsAsync(string component, IReadOnlyList<string> categories) =>
        js.InvokeAsync<string>("loopsmithManifest.findPlugs", component, categories.ToArray());

    /// <summary>A file the user picked with <c>InputFile</c>, as text (foreseeable failures become an error).</summary>
    public static async Task<Result<string, string>> ReadPickedFileAsync(IBrowserFile file)
    {
        try
        {
            await using var stream = file.OpenReadStream(MaxFileBytes);
            using var reader = new StreamReader(stream);
            var text = await reader.ReadToEndAsync();
            return new Result<string, string>.Ok(text);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or JSException)
        {
            return new Result<string, string>.Error($"Cannot read '{file.Name}': {exception.Message}");
        }
    }
}

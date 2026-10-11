using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.ManifestJoining;
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

    /// <summary>Moves the keyboard focus to an element (a moved step's card) and scrolls it into view.</summary>
    public ValueTask FocusElementAsync(string elementId) =>
        js.InvokeVoidAsync("loopsmith.focusElement", elementId);

    public ValueTask ClearLocationHashAsync() =>
        js.InvokeVoidAsync("loopsmith.clearHash");

    // ── Bungie's manifest (wwwroot/js/manifest.js holds the downloaded components; Core reads their entries) ──

    /// <summary>The manifest's index, or none when bungie.net doesn't answer it.</summary>
    public async Task<Optional<string>> ReadManifestIndexAsync(string url, string apiKey)
    {
        var index = await js.InvokeAsync<string?>("loopsmithManifest.readIndex", url, apiKey);
        return Optional.FromNullable(index);
    }

    // JS interop serializes plain dictionaries and arrays (not immutable collections or value objects), so each call
    // copies into them.

    /// <summary>Downloads (once per tab) and indexes each component (<c>{ component: url }</c>); why it couldn't, when it couldn't.</summary>
    public async Task<Result<Unit, string>> LoadManifestTablesAsync(IReadOnlyDictionary<string, string> urls)
    {
        var plain = new Dictionary<string, string>(urls);
        var problem = await js.InvokeAsync<string?>("loopsmithManifest.loadTables", plain);
        return Optional.FromNullable(problem).Match<Result<Unit, string>>(
            failed => new Result<Unit, string>.Error(failed.Value),
            _ => new Result<Unit, string>.Ok(new Unit.Value()));
    }

    /// <summary>The raw JSON of the entries asked for, per component (<c>{ component: { hash: entry } }</c>).</summary>
    public async Task<string> ReadManifestEntriesAsync(IReadOnlyDictionary<string, ImmutableArray<ItemHash>> request)
    {
        var plain = request.ToDictionary(entry => entry.Key, entry => entry.Value.Select(hash => hash.Value).ToArray());
        return await js.InvokeAsync<string>("loopsmithManifest.readEntries", plain);
    }

    /// <summary>The hashes of a component's entries whose JSON holds each needle (<c>{ needle: [hashes] }</c>).</summary>
    public async Task<string> FindManifestEntriesContainingAsync(ManifestTextSearch search)
    {
        var needles = search.Needles.ToArray();
        return await js.InvokeAsync<string>("loopsmithManifest.findEntriesContaining", search.Component, needles);
    }

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

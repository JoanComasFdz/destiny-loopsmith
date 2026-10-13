using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.ManifestJoining;
using Microsoft.AspNetCore.Components;
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

    /// <summary>Puts the keyboard focus in a dialog when it is anywhere else (the button that was focused went away), so Escape still reaches it.</summary>
    public ValueTask KeepFocusInAsync(ElementReference element) =>
        js.InvokeVoidAsync("loopsmith.keepFocusIn", element);

    public ValueTask ClearLocationHashAsync() =>
        js.InvokeVoidAsync("loopsmith.clearHash");

    // ── Bungie's login (wwwroot/js/bungie-login.js: its window, and the login page's answer back to this tab) ──

    /// <summary>Opens Bungie's login in a window and waits for the login page's answer (JSON: the code and state, or why not).</summary>
    public ValueTask<string> LogInWithBungieAsync(string url) =>
        js.InvokeAsync<string>("loopsmithBungie.login", url);

    /// <summary>Stops waiting for Bungie's login (the player closed its window).</summary>
    public ValueTask CancelBungieLoginAsync() =>
        js.InvokeVoidAsync("loopsmithBungie.cancel");

    /// <summary>On the login page: hands Bungie's answer to the tab that asked, and closes the window.</summary>
    public ValueTask RelayBungieLoginAsync() =>
        js.InvokeVoidAsync("loopsmithBungie.relay", (object?)null);

    // ── Bungie's manifest (wwwroot/js/manifest.js holds the downloaded components; Core reads their entries) ──

    /// <summary>The manifest's index, or none when bungie.net doesn't answer it.</summary>
    public async Task<Optional<string>> ReadManifestIndexAsync(string url, string apiKey)
    {
        var index = await js.InvokeAsync<string?>("loopsmithManifest.readIndex", url, apiKey);
        return Optional.FromNullable(index);
    }

    // JS interop serializes plain dictionaries and arrays (not immutable collections or value objects), so each call
    // copies into them.

    /// <summary>
    /// Downloads and indexes each component (<c>{ component: url }</c>) not in yet, and keeps them for the tab: a file whose
    /// download breaks starts again from its start, up to <paramref name="retries"/> more times. Tells
    /// <paramref name="reporter"/> how many bytes came and which try a file is on (<c>ReportDownload</c>); why it couldn't,
    /// when it couldn't.
    /// </summary>
    public async Task<Result<Unit, string>> LoadManifestTablesAsync<TReporter>(IReadOnlyDictionary<string, string> urls, int retries, DotNetObjectReference<TReporter> reporter)
        where TReporter : class
    {
        var plain = new Dictionary<string, string>(urls);
        var problem = await js.InvokeAsync<string?>("loopsmithManifest.loadTables", plain, retries, reporter);
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

    /// <summary>
    /// A component's entries holding <c>"key":value</c> for one of the query's values, each cut down to that value's paths,
    /// grouped by value (<c>{ value: { hash: { …parts… } } }</c>).
    /// </summary>
    public async Task<string> ReadManifestPartsAsync(ManifestPartsQuery query)
    {
        var paths = query.Paths.ToDictionary(entry => entry.Key, entry => entry.Value.Select(path => path.ToArray()).ToArray());
        return await js.InvokeAsync<string>("loopsmithManifest.readEntryParts", query.Component, query.Key, paths);
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

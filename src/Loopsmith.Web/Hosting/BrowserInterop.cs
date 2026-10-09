using Loopsmith.Core.Functional;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace Loopsmith.Web.Hosting;

/// <summary>
/// Impure: every browser side effect the web host performs (download, clipboard, localStorage, confirm,
/// URL hash, reading a picked file). Components call these on their own lines, never nested in a pure call.
/// </summary>
public sealed class BrowserInterop(IJSRuntime js)
{
    private const long MaxFileBytes = 2 * 1024 * 1024;

    public ValueTask DownloadTextAsync(string fileName, string text) =>
        js.InvokeVoidAsync("loopsmith.downloadText", fileName, text, "application/yaml");

    public ValueTask<bool> CopyTextAsync(string text) =>
        js.InvokeAsync<bool>("loopsmith.copyText", text);

    public async ValueTask<Optional<string>> ReadStorageAsync(string key)
    {
        var value = await js.InvokeAsync<string?>("loopsmith.readStorage", key);   // impure
        return Optional.FromNullable(value);                                         // pure
    }

    public ValueTask<bool> WriteStorageAsync(string key, string value) =>
        js.InvokeAsync<bool>("loopsmith.writeStorage", key, value);

    public ValueTask<bool> ConfirmAsync(string message) =>
        js.InvokeAsync<bool>("loopsmith.confirmAction", message);

    /// <summary>Scrolls the element into view on wide screens (on a phone the palette stays where the thumb is).</summary>
    public ValueTask RevealElementAsync(string elementId) =>
        js.InvokeVoidAsync("loopsmith.revealElement", elementId);

    public ValueTask ClearLocationHashAsync() =>
        js.InvokeVoidAsync("loopsmith.clearHash");

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

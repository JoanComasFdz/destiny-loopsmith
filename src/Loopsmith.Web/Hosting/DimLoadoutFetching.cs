using Loopsmith.Core.Functional;

namespace Loopsmith.Web.Hosting;

/// <summary>
/// Impure: asks DIM's API for a dim.gg share's loadout. DIM answers only apps registered with it, by the API key sent as
/// <c>X-API-Key</c> from the origin the key was registered for (docs/hosting.md, "DIM links"). Without a key in
/// <c>wwwroot/appsettings.json</c> (<c>DimApiKey</c>), dim.gg links can't be opened yet; links that carry their loadout can.
/// </summary>
public sealed class DimLoadoutFetching(HttpClient http, string apiKey)
{
    public const string NotRegistered =
        "Loopsmith can't open dim.gg links yet: DIM hands a shared loadout only to apps registered with its API, and "
        + "Loopsmith's registration isn't set up. Links that carry the loadout itself (…/loadouts?loadout=…, from "
        + "D2ArmorPicker or guardian.report) work now.";

    public bool CanFetchShares => apiKey.Length > 0;

    /// <summary>DIM's JSON answer (<c>{ "loadout": … }</c>) for the share at <paramref name="url"/>, or why it couldn't be had.</summary>
    public async Task<Result<string, string>> FetchSharedLoadoutAsync(string url)
    {
        if (!CanFetchShares)
        {
            return new Result<string, string>.Error(NotRegistered);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-API-Key", apiKey);
            using var response = await http.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? new Result<string, string>.Ok(text)
                : new Result<string, string>.Error(DescribeRefusal((int)response.StatusCode));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new Result<string, string>.Error($"Couldn't reach DIM to read the shared loadout ({exception.Message}). Check your connection and try again.");
        }
    }

    private static string DescribeRefusal(int status) =>
        status == 404
            ? "DIM doesn't know that share: check the link (copy it again from DIM's Share)."
            : $"DIM didn't hand over the shared loadout (HTTP {status}). Try again later.";
}

using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Web.Hosting;

/// <summary>
/// Impure: asks DIM's API for a dim.gg share's loadout. DIM hands a share to any caller (docs/hosting.md, "DIM links"),
/// so the request is a plain GET with no key and no header; Core builds the address and reads the answer.
/// </summary>
public sealed class DimLoadoutFetching(HttpClient http)
{
    /// <summary>DIM's JSON answer (<c>{ "loadout": … }</c>) for the share at <paramref name="url"/>, or why it couldn't be had.</summary>
    public async Task<Result<string, string>> FetchSharedLoadoutAsync(string url)
    {
        try
        {
            using var response = await http.GetAsync(url);                                       // impure
            var text = await response.Content.ReadAsStringAsync();                               // impure
            return LoopDesigning.ReadDimShareAnswer((int)response.StatusCode, text);            // pure
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new Result<string, string>.Error(LoopDesigning.DescribeDimUnreachable(exception.Message)); // pure
        }
    }
}

using System.Net.Http.Headers;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Web.Hosting;

/// <summary>
/// Impure shell: the player's Bungie profile (their armor and characters), after they log in with Bungie in a window
/// of its own — a public OAuth client, <c>BungieClientId</c> with <c>BungieApiKey</c> in wwwroot/appsettings.json
/// (docs/hosting.md, "Bungie login"). The access token serves the two reads and is dropped: nothing is kept, in the tab
/// or anywhere else. Core builds every URL and reads every answer.
/// </summary>
public sealed class BungieAccount(HttpClient http, BrowserInterop browser, string apiKey, string clientId)
{
    /// <summary>Whether the site is set up for Bungie's login (an API key and a client id).</summary>
    public bool CanLogIn => apiKey.Length > 0 && clientId.Length > 0;

    /// <summary>The player's profile, after their login; or why it couldn't be read (the login refused, Bungie down…).</summary>
    public async Task<Result<BungieProfile, string>> LoadProfileAsync()
    {
        var state = Guid.NewGuid().ToString("N");                                                // impure (random)
        var url = LoopDesigning.ToBungieLoginUrl(clientId, state);                              // pure
        var answer = await browser.LogInWithBungieAsync(url);                                    // impure
        var code = LoopDesigning.ReadBungieCallback(answer, state);                             // pure
        var token = await code.BindAsync(RequestTokenAsync);                                     // impure
        return await token.BindAsync(ReadProfileAsync);                                          // impure
    }

    /// <summary>Stops waiting for the login (the player closed Bungie's window).</summary>
    public ValueTask CancelAsync() =>
        browser.CancelBungieLoginAsync();                                                        // impure

    /// <summary>The login's code traded for an access token.</summary>
    private async Task<Result<string, string>> RequestTokenAsync(string code)
    {
        var form = LoopDesigning.ToBungieTokenForm(clientId, code);                             // pure
        var answer = await PostFormAsync(LoopDesigning.BungieTokenUrl, form);                    // impure
        return answer.Bind(LoopDesigning.ReadBungieAccessToken);                                // pure
    }

    /// <summary>The token's account: its Destiny membership, then that membership's profile.</summary>
    private async Task<Result<BungieProfile, string>> ReadProfileAsync(string accessToken)
    {
        var answer = await GetAsync(LoopDesigning.BungieMembershipsUrl, accessToken);            // impure
        var membership = answer.Bind(LoopDesigning.ReadDestinyMembership);                      // pure
        return await membership.BindAsync(member => ReadMemberProfileAsync(member, accessToken)); // impure
    }

    private async Task<Result<BungieProfile, string>> ReadMemberProfileAsync(DestinyMembership membership, string accessToken)
    {
        var url = LoopDesigning.ToBungieProfileUrl(membership);                                 // pure
        var answer = await GetAsync(url, accessToken);                                           // impure
        return answer.Bind(LoopDesigning.ReadBungieProfile);                                    // pure
    }

    private async Task<Result<string, string>> PostFormAsync(string url, IEnumerable<KeyValuePair<string, string>> form)
    {
        try
        {
            using var content = new FormUrlEncodedContent(form);                                 // pure
            using var response = await http.PostAsync(url, content);                             // impure
            var text = await response.Content.ReadAsStringAsync();                               // impure
            return LoopDesigning.ReadBungieAnswer((int)response.StatusCode, text);              // pure
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new Result<string, string>.Error(LoopDesigning.DescribeBungieUnreachable(exception.Message)); // pure
        }
    }

    private async Task<Result<string, string>> GetAsync(string url, string accessToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);                     // pure
            request.Headers.Add("X-API-Key", apiKey);                                            // pure (the request)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken); // pure (the request)
            using var response = await http.SendAsync(request);                                  // impure
            var text = await response.Content.ReadAsStringAsync();                               // impure
            return LoopDesigning.ReadBungieAnswer((int)response.StatusCode, text);              // pure
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new Result<string, string>.Error(LoopDesigning.DescribeBungieUnreachable(exception.Message)); // pure
        }
    }
}

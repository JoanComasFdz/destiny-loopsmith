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
        if (code is not Result<string, string>.Ok loggedIn)
        {
            return Fail(code);
        }

        var form = LoopDesigning.ToBungieTokenForm(clientId, loggedIn.Value);                   // pure
        var tokenAnswer = await PostFormAsync(LoopDesigning.BungieTokenUrl, form);               // impure
        var token = tokenAnswer.Bind(LoopDesigning.ReadBungieAccessToken);                      // pure
        if (token is not Result<string, string>.Ok access)
        {
            return Fail(token);
        }

        var membershipsAnswer = await GetAsync(LoopDesigning.BungieMembershipsUrl, access.Value); // impure
        var membership = membershipsAnswer.Bind(LoopDesigning.ReadDestinyMembership);           // pure
        if (membership is not Result<DestinyMembership, string>.Ok member)
        {
            return Fail(membership);
        }

        var profileUrl = LoopDesigning.ToBungieProfileUrl(member.Value);                        // pure
        var profileAnswer = await GetAsync(profileUrl, access.Value);                            // impure
        return profileAnswer.Bind(LoopDesigning.ReadBungieProfile);                             // pure
    }

    /// <summary>Stops waiting for the login (the player closed Bungie's window).</summary>
    public ValueTask CancelAsync() =>
        browser.CancelBungieLoginAsync();

    private async Task<Result<string, string>> PostFormAsync(string url, IEnumerable<KeyValuePair<string, string>> form)
    {
        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await http.PostAsync(url, content);
            return await ReadAnswerAsync(response);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return DescribeUnreachable(exception);
        }
    }

    private async Task<Result<string, string>> GetAsync(string url, string accessToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-API-Key", apiKey);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await http.SendAsync(request);
            return await ReadAnswerAsync(response);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return DescribeUnreachable(exception);
        }
    }

    /// <summary>Bungie's answer text (its JSON says what went wrong, even on an error status); a server error that isn't JSON, said as such.</summary>
    private static async Task<Result<string, string>> ReadAnswerAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return (int)response.StatusCode >= 500 && !text.TrimStart().StartsWith('{')
            ? new Result<string, string>.Error($"Bungie didn't answer (HTTP {(int)response.StatusCode}). Try again later.")
            : new Result<string, string>.Ok(text);
    }

    private static Result<string, string> DescribeUnreachable(Exception exception) =>
        new Result<string, string>.Error($"Couldn't reach Bungie ({exception.Message}). Check your connection and try again.");

    /// <summary>A step's failure, carried on as the profile's.</summary>
    private static Result<BungieProfile, string> Fail<TFrom>(Result<TFrom, string> failed) =>
        new Result<BungieProfile, string>.Error(failed.Match(_ => "", error => error.Failure));
}

using System.Collections.Immutable;
using System.Text.Json;
using Loopsmith.Functional;

namespace Loopsmith.Core.StatLoading;

/// <summary>
/// Pure: Bungie's OAuth for a public client (docs/hosting.md, "Bungie login"): the page where the player logs in, what
/// the token request sends, and what the login page's and the token endpoint's answers say. The host opens the page,
/// sends the request and keeps the token in the tab.
/// </summary>
public static class BungieAuthorizing
{
    private const string Host = "https://www.bungie.net";

    /// <summary>Where the login's code becomes an access token (a form POST).</summary>
    public const string TokenUrl = Host + "/platform/app/oauth/token/";

    /// <summary>
    /// Bungie's login page for the app <paramref name="clientId"/>: it comes back to the app's registered redirect URL
    /// with a code and <paramref name="state"/>, which the host checks is the one it sent.
    /// </summary>
    public static string ToAuthorizeUrl(string clientId, string state) =>
        $"{Host}/en/oauth/authorize?client_id={Uri.EscapeDataString(clientId)}&response_type=code&state={Uri.EscapeDataString(state)}";

    /// <summary>The token request's form: a public client sends its id and no secret.</summary>
    public static ImmutableArray<KeyValuePair<string, string>> ToTokenForm(string clientId, string code) =>
        [KeyValuePair.Create("grant_type", "authorization_code"), KeyValuePair.Create("code", code), KeyValuePair.Create("client_id", clientId)];

    /// <summary>
    /// The login page's answer as the host passes it on (<c>{ code, state, error, error_description }</c>, or
    /// <c>{ blocked: true }</c> when the browser didn't open Bungie's window, <c>{ timedOut: true }</c> when nothing came
    /// back, <c>{ cancelled: true }</c> when the player gave up): the code when the state is the one sent; else why there is none.
    /// </summary>
    public static Result<string, string> ReadCallback(string answerJson, string sentState) =>
        BungieJsonReading.ParseDocument(answerJson).Bind(answer => FindRefusal(answer).Match(
            why => Fail(why.Value),
            _ => answer.ReadString("state") == Optional.Some(sentState)
                ? answer.ReadString("code").ToResult(() => "Bungie's login came back without a code. Try again.")
                : Fail("Bungie's answer isn't for this login. Try again.")));

    /// <summary>Why the login gave no code (the window didn't open, nothing came back, the player gave up, Bungie's error), when it gave none.</summary>
    private static Optional<string> FindRefusal(JsonElement answer) =>
        new[]
        {
            ToReason(answer.ReadFlag("blocked", false), "Your browser didn't open Bungie's login window: allow pop-ups for this site and try again."),
            ToReason(answer.ReadFlag("timedOut", false), "Bungie's login didn't come back. Try again."),
            ToReason(answer.ReadFlag("cancelled", false), "Login cancelled."),
            answer.ReadString("error").Map(error => error == "access_denied"
                ? "You didn't allow Loopsmith to read your Destiny 2 information, so it can't load your stats."
                : $"Bungie's login said: {answer.ReadString("error_description").UnwrapOr(error)}."),
        }.FindFirstSome();

    private static Optional<string> ToReason(bool holds, string reason) =>
        holds ? Optional.Some(reason) : Optional.None<string>();

    /// <summary>The token endpoint's answer: its access token, or what Bungie said.</summary>
    public static Result<string, string> ReadAccessToken(string json) =>
        BungieJsonReading.ParseDocument(json).Bind(answer => answer.ReadString("access_token").Match(
            token => new Result<string, string>.Ok(token.Value),
            _ => Fail($"Bungie didn't log you in: {answer.ReadString("error_description").UnwrapOr(answer.ReadString("error").UnwrapOr("no reason given"))}.")));

    private static Result<string, string> Fail(string why) =>
        new Result<string, string>.Error(why);
}

using System.Globalization;
using System.Text.RegularExpressions;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.LoadoutImporting;

/// <summary>
/// Pure: pasted text → the DIM link it is. The two kinds DIM itself opens (its <c>decodeShareUrl</c>): a dim.gg share
/// (as DIM's Share button gives it, or just its id) and a link to DIM's Loadouts page that carries the loadout as JSON.
/// </summary>
public static partial class DimLinkReading
{
    /// <summary>Where DIM answers a share id with its loadout (<c>GET /loadout_share?shareId=…</c>, with an app's API key).</summary>
    public const string ShareApi = "https://api.destinyitemmanager.com/loadout_share";

    private const string ShareHost = "https://dim.gg/";

    public const string NotADimLink =
        "That isn't a DIM link. In DIM, open the loadout, choose Share and copy the dim.gg link; a link to DIM's Loadouts "
        + "page with ?loadout= in it (from D2ArmorPicker or guardian.report) works too.";

    public static Result<DimLink, string> ReadDimLink(string text)
    {
        var trimmed = text.Trim();
        var share = CreateSharePattern().Match(trimmed);
        if (share.Success && DimShareId.TryFrom(share.Groups["id"].Value) is { IsSuccess: true } id)
        {
            var link = trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? trimmed
                : trimmed.StartsWith("dim.gg/", StringComparison.Ordinal) ? "https://" + trimmed
                : ShareHost + trimmed;
            return new Result<DimLink, string>.Ok(new DimLink.Shared(id.ValueObject, link));
        }

        return ReadInlineLoadout(trimmed)
            .Map(loadout => (DimLink)new DimLink.Inline(loadout, trimmed))
            .ToResult(() => NotADimLink);
    }

    /// <summary>
    /// The link of a saved DIM share (<c>builds/&lt;slug&gt;/dim-loadout.json</c>: <c>{ "link": …, "loadout": … }</c>, the
    /// loadout as DIM's share page carries it), so a share ships with the app and opens without asking DIM.
    /// </summary>
    public static Result<DimLink, string> ReadSavedLink(string json) =>
        DimLoadoutParsing.ParseSavedLink(json).Bind(ReadDimLink);

    /// <summary>
    /// The address a host asks for a dim.gg share's loadout. DIM's API hands a share to any caller, from any origin
    /// (its <c>GET /loadout_share</c> needs no API key), so the host asks with a plain GET and no header: a header such
    /// as <c>X-API-Key</c> would make the browser ask DIM first (a CORS preflight), which DIM answers only for the
    /// origins of apps registered with it.
    /// </summary>
    public static string ToShareRequestUrl(DimShareId shareId) =>
        $"{ShareApi}?shareId={Uri.EscapeDataString(shareId.Value)}";

    /// <summary>What to paste when DIM can't be asked for a share: the dim.gg page's long link carries the loadout.</summary>
    private const string LongLinkInstead =
        "Or open the dim.gg link, copy its \"Open Loadout in DIM\" link (long-press or right-click it, then copy the "
        + "link) and paste that here instead: it carries the whole loadout.";

    /// <summary>DIM's answer to the share request (its status and text): the loadout's JSON, or why there is none.</summary>
    public static Result<string, string> ReadShareAnswer(int status, string text) =>
        status is >= 200 and < 300
            ? new Result<string, string>.Ok(text)
            : new Result<string, string>.Error(status == 404
                ? "DIM doesn't know that share: check the link (copy it again from DIM's Share)."
                : $"DIM didn't hand over the shared loadout (HTTP {status.ToString(CultureInfo.InvariantCulture)}). Try again later. {LongLinkInstead}");

    /// <summary>What to say when the browser couldn't reach DIM (<paramref name="why"/>: what it said).</summary>
    public static string DescribeUnreachable(string why) =>
        $"Couldn't reach DIM to read the shared loadout ({why}). Check your connection and try again. {LongLinkInstead}";

    /// <summary>
    /// The <c>loadout</c> query value of an http(s) link whose path ends in <c>/loadouts</c>, decoded as a browser does
    /// (<c>+</c> is a space). The JSON may be percent-encoded or not (guardian.report leaves its braces as they are).
    /// </summary>
    private static Optional<string> ReadInlineLoadout(string link)
    {
        var withoutFragment = link.Split('#')[0];
        var query = withoutFragment.IndexOf('?', StringComparison.Ordinal);
        var path = query < 0 ? withoutFragment : withoutFragment[..query];
        var isLoadoutsPage = (path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            && path.TrimEnd('/').EndsWith("/loadouts", StringComparison.Ordinal);
        return query < 0 || !isLoadoutsPage
            ? Optional.None<string>()
            : withoutFragment[(query + 1)..]
                .Split('&')
                .Select(parameter => parameter.Split('=', 2))
                .Where(pair => pair.Length == 2 && pair[0] == "loadout" && pair[1].Length > 0)
                .Select(pair => Optional.Some(Uri.UnescapeDataString(pair[1].Replace('+', ' '))))
                .FindFirstSome();
    }

    /// <summary>DIM's own pattern: an optional <c>https://dim.gg/</c>, an id of 7+ lowercase letters and digits, anything after a slash.</summary>
    [GeneratedRegex(@"^(?:(?:https?://)?dim\.gg/)?(?<id>[a-z0-9]{7,})(?:/.*)?$")]
    private static partial Regex CreateSharePattern();
}

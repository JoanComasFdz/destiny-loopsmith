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
        var share = SharePattern().Match(trimmed);
        if (share.Success)
        {
            var link = trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? trimmed
                : trimmed.StartsWith("dim.gg/", StringComparison.Ordinal) ? "https://" + trimmed
                : ShareHost + trimmed;
            return new Result<DimLink, string>.Ok(new DimLink.Shared(share.Groups["id"].Value, link));
        }

        return ReadInlineLoadout(trimmed)
            .Map(loadout => (DimLink)new DimLink.Inline(loadout, trimmed))
            .ToResult(() => NotADimLink);
    }

    /// <summary>The address a host asks for a dim.gg share's loadout.</summary>
    public static string ToShareRequestUrl(string shareId) =>
        $"{ShareApi}?shareId={Uri.EscapeDataString(shareId)}";

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
                .DefaultIfEmpty(Optional.None<string>())
                .First();
    }

    /// <summary>DIM's own pattern: an optional <c>https://dim.gg/</c>, an id of 7+ lowercase letters and digits, anything after a slash.</summary>
    [GeneratedRegex(@"^(?:(?:https?://)?dim\.gg/)?(?<id>[a-z0-9]{7,})(?:/.*)?$")]
    private static partial Regex SharePattern();
}

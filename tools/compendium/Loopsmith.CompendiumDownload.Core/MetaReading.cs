using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>The sheet a snapshot holds: its title, id and link, and when it was taken (a date, and a date and time).</summary>
public sealed record SheetMeta(string Title, string Id, string Url, string Extracted, string ExtractedAt);

/// <summary><c>tabs.json</c> and <c>meta.json</c>: written by a download, read back by every build of INDEX, README and the viewer.</summary>
public static partial class MetaReading
{
    public static SheetMeta CreateMeta(string title, string sheetId, DateTime now) =>
        new(title, sheetId, SheetNaming.BuildEditUrl(sheetId), now.ToString("yyyy-MM-dd"), now.ToString("yyyy-MM-dd HH:mm"));

    /// <summary>
    /// The tab names → their gids, one a line. A name twice keeps its first place and its last gid (it was a Python
    /// dict), so a rebuild sees each name once.
    /// </summary>
    public static string WriteTabsJson(ImmutableArray<SheetTab> tabs) =>
        JsonWriting.WriteObject(KeepEachNameOnce(tabs).Select(tab => (tab.Name, tab.Gid)), asciiOnly: false, indented: true);

    public static string WriteMetaJson(SheetMeta meta) =>
        JsonWriting.WriteObject(ListMetaMembers(meta), asciiOnly: true, indented: true);

    /// <summary>The meta's members in the order meta.json holds them, for the viewer's script too.</summary>
    public static ImmutableArray<(string Key, string Value)> ListMetaMembers(SheetMeta meta) =>
        [("title", meta.Title), ("id", meta.Id), ("url", meta.Url), ("extracted", meta.Extracted), ("extracted_at", meta.ExtractedAt)];

    public static Result<ImmutableArray<SheetTab>, string> ReadTabsJson(string json) =>
        ReadStringMembers(json, "tabs.json").Map(members => members.Select(member => new SheetTab(member.Key, member.Value)).ToImmutableArray());

    public static Result<SheetMeta, string> ReadMetaJson(string json) =>
        ReadStringMembers(json, "meta.json").Map(members =>
        {
            var found = members.GroupBy(member => member.Key).ToDictionary(group => group.Key, group => group.Last().Value);
            var extracted = found.GetValueOrDefault("extracted", "");
            return new SheetMeta(
                found.GetValueOrDefault("title", ""), found.GetValueOrDefault("id", ""), found.GetValueOrDefault("url", ""),
                extracted, found.GetValueOrDefault("extracted_at", extracted));
        });

    /// <summary>
    /// The meta of a snapshot older than meta.json: its title and the sheet's id from the tab-list page it kept
    /// (<c>htmlview_raw.html</c>, empty when it is gone), its date from tabs.json's.
    /// </summary>
    public static SheetMeta BuildFallbackMeta(string tabListHtml, string folderName, DateOnly tabsJsonDate)
    {
        var title = CreateTitlePattern().Match(tabListHtml);
        var id = CreateIdPattern().Match(tabListHtml);
        var sheetId = id.Success ? id.Groups[1].Value : "";
        var extracted = tabsJsonDate.ToString("yyyy-MM-dd");
        return new SheetMeta(
            title.Success ? TabListing.TrimGoogleSuffix(DecodeHtml(PythonText.StripWhitespace(title.Groups[1].Value))) : folderName,
            sheetId,
            sheetId.Length > 0 ? SheetNaming.BuildEditUrl(sheetId) : "",
            extracted,
            extracted);
    }

    private static string DecodeHtml(string text) =>
        HtmlReading.ParseHtml($"<p>{text.Replace("<", "&lt;")}</p>").QuerySelector("p")!.TextContent;

    private static IEnumerable<SheetTab> KeepEachNameOnce(ImmutableArray<SheetTab> tabs) =>
        tabs.Select(tab => tab.Name).Distinct().Select(name => new SheetTab(name, tabs.Last(tab => tab.Name == name).Gid));

    /// <summary>A JSON object's members in their order, each value as its text.</summary>
    public static Result<ImmutableArray<(string Key, string Value)>, string> ReadStringMembers(string json, string file)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? new Result<ImmutableArray<(string Key, string Value)>, string>.Ok([.. document.RootElement.EnumerateObject()
                    .Select(member => (member.Name, member.Value.ValueKind == JsonValueKind.String ? member.Value.GetString()! : member.Value.GetRawText()))])
                : new Result<ImmutableArray<(string Key, string Value)>, string>.Error($"{file} is not a JSON object.");
        }
        catch (JsonException exception)
        {
            return new Result<ImmutableArray<(string Key, string Value)>, string>.Error($"{file} is not JSON: {exception.Message}");
        }
    }

    [GeneratedRegex("<title>(.*?)</title>", RegexOptions.Singleline)]
    private static partial Regex CreateTitlePattern();

    [GeneratedRegex(@"spreadsheets\\?/d\\?/([a-zA-Z0-9_-]{20,})")]
    private static partial Regex CreateIdPattern();
}

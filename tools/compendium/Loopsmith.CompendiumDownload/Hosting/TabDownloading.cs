using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Hosting;

/// <summary>Impure: the sheet's tabs downloaded into a snapshot folder: Google's pages, then each tab's <c>.md</c> and <c>.csv</c>.</summary>
public static class TabDownloading
{
    private static readonly TimeSpan PageTimeout = TimeSpan.FromSeconds(120);

    /// <summary>The folder written (the one given, or a new dated one) and its log.</summary>
    public static async Task<Result<(string Folder, SnapshotLog Log), string>> DownloadTabs(
        SheetClient client, string sheet, Optional<string> givenFolder, IReadOnlyList<string> args)
    {
        var now = DateTime.Now;   // impure
        var sheetId = SheetNaming.ReadSheetId(sheet);   // pure
        var tabListUrl = SheetNaming.BuildTabListUrl(sheetId);   // pure
        var listed = await client.GetAsync(tabListUrl, [], PageTimeout);   // impure
        if (listed is Result<SheetAnswer, string>.Error noAnswer)
        {
            return new Result<(string, SnapshotLog), string>.Error($"GET {tabListUrl} failed: {noAnswer.Failure}");
        }

        var tabList = ((Result<SheetAnswer, string>.Ok)listed).Value;
        var refused = TabListing.IsAnswerFailed(tabList.Status);   // pure
        if (refused)
        {
            return new Result<(string, SnapshotLog), string>.Error($"GET {tabListUrl} failed: HTTP {tabList.Status}. Is the sheet public?");
        }

        var page = HtmlReading.ParseHtml(tabList.Text);   // pure
        var title = TabListing.ReadTitle(page, sheetId);   // pure
        var folder = givenFolder is Optional<string>.Some some ? some.Value : SheetNaming.NameDefaultFolder(title, now);   // pure
        var log = SnapshotLog.OpenLog(folder, args);   // impure
        var described = ResponseDescribing.DescribeAnswer(tabList.Facts);   // pure
        log.LogInfo($"GET {tabListUrl} -> {described}");   // impure
        log.Say($"Output folder: {folder}");   // impure
        SnapshotFiles.CreateFolder(folder, "gviz");   // impure
        SnapshotFiles.CreateFolder(folder, "raw");   // impure
        SnapshotFiles.WriteText(folder, "htmlview_raw.html", tabList.Text);   // impure
        var tabs = TabListing.ListTabs(tabList.Text, page);   // pure
        log.Say($"Found {tabs.Length} tabs");   // impure
        var tabsJson = MetaReading.WriteTabsJson(tabs);   // pure
        SnapshotFiles.WriteText(folder, "tabs.json", tabsJson);   // impure
        var meta = MetaReading.CreateMeta(title, sheetId, now);   // pure
        var metaJson = MetaReading.WriteMetaJson(meta);   // pure
        SnapshotFiles.WriteText(folder, "meta.json", metaJson);   // impure

        for (var at = 0; at < tabs.Length; at++)
        {
            var downloaded = await DownloadTab(client, sheetId, folder, log, at + 1, tabs[at]);   // impure
            if (downloaded is Result<Unit, string>.Error failed)
            {
                return new Result<(string, SnapshotLog), string>.Error(failed.Failure);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));   // impure
        }

        return new Result<(string, SnapshotLog), string>.Ok((folder, log));
    }

    private static async Task<Result<Unit, string>> DownloadTab(SheetClient client, string sheetId, string folder, SnapshotLog log, int place, SheetTab tab)
    {
        var stem = SheetNaming.ToStem(place, tab.Name);   // pure
        var quotedName = PythonText.QuoteForConsole(tab.Name);   // pure
        var tabUrl = SheetNaming.BuildTabUrl(sheetId, tab.Gid);   // pure
        var answered = await client.GetAsync(tabUrl, [], PageTimeout);   // impure
        if (answered is Result<SheetAnswer, string>.Error noAnswer)
        {
            return new Result<Unit, string>.Error($"Tab '{tab.Name}': GET {tabUrl} failed: {noAnswer.Failure}");
        }

        var answer = ((Result<SheetAnswer, string>.Ok)answered).Value;
        var described = ResponseDescribing.DescribeAnswer(answer.Facts);   // pure
        log.LogInfo($"tab {quotedName} GET {tabUrl} -> {described}");   // impure
        SnapshotFiles.WriteText(folder, Path.Combine("raw", $"{stem}.html"), answer.Text);   // impure
        var page = HtmlReading.ParseHtml(answer.Text);   // pure
        var table = HtmlReading.FindSheetTable(page);   // pure
        var rows = GridReading.ReadGrid(table);   // pure
        var csv = TabWriting.WriteCsv(rows);   // pure
        SnapshotFiles.WriteText(folder, $"{stem}.csv", csv);   // impure
        var markdown = TabWriting.WriteMarkdown(tab.Name, rows);   // pure
        SnapshotFiles.WriteText(folder, $"{stem}.md", markdown);   // impure

        var gvizUrl = SheetNaming.BuildGvizUrl(sheetId, tab.Gid);   // pure
        var exported = await client.GetAsync(gvizUrl, [], PageTimeout);   // impure
        if (exported is Result<SheetAnswer, string>.Ok gviz)
        {
            var gvizDescribed = ResponseDescribing.DescribeAnswer(gviz.Value.Facts);   // pure
            log.LogInfo($"tab {quotedName} gviz -> {gvizDescribed}");   // impure
            var isCsv = TabListing.IsGvizCsv(gviz.Value.Status, gviz.Value.Text);   // pure
            if (isCsv)
            {
                SnapshotFiles.WriteText(folder, Path.Combine("gviz", $"{stem}.csv"), gviz.Value.Text);   // impure
            }
        }
        else
        {
            log.LogWarning($"tab {quotedName} gviz -> {((Result<SheetAnswer, string>.Error)exported).Failure}");   // impure
        }

        log.Say($"  [{place:00}] {quotedName,-45} gid={tab.Gid,-12} rows={rows.Length}");   // impure
        return new Result<Unit, string>.Ok(new Unit.Value());
    }
}

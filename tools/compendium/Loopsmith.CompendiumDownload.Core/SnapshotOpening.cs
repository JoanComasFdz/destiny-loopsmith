using System.Collections.Immutable;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>
/// What a snapshot folder holds about itself, as read: <c>tabs.json</c>, <c>meta.json</c> and the tab-list page (each
/// none when missing), the folder's name and the date tabs.json was written.
/// </summary>
public sealed record SnapshotFolder(
    Optional<string> TabsJson, Optional<string> MetaJson, Optional<string> TabListHtml, string FolderName, DateOnly TabsJsonDate);

/// <summary>A snapshot folder → its sheet's meta and its tabs, or why it can't be read.</summary>
public static class SnapshotOpening
{
    public static Result<(SheetMeta Meta, ImmutableArray<SheetTab> Tabs), string> OpenSnapshot(SnapshotFolder folder)
    {
        if (folder.TabsJson is not Optional<string>.Some tabsJson)
        {
            return new Result<(SheetMeta, ImmutableArray<SheetTab>), string>.Error($"No tabs.json in {folder.FolderName}.");
        }

        var meta = folder.MetaJson is Optional<string>.Some metaJson
            ? MetaReading.ReadMetaJson(metaJson.Value)
            : new Result<SheetMeta, string>.Ok(MetaReading.BuildFallbackMeta(
                folder.TabListHtml is Optional<string>.Some html ? html.Value : "", folder.FolderName, folder.TabsJsonDate));
        return meta.Bind(found => MetaReading.ReadTabsJson(tabsJson.Value).Map(tabs => (found, tabs)));
    }
}

using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Tests;

/// <summary>A snapshot folder's own files → its sheet's meta and tabs.</summary>
public sealed class SnapshotOpeningTests
{
    [Fact]
    public void A_folder_without_tabs_json_is_not_a_snapshot() =>
        Assert.Equal("No tabs.json in x.", Assert.IsType<Result<(SheetMeta, System.Collections.Immutable.ImmutableArray<SheetTab>), string>.Error>(
            SnapshotOpening.OpenSnapshot(new(Optional.None<string>(), Optional.None<string>(), Optional.None<string>(), "x", new DateOnly(2025, 1, 2)))).Failure);

    [Fact]
    public void Without_meta_json_the_meta_comes_from_the_tab_list_page()
    {
        var opened = SnapshotOpening.OpenSnapshot(new(Optional.Some("{\"Arc\": \"1\"}"), Optional.None<string>(), Optional.Some("<title>Sheet</title>"), "x", new DateOnly(2025, 1, 2)));

        var (meta, tabs) = Assert.IsType<Result<(SheetMeta Meta, System.Collections.Immutable.ImmutableArray<SheetTab> Tabs), string>.Ok>(opened).Value;
        Assert.Equal("Sheet", meta.Title);
        Assert.Equal("2025-01-02", meta.Extracted);
        Assert.Equal([new SheetTab("Arc", "1")], tabs);
    }
}

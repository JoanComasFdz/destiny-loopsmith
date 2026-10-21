using System.Collections.Immutable;
using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Tests;

/// <summary>tabs.json and meta.json, written by a download and read back by each build of INDEX, README and the viewer.</summary>
public sealed class MetaReadingTests
{
    private static readonly SheetMeta Meta = MetaReading.CreateMeta("Café Sheet", "abc", new DateTime(2026, 10, 21, 9, 7, 0));

    [Fact]
    public void The_meta_says_the_sheet_and_when_it_was_taken() =>
        Assert.Equal(new SheetMeta("Café Sheet", "abc", "https://docs.google.com/spreadsheets/d/abc/edit", "2026-10-21", "2026-10-21 09:07"), Meta);

    [Fact]
    public void Meta_json_is_indented_and_ascii_only_and_reads_back()
    {
        var json = MetaReading.WriteMetaJson(Meta);

        Assert.Equal(
            "{\n  \"title\": \"Caf\\u00e9 Sheet\",\n  \"id\": \"abc\",\n  \"url\": \"https://docs.google.com/spreadsheets/d/abc/edit\",\n  \"extracted\": \"2026-10-21\",\n  \"extracted_at\": \"2026-10-21 09:07\"\n}",
            json);
        Assert.Equal(new Result<SheetMeta, string>.Ok(Meta), MetaReading.ReadMetaJson(json));
    }

    [Fact]
    public void A_meta_without_its_time_takes_its_date() =>
        Assert.Equal("2026-01-02", Assert.IsType<Result<SheetMeta, string>.Ok>(MetaReading.ReadMetaJson("""{"title": "T", "extracted": "2026-01-02"}""")).Value.ExtractedAt);

    [Fact]
    public void A_tab_name_twice_keeps_its_first_place_and_its_last_gid()
    {
        var json = MetaReading.WriteTabsJson([new SheetTab("Arc", "1"), new SheetTab("Solar", "2"), new SheetTab("Arc", "3")]);

        Assert.Equal("{\n  \"Arc\": \"3\",\n  \"Solar\": \"2\"\n}", json);
        Assert.Equal([new SheetTab("Arc", "3"), new SheetTab("Solar", "2")], Assert.IsType<Result<ImmutableArray<SheetTab>, string>.Ok>(MetaReading.ReadTabsJson(json)).Value);
    }

    [Fact]
    public void Tabs_json_that_isnt_json_says_so() =>
        Assert.StartsWith("tabs.json is not JSON", Assert.IsType<Result<ImmutableArray<SheetTab>, string>.Error>(MetaReading.ReadTabsJson("{")).Failure);

    [Fact]
    public void An_older_snapshot_takes_its_meta_from_its_tab_list_page()
    {
        var meta = MetaReading.BuildFallbackMeta(
            "<title>Tom &amp; Jerry - Google Sheets</title><script>x='spreadsheets\\/d\\/1WaxvbLx7UoSZaBqdFr1u3'</script>", "folder", new DateOnly(2025, 5, 6));

        Assert.Equal(new SheetMeta("Tom & Jerry", "1WaxvbLx7UoSZaBqdFr1u3", "https://docs.google.com/spreadsheets/d/1WaxvbLx7UoSZaBqdFr1u3/edit", "2025-05-06", "2025-05-06"), meta);
    }

    [Fact]
    public void Without_its_tab_list_page_it_is_named_by_its_folder() =>
        Assert.Equal(new SheetMeta("folder", "", "", "2025-05-06", "2025-05-06"), MetaReading.BuildFallbackMeta("", "folder", new DateOnly(2025, 5, 6)));
}

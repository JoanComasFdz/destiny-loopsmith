using System.Collections.Immutable;
using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Tests;

/// <summary>A snapshot → its README.md: the folder's layout, its current and archived tabs, how to rebuild it.</summary>
public sealed class ReadmeWritingTests
{
    [Theory]
    [InlineData(new int[0], "none")]
    [InlineData(new[] { 4 }, "04")]
    [InlineData(new[] { 1, 2, 3, 5 }, "01–03, 05")]
    [InlineData(new[] { 1, 3, 4, 7, 8, 9 }, "01, 03–04, 07–09")]
    public void Places_are_written_as_runs(int[] places, string written) =>
        Assert.Equal(written, ReadmeWriting.WritePlaces([.. places]));

    [Theory]
    [InlineData("OLD Armor Mods", true)]
    [InlineData("old", true)]
    [InlineData("Olden Days", false)]
    [InlineData("Armor Mods (OLD)", false)]
    public void A_tab_is_archived_when_its_name_starts_with_the_word_OLD(string name, bool archived) =>
        Assert.Equal(archived, ReadmeWriting.IsArchived(name));

    [Fact]
    public void The_readme_names_the_tabs_the_tokens_and_how_to_rebuild()
    {
        var meta = new SheetMeta("Compendium", "abc", "https://docs.google.com/spreadsheets/d/abc/edit", "2026-10-21", "2026-10-21 09:07");
        ImmutableArray<SnapshotTab> tabs =
        [
            new(1, new SheetTab("Arc", "1"), Optional.Some(6_000L), Optional.None<string>(), Optional.None<string>()),
            new(2, new SheetTab("Solar", "2"), Optional.Some(4_000L), Optional.None<string>(), Optional.None<string>()),
            new(3, new SheetTab("OLD Void", "3"), Optional.None<long>(), Optional.None<string>(), Optional.None<string>()),
        ];

        var readme = ReadmeWriting.WriteReadme(meta, tabs, "compendium-2026-10-21");

        Assert.StartsWith("# Compendium — offline copy (2026-10-21 09:07)\n\nSnapshot of the public Google Sheet https://docs.google.com/spreadsheets/d/abc/edit, taken 2026-10-21 09:07.\nIt has 3 tabs.", readme);
        Assert.Contains("(~3k tokens in total)", readme);
        Assert.Contains("- Current tabs: 01–02.\n- Archived tabs (name starts with OLD, outdated values): 03.\n", readme);
        Assert.Contains("-- --rebuild <path to compendium-2026-10-21> --no-images`", readme);
        Assert.EndsWith("-- \"https://docs.google.com/spreadsheets/d/abc/edit\"`\n", readme);
    }
}

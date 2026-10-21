using Loopsmith.CompendiumDownload.Core;

namespace Loopsmith.CompendiumDownload.Tests;

/// <summary>The sheet's id and pages, and the names a snapshot's files and folder take.</summary>
public sealed class SheetNamingTests
{
    [Theory]
    [InlineData("https://docs.google.com/spreadsheets/d/1Wax_b-9/edit#gid=0", "1Wax_b-9")]
    [InlineData("1Wax_b-9", "1Wax_b-9")]
    public void The_id_is_read_from_a_URL_or_taken_as_given(string sheet, string id) =>
        Assert.Equal(id, SheetNaming.ReadSheetId(sheet));

    [Fact]
    public void A_tabs_pages_are_its_HTML_view_and_its_CSV_export()
    {
        Assert.Equal("https://docs.google.com/spreadsheets/d/abc/htmlview/sheet?headers=false&gid=-12", SheetNaming.BuildTabUrl("abc", "-12"));
        Assert.Equal("https://docs.google.com/spreadsheets/d/abc/gviz/tq?tqx=out%3Acsv&gid=7", SheetNaming.BuildGvizUrl("abc", "7"));
    }

    [Theory]
    [InlineData("Class Abilities", "13_Class_Abilities")]
    [InlineData("OLD Armor Mods (2024)!", "13_OLD_Armor_Mods_2024")]
    [InlineData("  Café ²-Notes ", "13_Café_²-Notes")]
    [InlineData("???", "13_tab")]
    public void A_tabs_stem_is_its_place_then_its_name_made_safe(string name, string stem) =>
        Assert.Equal(stem, SheetNaming.ToStem(13, name));

    [Fact]
    public void A_name_keeps_its_first_60_characters() =>
        Assert.Equal(new string('a', 60), SheetNaming.ToSlug(new string('a', 70)));

    [Fact]
    public void The_default_folder_is_the_titles_slug_lowercased_then_the_time() =>
        Assert.Equal("destiny_data_compendium_2026-10-21_0907", SheetNaming.NameDefaultFolder("Destiny Data Compendium", new DateTime(2026, 10, 21, 9, 7, 30)));
}

using Loopsmith.CompendiumDownload.Core;

namespace Loopsmith.CompendiumDownload.Tests;

/// <summary>The tab-list page → the sheet's title and its tabs, in the three shapes Google has served it.</summary>
public sealed class TabListingTests
{
    [Fact]
    public void Todays_page_lists_its_tabs_in_its_script_with_their_escapes_read()
    {
        const string html = """
            <html><head><title>My Sheet - Google Sheets</title></head><body><script>
            items.push({name: "Arc", pageUrl: "x", gid: "618967225", initialSheet: true});
            items.push({name: "Weapons \x26 \u00e9 \"Perks\"", pageUrl: "y", gid: "-5"});
            </script></body></html>
            """;

        var page = HtmlReading.ParseHtml(html);

        Assert.Equal([new SheetTab("Arc", "618967225"), new SheetTab("Weapons & é \"Perks\"", "-5")], TabListing.ListTabs(html, page));
        Assert.Equal("My Sheet", TabListing.ReadTitle(page, "abc"));
    }

    [Fact]
    public void An_older_page_lists_its_tabs_in_its_menu()
    {
        const string html = """<ul id="sheet-menu"><li id="sheet-button-12"> <a>Solar </a></li><li id="other"></li><li id="sheet-button-34">Void</li></ul>""";

        Assert.Equal([new SheetTab("Solar", "12"), new SheetTab("Void", "34")], TabListing.ListTabs(html, HtmlReading.ParseHtml(html)));
    }

    [Fact]
    public void Else_each_sheet_it_switches_to_is_a_tab_named_by_its_gid()
    {
        const string html = "<a onclick=\"switchToSheet('7')\"></a><a onclick=\"switchToSheet('9')\"></a><a onclick=\"switchToSheet('7')\"></a>";

        Assert.Equal([new SheetTab("gid_7", "7"), new SheetTab("gid_9", "9")], TabListing.ListTabs(html, HtmlReading.ParseHtml(html)));
    }

    [Fact]
    public void A_page_without_a_title_is_named_by_the_sheets_id() =>
        Assert.Equal("abc", TabListing.ReadTitle(HtmlReading.ParseHtml("<p>x</p>"), "abc"));

    [Theory]
    [InlineData(200, "a,b\n1,2", true)]
    [InlineData(200, "  <html>sign in</html>", false)]
    [InlineData(404, "a,b", false)]
    public void A_csv_export_is_one_that_succeeded_and_isnt_a_page(int status, string text, bool isCsv) =>
        Assert.Equal(isCsv, TabListing.IsGvizCsv(status, text));
}

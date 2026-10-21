using System.Collections.Immutable;
using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Tests;

/// <summary>A snapshot → its offline viewer: each tab's table, its styles renamed per tab, its images and links rewritten.</summary>
public sealed class ViewerWritingTests
{
    private static readonly SheetMeta Meta = new("Tom & Jerry", "abc", "https://docs.google.com/spreadsheets/d/abc/edit", "2026-10-21", "2026-10-21 09:07");

    private const string Raw = """
        <html><head><style>.ritz .waffle .s0{font-family:docs-Roboto,Arial;}.ritz .waffle .s12{font-family:docs-Lexend Deca;}.other{}</style></head>
        <body><table class="waffle"><tr><td class="s0 softmerge">a<script>x()</script></td>
        <td class="s12"><img src="https://img/saved"><img src="https://img/remote"><img src="https://img/gone"></td>
        <td><a href="https://www.google.com/url?q=https://example.com/a%3Fb%3D1+2&amp;sa=D" target="_self">out</a><a href="#gid=5" target="_blank">in</a></td>
        </tr></table></body></html>
        """;

    [Fact]
    public void A_tabs_cell_styles_are_renamed_for_it_and_Googles_fonts_named_plainly() =>
        Assert.Equal(
            [".ritz .waffle .t3s0{font-family:Roboto,Arial;}", ".ritz .waffle .t3s12{font-family:\"Lexend Deca\";}"],
            ViewerWriting.ReadCellStyles(".ritz .waffle .s0{font-family:docs-Roboto,Arial;}.ritz .waffle .s12{font-family:docs-Lexend Deca;}.other{}", 3));

    [Fact]
    public void The_page_holds_each_tab_as_a_template_with_its_table_rewritten()
    {
        var page = WriteViewer([new(3, new SheetTab("Arc", "618"), Optional.None<long>(), Optional.None<string>(), Optional.Some(Raw))]);

        var start = page.IndexOf("<template id=\"tpl-618\">", StringComparison.Ordinal);
        var template = page[start..page.IndexOf("</template>", start, StringComparison.Ordinal)];
        Assert.Contains("class=\"t3s0 softmerge\"", template);
        Assert.DoesNotContain("x()", template);
        Assert.Contains("<img src=\"images/0123.png\" loading=\"lazy\">", template);
        Assert.Contains("<img src=\"https://img/remote\" referrerpolicy=\"no-referrer\" loading=\"lazy\">", template);
        Assert.DoesNotContain("https://img/gone", template);
        Assert.Contains("<a href=\"https://example.com/a?b=1 2\" target=\"_blank\" rel=\"noopener noreferrer\">out</a>", template);
        Assert.Contains("<a href=\"#gid=5\">in</a>", template);
        Assert.Contains(".ritz .waffle .t3s0{font-family:Roboto,Arial;}", page);
    }

    [Fact]
    public void The_page_says_its_title_tabs_and_meta()
    {
        var page = WriteViewer([new(1, new SheetTab("Arc", "618"), Optional.None<long>(), Optional.None<string>(), Optional.None<string>())]);

        Assert.Contains("<title>Tom &amp; Jerry (2026-10-21 09:07)</title>", page);
        Assert.Contains("const TABS = [{\"gid\": \"618\", \"name\": \"Arc\", \"file\": \"01_Arc.md\"}];", page);
        Assert.Contains("const META = {\"title\": \"Tom & Jerry\", \"id\": \"abc\",", page);
        Assert.Contains("<template id=\"tpl-618\"><p class=\"missing\">This tab could not be loaded.</p></template>", page);
        Assert.EndsWith("</body></html>\n", page);
    }

    [Theory]
    [InlineData("https://www.google.com/url?sa=D&q=https%3A%2F%2Fd2.com%2Fx&ust=1", "https://d2.com/x")]
    [InlineData("https://www.google.com/url?q=&sa=D", "https://www.google.com/url?q=&sa=D")]
    [InlineData("https://example.com/url?q=x", "https://example.com/url?q=x")]
    public void A_link_through_Googles_redirect_goes_straight_to_its_target(string href, string target) =>
        Assert.Equal(target, ViewerWriting.UnwrapGoogleUrl(href));

    private static string WriteViewer(ImmutableArray<SnapshotTab> tabs) =>
        ViewerWriting.WriteViewer(Meta, tabs, new Dictionary<string, string> { ["https://img/saved"] = "0123.png" }, new HashSet<string> { "https://img/gone" });
}

using Loopsmith.CompendiumDownload.Core;

namespace Loopsmith.CompendiumDownload.Tests;

/// <summary>The string operations done Python's way, so a snapshot comes out as the Python version wrote it.</summary>
public sealed class PythonTextTests
{
    [Theory]
    [InlineData(0, "0.0")]
    [InlineData(12_345, "3.1")]
    [InlineData(200, "0.1")]   // 0.05 as a double is a little more than 0.05
    // 0.15 and 0.35 as doubles are a little less: Python rounds the double, not the decimal, so they go down
    [InlineData(600, "0.1")]
    [InlineData(1_000, "0.2")]
    [InlineData(1_400, "0.3")]
    [InlineData(4_000_000, "1000.0")]
    public void Bytes_become_thousands_of_tokens_to_a_tenth_as_Pythons_round_gives(long bytes, string tokens) =>
        Assert.Equal(tokens, PythonText.FormatThousandsToTenths(bytes));

    [Theory]
    [InlineData(2_000, "0")]
    [InlineData(6_000, "2")]
    [InlineData(10_000, "2")]
    [InlineData(10_001, "3")]
    [InlineData(3_999_999, "1000")]
    public void To_a_whole_number_a_tie_goes_to_the_even_one(long bytes, string tokens) =>
        Assert.Equal(tokens, PythonText.FormatThousandsToWhole(bytes));

    [Fact]
    public void Stripping_also_trims_the_separators_Python_counts_as_space() =>
        Assert.Equal("a b", PythonText.StripWhitespace("\u001f  a b　\n"));

    [Fact]
    public void Taking_characters_counts_code_points() =>
        Assert.Equal("\U0001F600a", PythonText.TakeCharacters("\U0001F600ab", 2));

    [Fact]
    public void Html_escaping_escapes_both_quotes() =>
        Assert.Equal("&lt;a href=&quot;x&quot;&gt;Tom &amp; Jerry&#x27;s", PythonText.EscapeHtml("<a href=\"x\">Tom & Jerry's"));

    [Theory]
    [InlineData("Arc", "'Arc'")]
    [InlineData("Titan's", "\"Titan's\"")]
    public void A_name_is_quoted_for_the_console_as_Python_quotes_it(string name, string quoted) =>
        Assert.Equal(quoted, PythonText.QuoteForConsole(name));
}

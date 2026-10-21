using Loopsmith.CompendiumDownload.Core;

namespace Loopsmith.CompendiumDownload.Tests;

/// <summary>Trimming and cutting a snapshot's text by Unicode's rules.</summary>
public sealed class TextTrimmingTests
{
    [Fact]
    public void Trimming_also_trims_the_information_separators() =>
        Assert.Equal("a b", TextTrimming.TrimWhitespace("\u001f  a b　\n"));

    [Fact]
    public void Taking_characters_counts_code_points() =>
        Assert.Equal("\U0001F600a", TextTrimming.TakeCharacters("\U0001F600ab", 2));

    [Fact]
    public void Counting_characters_counts_code_points() =>
        Assert.Equal(2, TextTrimming.CountCharacters("\U0001F600a"));
}

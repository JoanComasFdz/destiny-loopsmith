using Loopsmith.ManifestTools.Core;

namespace Loopsmith.ManifestTools.Tests;

/// <summary>Bungie's text as the excerpt writes it: quoted, ASCII-escaped like a JSON string, without the icon font's glyphs.</summary>
public sealed class TextWritingTests
{
    [Fact]
    public void A_plain_text_is_quoted() => Assert.Equal("\"Hand Cannon\"", TextWriting.ToText("Hand Cannon"));

    [Fact]
    public void The_icon_fonts_glyphs_go_and_the_spaces_they_leave_become_one() =>
        Assert.Equal("\"Grenade kills grant Bolt Charge.\"", TextWriting.ToText("   Grenade  kills   grant Bolt Charge. \u001c"));

    [Fact]
    public void Quotes_backslashes_and_control_characters_are_escaped() =>
        Assert.Equal("\"say \\\"hi\\\"\\\\\\n\\t\\r\\b\\f\\u0001\\u007f\"", TextWriting.QuoteText("say \"hi\"\\\n\t\r\b\f\u0001\u007f"));

    [Fact]
    public void A_character_past_ASCII_is_escaped_in_lowercase_hex_and_one_past_U_FFFF_as_its_two_halves() =>
        Assert.Equal("\"Caf\\u00e9 \\u2014 \\ud83d\\ude00\"", TextWriting.QuoteText("Café — 😀"));

    [Fact]
    public void A_newline_inside_stays_a_newline_and_only_runs_of_spaces_shrink() =>
        Assert.Equal("\"one\\n two\"", TextWriting.ToText("one\n  two"));

    [Fact]
    public void A_breaker_is_compared_by_its_name_without_hyphens_or_case() =>
        Assert.Equal("shield piercing", TextWriting.ToBreakerName(" Shield-Piercing "));
}

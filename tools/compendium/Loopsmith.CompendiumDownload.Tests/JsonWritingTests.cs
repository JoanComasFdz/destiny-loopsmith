using Loopsmith.CompendiumDownload.Core;

namespace Loopsmith.CompendiumDownload.Tests;

/// <summary>JSON as tabs.json, meta.json and map.json hold it.</summary>
public sealed class JsonWritingTests
{
    [Fact]
    public void Indented_it_is_one_member_a_line() =>
        Assert.Equal("{\n  \"a\": \"1\",\n  \"b\": \"2\"\n}", JsonWriting.WriteObject([("a", "1"), ("b", "2")], asciiOnly: false, indented: true));

    [Fact]
    public void On_one_line_its_members_are_split_by_a_comma_and_a_space() =>
        Assert.Equal("{\"a\": \"1\", \"b\": \"2\"}", JsonWriting.WriteObject([("a", "1"), ("b", "2")], asciiOnly: true, indented: false));

    [Fact]
    public void Nothing_is_two_braces() =>
        Assert.Equal("{}", JsonWriting.WriteObject([], asciiOnly: false, indented: true));

    [Fact]
    public void Ascii_only_escapes_every_character_past_tilde_in_lowercase_hex()
    {
        var text = "Caf\u00e9 \u007f \U0001F600";

        Assert.Equal("\"Caf\\u00e9 \\u007f \\ud83d\\ude00\"", JsonWriting.WriteString(text, asciiOnly: true));
        Assert.Equal($"\"{text}\"", JsonWriting.WriteString(text, asciiOnly: false));
    }

    [Fact]
    public void Quotes_backslashes_and_control_characters_are_always_escaped() =>
        Assert.Equal("\"\\\" \\\\ \\n \\t \\u0001\"", JsonWriting.WriteString("\" \\ \n \t \u0001", asciiOnly: false));

    [Fact]
    public void An_array_of_objects_is_on_one_line() =>
        Assert.Equal("[{\"gid\": \"1\"}, {\"gid\": \"2\"}]", JsonWriting.WriteArrayOfObjects([[("gid", "1")], [("gid", "2")]], asciiOnly: false));
}

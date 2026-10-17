using Loopsmith.Core.CompendiumReading;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.CompendiumReading;

/// <summary>The alias table's format, and that the repository's <c>tools/compendium/aliases.yaml</c> reads.</summary>
public sealed class AliasParsingTests
{
    [Fact]
    public void The_alias_table_reads_its_six_lists()
    {
        var aliases = Parse("""
            prefixes: [ { tab: Arc, prefix: "Spark of", why: fragments } ]
            suffixes: [ { tab: Weapon Perks, suffix: "(enhanced)", why: shared row } ]
            dropWords: [ { tab: Armor Mods, words: [Arc, Void], why: one row } ]
            names: [ { element: jolt, name: Jolted, why: adjective } ]
            worded: [ { element: healthy-finisher, value: "100%", why: in words } ]
            alternativeBrackets: [ { tab: Arc, section: Grenade Abilities, why: its note } ]
            """).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Equal("Spark of", aliases.Prefixes.Single().Text);
        Assert.Equal("(enhanced)", aliases.Suffixes.Single().Text);
        Assert.Equal(["Arc", "Void"], aliases.DroppedWords.Single().Words);
        Assert.Equal(new EntryName(ElementId.From("jolt"), "Jolted", "adjective"), aliases.Names.Single());
        Assert.Equal(1m, aliases.Worded.Single().Value);
        Assert.Equal(new AlternativeBrackets("Arc", "Grenade Abilities", "its note"), aliases.AlternativeBrackets.Single());
    }

    [Theory]
    [InlineData("names: [ { element: jolt, name: Jolted } ]", "aliases.yaml:1: names: needs element (an element id), name and why")]
    [InlineData("worded: [ { element: jolt, value: lots, why: x } ]", "aliases.yaml:1: worded: needs element (an element id), value (\"100%\", \"1\") and why")]
    [InlineData("synonyms: []", "aliases.yaml: unknown list 'synonyms'")]
    [InlineData("names: { jolt: Jolted }", "aliases.yaml: 'names' must be a list")]
    public void A_wrong_entry_is_an_error_naming_the_file_the_line_and_the_list(string yaml, string expected)
    {
        Assert.Equal(expected, Assert.IsType<Result<CompendiumAliases, string>.Error>(Parse(yaml)).Failure);
    }

    [Fact]
    public void The_repository_alias_table_reads()
    {
        var path = RepoFiles.ToPath("tools/compendium/aliases.yaml");                               // pure
        var file = FileSourceFetching.ReadTextFile(path);                                           // impure

        var aliases = file.Bind(AliasParsing.ParseAliases);                                         // pure
        Assert.IsType<Result<CompendiumAliases, string>.Ok>(aliases);
    }

    private static Result<CompendiumAliases, string> Parse(string yaml) =>
        AliasParsing.ParseAliases(new SourceText("aliases.yaml", yaml));
}

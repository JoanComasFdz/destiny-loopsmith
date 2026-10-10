using System.Collections.Immutable;
using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.LoopFiles;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.LoopFiles;

/// <summary>Writing build files (a build from a DIM link), and the round trip: the written file reads back as the same build.</summary>
public sealed class BuildFileWritingTests
{
    [Theory]
    [InlineData("builds/skip-grenade-hunter/build.yaml")]
    [InlineData("builds/skip-grenade-hunter-ascension/build.yaml")]
    public void A_bundled_build_written_out_reads_back_the_same(string path)
    {
        var build = ParseBuild(new SourceText(path, File.ReadAllText(RepoFiles.ToPath(path))));

        var written = BuildFileWriting.WriteBuildFile(build, []);

        Assert.Equal(written, BuildFileWriting.WriteBuildFile(ParseBuild(new SourceText("written.yaml", written)), []));
    }

    [Fact]
    public void Unknown_abilities_left_out_hashes_and_notes_are_written_as_the_format_says()
    {
        var build = new Build(
            "Grenadier: \"skip\" #1",
            Optional.None<string>(),
            Optional.Some("https://app.destinyitemmanager.com/loadouts?loadout={\"name\":\"A\"}"),
            Optional.None<CatalogVersion>(),
            GuardianClass.Hunter,
            Subclass.Arc,
            new AbilityLoadout(Optional.None<ElementId>(), Optional.None<ElementId>(), Optional.None<ElementId>(), Optional.Some(ElementId.From("gamblers-dodge"))),
            [ElementId.From("tempest-strike")],
            [],
            Optional.None<ElementId>(),
            [],
            [ElementId.From("bomber"), ElementId.From("bomber")],
            [],
            [],
            new StatLine(Optional.None<StatValue>(), Optional.None<StatValue>(), Optional.None<StatValue>(), Optional.None<StatValue>(), Optional.None<StatValue>(), Optional.None<StatValue>()),
            [new LeftOutItem(LoadoutPart.Item, ItemHash.From(2005u)), new LeftOutItem(LoadoutPart.ArmorMod, ItemHash.From(3007u)), new LeftOutItem(LoadoutPart.Item, ItemHash.From(2006u))]);

        var written = BuildFileWriting.WriteBuildFile(build, ["From a DIM link.\nSecond line."]);
        var read = ParseBuild(new SourceText("written.yaml", written));

        Assert.StartsWith("# From a DIM link.\n# Second line.\n", written, StringComparison.Ordinal);
        Assert.Contains("super: \"?\"\n", written, StringComparison.Ordinal);
        Assert.Contains("leftOut: {items: [2005, 2006], armorMods: [3007]}\n", written, StringComparison.Ordinal);
        Assert.Equal(build.Name, read.Name);
        Assert.Equal(build.SourceUrl, read.SourceUrl);
        Assert.Equal(build.Abilities, read.Abilities);
        Assert.Equal(["bomber", "bomber"], read.ArmorMods.Select(id => id.Value));
        Assert.Equal(
            [(LoadoutPart.Item, 2005u), (LoadoutPart.Item, 2006u), (LoadoutPart.ArmorMod, 3007u)],
            read.LeftOut.Select(item => (item.Part, item.Hash.Value)));
    }

    private static Build ParseBuild(SourceText file) =>
        BuildFileParsing.ParseBuildFile(file).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}

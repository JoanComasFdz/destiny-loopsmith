using Loopsmith.Core.CompendiumReading;
using Loopsmith.Core.Functional;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.CompendiumReading;

/// <summary>
/// The Compendium check (requirements FR-11) against a downloaded snapshot: every rule citing a tab this slice reads finds
/// its element in the cited row and every number it takes from its source among the row's. The snapshot is private, never
/// in the repository or CI, so the check runs only when <c>LOOPSMITH_COMPENDIUM_DIR</c> names one
/// (<c>tools/compendium/README.md</c>, "Checking the rules against a snapshot").
/// </summary>
public sealed class CompendiumCheckTests
{
    [Fact]
    public void Every_rule_citing_a_read_tab_finds_its_element_and_its_numbers_in_the_snapshot()
    {
        var directory = Environment.GetEnvironmentVariable("LOOPSMITH_COMPENDIUM_DIR");           // impure
        Assert.SkipWhen(string.IsNullOrEmpty(directory), "Set LOOPSMITH_COMPENDIUM_DIR to a downloaded Compendium snapshot (tools/compendium/README.md) to run it.");
        var aliasesPath = RepoFiles.ToPath("tools/compendium/aliases.yaml");                     // pure
        var files = FileSourceFetching.ReadCompendiumSnapshot(directory!);                          // impure
        var aliasFile = FileSourceFetching.ReadTextFile(aliasesPath);                               // impure

        var aliases = aliasFile.Bind(AliasParsing.ParseAliases).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));   // pure
        var snapshot = SnapshotReading.ReadSnapshot(files.Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure)), aliases.AlternativeBrackets);   // pure
        var check = CompendiumChecking.CheckCatalog(RepoCatalog.Catalog, snapshot, aliases);       // pure
        var description = CompendiumChecking.DescribeCheck(check);                                  // pure
        TestContext.Current.TestOutputHelper?.WriteLine(description);                               // impure

        Assert.NotEmpty(snapshot.Entries);
        Assert.True(check.Problems.IsEmpty, description);
    }
}

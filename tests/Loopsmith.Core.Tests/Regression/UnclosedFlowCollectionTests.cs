using System.Collections.Immutable;
using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.RuleParsing;

namespace Loopsmith.Core.Tests.Regression;

/// <summary>
/// YamlDotNet throws InvalidOperationException (not YamlException) on an unclosed flow collection followed by a
/// mapping line. Builds and rules can come from users (the web app imports build files), so this must be a
/// located error, never a crash.
/// </summary>
public class UnclosedFlowCollectionTests
{
    private const string Unclosed = "name: X\nweapons: [\nstats: x\n";

    [Fact]
    public void Build_file_with_an_unclosed_flow_collection_is_an_error()
    {
        var result = BuildFileParsing.ParseBuildFile(new SourceText("bad/build.yaml", Unclosed));

        var error = Assert.IsType<Result<Build, string>.Error>(result);
        Assert.Contains("bad/build.yaml", error.Failure);
    }

    [Fact]
    public void Rule_file_with_an_unclosed_flow_collection_is_an_error()
    {
        var files = ImmutableArray.Create(
            new SourceText("glossary.yaml", "statuses: [\npickups: x\n"),
            new SourceText("keywords/x.yaml", "elements: [\nother: y\n"));

        var result = RuleCatalogParsing.ParseCatalog(files);

        var error = Assert.IsType<Result<RuleCatalog, string>.Error>(result);
        Assert.Contains("glossary.yaml", error.Failure);
    }
}

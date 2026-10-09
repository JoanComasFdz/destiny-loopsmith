// TEMPORARY: replaced by the real RuleParsing / BuildParsing slices at merge time.
using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.RuleParsing
{
    public static class RuleCatalogParsing
    {
        public static Result<RuleCatalog, string> ParseCatalog(ImmutableArray<SourceText> files) =>
            new Result<RuleCatalog, string>.Error($"RuleParsing not merged yet ({files.Length} files).");
    }
}

namespace Loopsmith.Core.BuildParsing
{
    public static class BuildFileParsing
    {
        public static Result<Build, string> ParseBuildFile(SourceText file) =>
            new Result<Build, string>.Error($"BuildParsing not merged yet ({file.Path}).");
    }
}

using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.RuleParsing;
using Loopsmith.Core.SourceFetching;

namespace Loopsmith.Core.Orchestration;

/// <summary>Read (impure) → parse + validate on a railway (pure). Shared by every shell.</summary>
public static class BuildLoading
{
    public static Result<ValidatedBuild, string> LoadValidatedBuild(LoadRequest request)
    {
        var rulesDirectory = LocateRulesDirectory(request);                                           // impure
        var ruleFiles = rulesDirectory.Bind(FileSourceFetching.ReadRuleFiles);                         // impure
        var buildFile = FileSourceFetching.ReadTextFile(request.BuildPath);                           // impure

        var validated = ComposeValidatedBuild(ruleFiles, buildFile);                                  // pure
        return validated;
    }

    public static Result<ValidatedBuild, string> ComposeValidatedBuild(
        Result<ImmutableArray<SourceText>, string> ruleFiles,
        Result<SourceText, string> buildFile) =>
        ruleFiles
            .Bind(RuleCatalogParsing.ParseCatalog)
            .Bind(catalog => buildFile
                .Bind(BuildFileParsing.ParseBuildFile)
                .Bind(build => BuildValidation.ValidateBuild(build, catalog)));

    private static Result<string, string> LocateRulesDirectory(LoadRequest request)
    {
        var found = request.RulesDirectory.IsSome()
            ? request.RulesDirectory
            : FileSourceFetching.FindRulesDirectoryFor(request.BuildPath);
        return found.ToResult(() => "No rules directory found (a 'rules/' folder with glossary.yaml above the build or the current directory). Use --rules <dir>.");
    }
}

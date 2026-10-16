using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;

namespace Loopsmith.Core.Tests.Support;

/// <summary>The repository's rules (<c>rules/</c>), parsed once: their manifest excerpt is what a build file's weapons given by hash are read from.</summary>
public static class RepoCatalog
{
    public static readonly RuleCatalog Catalog = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))   // impure
        .Bind(CatalogLoading.ParseCatalog)
        .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    public static ManifestExcerpt Manifest => Catalog.Manifest;
}

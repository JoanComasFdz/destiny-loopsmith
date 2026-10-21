using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Web.Hosting;

/// <summary>Everything the app needs after startup: the rule catalog, parsed once.</summary>
public sealed record LoopsmithBundle(RuleCatalog Catalog);

/// <summary>Pure: the embedded rule files → the catalog (parsed once).</summary>
public static class BundleComposing
{
    private const string RulesPrefix = "rules/";

    public static Result<LoopsmithBundle, string> ComposeBundle(ImmutableArray<SourceText> files)
    {
        var ruleFiles = files
            .Where(file => file.Path.StartsWith(RulesPrefix, StringComparison.Ordinal))
            .Select(file => file with { Path = file.Path[RulesPrefix.Length..] })
            .ToImmutableArray();
        return CatalogLoading.ParseCatalog(ruleFiles)
            .MapError(error => $"The bundled rules do not parse:{Environment.NewLine}{error}")
            .Map(catalog => new LoopsmithBundle(catalog));
    }

    /// <summary>A design for a build file the user brought: parsed and validated against the catalog, then named.</summary>
    public static Result<DesignSession, string> StartNamedDesign(RuleCatalog catalog, SourceText buildFile) =>
        LoopDesigning.StartDesign(catalog, buildFile, "New loop").Map(NameFreshDesign);

    /// <summary>The name a new design starts with: "&lt;build name&gt; loop".</summary>
    private static DesignSession NameFreshDesign(DesignSession session) =>
        LoopDesigning.RenameDesign(session, $"{session.Build.Build.Name} loop", session.Design.Author, session.Design.Description);
}

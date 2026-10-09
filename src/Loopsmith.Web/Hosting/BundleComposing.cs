using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Web.Hosting;

/// <summary>A build shipped with the app, and the fresh design session it starts (or why it cannot).</summary>
public sealed record BundledBuild(string Slug, SourceText File, Result<DesignSession, string> Fresh);

/// <summary>A loop file shipped with the app (<c>builds/&lt;slug&gt;/loops/*.loop.yaml</c>), imported once at startup.</summary>
public sealed record BundledLoop(string Slug, SourceText File, Result<DesignSession, string> Opened);

/// <summary>Everything the app needs after startup: the parsed catalog once, the bundled builds and loops.</summary>
public sealed record LoopsmithBundle(RuleCatalog Catalog, ImmutableArray<BundledBuild> Builds, ImmutableArray<BundledLoop> Loops);

/// <summary>Pure: the embedded files → the catalog (parsed once) plus the bundled builds and loops.</summary>
public static class BundleComposing
{
    private const string RulesPrefix = "rules/";
    private const string LoopSuffix = ".loop.yaml";

    public static Result<LoopsmithBundle, string> ComposeBundle(ImmutableArray<SourceText> files)
    {
        var ruleFiles = files
            .Where(file => file.Path.StartsWith(RulesPrefix, StringComparison.Ordinal))
            .Select(file => file with { Path = file.Path[RulesPrefix.Length..] })
            .ToImmutableArray();
        return CatalogLoading.ParseCatalog(ruleFiles)
            .MapError(error => $"The bundled rules do not parse:{Environment.NewLine}{error}")
            .Map(catalog => new LoopsmithBundle(catalog, ListBuilds(catalog, files), ListLoops(catalog, files)));
    }

    /// <summary>A design for a build file the user brought: parsed and validated against the catalog, then named.</summary>
    public static Result<DesignSession, string> StartNamedDesign(RuleCatalog catalog, SourceText buildFile) =>
        LoopDesigning.StartDesign(catalog, buildFile, "New loop").Map(NameFreshDesign);

    /// <summary>The name a new design starts with: "&lt;build name&gt; loop".</summary>
    public static DesignSession NameFreshDesign(DesignSession session) =>
        LoopDesigning.RenameDesign(session, $"{session.Build.Build.Name} loop", session.Design.Author, session.Design.Description);

    private static ImmutableArray<BundledBuild> ListBuilds(RuleCatalog catalog, ImmutableArray<SourceText> files) =>
    [
        .. from file in files
           let parts = file.Path.Split('/')
           where parts is ["builds", _, "build.yaml"]
           select new BundledBuild(parts[1], file, StartNamedDesign(catalog, file)),
    ];

    private static ImmutableArray<BundledLoop> ListLoops(RuleCatalog catalog, ImmutableArray<SourceText> files) =>
    [
        .. from file in files
           let parts = file.Path.Split('/')
           where parts is ["builds", _, "loops", _] && parts[3].EndsWith(LoopSuffix, StringComparison.Ordinal)
           select new BundledLoop(parts[1], file, LoopDesigning.ImportLoop(catalog, file)),
    ];
}

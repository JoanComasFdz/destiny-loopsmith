using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.RuleViewer.Tests.Support;

/// <summary>The repository's rules (<c>rules/</c>), parsed once, as the rule viewer reads them: every <c>*.yaml</c>, named by its path from it.</summary>
public static class RepoCatalog
{
    public static readonly string Root = FindRoot();

    public static readonly ImmutableArray<SourceText> Files = ReadRuleFiles(Path.Combine(Root, "rules"));

    public static readonly RuleCatalog Catalog = CatalogLoading.ParseCatalog(Files)
        .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static ImmutableArray<SourceText> ReadRuleFiles(string root) =>
        [.. Directory.EnumerateFiles(root, "*.yaml", SearchOption.AllDirectories)
            .Select(path => new SourceText(Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path)))
            .OrderBy(file => file.Path, StringComparer.Ordinal)];

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Loopsmith.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root (Loopsmith.slnx) not found.");
    }
}

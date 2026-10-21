using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.RuleViewer.Core;

namespace Loopsmith.RuleViewer.E2E.Infrastructure;

/// <summary>
/// The rules the scenarios' page is drawn from (<c>Fixtures/rules/</c>, copies of real rule files) still open against the
/// rules' format and draw a tree, as the rule viewer reads them: every <c>*.yaml</c> under the folder (runs without a
/// browser). A change to the format that a copy no longer follows fails here, naming the file and the error.
/// </summary>
public sealed class FixtureRulesTests
{
    [Fact]
    public void FixtureRules_DrawATree()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "rules");
        var files = Directory.EnumerateFiles(root, "*.yaml", SearchOption.AllDirectories)
            .Select(path => new SourceText(Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path)))
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToImmutableArray();

        var tree = RuleViewing.DrawTree(files);

        Assert.True(tree is Result<RuleTree, string>.Ok, tree is Result<RuleTree, string>.Error error ? error.Failure : "");
    }
}

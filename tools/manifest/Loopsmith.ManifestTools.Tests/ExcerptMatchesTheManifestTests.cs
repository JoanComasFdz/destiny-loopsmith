using Loopsmith.Functional;
using Loopsmith.ManifestTools.Core;
using Loopsmith.ManifestTools.Tests.Support;

namespace Loopsmith.ManifestTools.Tests;

/// <summary>
/// The committed rules/manifest.yaml is what the tool writes from its manifest version: run with a download of that version
/// (<c>LOOPSMITH_MANIFEST_DIR</c>, by <c>download</c>; tools/manifest/README.md), it writes the excerpt in memory from the
/// repository's rules and test items and compares it with the committed file byte for byte. Skipped without the download,
/// or when the download is of another version (then the excerpt is regenerated, not compared).
/// </summary>
public sealed class ExcerptMatchesTheManifestTests
{
    [Fact]
    public void The_committed_excerpt_is_what_the_tool_writes_from_its_manifest_version()
    {
        var folder = Environment.GetEnvironmentVariable("LOOPSMITH_MANIFEST_DIR");   // impure
        Assert.SkipWhen(string.IsNullOrEmpty(folder), "Set LOOPSMITH_MANIFEST_DIR to a downloaded manifest (tools/manifest/README.md) to run it.");
        var root = RepoFiles.FindRoot();   // impure
        var committed = File.ReadAllText(Path.Combine(root, "rules", "manifest.yaml"));   // impure
        var versionFile = File.ReadAllText(Path.Combine(folder!, "version.txt"));   // impure
        var version = ManifestDownloading.ReadVersionFile(versionFile).UnwrapOr("");   // pure
        Assert.SkipUnless(committed.Contains($"\nversion: {TextWriting.QuoteText(version)}\n", StringComparison.Ordinal), $"The download is of {version}, rules/manifest.yaml of another version.");
        var rulePaths = Directory.EnumerateFiles(Path.Combine(root, "rules"), "*.yaml", SearchOption.AllDirectories);   // impure
        var ruleTexts = rulePaths.Where(path => NamedHashReading.IsRuleSource(Path.GetRelativePath(root, path).Replace('\\', '/'))).Select(File.ReadAllText).ToArray();   // impure
        var testItems = File.ReadAllText(Path.Combine(root, "tools", "manifest", "test-items.yaml"));   // impure
        var components = ManifestTableParsing.ComponentNames.Select(name => (name, File.ReadAllBytes(Path.Combine(folder!, $"{name}.json")))).ToArray();   // impure

        var excerpt = NamedHashReading.ReadNamedHashes(ruleTexts, testItems)
            .Bind(named => components.Select(component => ManifestTableParsing.ParseTable(component.name, component.Item2)).CombineAll().MapError(failures => failures[0])
            .Bind(ManifestTableParsing.ToComponents)
            .Bind(manifest => ExcerptWriting.WriteExcerpt(manifest, named, version)))
            .ToValue();   // pure

        Assert.True(excerpt.Text == committed, "rules/manifest.yaml isn't what the tool writes from its manifest version: rerun it (tools/manifest/README.md).");
    }
}

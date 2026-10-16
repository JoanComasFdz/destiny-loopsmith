using Loopsmith.Core.Domain;
using Loopsmith.Core.LoopFiles;
using Loopsmith.Core.Tests.Support;
using YamlDotNet.RepresentationModel;

namespace Loopsmith.Core.Tests.BuildParsing;

/// <summary>
/// Every hash a weapon of the repo's builds and loops gives (its <c>hash</c>, its <c>roll</c> picks and its <c>mod</c>) is
/// in the manifest excerpt (rules/manifest.yaml), which is all the tests read a weapon given by hash from:
/// a committed build or loop with a new weapon needs <c>tools/manifest/</c> rerun (tools/manifest/README.md).
/// </summary>
public sealed class RepoWeaponHashesTests
{
    public static TheoryData<string> BuildAndLoopFiles =>
        new(Directory.EnumerateFiles(RepoFiles.ToPath("builds"), "build.yaml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(RepoFiles.ToPath("builds"), "*.loop.yaml", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(RepoFiles.Root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(BuildAndLoopFiles))]
    public void Every_weapon_hash_of_a_build_or_loop_is_in_the_manifest_excerpt(string path)
    {
        var text = File.ReadAllText(RepoFiles.ToPath(path));   // impure

        var buildText = ToBuildText(path, text);

        var missing = ListWeaponHashes(buildText)
            .Where(hash => !RepoCatalog.Manifest.Items.ContainsKey(ItemHash.From(hash)))
            .Select(hash => $"{path}: weapon hash {hash} isn't in rules/manifest.yaml — rerun tools/manifest/ (tools/manifest/README.md)")
            .ToArray();

        Assert.True(missing.Length == 0, string.Join(Environment.NewLine, missing));
    }

    private static string ToBuildText(string path, string text) =>
        path.EndsWith(".loop.yaml", StringComparison.Ordinal)
            ? LoopFileParsing.ParseLoopFile(new SourceText(path, text))
                .Match(ok => ok.Value.Build.Text, error => throw new InvalidOperationException(error.Failure))
            : text;

    private static IEnumerable<uint> ListWeaponHashes(string buildText)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(buildText));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        var weapons = root.Children.TryGetValue("weapons", out var node) ? (YamlSequenceNode)node : new YamlSequenceNode();
        return weapons.Cast<YamlMappingNode>().SelectMany(ListHashes);
    }

    private static IEnumerable<uint> ListHashes(YamlMappingNode weapon) =>
        new[] { "hash", "mod" }
            .Where(key => weapon.Children.ContainsKey(key))
            .Select(key => weapon.Children[key])
            .Concat(weapon.Children.TryGetValue("roll", out var roll) ? (YamlSequenceNode)roll : [])
            .Select(value => ((YamlScalarNode)value).Value)
            .Where(value => value is not "?")
            .Select(value => uint.Parse(value!, System.Globalization.CultureInfo.InvariantCulture));
}

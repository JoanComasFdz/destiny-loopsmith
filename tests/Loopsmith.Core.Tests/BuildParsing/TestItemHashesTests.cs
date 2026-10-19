using System.Text.RegularExpressions;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.BuildParsing;

/// <summary>
/// Every item <c>tools/manifest/test-items.yaml</c> lists (the weapons, armor and mods the Core tests equip that no rule
/// names) is in the manifest excerpt the tests read: the extractor writes them, so a missing one means rerun it.
/// </summary>
public sealed partial class TestItemHashesTests
{
    public static TheoryData<uint> ListedHashes
    {
        get
        {
            var text = File.ReadAllText(RepoFiles.ToPath("tools/manifest/test-items.yaml"));   // impure

            return [.. HashPattern().Matches(text).Select(match => uint.Parse(match.Groups[1].Value))];
        }
    }

    [Theory]
    [MemberData(nameof(ListedHashes))]
    public void A_test_item_is_in_the_manifest_excerpt(uint hash) =>
        Assert.True(RepoCatalog.Manifest.FindItem(ItemHash.From(hash)).IsSome(),
            $"tools/manifest/test-items.yaml: {hash} isn't in rules/manifest.yaml; rerun tools/manifest/ (its README).");

    [GeneratedRegex(@"hash:\s*(\d+)")]
    private static partial Regex HashPattern();
}

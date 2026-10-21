using Loopsmith.ManifestTools.Core;
using Loopsmith.ManifestTools.Tests.Support;

namespace Loopsmith.ManifestTools.Tests;

/// <summary>The hashes Loopsmith names: the rules' <c>hash:</c> and <c>trait:</c> numbers, and test-items.yaml's items.</summary>
public sealed class NamedHashReadingTests
{
    [Fact]
    public void A_rule_names_a_hash_alone_or_in_a_list()
    {
        const string rule = """
            - id: combination-blow
              hash: 1234
            - id: set
              hash: [ 11, 12,
                13 ]
            - id: other
              hashes: 99
            """;

        Assert.Equal([1234, 11, 12, 13], NamedHashReading.ReadRuleHashes(rule));
    }

    [Fact]
    public void A_glossary_keyword_names_its_trait() =>
        Assert.Equal([2935077680, 7], NamedHashReading.ReadRuleTraits("bolt-charge: { trait: 2935077680 }\njolted:\n  trait:  7\n"));

    [Theory]
    [InlineData("rules/hunter/arc/melees.yaml", true)]
    [InlineData("rules/glossary.yaml", true)]
    [InlineData("rules/manifest.yaml", false)]
    [InlineData("rules/mods/manifest.yaml", true)]
    [InlineData("rules/notes.md", false)]
    [InlineData("tools/manifest/test-items.yaml", false)]
    public void Every_yaml_file_under_rules_but_the_excerpt_is_read(string path, bool read) =>
        Assert.Equal(read, NamedHashReading.IsRuleSource(path));

    [Fact]
    public void The_test_items_are_each_entrys_hash()
    {
        const string yaml = """
            # what the Core tests equip
            - { hash: 4019651319, name: "Festival Flight", why: "a Strand kinetic weapon" }
            - { hash: 3122197216, name: "Balanced Tuning", why: "an armor tuning plug" }
            """;

        Assert.Equal([3122197216, 4019651319], NamedHashReading.ReadTestItemHashes(yaml).ToValue().Order());
    }

    [Fact]
    public void An_empty_test_items_file_lists_nothing() => Assert.Empty(NamedHashReading.ReadTestItemHashes("# none yet\n").ToValue());

    [Fact]
    public void A_test_item_without_a_numeric_hash_is_an_error_naming_the_file() =>
        Assert.StartsWith("tools/manifest/test-items.yaml:", NamedHashReading.ReadTestItemHashes("- { name: Festival Flight }\n").ToFailure());

    [Fact]
    public void A_test_items_file_that_is_not_a_list_is_an_error() =>
        Assert.StartsWith("tools/manifest/test-items.yaml isn't a list", NamedHashReading.ReadTestItemHashes("hash: 1\n").ToFailure());

    [Fact]
    public void The_named_hashes_join_every_rule_file_and_the_test_items()
    {
        var named = NamedHashReading.ReadNamedHashes(["hash: 1\ntrait: 5", "hash: [2, 1]"], "- { hash: 3 }").ToValue();

        Assert.Equal([1, 2], named.RuleHashes.Order());
        Assert.Equal([5], named.RuleTraits);
        Assert.Equal([3], named.TestItemHashes);
    }
}

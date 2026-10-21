using Loopsmith.Functional;
using Loopsmith.ManifestTools.Core;
using Loopsmith.ManifestTools.Tests.Support;

namespace Loopsmith.ManifestTools.Tests;

/// <summary>The plugs that change an armor stat, as the excerpt's <c>statPlugs</c> write them.</summary>
public sealed class StatPlugReadingTests
{
    [Theory]
    [InlineData("""{ "plug": { "plugCategoryHash": 748854354 }, "investmentStats": [ { "statTypeHash": 392767087, "value": 10 } ] }""", "base")]
    [InlineData("""{ "plug": { "plugCategoryHash": 748854354 }, "investmentStats": [] }""", "")]
    [InlineData("""{ "plug": { "plugCategoryHash": 2198080209 } }""", "masterwork")]
    [InlineData("""{ "plug": { "plugCategoryHash": 3481777685 } }""", "tuning")]
    [InlineData("""{ "plug": { "plugCategoryHash": 2487827355 } }""", "")]
    [InlineData("""{ "plug": { "plugCategoryHash": 2487827355 }, "investmentStats": [ { "statTypeHash": 1735777505, "value": 10 } ] }""", "mod")]
    [InlineData("""{ "plug": { "plugCategoryHash": 1, "plugCategoryIdentifier": "shared.arc.fragments" }, "investmentStats": [ { "statTypeHash": 4244567218, "value": -10 } ] }""", "subclass")]
    [InlineData("""{ "plug": { "plugCategoryHash": 1, "plugCategoryIdentifier": "shared.arc.fragments" }, "investmentStats": [ { "statTypeHash": 999, "value": 10 } ] }""", "")]
    [InlineData("""{ }""", "")]
    public void A_plugs_kind_is_its_categorys_with_stats_masterworks_and_tuning_always_and_fragments_with_stats(string item, string kind) =>
        Assert.Equal(
            kind.Length > 0 ? Optional.Some(kind) : Optional.None<string>(),
            StatPlugReading.ClassifyStatPlug(Manifests.ToDefinition(item)));

    [Fact]
    public void A_stat_plug_lists_the_armor_stats_it_changes_in_the_manifests_order()
    {
        var item = Manifests.ToDefinition("""
            { "investmentStats": [ { "statTypeHash": 4244567218, "value": 5 }, { "statTypeHash": 999, "value": 1 }, { "statTypeHash": 2996146975, "value": -3 } ] }
            """);

        Assert.Equal("  - { hash: 7, kind: statMod, stats: [{ stat: melee, value: 5 }, { stat: weapons, value: -3 }] }", StatPlugReading.WriteStatPlugEntry(7, item, "mod"));
    }

    [Fact]
    public void Spark_of_Focus_lowers_only_the_classes_own_regeneration_stat_with_its_class()
    {
        var item = Manifests.ToDefinition("""
            { "investmentStats": [ { "statTypeHash": 2996146975, "value": -10 }, { "statTypeHash": 392767087, "value": -10 }, { "statTypeHash": 1943323491, "value": -10 }, { "statTypeHash": 4244567218, "value": -10 } ] }
            """);

        Assert.Equal(
            "  - { hash: 1727069360, kind: fragment, stats: [{ stat: weapons, value: -10, class: hunter }, { stat: health, value: -10, class: titan }, { stat: class, value: -10, class: warlock }] }",
            StatPlugReading.WriteStatPlugEntry(1727069360, item, "subclass"));
    }

    [Fact]
    public void A_tuning_plug_without_stats_marks_the_socket_and_Balanced_Tuning_is_its_own_kind()
    {
        Assert.Equal("tuningSocket", StatPlugReading.ToWrittenKind(1, "tuning", []));
        Assert.Equal("balancedTuning", StatPlugReading.ToWrittenKind(3122197216, "tuning", [("health", "1")]));
        Assert.Equal("tuning", StatPlugReading.ToWrittenKind(2, "tuning", [("health", "5")]));
        Assert.Equal("masterwork", StatPlugReading.ToWrittenKind(3, "masterwork", []));
    }
}

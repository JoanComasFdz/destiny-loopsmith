using System.Text;
using Loopsmith.ManifestTools.Core;
using Loopsmith.ManifestTools.Tests.Support;

namespace Loopsmith.ManifestTools.Tests;

/// <summary>A component's JSON → its definitions by hash, in the file's order.</summary>
public sealed class ManifestTablesTests
{
    [Fact]
    public void A_component_keeps_its_files_order_and_a_repeated_hash_its_first_place_with_its_last_definition()
    {
        var table = Manifests.ToTable("""{ "3": { "n": 1 }, "1": { "n": 2 }, "3": { "n": 3 } }""");

        Assert.Equal([3, 1], table.Entries.Select(pair => pair.Key));
        Assert.Equal(3, table.ByKey[3].GetProperty("n").GetInt32());
    }

    [Theory]
    [InlineData("""{ "12": {}, "x1": {} }""", "Test.json has a definition whose key isn't a hash: 'x1'.")]
    [InlineData("""[ 1, 2 ]""", "Test.json isn't a JSON object of definitions.")]
    [InlineData("""{ "12": """, "Test.json isn't JSON.")]
    public void A_component_that_is_not_an_object_of_definitions_by_hash_is_an_error_naming_it(string json, string failure) =>
        Assert.Equal(failure, ManifestTableParsing.ParseTable("Test", Encoding.UTF8.GetBytes(json)).ToFailure());

    [Fact]
    public void The_components_are_the_twelve_tables_in_their_order() =>
        Assert.StartsWith("12 components are needed", ManifestTableParsing.ToComponents([Manifests.ToTable("{}")]).ToFailure());
}

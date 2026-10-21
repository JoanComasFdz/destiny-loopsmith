using System.Text;
using Loopsmith.Functional;
using Loopsmith.ManifestTools.Core;

namespace Loopsmith.ManifestTools.Tests.Support;

/// <summary>Small hand-made manifests: each component a JSON object of definitions, "{}" when a test needs none.</summary>
internal static class Manifests
{
    /// <summary>The socket categories the excerpt looks up by name.</summary>
    public const string SocketCategories = """
        {
          "10": { "displayProperties": { "name": "WEAPON PERKS" } },
          "11": { "displayProperties": { "name": "WEAPON MODS" } },
          "12": { "displayProperties": { "name": "INTRINSIC TRAITS" } },
          "13": { "displayProperties": { "name": "ARMOR PERKS" } }
        }
        """;

    /// <summary>The buckets the items of these tests sit in.</summary>
    public const string Buckets = """
        {
          "1": { "displayProperties": { "name": "Kinetic Weapons" } },
          "2": { "displayProperties": { "name": "Power Weapons" } },
          "3": { "displayProperties": { "name": "Helmet" } },
          "4": { "displayProperties": { "name": "Subclass" } },
          "5": { "displayProperties": { "name": "Class Armor" } }
        }
        """;

    public static ManifestTable ToTable(string json) =>
        ManifestTableParsing.ParseTable("Test", Encoding.UTF8.GetBytes(json))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    public static System.Text.Json.JsonElement ToDefinition(string json) =>
        System.Text.Json.JsonDocument.Parse(json).RootElement;

    public static ManifestComponents ToComponents(
        string items = "{}", string buckets = Buckets, string damageTypes = "{}", string socketTypes = "{}",
        string socketCategories = SocketCategories, string plugSets = "{}", string perks = "{}", string itemSets = "{}",
        string breakerTypes = "{}", string itemConstants = "{}", string traits = "{}", string stats = "{}") =>
        new(ToTable(items), ToTable(buckets), ToTable(damageTypes), ToTable(socketTypes), ToTable(socketCategories), ToTable(plugSets),
            ToTable(perks), ToTable(itemSets), ToTable(breakerTypes), ToTable(itemConstants), ToTable(traits), ToTable(stats));

    public static T ToValue<T>(this Result<T, string> result) =>
        result.Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    public static string ToFailure<T>(this Result<T, string> result) =>
        result.Match(ok => throw new InvalidOperationException($"expected an error, got {ok.Value}"), error => error.Failure);
}

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Loopsmith.Functional;

namespace Loopsmith.ManifestTools.Core;

/// <summary>
/// One manifest component (DestinyInventoryItemDefinition, …): its definitions in the file's order, and by their hash (the
/// file's key). A hash the file holds twice keeps its first place and its last definition, as a JSON reader's dictionary does.
/// </summary>
public sealed record ManifestTable(ImmutableArray<KeyValuePair<long, JsonElement>> Entries, FrozenDictionary<long, JsonElement> ByKey);

/// <summary>The twelve English components the excerpt is read from (get by <see cref="ManifestDownloading"/>).</summary>
public sealed record ManifestComponents(
    ManifestTable Items,
    ManifestTable Buckets,
    ManifestTable DamageTypes,
    ManifestTable SocketTypes,
    ManifestTable SocketCategories,
    ManifestTable PlugSets,
    ManifestTable Perks,
    ManifestTable ItemSets,
    ManifestTable BreakerTypes,
    ManifestTable ItemConstants,
    ManifestTable Traits,
    ManifestTable Stats);

/// <summary>A component's JSON → its table, and the twelve tables → the components.</summary>
public static class ManifestTableParsing
{
    /// <summary>The components, each a file <c>&lt;name&gt;.json</c> of the manifest folder, in the order the host reads them.</summary>
    public static readonly ImmutableArray<string> ComponentNames =
    [
        "DestinyInventoryItemDefinition", "DestinyInventoryBucketDefinition", "DestinyDamageTypeDefinition",
        "DestinySocketTypeDefinition", "DestinySocketCategoryDefinition", "DestinyPlugSetDefinition",
        "DestinySandboxPerkDefinition", "DestinyEquipableItemSetDefinition", "DestinyBreakerTypeDefinition",
        "DestinyInventoryItemConstantsDefinition", "DestinyTraitDefinition", "DestinyStatDefinition",
    ];

    /// <summary>
    /// A component's JSON (an object of definitions by hash) → its table; an error naming it when it isn't one, or when a
    /// key isn't a hash.
    /// </summary>
    public static Result<ManifestTable, string> ParseTable(string name, ReadOnlyMemory<byte> json) =>
        ParseDocument(json).Match<Result<ManifestTable, string>>(
            root => root.Value.ValueKind != JsonValueKind.Object
                ? new Result<ManifestTable, string>.Error($"{name}.json isn't a JSON object of definitions.")
                : root.Value.EnumerateObject().Select(property => property.Name).Where(key => !ParseHash(key).IsSome())
                    .Select(Optional.Some).FirstOrDefault(Optional.None<string>()) is Optional<string>.Some notHash
                ? new Result<ManifestTable, string>.Error($"{name}.json has a definition whose key isn't a hash: '{notHash.Value}'.")
                : new Result<ManifestTable, string>.Ok(ToTable(root.Value.EnumerateObject()
                    .Select(property => KeyValuePair.Create(ParseHash(property.Name).UnwrapOr(0), property.Value)))),
            _ => new Result<ManifestTable, string>.Error($"{name}.json isn't JSON."));

    /// <summary>A key that is a hash: digits only, as Bungie writes them.</summary>
    public static Optional<long> ParseHash(string key) =>
        key.Length > 0 && key.All(char.IsAsciiDigit) && long.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var hash)
            ? Optional.Some(hash)
            : Optional.None<long>();

    /// <summary>The tables in <see cref="ComponentNames"/>' order → the components.</summary>
    public static Result<ManifestComponents, string> ToComponents(ImmutableArray<ManifestTable> tables) =>
        tables.Length == ComponentNames.Length
            ? new Result<ManifestComponents, string>.Ok(new ManifestComponents(
                tables[0], tables[1], tables[2], tables[3], tables[4], tables[5], tables[6], tables[7], tables[8], tables[9], tables[10], tables[11]))
            : new Result<ManifestComponents, string>.Error($"{ComponentNames.Length} components are needed, not {tables.Length}.");

    /// <summary>Definitions in order → a table: a repeated key keeps its first place and takes its last definition.</summary>
    public static ManifestTable ToTable(IEnumerable<KeyValuePair<long, JsonElement>> definitions)
    {
        var byKey = new Dictionary<long, JsonElement>();
        var order = new List<long>();
        foreach (var (key, value) in definitions)
        {
            if (!byKey.ContainsKey(key))
            {
                order.Add(key);
            }

            byKey[key] = value;
        }

        return new ManifestTable([.. order.Select(key => KeyValuePair.Create(key, byKey[key]))], byKey.ToFrozenDictionary());
    }

    /// <summary>The parsed JSON's root; none for a text that isn't JSON (the parser's only way to say so is its exception).</summary>
    private static Optional<JsonElement> ParseDocument(ReadOnlyMemory<byte> json)
    {
        try
        {
            return Optional.Some(JsonDocument.Parse(json).RootElement);
        }
        catch (JsonException)
        {
            return Optional.None<JsonElement>();
        }
    }
}

/// <summary>
/// Reading a definition's fields the way the excerpt needs: a missing field, a null one and one of another JSON kind are
/// all absent, never an exception. "Set" follows the Python extractor's truthiness (it wrote the format): a number other
/// than 0, a text other than "", a list or object with something in it, <c>true</c>.
/// </summary>
public static class DefinitionReading
{
    public static Optional<JsonElement> FindDefinition(this ManifestTable table, long hash) =>
        table.ByKey.TryGetValue(hash, out var definition) ? Optional.Some(definition) : Optional.None<JsonElement>();

    public static bool HasDefinition(this ManifestTable table, long hash) => table.ByKey.ContainsKey(hash);

    public static string ToKey(long hash) => hash.ToString(CultureInfo.InvariantCulture);

    /// <summary>A field present and not null.</summary>
    public static Optional<JsonElement> ReadField(JsonElement definition, string name) =>
        definition.ValueKind == JsonValueKind.Object && definition.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? Optional.Some(value)
            : Optional.None<JsonElement>();

    public static Optional<JsonElement> ReadObject(JsonElement definition, string name) =>
        ReadField(definition, name).Bind(value => value.ValueKind == JsonValueKind.Object ? Optional.Some(value) : Optional.None<JsonElement>());

    /// <summary>An object field with something in it (an empty one is as good as none).</summary>
    public static Optional<JsonElement> ReadSetObject(JsonElement definition, string name) =>
        ReadObject(definition, name).Bind(value => value.EnumerateObject().Any() ? Optional.Some(value) : Optional.None<JsonElement>());

    public static ImmutableArray<JsonElement> ReadArray(JsonElement definition, string name) =>
        ReadField(definition, name).Match(
            value => value.Value.ValueKind == JsonValueKind.Array ? [.. value.Value.EnumerateArray()] : ImmutableArray<JsonElement>.Empty,
            _ => []);

    /// <summary>A text field, empty or not.</summary>
    public static Optional<string> ReadString(JsonElement definition, string name) =>
        ReadField(definition, name).Bind(value => value.ValueKind == JsonValueKind.String ? Optional.Some(value.GetString()!) : Optional.None<string>());

    /// <summary>A text field that isn't empty.</summary>
    public static Optional<string> ReadText(JsonElement definition, string name) =>
        ReadString(definition, name).Bind(text => text.Length > 0 ? Optional.Some(text) : Optional.None<string>());

    public static Optional<long> ReadNumber(JsonElement definition, string name) =>
        ReadField(definition, name).Bind(value => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? Optional.Some(number)
            : Optional.None<long>());

    /// <summary>A number field other than 0 (a hash that is set).</summary>
    public static Optional<long> ReadSetNumber(JsonElement definition, string name) =>
        ReadNumber(definition, name).Bind(number => number != 0 ? Optional.Some(number) : Optional.None<long>());

    /// <summary>Whether a field is set; <paramref name="missing"/> when the definition lacks it.</summary>
    public static bool IsSet(JsonElement definition, string name, bool missing) =>
        definition.ValueKind == JsonValueKind.Object && definition.TryGetProperty(name, out var value)
            ? IsSetValue(value)
            : missing;

    public static bool IsSetValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => value.GetDouble() != 0,
            JsonValueKind.String => value.GetString()!.Length > 0,
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.Object => value.EnumerateObject().Any(),
            _ => false,
        };

    /// <summary>The definition's <c>displayProperties</c> (an empty object when it has none).</summary>
    public static JsonElement ReadDisplay(JsonElement definition) =>
        ReadObject(definition, "displayProperties").UnwrapOr(EmptyObject);

    /// <summary>The definition's <c>displayProperties.name</c>, "" when it has none.</summary>
    public static string ReadName(JsonElement definition) => ReadString(ReadDisplay(definition), "name").UnwrapOr("");

    /// <summary>The number values of a list of numbers (a plug set's hashes), skipping anything else.</summary>
    public static ImmutableArray<long> ReadNumbers(IEnumerable<JsonElement> values) =>
        [.. values.Where(value => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _)).Select(value => value.GetInt64())];

    public static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement;
}

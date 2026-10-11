using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.ManifestJoining;

/// <summary>
/// Reading Bungie's raw JSON: the slice boundary for <see cref="JsonDocument"/> (<see cref="ParseDocument"/>, once, here;
/// a malformed entry is as if the manifest lacked it), and total helpers over a parsed entry — a missing or mistyped
/// field is none, never an exception.
/// </summary>
internal static partial class ManifestEntryReading
{
    /// <summary>Every entry of the answers that parses, by key (an entry that doesn't parse is as if the manifest lacked it).</summary>
    internal static ImmutableDictionary<ManifestEntryKey, JsonElement> ParseEntries(ImmutableDictionary<ManifestEntryKey, string> entries) =>
        entries
            .SelectMany(entry => ParseEntry(entry.Value).Match(
                parsed => new[] { KeyValuePair.Create(entry.Key, parsed.Value) },
                _ => []))
            .ToImmutableDictionary();

    private static Optional<JsonElement> ParseEntry(string json) =>
        ParseDocument(json).Match(parsed => Optional.Some(parsed.Value), _ => Optional.None<JsonElement>());

    /// <summary>The value at a path of property names, when every step is an object that has it.</summary>
    internal static Optional<JsonElement> ReadPath(this JsonElement element, params string[] path) =>
        path.Aggregate(Optional.Some(element), (current, name) => current.Bind(found =>
            found.ValueKind == JsonValueKind.Object && found.TryGetProperty(name, out var next) ? Optional.Some(next) : Optional.None<JsonElement>()));

    internal static Optional<string> ReadString(this JsonElement element, params string[] path) =>
        element.ReadPath(path).Bind(found => found.ValueKind == JsonValueKind.String ? Optional.FromNullable(found.GetString()) : Optional.None<string>());

    /// <summary>A text as Loopsmith shows it: without the private-use glyphs of Destiny's icon font, single-spaced, trimmed; none when empty.</summary>
    internal static Optional<string> ReadText(this JsonElement element, params string[] path) =>
        element.ReadString(path).Map(ToShownText).Bind(text => text.Length > 0 ? Optional.Some(text) : Optional.None<string>());

    /// <summary>A manifest hash: a whole number that is a valid <see cref="ItemHash"/> (never 0), else none.</summary>
    internal static Optional<ItemHash> ReadHash(this JsonElement element, params string[] path) =>
        element.ReadPath(path).Bind(ToItemHash);

    /// <summary>A JSON number that is a manifest hash.</summary>
    internal static Optional<ItemHash> ToItemHash(this JsonElement found) =>
        found.ValueKind == JsonValueKind.Number && found.TryGetUInt32(out var hash) ? ToItemHash(hash) : Optional.None<ItemHash>();

    internal static Optional<int> ReadInteger(this JsonElement element, params string[] path) =>
        element.ReadPath(path).Bind(found => found.ValueKind == JsonValueKind.Number && found.TryGetInt32(out var number)
            ? Optional.Some(number)
            : Optional.None<int>());

    /// <summary>A true/false field; <paramref name="missing"/> when it isn't there (or isn't a boolean).</summary>
    internal static bool ReadFlag(this JsonElement element, string name, bool missing) =>
        element.ReadPath(name).Match(
            found => found.Value.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => missing },
            _ => missing);

    internal static ImmutableArray<JsonElement> ReadArray(this JsonElement element, params string[] path) =>
        element.ReadPath(path).Match(
            found => found.Value.ValueKind == JsonValueKind.Array ? [.. found.Value.EnumerateArray()] : ImmutableArray<JsonElement>.Empty,
            _ => ImmutableArray<JsonElement>.Empty);

    internal static Optional<ItemHash> ToItemHash(uint hash) =>
        ItemHash.TryFrom(hash).ToOptional();

    internal static Optional<PieceCount> ToPieceCount(int count) =>
        PieceCount.TryFrom(count).ToOptional();

    internal static Optional<PlugCategoryId> ToPlugCategory(string category) =>
        PlugCategoryId.TryFrom(category).ToOptional();

    /// <summary>The JSON text of a document, parsed — the one boundary for <see cref="JsonDocument"/>: why it isn't an object, or its root.</summary>
    internal static Result<JsonElement, string> ParseDocument(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? new Result<JsonElement, string>.Ok(document.RootElement.Clone())
                : new Result<JsonElement, string>.Error("not a JSON object");
        }
        catch (JsonException exception)
        {
            return new Result<JsonElement, string>.Error(exception.Message);
        }
    }

    private static string ToShownText(string text) =>
        CreateRepeatedSpacePattern().Replace(CreateIconGlyphPattern().Replace(text, ""), " ").Trim();

    private static Optional<T> ToOptional<T>(this Vogen.ValueObjectOrError<T> outcome) =>
        outcome.IsSuccess ? Optional.Some(outcome.ValueObject) : Optional.None<T>();

    [GeneratedRegex("[-]")]
    private static partial Regex CreateIconGlyphPattern();

    [GeneratedRegex(" {2,}")]
    private static partial Regex CreateRepeatedSpacePattern();
}

using System.Collections.Immutable;
using System.Text.Json;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.StatLoading;

/// <summary>
/// Reading Bungie's JSON answers: the slice boundary for <see cref="JsonDocument"/> (<see cref="ParseDocument"/>, once,
/// here), Bungie's answer envelope, and total helpers — a missing or mistyped field is none, never an exception.
/// </summary>
internal static class BungieJsonReading
{
    private const int Success = 1;

    /// <summary>The JSON text of an answer, parsed: its root object, or why it isn't one.</summary>
    internal static Result<JsonElement, string> ParseDocument(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? new Result<JsonElement, string>.Ok(document.RootElement.Clone())
                : new Result<JsonElement, string>.Error("Bungie's answer isn't what Loopsmith expected.");
        }
        catch (JsonException)
        {
            return new Result<JsonElement, string>.Error("Bungie's answer isn't what Loopsmith expected.");
        }
    }

    /// <summary>
    /// A platform answer's <c>Response</c>, when its <c>ErrorCode</c> is 1 (Success); else what Bungie said
    /// (maintenance, an expired login…).
    /// </summary>
    internal static Result<JsonElement, string> ReadResponse(string json) =>
        ParseDocument(json).Bind(root => root.ReadInteger("ErrorCode").Match(
            code => code.Value == Success
                ? root.ReadPath("Response").ToResult(() => "Bungie's answer has no response.")
                : new Result<JsonElement, string>.Error(DescribeRefusal(root)),
            _ => new Result<JsonElement, string>.Error(DescribeRefusal(root))));

    private static string DescribeRefusal(JsonElement root) =>
        $"Bungie said: {root.ReadString("Message").UnwrapOr("no answer")}" + root.ReadString("ErrorStatus").Match(status => $" ({status.Value}).", _ => ".");

    /// <summary>The value at a path of property names, when every step is an object that has it.</summary>
    internal static Optional<JsonElement> ReadPath(this JsonElement element, params string[] path) =>
        path.Aggregate(Optional.Some(element), (current, name) => current.Bind(found =>
            found.ValueKind == JsonValueKind.Object && found.TryGetProperty(name, out var next) ? Optional.Some(next) : Optional.None<JsonElement>()));

    internal static Optional<string> ReadString(this JsonElement element, params string[] path) =>
        element.ReadPath(path).Bind(found => found.ValueKind == JsonValueKind.String ? Optional.FromNullable(found.GetString()) : Optional.None<string>());

    internal static Optional<int> ReadInteger(this JsonElement element, params string[] path) =>
        element.ReadPath(path).Bind(found => found.ValueKind == JsonValueKind.Number && found.TryGetInt32(out var number)
            ? Optional.Some(number)
            : Optional.None<int>());

    /// <summary>A manifest hash: a whole number that is a valid <see cref="ItemHash"/> (never 0), else none.</summary>
    internal static Optional<ItemHash> ReadHash(this JsonElement element, params string[] path) =>
        element.ReadPath(path).Bind(found => found.ValueKind == JsonValueKind.Number && found.TryGetUInt32(out var hash)
            ? ItemHash.TryFrom(hash).ToOptional()
            : Optional.None<ItemHash>());

    /// <summary>A true/false field; <paramref name="missing"/> when it isn't there (or isn't a boolean).</summary>
    internal static bool ReadFlag(this JsonElement element, string name, bool missing) =>
        element.ReadPath(name).Match(
            found => found.Value.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => missing },
            _ => missing);

    internal static ImmutableArray<JsonElement> ReadArray(this JsonElement element, params string[] path) =>
        element.ReadPath(path).Match(
            found => found.Value.ValueKind == JsonValueKind.Array ? [.. found.Value.EnumerateArray()] : ImmutableArray<JsonElement>.Empty,
            _ => ImmutableArray<JsonElement>.Empty);

    /// <summary>A Vogen value object's outcome as an optional: the value when it is valid.</summary>
    internal static Optional<T> ToOptional<T>(this Vogen.ValueObjectOrError<T> outcome) =>
        outcome.IsSuccess ? Optional.Some(outcome.ValueObject) : Optional.None<T>();

    /// <summary>An object's properties (name and value), in order; none when it isn't an object.</summary>
    internal static ImmutableArray<KeyValuePair<string, JsonElement>> ReadProperties(this JsonElement element, params string[] path) =>
        element.ReadPath(path).Match(
            found => found.Value.ValueKind == JsonValueKind.Object
                ? [.. found.Value.EnumerateObject().Select(property => KeyValuePair.Create(property.Name, property.Value))]
                : ImmutableArray<KeyValuePair<string, JsonElement>>.Empty,
            _ => ImmutableArray<KeyValuePair<string, JsonElement>>.Empty);
}

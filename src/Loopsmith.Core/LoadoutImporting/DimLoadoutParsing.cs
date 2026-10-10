using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.LoadoutImporting;

/// <summary>An equipped item of a DIM loadout: its manifest hash and, for a subclass, the plugs by socket index.</summary>
internal sealed record DimLoadoutItem(ItemHash Hash, ImmutableArray<ItemHash> Plugs);

/// <summary>
/// What Loopsmith reads of a DIM loadout (DIM's API type <c>Loadout</c>): its name, class, equipped items, armor mods,
/// artifact perks and the exotic the Loadout Optimizer was asked for. Unequipped items are only carried, not worn.
/// </summary>
internal sealed record DimLoadout(
    string Name,
    Optional<int> ClassType,
    ImmutableArray<DimLoadoutItem> Equipped,
    ImmutableArray<ItemHash> Mods,
    ImmutableArray<ItemHash> ArtifactPerks,
    Optional<ItemHash> ExoticArmor);

/// <summary>Pure: DIM loadout JSON (from a link, or DIM's <c>{ "loadout": … }</c> answer for a share) → <see cref="DimLoadout"/>.</summary>
internal static class DimLoadoutParsing
{
    private const string Unreadable = "The DIM link's loadout can't be read";

    internal static Result<DimLoadout, string> ParseLoadout(string json) =>
        ParseDocument(json).Bind(ReadLoadout);

    /// <summary>The slice boundary: System.Text.Json's exception for text that isn't JSON becomes an error here, once.</summary>
    private static Result<JsonElement, string> ParseDocument(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return new Result<JsonElement, string>.Ok(document.RootElement.Clone());
        }
        catch (JsonException exception)
        {
            return new Result<JsonElement, string>.Error($"{Unreadable}: it isn't JSON ({exception.Message})");
        }
    }

    private static Result<DimLoadout, string> ReadLoadout(JsonElement root)
    {
        var loadout = FindProperty(root, "loadout").UnwrapOr(root);
        if (loadout.ValueKind != JsonValueKind.Object)
        {
            return new Result<DimLoadout, string>.Error($"{Unreadable}: it isn't a loadout.");
        }

        var parameters = FindProperty(loadout, "parameters");
        var artifact = parameters.Bind(p => FindProperty(p, "artifactUnlocks"));
        return new Result<DimLoadout, string>.Ok(new DimLoadout(
            FindProperty(loadout, "name").Bind(ReadText).UnwrapOr(""),
            FindProperty(loadout, "classType").Bind(ReadInteger),
            [.. ListItems(FindProperty(loadout, "equipped")).Select(ReadItem).SelectMany(ToSome)],
            ReadHashes(parameters.Bind(p => FindProperty(p, "mods"))),
            ReadHashes(artifact.Bind(a => FindProperty(a, "unlockedItemHashes"))),
            parameters.Bind(p => FindProperty(p, "exoticArmorHash")).Bind(ReadHash)));
    }

    /// <summary>An item with a hash; a subclass's <c>socketOverrides</c> (<c>{ "0": hash, … }</c>) in socket order.</summary>
    private static Optional<DimLoadoutItem> ReadItem(JsonElement item) =>
        FindProperty(item, "hash").Bind(ReadHash).Map(hash => new DimLoadoutItem(hash, ReadPlugs(item)));

    private static ImmutableArray<ItemHash> ReadPlugs(JsonElement item) =>
        FindProperty(item, "socketOverrides").Match(
            overrides => overrides.Value.ValueKind == JsonValueKind.Object
                ? overrides.Value.EnumerateObject()
                    .Select(socket => (Index: ParseInteger(socket.Name), Plug: ReadHash(socket.Value)))
                    .Where(socket => socket.Index.IsSome() && socket.Plug.IsSome())
                    .OrderBy(socket => socket.Index.UnwrapOr(0))
                    .SelectMany(socket => ToSome(socket.Plug))
                    .ToImmutableArray()
                : [],
            _ => []);

    private static ImmutableArray<ItemHash> ReadHashes(Optional<JsonElement> list) =>
        [.. ListItems(list).Select(ReadHash).SelectMany(ToSome)];

    private static IEnumerable<JsonElement> ListItems(Optional<JsonElement> list) =>
        list.Match(
            some => some.Value.ValueKind == JsonValueKind.Array ? some.Value.EnumerateArray().ToImmutableArray() : [],
            _ => ImmutableArray<JsonElement>.Empty);

    private static Optional<JsonElement> FindProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? Optional.Some(value)
            : Optional.None<JsonElement>();

    /// <summary>A manifest hash: a positive number, or the same number as text (DIM writes some hashes as strings).</summary>
    private static Optional<ItemHash> ReadHash(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetUInt32(out var hash) && hash > 0 => ToItemHash(hash),
            JsonValueKind.String when uint.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var hash) && hash > 0 => ToItemHash(hash),
            _ => Optional.None<ItemHash>(),
        };

    private static Optional<ItemHash> ToItemHash(uint hash) =>
        ItemHash.TryFrom(hash) is { IsSuccess: true } outcome ? Optional.Some(outcome.ValueObject) : Optional.None<ItemHash>();

    private static Optional<int> ReadInteger(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? Optional.Some(number) : Optional.None<int>();

    private static Optional<int> ParseInteger(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? Optional.Some(number) : Optional.None<int>();

    private static Optional<string> ReadText(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? Optional.Some(value.GetString() ?? "") : Optional.None<string>();

    private static IEnumerable<T> ToSome<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);
}

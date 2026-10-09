using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Loopsmith.Core.Functional;

namespace Loopsmith.Web.State;

/// <summary>A loop the user saved in this browser: the <c>.loop.yaml</c> text plus what the list shows without parsing it.</summary>
public sealed record SavedLoop(string Id, string Name, string BuildName, int Steps, DateTimeOffset SavedAt, string LoopFile);

public enum LibraryOrigin { Saved, Bundled }

/// <summary>A row of the library: a saved loop or a bundled example loop.</summary>
public sealed record LibraryEntry(
    string Id,
    string Name,
    string BuildName,
    int Steps,
    LibraryOrigin Origin,
    Optional<DateTimeOffset> SavedAt,
    string LoopFile,
    Optional<string> Problem);

[JsonSerializable(typeof(SavedLoop[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class LibraryJsonContext : JsonSerializerContext;

/// <summary>Pure: the saved-loop list ↔ its localStorage JSON, and edits to the list.</summary>
public static class LoopLibrary
{
    public const string StorageKey = "loopsmith.library.v1";

    public static Result<ImmutableArray<SavedLoop>, string> ParseLibrary(Optional<string> json) =>
        json.Match(
            some => ParseLibraryJson(some.Value),
            _ => new Result<ImmutableArray<SavedLoop>, string>.Ok([]));

    public static string WriteLibrary(ImmutableArray<SavedLoop> loops) =>
        JsonSerializer.Serialize(loops.ToArray(), LibraryJsonContext.Default.SavedLoopArray);

    /// <summary>Replaces the loop with the same id, or adds it first.</summary>
    public static ImmutableArray<SavedLoop> SaveLoop(ImmutableArray<SavedLoop> loops, SavedLoop loop) =>
        loops.Any(existing => existing.Id == loop.Id)
            ? loops.Select(existing => existing.Id == loop.Id ? loop : existing).ToImmutableArray()
            : loops.Insert(0, loop);

    public static ImmutableArray<SavedLoop> DeleteLoop(ImmutableArray<SavedLoop> loops, string id) =>
        loops.RemoveAll(loop => loop.Id == id);

    public static Optional<SavedLoop> FindLoop(ImmutableArray<SavedLoop> loops, string id) =>
        Optional.FromNullable(loops.FirstOrDefault(loop => loop.Id == id));

    /// <summary>A file name for a loop: <c>&lt;name&gt;.loop.yaml</c>, the name reduced to a safe slug.</summary>
    public static string ToLoopFileName(string name)
    {
        var slug = new string(name.Trim().ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray());
        var collapsed = string.Join('-', slug.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return $"{(collapsed.Length == 0 ? "loop" : collapsed)}.loop.yaml";
    }

    private static Result<ImmutableArray<SavedLoop>, string> ParseLibraryJson(string json)
    {
        try
        {
            var loops = JsonSerializer.Deserialize(json, LibraryJsonContext.Default.SavedLoopArray) ?? [];
            return new Result<ImmutableArray<SavedLoop>, string>.Ok([.. loops]);
        }
        catch (JsonException exception)
        {
            return new Result<ImmutableArray<SavedLoop>, string>.Error($"The saved library in this browser is unreadable ({exception.Message}).");
        }
    }
}

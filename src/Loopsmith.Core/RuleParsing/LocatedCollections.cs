using System.Collections.Immutable;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.RuleParsing.ResultAccumulation;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary>A parsed value and where it was written (for duplicate reports and default provenance).</summary>
internal sealed record Located<T>(T Value, string Path, long Line);

internal static class LocatedCollections
{
    internal static Located<T> ToLocated<T>(this YamlMap map, T value) =>
        new(value, map.Path, map.Node.Start.Line);

    /// <summary>Keys the values by id; every repeated id is an error at the repeat, naming the first definition.</summary>
    internal static Result<ImmutableDictionary<TKey, T>, Errors> ToUniqueDictionary<TKey, T>(
        IEnumerable<Located<T>> items,
        Func<T, TKey> keyOf,
        string what)
        where TKey : notnull
    {
        var groups = items.GroupBy(item => keyOf(item.Value)).ToImmutableArray();
        var duplicates = groups
            .SelectMany(group => group.Skip(1).Select(repeat => new ParseError(
                repeat.Path,
                Optional.Some(repeat.Line),
                $"duplicate {what} id '{group.Key}' (first defined at {group.First().Path}:{group.First().Line})")))
            .ToImmutableArray();
        return duplicates.IsEmpty
            ? Succeed(groups.ToImmutableDictionary(group => group.Key, group => group.First().Value))
            : Fail<ImmutableDictionary<TKey, T>>(duplicates);
    }
}

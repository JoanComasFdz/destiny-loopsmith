using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Loopsmith.Core.LoopFiles;

/// <summary>
/// Changes one part of a build file in place, so the rest of it — its comments, its order, how its author wrote it —
/// stays as it was (a perk picked in the designer changes that weapon's entry, nothing else). Pure. It reads YamlDotNet's
/// event stream rather than its node tree: the events mark where each entry's text ends.
/// </summary>
public static class BuildFileEditing
{
    /// <summary>
    /// The build file with its weapon number <paramref name="index"/> (0-based, in the file's order) written anew as
    /// <paramref name="weapon"/>: one flow mapping (<c>{slot: …, roll: […]}</c>) where that entry was. Comments inside the
    /// entry itself go with it.
    /// </summary>
    public static Result<string, string> ReplaceWeapon(SourceText buildFile, int index, WeaponLoadout weapon) =>
        ReadEvents(buildFile)
            .Bind(events => FindWeapon(buildFile, events, index).Map(entry => ToTextSpan(events, entry)))
            .Map(span => string.Concat(buildFile.Text.AsSpan(0, span.Start), BuildFileWriting.WriteWeapon(weapon), buildFile.Text.AsSpan(span.End)));

    /// <summary>A node as the indices of its first and last event (the same one for a scalar).</summary>
    private sealed record EventSpan(int First, int Last);

    private sealed record TextSpan(int Start, int End);

    /// <summary>The slice boundary for YamlDotNet's parser: its exceptions become an error here, once.</summary>
    private static Result<ImmutableArray<ParsingEvent>, string> ReadEvents(SourceText file)
    {
        try
        {
            var parser = new Parser(new StringReader(file.Text));
            return new Result<ImmutableArray<ParsingEvent>, string>.Ok([.. ReadAll(parser)]);
        }
        catch (YamlException exception)
        {
            return new Result<ImmutableArray<ParsingEvent>, string>.Error($"{file.Path}:{exception.Start.Line}: invalid YAML: {exception.Message}");
        }
    }

    private static IEnumerable<ParsingEvent> ReadAll(Parser parser)
    {
        while (parser.MoveNext())
        {
            yield return parser.Current!;
        }
    }

    /// <summary>The <c>weapons:</c> entry number <paramref name="index"/> of the file's top-level mapping.</summary>
    private static Result<EventSpan, string> FindWeapon(SourceText file, ImmutableArray<ParsingEvent> events, int index)
    {
        const int Root = 2; // after StreamStart and DocumentStart
        var weapons = events.Length > Root && events[Root] is MappingStart
            ? ListChildren(events, Root)
                .Chunk(2)
                .Where(entry => entry.Length == 2 && events[entry[0].First] is Scalar { Value: "weapons" } && events[entry[1].First] is SequenceStart)
                .Select(entry => Optional.Some(entry[1]))
                .FindFirstSome()
            : Optional.None<EventSpan>();
        return weapons.Match(
            list => FindEntry(file, ListChildren(events, list.Value.First), index),
            _ => new Result<EventSpan, string>.Error($"{file.Path}: the build has no weapons"));
    }

    private static Result<EventSpan, string> FindEntry(SourceText file, ImmutableArray<EventSpan> entries, int index) =>
        index >= 0 && index < entries.Length
            ? new Result<EventSpan, string>.Ok(entries[index])
            : new Result<EventSpan, string>.Error($"{file.Path}: the build has no weapon {index + 1}");

    /// <summary>The nodes directly inside the collection whose start event is at <paramref name="collection"/>: a mapping's keys and values in turn.</summary>
    private static ImmutableArray<EventSpan> ListChildren(ImmutableArray<ParsingEvent> events, int collection)
    {
        var children = ImmutableArray.CreateBuilder<EventSpan>();
        var child = collection + 1;
        while (child < events.Length && events[child] is not (MappingEnd or SequenceEnd))
        {
            var last = FindNodeEnd(events, child);
            children.Add(new EventSpan(child, last));
            child = last + 1;
        }

        return children.ToImmutable();
    }

    /// <summary>The index of a node's last event: itself for a scalar or an alias, its matching end for a collection.</summary>
    private static int FindNodeEnd(ImmutableArray<ParsingEvent> events, int node)
    {
        var depth = 0;
        for (var at = node; at < events.Length; at++)
        {
            depth += events[at] switch
            {
                MappingStart or SequenceStart => 1,
                MappingEnd or SequenceEnd => -1,
                _ => 0,
            };
            if (depth == 0)
            {
                return at;
            }
        }

        return events.Length - 1;
    }

    /// <summary>
    /// Where a node's own text starts and ends: the end of its last scalar or flow collection (its closing bracket). A
    /// block collection's end event marks the next token instead (past the line break and the next line's indentation),
    /// so it doesn't count.
    /// </summary>
    private static TextSpan ToTextSpan(ImmutableArray<ParsingEvent> events, EventSpan node)
    {
        var flow = ImmutableStack<bool>.Empty;
        var end = events[node.First].End.Index;
        for (var at = node.First; at <= node.Last; at++)
        {
            switch (events[at])
            {
                case MappingStart mapping:
                    flow = flow.Push(mapping.Style == MappingStyle.Flow);
                    break;
                case SequenceStart sequence:
                    flow = flow.Push(sequence.Style == SequenceStyle.Flow);
                    break;
                case MappingEnd or SequenceEnd:
                    end = flow.Peek() ? events[at].Start.Index + 1 : end; // the event marks its "}" or "]", one character
                    flow = flow.Pop();
                    break;
                default:
                    end = events[at].End.Index;
                    break;
            }
        }

        return new TextSpan((int)events[node.First].Start.Index, (int)end);
    }
}

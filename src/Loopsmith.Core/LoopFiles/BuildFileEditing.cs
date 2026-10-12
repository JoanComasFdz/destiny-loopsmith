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

    /// <summary>
    /// The build file with <paramref name="weapon"/> added as its last weapon (one flow mapping): on a line of its own
    /// after the last entry of a block list (with that entry's indentation), inside a flow list, or as a new
    /// <c>weapons:</c> at the end when the file has none. The rest of the file stays as it was.
    /// </summary>
    public static Result<string, string> AddWeapon(SourceText buildFile, WeaponLoadout weapon) =>
        ReadEvents(buildFile).Bind(events => FindTopLevelValue(events, "weapons").Match(
            list => AddToList(buildFile, events, list.Value, BuildFileWriting.WriteWeapon(weapon)),
            _ => new Result<string, string>.Ok($"{buildFile.Text.TrimEnd('\n')}\nweapons:\n  - {BuildFileWriting.WriteWeapon(weapon)}\n")));

    /// <summary>An entry added at the end of a list: after its last entry's line (a block list), or before its "]" (a flow list).</summary>
    private static Result<string, string> AddToList(SourceText file, ImmutableArray<ParsingEvent> events, EventSpan list, string entry)
    {
        var text = file.Text;
        var children = ListChildren(events, list.First);
        if (events[list.First] is SequenceStart { Style: SequenceStyle.Flow })
        {
            var close = (int)events[list.Last].Start.Index;                                     // the "]"
            var trailingComma = text[..close].TrimEnd().EndsWith(',');                             // "[{…}, ]": YAML allows it
            return new Result<string, string>.Ok(text.Insert(close, children.IsEmpty || trailingComma ? entry : $", {entry}"));
        }

        if (events[list.First] is not SequenceStart || children.IsEmpty)
        {
            return new Result<string, string>.Error($"{file.Path}: its weapons aren't a list Loopsmith can add to");
        }

        var start = (int)events[children[^1].First].Start.Index;
        var prefix = text[(text.LastIndexOf('\n', Math.Max(start - 1, 0)) + 1)..start];         // "  - "
        var lineEnd = text.IndexOf('\n', ToTextSpan(events, children[^1]).End);
        return prefix.Trim() == "-"
            ? new Result<string, string>.Ok(lineEnd < 0 ? $"{text}\n{prefix}{entry}\n" : text.Insert(lineEnd, $"\n{prefix}{entry}"))
            : new Result<string, string>.Error($"{file.Path}: its weapons aren't a list Loopsmith can add to");
    }

    /// <summary>
    /// The build file with its <c>stats:</c> written anew as <paramref name="stats"/> (one flow mapping where the old value
    /// was), or added at its end when it has none; the rest of the file stays as it was.
    /// </summary>
    public static Result<string, string> ReplaceStats(SourceText buildFile, StatLine stats) =>
        ReadEvents(buildFile).Map(events => FindTopLevelValue(events, "stats").Match(
            value => ReplaceText(buildFile.Text, ToTextSpan(events, value.Value), BuildFileWriting.WriteStats(stats)),
            _ => $"{buildFile.Text.TrimEnd('\n')}\nstats: {BuildFileWriting.WriteStats(stats)}\n"));

    /// <summary>
    /// The build file with its armor entries — <c>exoticArmor</c>, <c>armorSetBonuses</c>, <c>leftOut</c>,
    /// <c>armorCopies</c> — written anew as <paramref name="build"/> has them (a piece picked from the player's armor
    /// changes all four): each replaced where it was, added at the end when the file has none, removed when the build has
    /// nothing for it. The rest of the file stays as it was.
    /// </summary>
    public static Result<string, string> ReplaceArmor(SourceText buildFile, Build build) =>
        BuildFileWriting.WriteArmorEntries(build).Aggregate(
            new Result<string, string>.Ok(buildFile.Text) as Result<string, string>,
            (text, entry) => text.Bind(current => ReplaceEntry(buildFile with { Text = current }, entry.Key, entry.Value)));

    /// <summary>
    /// The build file with its subclass entries — <c>super</c>, <c>grenade</c>, <c>melee</c>, <c>classAbility</c>,
    /// <c>aspects</c>, <c>fragments</c>, <c>leftOut</c> — written anew as <paramref name="build"/> has them (a subclass
    /// edited in the designer changes all of them): each replaced where it was, added at the end when the file has none,
    /// removed when the build has nothing for it. The rest of the file stays as it was.
    /// </summary>
    public static Result<string, string> ReplaceSubclass(SourceText buildFile, Build build) =>
        BuildFileWriting.WriteSubclassEntries(build).Aggregate(
            new Result<string, string>.Ok(buildFile.Text) as Result<string, string>,
            (text, entry) => text.Bind(current => ReplaceEntry(buildFile with { Text = current }, entry.Key, entry.Value)));

    /// <summary>A top-level entry's value replaced (or the entry added at the end), or the entry removed when there is no value.</summary>
    private static Result<string, string> ReplaceEntry(SourceText buildFile, string key, Optional<string> value) =>
        ReadEvents(buildFile).Map(events => FindTopLevelEntry(events, key).Match(
            entry => value.Match(
                written => ReplaceText(buildFile.Text, ToTextSpan(events, entry.Value.Value), written.Value),
                _ => RemoveLines(buildFile.Text, (int)events[entry.Value.Key.First].Start.Index, ToTextSpan(events, entry.Value.Value).End)),
            _ => value.Match(
                written => $"{buildFile.Text.TrimEnd('\n')}\n{key}: {written.Value}\n",
                _ => buildFile.Text)));

    /// <summary>The text without the lines from <paramref name="start"/>'s through <paramref name="end"/>'s (an entry, its key to its value's end).</summary>
    private static string RemoveLines(string text, int start, int end)
    {
        var lineStart = text.LastIndexOf('\n', Math.Max(start - 1, 0)) + 1;
        var lineEnd = text.IndexOf('\n', end);
        return string.Concat(text.AsSpan(0, start == 0 ? 0 : lineStart), lineEnd < 0 ? "" : text.AsSpan(lineEnd + 1));
    }

    private static string ReplaceText(string text, TextSpan span, string replacement) =>
        string.Concat(text.AsSpan(0, span.Start), replacement, text.AsSpan(span.End));

    /// <summary>The value of a key of the file's top-level mapping, when it has the key.</summary>
    private static Optional<EventSpan> FindTopLevelValue(ImmutableArray<ParsingEvent> events, string key) =>
        FindTopLevelEntry(events, key).Map(entry => entry.Value);

    /// <summary>A top-level entry: its key's node and its value's.</summary>
    private sealed record EntrySpan(EventSpan Key, EventSpan Value);

    /// <summary>The entry of a key of the file's top-level mapping, when it has the key.</summary>
    private static Optional<EntrySpan> FindTopLevelEntry(ImmutableArray<ParsingEvent> events, string key)
    {
        const int Root = 2; // after StreamStart and DocumentStart
        return events.Length > Root && events[Root] is MappingStart
            ? ListChildren(events, Root)
                .Chunk(2)
                .Where(entry => entry.Length == 2 && events[entry[0].First] is Scalar scalar && scalar.Value == key)
                .Select(entry => Optional.Some(new EntrySpan(entry[0], entry[1])))
                .FindFirstSome()
            : Optional.None<EntrySpan>();
    }

    /// <summary>A node as the indices of its first and last event (the same one for a scalar).</summary>
    private sealed record EventSpan(int First, int Last);

    private sealed record TextSpan(int Start, int End);

    /// <summary>
    /// The slice boundary for YamlDotNet's parser: its foreseeable exceptions become an error here, once (it throws
    /// <c>InvalidOperationException</c> on some unclosed flow collections, as <see cref="YamlReading.LoadDocument"/> notes).
    /// </summary>
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
        catch (InvalidOperationException exception)
        {
            return new Result<ImmutableArray<ParsingEvent>, string>.Error($"{file.Path}:1: invalid YAML (check for an unclosed [ or {{): {exception.Message}");
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

using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using static Loopsmith.Core.RuleParsing.ResultAccumulation;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary>One problem in a rule file. <see cref="Line"/> is absent for whole-file problems (a missing glossary).</summary>
internal sealed record ParseError(string Path, Optional<long> Line, string Message);

/// <summary>A YAML node, the file it came from, and how messages name it ("applyBuff.status").</summary>
internal sealed record YamlValue(string Path, YamlNode Node, string Label);

/// <summary>A YAML mapping, the file it came from, and how messages name it ("element").</summary>
internal sealed record YamlMap(string Path, YamlMappingNode Node, string Label);

/// <summary>The single key of a one-key mapping (<c>{ kill: … }</c>) and its value.</summary>
internal sealed record YamlEntry(string Key, YamlValue Value);

/// <summary>
/// Reading YAML through YamlDotNet's representation model. The rest of the slice sees located
/// nodes and <see cref="Result{T, TFailure}"/>s; YamlDotNet's exceptions stop in <see cref="LoadDocument"/>.
/// </summary>
internal static class YamlReading
{
    private static readonly ImmutableArray<string> NullScalars = ["", "~", "null", "Null", "NULL"];

    /// <summary>The slice boundary: YamlDotNet's foreseeable exceptions become located errors here, once.</summary>
    internal static Result<YamlValue, Errors> LoadDocument(SourceText file, string label)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(file.Text));
            return ToSingleDocument(file.Path, stream, label);
        }
        catch (YamlException exception)
        {
            return Fail<YamlValue>(ToErrorsAt(file.Path, exception.Start.Line, $"invalid YAML: {exception.Message}"));
        }
        catch (InvalidOperationException exception)
        {
            // YamlDotNet throws this (not YamlException) on an unclosed flow collection followed by a key line.
            return Fail<YamlValue>(ToErrorsAt(file.Path, 1, $"invalid YAML (check for an unclosed [ or {{): {exception.Message}"));
        }
    }

    private static Result<YamlValue, Errors> ToSingleDocument(string path, YamlStream stream, string label) =>
        stream.Documents.Count switch
        {
            0 => Fail<YamlValue>(ToErrorsAt(path, 1, "the file is empty")),
            1 => Succeed(new YamlValue(path, stream.Documents[0].RootNode, label)),
            _ => Fail<YamlValue>(ToErrorsAt(path, stream.Documents[1].RootNode.Start.Line, "expected a single YAML document")),
        };

    internal static Errors ToErrorsAt(string path, long line, string message) =>
        [new ParseError(path, Optional.Some(Math.Max(1L, line)), message)];

    internal static Errors ToErrors(this YamlValue value, string message) =>
        ToErrorsAt(value.Path, value.Node.Start.Line, message);

    internal static Result<T, Errors> FailAt<T>(this YamlValue value, string message) =>
        Fail<T>(value.ToErrors(message));

    internal static Result<T, Errors> FailAt<T>(this YamlMap map, string message) =>
        Fail<T>(ToErrorsAt(map.Path, map.Node.Start.Line, message));

    /// <summary>Fails at <paramref name="key"/>'s value when present, else at the mapping.</summary>
    internal static Result<T, Errors> FailAtKey<T>(this YamlMap map, string key, string message) =>
        map.FindValue(key).Match(
            some => some.Value.FailAt<T>(message),
            _ => map.FailAt<T>(message));

    internal static Result<YamlMap, Errors> ToMap(this YamlValue value) =>
        value.Node is YamlMappingNode mapping
            ? Succeed(new YamlMap(value.Path, mapping, value.Label))
            : value.FailAt<YamlMap>($"{value.Label} must be a mapping ({{ key: value }})");

    internal static Result<ImmutableArray<YamlValue>, Errors> ToItems(this YamlValue value, string itemLabel) =>
        value.Node is YamlSequenceNode sequence
            ? Succeed(sequence.Children.Select(child => new YamlValue(value.Path, child, itemLabel)).ToImmutableArray())
            : value.FailAt<ImmutableArray<YamlValue>>($"{value.Label} must be a list ([ … ])");

    internal static Result<ImmutableArray<T>, Errors> ReadEach<T>(
        this YamlValue value,
        string itemLabel,
        Func<YamlValue, Result<T, Errors>> read) =>
        value.ToItems(itemLabel).Bind(items => items.Select(read).CollectAll());

    internal static Result<string, Errors> ToText(this YamlValue value) =>
        value.Node is YamlScalarNode { Value: { } text }
            ? Succeed(text)
            : value.FailAt<string>($"{value.Label} must be a single value, not a list or mapping");

    /// <summary>Parses a scalar with a notation parser, locating its message at the node.</summary>
    internal static Result<T, Errors> ParseWith<T>(this YamlValue value, Func<string, Result<T, string>> parse) =>
        value.ToText().Bind(text => parse(text).MapError(message => value.ToErrors($"{value.Label}: {message}")));

    /// <summary>A one-key mapping such as <c>{ kill: { via: any } }</c> or <c>{ removeBuff: amplified }</c>.</summary>
    internal static Result<YamlEntry, Errors> ToSingleEntry(this YamlValue value) =>
        value.ToMap().Bind(map => map.Node.Children.Count == 1
            && map.Node.Children.First() is { Key: YamlScalarNode { Value: { } key } } entry
                ? Succeed(new YamlEntry(key, new YamlValue(map.Path, entry.Value, key)))
                : map.FailAt<YamlEntry>(
                    $"{map.Label} must have exactly one key, found {map.Node.Children.Count}: {DescribeKeys(map)}"));

    /// <summary>The value of <paramref name="key"/>; absent when the key is missing or its value is YAML null.</summary>
    internal static Optional<YamlValue> FindValue(this YamlMap map, string key) =>
        ToFirst(map.Node.Children
            .Where(entry => entry.Key is YamlScalarNode { Value: { } name } && name == key && !IsNull(entry.Value))
            .Select(entry => new YamlValue(map.Path, entry.Value, $"{map.Label}.{key}")));

    internal static Result<T, Errors> ReadRequired<T>(this YamlMap map, string key, Func<YamlValue, Result<T, Errors>> read) =>
        map.FindValue(key).Match(
            some => read(some.Value),
            _ => map.FailAt<T>($"{map.Label} is missing '{key}'"));

    internal static Result<Optional<T>, Errors> ReadOptional<T>(
        this YamlMap map,
        string key,
        Func<YamlValue, Result<T, Errors>> read) =>
        map.FindValue(key).Match(
            some => read(some.Value).Map(Optional.Some),
            _ => Succeed(Optional.None<T>()));

    internal static Result<T, Errors> ReadOrDefault<T>(
        this YamlMap map,
        string key,
        Func<YamlValue, Result<T, Errors>> read,
        T fallback) =>
        map.FindValue(key).Match(
            some => read(some.Value),
            _ => Succeed(fallback));

    /// <summary>Unknown keys are errors (one per key, at the key's line).</summary>
    internal static Result<Unit, Errors> CheckKeys(this YamlMap map, ImmutableArray<string> allowed) =>
        FailIfAny(map.Node.Children.Keys
            .Where(key => !(key is YamlScalarNode { Value: { } name } && allowed.Contains(name)))
            .SelectMany(key => ToErrorsAt(
                map.Path,
                key.Start.Line,
                $"unknown key '{DescribeKey(key)}' in {map.Label} (allowed: {string.Join(", ", allowed)})"))
            .ToImmutableArray());

    private static bool IsNull(YamlNode node) =>
        node is YamlScalarNode { Style: ScalarStyle.Plain } scalar && (scalar.Value is null || NullScalars.Contains(scalar.Value));

    private static string DescribeKeys(YamlMap map) =>
        map.Node.Children.Count == 0 ? "none" : string.Join(", ", map.Node.Children.Keys.Select(DescribeKey));

    private static string DescribeKey(YamlNode key) =>
        key is YamlScalarNode { Value: { } name } ? name : "(complex key)";

    private static Optional<T> ToFirst<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            return Optional.Some(item);
        }

        return Optional.None<T>();
    }
}

using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Vogen;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using static Loopsmith.Core.BuildParsing.ResultAccumulation;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.BuildParsing.ParseError>;

namespace Loopsmith.Core.BuildParsing;

// Private to this slice by design: slices never reference each other sideways, so the few YAML
// helpers it shares in spirit with RuleParsing are duplicated here rather than shared.

/// <summary>One problem in a build file.</summary>
internal sealed record ParseError(string Path, long Line, string Message);

/// <summary>A YAML node, the file it came from, and how messages name it ("build.stats.grenade").</summary>
internal sealed record YamlValue(string Path, YamlNode Node, string Label);

/// <summary>A YAML mapping, the file it came from, and how messages name it ("weapon").</summary>
internal sealed record YamlMap(string Path, YamlMappingNode Node, string Label);

/// <summary>Reading YAML through YamlDotNet's representation model; its exceptions stop in <see cref="LoadDocument"/>.</summary>
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
        [new ParseError(path, Math.Max(1L, line), message)];

    internal static Result<T, Errors> FailAt<T>(this YamlValue value, string message) =>
        Fail<T>(ToErrorsAt(value.Path, value.Node.Start.Line, message));

    internal static Result<YamlMap, Errors> ToMap(this YamlValue value) =>
        value.Node is YamlMappingNode mapping
            ? Succeed(new YamlMap(value.Path, mapping, value.Label))
            : value.FailAt<YamlMap>($"{value.Label} must be a mapping ({{ key: value }})");

    internal static Result<ImmutableArray<T>, Errors> ReadEach<T>(
        this YamlValue value,
        string itemLabel,
        Func<YamlValue, Result<T, Errors>> read) =>
        value.Node is YamlSequenceNode sequence
            ? sequence.Children.Select(child => read(new YamlValue(value.Path, child, itemLabel))).CollectAll()
            : value.FailAt<ImmutableArray<T>>($"{value.Label} must be a list ([ … ])");

    internal static Result<string, Errors> ToText(this YamlValue value) =>
        value.Node is YamlScalarNode { Value: { } text }
            ? Succeed(text)
            : value.FailAt<string>($"{value.Label} must be a single value, not a list or mapping");

    /// <summary>Parses a scalar with a notation parser, locating its message at the node.</summary>
    internal static Result<T, Errors> ParseWith<T>(this YamlValue value, Func<string, Result<T, string>> parse) =>
        value.ToText().Bind(text => parse(text).MapError(message =>
            ToErrorsAt(value.Path, value.Node.Start.Line, $"{value.Label}: {message}")));

    /// <summary>The value of <paramref name="key"/>; absent when the key is missing or its value is YAML null.</summary>
    internal static Optional<YamlValue> FindValue(this YamlMap map, string key) =>
        ToFirst(map.Node.Children
            .Where(entry => entry.Key is YamlScalarNode { Value: { } name } && name == key && !IsNull(entry.Value))
            .Select(entry => new YamlValue(map.Path, entry.Value, $"{map.Label}.{key}")));

    internal static Result<T, Errors> ReadRequired<T>(this YamlMap map, string key, Func<YamlValue, Result<T, Errors>> read) =>
        map.FindValue(key).Match(
            some => read(some.Value),
            _ => Fail<T>(ToErrorsAt(map.Path, map.Node.Start.Line, $"{map.Label} is missing '{key}'")));

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

    /// <summary>A closed vocabulary word: the camelCase name of an enum member (<c>classAbility</c>).</summary>
    internal static Result<TEnum, Errors> ReadVocabularyWord<TEnum>(YamlValue value)
        where TEnum : struct, Enum =>
        value.ParseWith<TEnum>(text =>
        {
            var matches = Enum.GetValues<TEnum>().Where(candidate => ToVocabularyWord(candidate) == text).ToImmutableArray();
            return matches.Length == 1
                ? new Result<TEnum, string>.Ok(matches[0])
                : new Result<TEnum, string>.Error(
                    $"'{text}' is not one of: {string.Join(", ", Enum.GetValues<TEnum>().Select(ToVocabularyWord))}");
        });

    /// <summary>A Vogen validation outcome as a <see cref="Result{T, TFailure}"/> (no Vogen exception ever escapes).</summary>
    internal static Result<T, string> ToResult<T>(this ValueObjectOrError<T> outcome) =>
        outcome.IsSuccess
            ? new Result<T, string>.Ok(outcome.ValueObject)
            : new Result<T, string>.Error(outcome.Error.ErrorMessage);

    internal static Result<int, string> ParseInteger(string text) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? new Result<int, string>.Ok(number)
            : new Result<int, string>.Error($"'{text}' is not a whole number");

    private static string ToVocabularyWord<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();
        return string.Concat(name[..1].ToLowerInvariant(), name[1..]);
    }

    private static bool IsNull(YamlNode node) =>
        node is YamlScalarNode { Style: ScalarStyle.Plain } scalar && (scalar.Value is null || NullScalars.Contains(scalar.Value));

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

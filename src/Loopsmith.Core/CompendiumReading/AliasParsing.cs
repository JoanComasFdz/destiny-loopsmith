using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Loopsmith.Core.CompendiumReading;

/// <summary>
/// Reads <c>tools/compendium/aliases.yaml</c> into <see cref="CompendiumAliases"/>. Its five lists are optional; every
/// entry needs its keys and a <c>why</c>. The first problem found is the error, naming the list and the entry.
/// </summary>
public static class AliasParsing
{
    public static Result<CompendiumAliases, string> ParseAliases(SourceText file)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(file.Text));
            return stream.Documents.Count == 1 && stream.Documents[0].RootNode is YamlMappingNode root
                ? ReadAliases(file.Path, root)
                : new Result<CompendiumAliases, string>.Error($"{file.Path}: expected one YAML mapping");
        }
        catch (YamlException exception)
        {
            return new Result<CompendiumAliases, string>.Error($"{file.Path}:{exception.Start.Line}: invalid YAML: {exception.Message}");
        }
    }

    private static Result<CompendiumAliases, string> ReadAliases(string path, YamlMappingNode root)
    {
        var unknown = root.Children.Keys.Select(ReadScalar).Except(["prefixes", "suffixes", "dropWords", "names", "worded"]).ToImmutableArray();
        var prefixes = ReadList(path, root, "prefixes", entry => ReadAffix(entry, "prefix"));
        var suffixes = ReadList(path, root, "suffixes", entry => ReadAffix(entry, "suffix"));
        var dropped = ReadList(path, root, "dropWords", ReadDroppedWords);
        var names = ReadList(path, root, "names", ReadEntryName);
        var worded = ReadList(path, root, "worded", ReadWordedNumber);
        ImmutableArray<string> errors = [.. unknown.Select(key => $"{path}: unknown list '{key}'"),
            .. ReadErrors(prefixes), .. ReadErrors(suffixes), .. ReadErrors(dropped), .. ReadErrors(names), .. ReadErrors(worded)];
        return errors.IsEmpty
            ? new Result<CompendiumAliases, string>.Ok(new CompendiumAliases(
                ReadValue(prefixes), ReadValue(suffixes), ReadValue(dropped), ReadValue(names), ReadValue(worded)))
            : new Result<CompendiumAliases, string>.Error(string.Join("\n", errors));
    }

    private static Result<ImmutableArray<T>, string> ReadList<T>(
        string path, YamlMappingNode root, string key, Func<YamlMappingNode, Result<T, string>> read)
    {
        var found = root.Children.TryGetValue(new YamlScalarNode(key), out var node) ? node : new YamlSequenceNode();
        if (found is not YamlSequenceNode sequence)
        {
            return new Result<ImmutableArray<T>, string>.Error($"{path}: '{key}' must be a list");
        }

        var entries = sequence.Children
            .Select(entry => entry is YamlMappingNode map
                ? read(map).Match<Result<T, string>>(ok => ok, error => new Result<T, string>.Error($"{path}:{entry.Start.Line}: {key}: {error.Failure}"))
                : new Result<T, string>.Error($"{path}:{entry.Start.Line}: {key}: each entry must be a mapping"))
            .ToImmutableArray();
        var errors = entries.OfType<Result<T, string>.Error>().Select(error => error.Failure).ToImmutableArray();
        return errors.IsEmpty
            ? new Result<ImmutableArray<T>, string>.Ok([.. entries.OfType<Result<T, string>.Ok>().Select(ok => ok.Value)])
            : new Result<ImmutableArray<T>, string>.Error(string.Join("\n", errors));
    }

    private static Result<NameAffix, string> ReadAffix(YamlMappingNode entry, string textKey) =>
        (ReadText(entry, "tab"), ReadText(entry, textKey), ReadText(entry, "why")) switch
        {
            (Optional<string>.Some tab, Optional<string>.Some text, Optional<string>.Some why) => new Result<NameAffix, string>.Ok(new NameAffix(tab.Value, text.Value, why.Value)),
            _ => new Result<NameAffix, string>.Error($"needs tab, {textKey} and why"),
        };

    private static Result<DroppedWords, string> ReadDroppedWords(YamlMappingNode entry)
    {
        var words = entry.Children.TryGetValue(new YamlScalarNode("words"), out var node) && node is YamlSequenceNode list
            ? list.Children.Select(ReadScalar).Where(word => word.Length > 0).ToImmutableArray()
            : [];
        return (ReadText(entry, "tab"), words.IsEmpty, ReadText(entry, "why")) switch
        {
            (Optional<string>.Some tab, false, Optional<string>.Some why) => new Result<DroppedWords, string>.Ok(new DroppedWords(tab.Value, words, why.Value)),
            _ => new Result<DroppedWords, string>.Error("needs tab, a list of words and why"),
        };
    }

    private static Result<EntryName, string> ReadEntryName(YamlMappingNode entry) =>
        (ReadElement(entry), ReadText(entry, "name"), ReadText(entry, "why")) switch
        {
            (Optional<ElementId>.Some element, Optional<string>.Some name, Optional<string>.Some why) => new Result<EntryName, string>.Ok(new EntryName(element.Value, name.Value, why.Value)),
            _ => new Result<EntryName, string>.Error("needs element (an element id), name and why"),
        };

    private static Result<WordedNumber, string> ReadWordedNumber(YamlMappingNode entry) =>
        (ReadElement(entry), ReadText(entry, "value").Bind(ParseValue), ReadText(entry, "why")) switch
        {
            (Optional<ElementId>.Some element, Optional<decimal>.Some value, Optional<string>.Some why) => new Result<WordedNumber, string>.Ok(new WordedNumber(element.Value, value.Value, why.Value)),
            _ => new Result<WordedNumber, string>.Error("needs element (an element id), value (\"100%\", \"1\") and why"),
        };

    /// <summary>"100%" → 1 (a percent as the rules hold it), "1" → 1.</summary>
    private static Optional<decimal> ParseValue(string text)
    {
        var percent = text.EndsWith('%');
        var parsed = decimal.TryParse(percent ? text[..^1] : text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value);
        return parsed ? Optional.Some(percent ? value / 100m : value) : Optional.None<decimal>();
    }

    private static Optional<ElementId> ReadElement(YamlMappingNode entry) =>
        ReadText(entry, "element").Bind(text => ElementId.TryFrom(text, out var id) ? Optional.Some(id) : Optional.None<ElementId>());

    private static Optional<string> ReadText(YamlMappingNode entry, string key) =>
        entry.Children.TryGetValue(new YamlScalarNode(key), out var node) && ReadScalar(node) is { Length: > 0 } text
            ? Optional.Some(text)
            : Optional.None<string>();

    private static string ReadScalar(YamlNode node) =>
        node is YamlScalarNode { Value: { } value } ? value : "";

    private static IEnumerable<string> ReadErrors<T>(Result<ImmutableArray<T>, string> result) =>
        result.Match<IEnumerable<string>>(_ => [], error => [error.Failure]);

    private static ImmutableArray<T> ReadValue<T>(Result<ImmutableArray<T>, string> result) =>
        result.Match(ok => ok.Value, _ => []);
}

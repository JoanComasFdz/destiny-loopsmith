using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Loopsmith.Core.LoopFiles;

/// <summary>
/// <see cref="LoopDesign"/> → <c>*.loop.yaml</c> text. Pure. Written through YamlDotNet's emitter (never by
/// concatenating strings), so any name, note or description is quoted as YAML needs, and the result
/// round-trips: <c>ParseLoopFile(WriteLoopFile(design))</c> gives back the same design with a
/// byte-identical build text.
/// </summary>
public static class LoopFileWriting
{
    public const string Header = "Loopsmith loop v1";

    /// <summary>No line folding: a long note stays on one line.</summary>
    private static readonly EmitterSettings Settings = new(
        bestIndent: 2,
        bestWidth: int.MaxValue,
        isCanonical: false,
        maxSimpleKeyLength: 1024,
        skipAnchorName: false,
        indentSequences: true,
        newLine: "\n",
        useUtf16SurrogatePairs: false);

    public static string WriteLoopFile(LoopDesign design)
    {
        var events = ListEvents(design);
        return EmitYaml(events);
    }

    /// <summary>Keys in the order of the spec: loop, author, description, catalog, steps, build (last, literal).</summary>
    private static ImmutableArray<ParsingEvent> ListEvents(LoopDesign design) =>
    [
        new StreamStart(),
        new DocumentStart(),
        new Comment(Header, false),
        new MappingStart(null, null, true, MappingStyle.Block),
        .. ToEntry("loop", design.Name, TextKind.Line),
        .. design.Author.Match(author => ToEntry("author", author.Value, TextKind.Line), _ => []),
        .. design.Description.Match(description => ToEntry("description", description.Value, TextKind.Paragraph), _ => []),
        .. design.Catalog.Match(catalog => ToEntry("catalog", catalog.Value.Value, TextKind.Line), _ => []),
        ToKey("steps"),
        .. ListStepEvents(design.Steps),
        .. ToEntry("build", design.Build.Text, TextKind.Document),
        new MappingEnd(),
        new DocumentEnd(true),
        new StreamEnd(),
    ];

    private static ImmutableArray<ParsingEvent> ListStepEvents(ImmutableArray<LoopStep> steps) =>
        steps.IsEmpty
            ? [new SequenceStart(null, null, true, SequenceStyle.Flow), new SequenceEnd()]
            :
            [
                new SequenceStart(null, null, true, SequenceStyle.Block),
                .. steps.SelectMany(step => ToStepEvents(step)),
                new SequenceEnd(),
            ];

    private static ImmutableArray<ParsingEvent> ToStepEvents(LoopStep step) =>
    [
        new MappingStart(null, null, true, MappingStyle.Block),
        .. ToEntry("do", step.Action.ToActionToken(), TextKind.Line),
        .. step.Note.Match(note => ToEntry("note", note.Value, TextKind.Line), _ => []),
        new MappingEnd(),
    ];

    /// <summary>How a value reads best: a short line, free text (literal when multi-line), or a whole embedded file.</summary>
    private enum TextKind { Line, Paragraph, Document }

    private static ImmutableArray<ParsingEvent> ToEntry(string key, string value, TextKind kind) =>
        [ToKey(key), new Scalar(null, null, value, ChooseStyle(value, kind), true, true)];

    private static Scalar ToKey(string key) => new(null, null, key, ScalarStyle.Plain, true, false);

    /// <summary>
    /// The emitter falls back from the requested style whenever YAML needs quoting; double quotes (with escapes)
    /// are forced for text no other style carries exactly: line breaks other than <c>\n</c> (<c>\r</c>, NEL, LS, PS),
    /// tabs and other control characters, a BOM, text made only of line breaks, whitespace-only leading lines, and
    /// the words a plain scalar would read back as null. (Found by the property tests: YamlDotNet's emitter would
    /// otherwise write some of these in a style that reads back differently.)
    /// </summary>
    private static ScalarStyle ChooseStyle(string value, TextKind kind) =>
        !IsSafeWithoutEscapes(value) ? ScalarStyle.DoubleQuoted
        : kind == TextKind.Document || (kind == TextKind.Paragraph && value.Contains('\n')) ? ScalarStyle.Literal
        : ScalarStyle.Any;

    private static bool IsSafeWithoutEscapes(string value) =>
        !YamlReading.NullScalars.Contains(value)
        && value.Any(c => c != '\n')
        && !HasWhitespaceOnlyLeadingLine(value)
        && value.Select((c, index) => IsCarried(value, index)).All(carried => carried);

    private static bool HasWhitespaceOnlyLeadingLine(string value) =>
        value.Split('\n')
            .TakeWhile(line => line.Trim(' ', '\t').Length == 0)
            .Any(line => line.Length > 0)
        && value.Contains('\n');

    private static bool IsCarried(string value, int index)
    {
        var c = value[index];
        return c switch
        {
            '\n' => true,
            '\t' => false,
            _ when IsForeignBreakOrMark(c) => false,
            _ when char.IsHighSurrogate(c) => index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]),
            _ when char.IsLowSurrogate(c) => index > 0 && char.IsHighSurrogate(value[index - 1]),
            _ => !char.IsControl(c),
        };
    }

    /// <summary>NEL, LINE SEPARATOR, PARAGRAPH SEPARATOR (YAML 1.1 line breaks) and the byte-order mark.</summary>
    private static bool IsForeignBreakOrMark(char c) =>
        c is (char)0x85 or (char)0x2028 or (char)0x2029 or (char)0xFEFF;

    private static string EmitYaml(ImmutableArray<ParsingEvent> events)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var emitter = new Emitter(writer, Settings);
        foreach (var parsingEvent in events)
        {
            emitter.Emit(parsingEvent);
        }

        return writer.ToString();
    }
}

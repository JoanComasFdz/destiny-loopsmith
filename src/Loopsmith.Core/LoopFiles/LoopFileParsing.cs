using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using static Loopsmith.Core.LoopFiles.ResultAccumulation;
using static Loopsmith.Core.LoopFiles.YamlReading;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.LoopFiles.ParseError>;

namespace Loopsmith.Core.LoopFiles;

/// <summary>
/// <c>*.loop.yaml</c> (see <c>docs/loop-format.md</c>) → <see cref="LoopDesign"/>. Pure. Only the file's own shape
/// and its action tokens are checked here; the embedded build is parsed and validated by the caller.
/// </summary>
public static class LoopFileParsing
{
    /// <summary>Suffix of the embedded build's path, so its own errors read <c>x.loop.yaml#build:3: …</c>.</summary>
    public const string BuildPathSuffix = "#build";

    internal static readonly ImmutableArray<string> LoopKeys = ["loop", "author", "description", "catalog", "steps", "build"];
    internal static readonly ImmutableArray<string> StepKeys = ["do", "note"];

    /// <summary>Parses one loop file; every problem is reported at once, one <c>file:line: message</c> per line.</summary>
    public static Result<LoopDesign, string> ParseLoopFile(SourceText file) =>
        LoadDocument(file, "loop file")
            .Bind(root => root.ToMap())
            .Bind(ReadLoop)
            .MapError(FormatErrors);

    private static Result<LoopDesign, Errors> ReadLoop(YamlMap map) =>
        Combine(
            map.CheckKeys(LoopKeys),
            map.ReadRequired("loop", value => value.ToText()),
            map.ReadOptional("author", value => value.ToText()),
            map.ReadOptional("description", value => value.ToText()),
            map.ReadOptional("catalog", value => value.ParseWith(text => CatalogVersion.TryFrom(text).ToResult())),
            map.ReadRequired("steps", steps => steps.ReadEach(index => $"step {index + 1}", ReadStep)),
            map.ReadRequired("build", value => value.ToText().Map(text => new SourceText(map.Path + BuildPathSuffix, text))),
            (_, name, author, description, catalog, steps, build) => new LoopDesign(name, author, description, catalog, build, steps));

    private static Result<LoopStep, Errors> ReadStep(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(StepKeys),
            map.ReadRequired("do", action => action.ParseWith(ActionTokenParsing.ParseActionToken)),
            map.ReadOptional("note", note => note.ToText()),
            (_, action, note) => new LoopStep(action, note)));

    private static string FormatErrors(Errors errors) =>
        string.Join(
            "\n",
            errors
                .Distinct()
                .OrderBy(error => error.Line)
                .Select(error => $"{error.Path}:{error.Line}: {error.Message}"));
}

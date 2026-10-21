using System.Collections.Immutable;
using Dunet;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Core;

/// <summary>
/// One entry of <c>scenarios.json</c> (tools/e2e/record.sh): a scenario (its class and method) played on a screen. Its
/// paths are relative to record.sh's output folder: its GIF and MP4 (<c>&lt;domain folder&gt;/&lt;GIF name&gt;.gif</c>) and the
/// flow PNG of the rule its Gherkin names; <see cref="FlowError"/> says why the rule viewer drew none.
/// </summary>
public sealed record ScenarioEntry(
    string Class,
    string Method,
    string Title,
    Optional<string> Gherkin,
    Optional<string> Screen,
    string Gif,
    Optional<string> Mp4,
    Optional<string> Flow,
    Optional<string> Rule,
    Optional<string> FlowError,
    bool Passed,
    RunSelection Selection);

/// <summary>Why record.sh recorded a run: its scenario changed, or only fixture files it uses did (and which).</summary>
[Union]
public partial record RunSelection
{
    partial record Changed();
    partial record Fixtures(ImmutableArray<string> Paths);
}

/// <summary>Which of a run's files are on disk: its GIF, its MP4 and its rule's flow PNG.</summary>
public sealed record RecordedFiles(bool HasGif, bool HasMp4, bool HasFlow);

/// <summary>A run ready to upload and post: its release and the names its GIF, MP4 and flow take there.</summary>
public sealed record PlannedRun(
    ScenarioEntry Entry,
    RecordedFiles Files,
    string Release,
    string GifAsset,
    string Mp4Asset,
    Optional<string> FlowAsset);

using System.Collections.Immutable;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Core;

/// <summary>
/// The record a recorded run writes next to its video (<c>&lt;name&gt;.json</c> beside <c>&lt;name&gt;.webm</c>,
/// tests/Loopsmith.E2E.Shared): its domain folder, scenario, Gherkin, screen, GIF name (without its extension), the
/// second the test saw Home (none when it never did) and whether it passed.
/// </summary>
public sealed record RunRecord(
    string Domain,
    string Class,
    string Method,
    string Title,
    Optional<string> Gherkin,
    string Screen,
    string Gif,
    Optional<double> HomeShownSeconds,
    bool Passed);

/// <summary>An unrecorded run, from the test results: its name ("… [📱 Mobile]"), outcome and time.</summary>
public sealed record TestRun(string Name, string Outcome, double Seconds);

/// <summary>A rule a recorded run's Gherkin names, and the domain folder its flow PNG is drawn in.</summary>
public sealed record FlowToDraw(string Domain, string Rule);

/// <summary>What became of a rule's flow: its PNG is in the domain folder, or the rule viewer's error (none: not drawn).</summary>
public sealed record DrawnFlow(bool HasPng, Optional<string> Error);

/// <summary>The scenario classes record.sh picked with --changed-since: those that changed, and those only a fixture file selected.</summary>
public sealed record ClassSelection(ImmutableHashSet<string> Changed, ImmutableDictionary<string, ImmutableArray<string>> Fixtures);

/// <summary>A file packed into the .vsix: where it is on disk and its name in the zip.</summary>
public sealed record VsixEntry(string From, string Name);

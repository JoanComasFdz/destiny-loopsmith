namespace Loopsmith.E2E.Shared;

/// <summary>
/// What tools/e2e/record.sh reads next to a run's video (each project's BaseE2E writes it, camelCase JSON): the
/// scenario's names and Gherkin, the screen it was played on, when the app's first screen was shown (Home, or VS Code's
/// workbench) and whether it passed. Next to it, <c>&lt;gif&gt;.home.png</c> is that screen as the test saw it: record.sh
/// trims the video from the first frame that matches it (from the noted time when there is none; the video's clock runs behind).
/// </summary>
public sealed record ScenarioRecording(
    string Domain,
    string Class,
    string Method,
    string Title,
    string Gherkin,
    string Screen,
    string Gif,
    double? HomeShownSeconds,
    bool Passed);

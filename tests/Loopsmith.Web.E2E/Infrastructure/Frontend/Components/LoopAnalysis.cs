using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>
/// The "Loop analysis" popup (Components/AnalysisView.razor), opened by "Analyze": its verdict ("✗ Breaks at #1"), the
/// verdict's sentence, and each step of the repeating pass with why its ability wasn't ready or may not be (ADRs D48).
/// </summary>
public sealed class LoopAnalysis(IPage page)
{
    private ILocator Box => page.Locator("[data-testid=loop-analysis]");

    private ILocator Verdict => Box.Locator("[data-testid=verdict]");

    /// <summary>Waits until the popup shows its verdict.</summary>
    public Task WaitUntilShownAsync() => Verdict.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>What the popup shows now.</summary>
    public Task<LoopAnalysisSnapshot> ReadAsync() => OnScreen.ReadAsync(Box, ReadShownAsync);

    private async Task<LoopAnalysisSnapshot> ReadShownAsync()
    {
        var shown = (await Box.EvaluateAsync<JsonElement>(ReadScript)).Deserialize<ShownAnalysis>(ShownJson.Options)!;
        return new LoopAnalysisSnapshot(shown.Verdict, shown.VerdictText, [.. shown.Steps.Select(step => new AnalysedStep(step.Heading, step.NotReady, step.MayNotBeReady))]);
    }

    // The verdict badge, its sentence and each step's heading ("#1 Grenade (💀 kill)") with its "not ready" and "may not be
    // ready" fields (their text after the label), white space collapsed.
    private const string ReadScript = """
        box => {
          const text = el => (el ? el.innerText : '').replace(/\s+/g, ' ').trim();
          const field = (step, id) => { const el = step.querySelector(`[data-testid=${id}] [data-testid=readiness-note]`); return el ? text(el) : null; };
          return {
            verdict: text(box.querySelector('[data-testid=verdict]')),
            verdictText: text(box.querySelector('[data-testid=verdict-text]')),
            steps: [...box.querySelectorAll('[data-testid=analysis-step]')].map(step => ({
              heading: text(step.querySelector("[data-testid=analysis-step-heading]")),
              notReady: field(step, 'not-ready'),
              mayNotBeReady: field(step, 'may-not-be-ready'),
            })),
          };
        }
        """;

    private sealed record ShownAnalysis(string Verdict, string VerdictText, ShownStep[] Steps);

    private sealed record ShownStep(string Heading, string? NotReady, string? MayNotBeReady);
}

/// <summary>What the "Loop analysis" popup shows: its verdict badge, the verdict's sentence and each step.</summary>
public sealed record LoopAnalysisSnapshot(string Verdict, string VerdictText, ImmutableArray<AnalysedStep> Steps);

/// <summary>A step of the analysis ("#1 Grenade (💀 kill)"), with why its ability wasn't ready or may not be (none: it was).</summary>
public sealed record AnalysedStep(string Heading, string? NotReady, string? MayNotBeReady);

using Microsoft.Playwright;

namespace Loopsmith.RuleViewer.E2E.Infrastructure.RulesPage;

/// <summary>
/// The rules page as the user sees it (E2E-TEST-CODE-RULES.md, rule c): its tree of the rules folder (<see cref="Tree"/>)
/// beside every rule's flows (<see cref="Flows"/>), or above them on a phone.
/// </summary>
public sealed class RulesPage(IPage page)
{
    /// <summary>The rules folder as a tree: an arrow, a checkbox and a name per item.</summary>
    public RuleTree Tree { get; } = new(page);

    /// <summary>Every rule's flows, file by file, element by element.</summary>
    public RuleFlows Flows { get; } = new(page);

    /// <summary>Waits until the page shows its tree, has loaded its icons and fonts and has wired its flows.</summary>
    public async Task WaitUntilShownAsync(TimeSpan timeout)
    {
        await page.Locator(".rtree").WaitForAsync(new() { Timeout = (float)timeout.TotalMilliseconds });
        await page.WaitForFunctionAsync(WiredScript, null, new() { Timeout = (float)timeout.TotalMilliseconds });
    }

    /// <summary>
    /// Where the tree and the flows are, and how wide the page is against the window, read where the page is (scrolling it
    /// would move the tree, which stays in view as the flows scroll, away from the flows' top).
    /// </summary>
    public Task<RulesPageLayout> ReadLayoutAsync() => OnScreen.ReadInPlaceAsync(page, ReadLayoutShownAsync);

    private async Task<RulesPageLayout> ReadLayoutShownAsync() =>
        ShownJson.Read<RulesPageLayout>(await page.EvaluateAsync<string>(LayoutScript));

    // The page draws its wires a frame after layout, and again once its fonts are ready: every icon is loaded, the fonts
    // are, and every flow has its arrows.
    private const string WiredScript = """
        () => document.fonts.status === 'loaded'
          && [...document.images].every(image => image.complete && image.naturalWidth > 0)
          && document.querySelectorAll('.flow').length > 0
          && [...document.querySelectorAll('.flow')].every(flow => flow.querySelector('.wires path.arrow'))
        """;

    private const string LayoutScript = """
        () => {
          const box = el => { const r = el.getBoundingClientRect(); return { left: r.left, top: r.top, right: r.right, bottom: r.bottom }; };
          return JSON.stringify({
            tree: box(document.querySelector('.rtree')),
            flows: box(document.querySelector('.rflows')),
            windowWidth: document.documentElement.clientWidth,
            pageWidth: document.documentElement.scrollWidth,
          });
        }
        """;
}

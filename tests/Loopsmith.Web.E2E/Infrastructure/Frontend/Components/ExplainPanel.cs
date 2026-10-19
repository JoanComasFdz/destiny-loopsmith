using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>
/// The "What this build does" panel above the loop (Components/ExplainPanel.razor): a tree, each trigger's heading followed
/// by one row per outcome ("└ Sidearms: … [Mechaneer's Tricksleeves]"), closed until its title is tapped.
/// </summary>
public sealed class ExplainPanel(IPage page)
{
    private ILocator Box => page.Locator("[data-testid=build-explanation]");

    private ILocator Toggle => Box.Locator("[data-action=build-explanation-toggle]");

    private ILocator Lines => Box.Locator("[data-testid=styled-line]");

    private ILocator Choices => Box.GetByRole(AriaRole.Radio);

    /// <summary>Clicks the panel's title, "What this build does", which opens it when it is closed.</summary>
    public Task ClickOpenAsync() => OnScreen.PressAsync(Box, Toggle);

    /// <summary>Waits until the panel shows its lines.</summary>
    public Task WaitUntilOpenAsync() => Lines.First.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>What the panel shows now: its lines, in order, and the choices it offers.</summary>
    public Task<ExplainPanelSnapshot> ReadAsync() => OnScreen.ReadAsync(Box, ReadShownAsync);

    /// <summary>Where the panel's rows are on the screen, how many lines each takes, and how wide the screen and the page are.</summary>
    public Task<ExplainPanelLayout> ReadLayoutAsync() => OnScreen.ReadAsync(Box, ReadLayoutShownAsync);

    private async Task<ExplainPanelSnapshot> ReadShownAsync() =>
        new(
            [.. (await Lines.AllInnerTextsAsync()).Select(line => line.Trim())],
            [.. await Choices.EvaluateAllAsync<string[]>("choices => choices.map(choice => (choice.labels[0]?.innerText ?? '').trim())")]);

    private async Task<ExplainPanelLayout> ReadLayoutShownAsync() =>
        (await Box.EvaluateAsync<JsonElement>(LayoutScript)).Deserialize<ExplainPanelLayout>(ShownJson.Options)!;

    // The panel's box; each line's text (as shown), the box of its text (what is seen of it), how many lines it is drawn on, and whether its last
    // part (a row's "]" after its source) is on its last line; whether the lines scroll sideways inside the panel; the
    // screen's width and the page's (wider when it scrolls sideways).
    private const string LayoutScript = """
        panel => {
          const box = r => ({ left: r.left, top: r.top, right: r.right, bottom: r.bottom });
          const lines = [...panel.querySelectorAll('[data-testid=styled-line]')];
          const scroller = lines.length > 0 ? lines[0].parentElement : panel;
          return {
            panel: box(panel.getBoundingClientRect()),
            lines: lines.map(line => {
              // Only the parts with text: the spaces that align the sources hang past a wrapped line's end, unseen.
              const rects = [...line.children].flatMap(part => [...part.childNodes]).flatMap(text => {
                const start = text.textContent.search(/\S/);
                if (start < 0) return [];
                const range = document.createRange();
                range.setStart(text, start);
                range.setEnd(text, text.textContent.trimEnd().length);
                return [...range.getClientRects()].filter(r => r.width > 0);
              });
              const tops = [...new Set(rects.map(r => Math.round(r.top)))];
              const last = line.lastElementChild ? line.lastElementChild.getBoundingClientRect() : line.getBoundingClientRect();
              const shown = rects.length > 0
                ? { left: Math.min(...rects.map(r => r.left)), top: Math.min(...rects.map(r => r.top)), right: Math.max(...rects.map(r => r.right)), bottom: Math.max(...rects.map(r => r.bottom)) }
                : box(line.getBoundingClientRect());
              return {
                text: line.innerText.trim(),
                textBox: shown,
                lineCount: tops.length,
                endsOnLastLine: tops.length === 0 || Math.round(last.top) >= Math.max(...tops),
              };
            }),
            scrollsSideways: scroller.scrollWidth > scroller.clientWidth + 1,
            screenWidth: window.innerWidth,
            pageWidth: document.documentElement.scrollWidth,
          };
        }
        """;
}

/// <summary>
/// What the "What this build does" panel shows: its lines, in order (a trigger's heading, "Always", then its rows, "└ Sidearms:
/// … [Mechaneer's Tricksleeves]"), and the captions of the choices it offers (none).
/// </summary>
public sealed record ExplainPanelSnapshot(ImmutableArray<string> Lines, ImmutableArray<string> Choices);

/// <summary>Where the panel and its lines are on the screen, whether its lines scroll sideways, and the screen's and the page's width.</summary>
public sealed record ExplainPanelLayout(ScreenBox Panel, ImmutableArray<ExplainLineBox> Lines, bool ScrollsSideways, double ScreenWidth, double PageWidth);

/// <summary>A line of the panel as shown, the box of its text, how many lines it is drawn on, and whether its end is on its last one.</summary>
public sealed record ExplainLineBox(string Text, ScreenBox TextBox, int LineCount, bool EndsOnLastLine);

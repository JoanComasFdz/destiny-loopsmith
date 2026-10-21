using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>
/// The "What this build does" panel above the loop (Components/ExplainPanel.razor), closed until its title is tapped. Its
/// "Triggers" tab is a tree, each trigger's heading followed by one row per outcome ("└ Sidearms: … [Mechaneer's
/// Tricksleeves]"); its "Outcomes" tab lists each outcome ("Jolt · Arc debuff"), each block of ways to it ("Is given by")
/// and their rows ("├ Class ability in the air → Jolt target (10s)  [Ascension]"). A keyword or a source with details is
/// a button: a tap opens its item details popup, the mouse on it its preview.
/// </summary>
public sealed class ExplainPanel(IPage page)
{
    private ILocator Box => page.Locator("[data-testid=build-explanation]");

    private ILocator Toggle => Box.Locator("[data-action=build-explanation-toggle]");

    private ILocator Lines => Box.Locator("[data-testid=styled-line]");

    private ILocator Choices => Box.GetByRole(AriaRole.Radio);

    private ILocator Tabs => Box.GetByRole(AriaRole.Tab);

    private ILocator OutcomesTab => Box.Locator("[data-action=build-explanation-outcomes]");

    /// <summary>Clicks the panel's title, "What this build does", which opens it when it is closed.</summary>
    public Task ClickOpenAsync() => OnScreen.PressAsync(Box, Toggle);

    /// <summary>Clicks the panel's title, "What this build does", which closes it when it is open.</summary>
    public Task ClickCloseAsync() => OnScreen.PressAsync(Box, Toggle);

    /// <summary>Clicks the panel's "Outcomes" tab.</summary>
    public Task ClickOutcomesTabAsync() => OnScreen.PressAsync(Box, OutcomesTab);

    /// <summary>Clicks <paramref name="name"/> in the line <paramref name="line"/> (a heading, or a row as the Gherkin quotes it, without its "├ " and its padding).</summary>
    public Task ClickNameAsync(string line, Keyword name) => ClickNameAsync(line, name.ToShownText());

    /// <summary>Clicks the source <paramref name="name"/> in the row <paramref name="line"/>.</summary>
    public Task ClickNameAsync(string line, Aspect name) => ClickNameAsync(line, name.ToShownText());

    /// <summary>Clicks <paramref name="name"/> in the row <paramref name="line"/> under the outcome <paramref name="outcome"/> ("Jolt · Arc debuff").</summary>
    public async Task ClickNameUnderAsync(string outcome, string line, Keyword name) =>
        await OnScreen.PressAsync(Box, FindName(await FindLineUnderAsync(outcome, line), name.ToShownText()));

    /// <summary>Moves the mouse onto <paramref name="name"/> in the line <paramref name="line"/>.</summary>
    public async Task HoverNameAsync(string line, Keyword name) =>
        await OnScreen.ActAsync(Box, FindName(await FindLineAsync(line), name.ToShownText()), target => target.HoverAsync());

    /// <summary>Waits until a name's preview shows.</summary>
    public Task WaitUntilPreviewShownAsync() => Preview.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>The preview a name shows: its title, its box and the name's, and the screen's size.</summary>
    public Task<ItemPreviewLayout> ReadPreviewLayoutAsync() => OnScreen.ReadAsync(Box, ReadPreviewShownAsync);

    private ILocator Preview => Box.Locator("[data-testid=item-preview]");

    private async Task ClickNameAsync(string line, string name) =>
        await OnScreen.PressAsync(Box, FindName(await FindLineAsync(line), name));

    private static ILocator FindName(ILocator line, string name) =>
        line.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    private async Task<ILocator> FindLineAsync(string line) =>
        Lines.Nth(FindIndex(await ReadLineTextsAsync(), line, 0));

    private async Task<ILocator> FindLineUnderAsync(string outcome, string line)
    {
        var texts = await ReadLineTextsAsync();
        return Lines.Nth(FindIndex(texts, line, FindIndex(texts, outcome, 0) + 1));
    }

    private async Task<string[]> ReadLineTextsAsync() => [.. (await Lines.AllInnerTextsAsync()).Select(ReadLineText)];

    private static int FindIndex(string[] texts, string line, int from)
    {
        var index = Array.FindIndex(texts, from, text => text == line);
        return index >= 0 ? index : throw new InvalidOperationException($"The panel shows no line \"{line}\" (it shows {string.Join(", ", texts.Select(text => $"\"{text}\""))}).");
    }

    // "  ├ Class ability in the air → Jolt target (10s)  [Ascension]" → "Class ability in the air → Jolt target (10s) [Ascension]".
    private static string ReadLineText(string line) =>
        Regex.Replace(Regex.Replace(line.Trim(), "^[├└] ", ""), "\\s+", " ");

    private async Task<ItemPreviewLayout> ReadPreviewShownAsync() =>
        (await Preview.EvaluateAsync<JsonElement>(PreviewScript)).Deserialize<ItemPreviewLayout>(ShownJson.Options)!;

    // The preview's title, its box and the box of the name it is for (the button beside it), and the screen's size.
    private const string PreviewScript = """
        preview => {
          const box = r => ({ left: r.left, top: r.top, right: r.right, bottom: r.bottom });
          return {
            title: preview.querySelector('[data-testid=item-preview-name]')?.innerText.trim() ?? '',
            box: box(preview.getBoundingClientRect()),
            name: box(preview.parentElement.querySelector('[data-action=explanation-open-details]').getBoundingClientRect()),
            screenWidth: window.innerWidth,
            screenHeight: window.innerHeight,
          };
        }
        """;

    /// <summary>Waits until the panel's "Outcomes" tab is selected.</summary>
    public Task WaitUntilOutcomesShownAsync() =>
        Box.GetByRole(AriaRole.Tab, new() { Name = "Outcomes", Selected = true }).WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>Waits until the panel is closed: its lines are hidden.</summary>
    public Task WaitUntilClosedAsync() => Lines.First.WaitForAsync(new() { State = WaitForSelectorState.Hidden });

    /// <summary>Waits until the panel shows its lines.</summary>
    public Task WaitUntilOpenAsync() => Lines.First.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>What the panel shows now: its lines, in order, the choices it offers and its tabs.</summary>
    public Task<ExplainPanelSnapshot> ReadAsync() => OnScreen.ReadAsync(Box, ReadShownAsync);

    /// <summary>Where the panel's rows are on the screen, how many lines each takes, and how wide the screen and the page are.</summary>
    public Task<ExplainPanelLayout> ReadLayoutAsync() => OnScreen.ReadAsync(Box, ReadLayoutShownAsync);

    private async Task<ExplainPanelSnapshot> ReadShownAsync() =>
        new(
            await Box.EvaluateAsync<bool>("panel => panel.open"),
            [.. (await Lines.AllInnerTextsAsync()).Select(line => line.Trim())],
            [.. await Choices.EvaluateAllAsync<string[]>("choices => choices.map(choice => (choice.labels[0]?.innerText ?? '').trim())")],
            [.. (await Tabs.EvaluateAllAsync<JsonElement>(TabsScript)).Deserialize<ShownTab[]>(ShownJson.Options)!]);

    private async Task<ExplainPanelLayout> ReadLayoutShownAsync() =>
        (await Box.EvaluateAsync<JsonElement>(LayoutScript)).Deserialize<ExplainPanelLayout>(ShownJson.Options)!;

    // Each tab's name and whether it is selected.
    private const string TabsScript = """
        tabs => tabs.map(tab => ({ name: tab.innerText.trim(), selected: tab.getAttribute('aria-selected') === 'true' }))
        """;

    // The panel's box; each tab's name and box; each line's text (as shown), the box of its text (what is seen of it), how many lines it is drawn on, and whether its last
    // part (a row's "]" after its source) is on its last line; whether the lines scroll sideways inside the panel; the
    // screen's width and the page's (wider when it scrolls sideways).
    private const string LayoutScript = """
        panel => {
          const box = r => ({ left: r.left, top: r.top, right: r.right, bottom: r.bottom });
          const lines = [...panel.querySelectorAll('[data-testid=styled-line]')];
          const scroller = lines.length > 0 ? lines[0].parentElement : panel;
          return {
            panel: box(panel.getBoundingClientRect()),
            tabs: [...panel.querySelectorAll('[role=tab]')].map(tab => ({ name: tab.innerText.trim(), box: box(tab.getBoundingClientRect()) })),
            lines: lines.map(line => {
              // Only the parts with text: the spaces that align the sources hang past a wrapped line's end, unseen.
              // Its texts, a name's inside its button too (a name's preview, when it shows, isn't the line's).
              const walker = document.createTreeWalker(line, NodeFilter.SHOW_TEXT);
              const texts = [];
              for (let node = walker.nextNode(); node; node = walker.nextNode()) {
                if (!node.parentElement.closest('[data-testid=item-preview]')) texts.push(node);
              }
              const rects = texts.flatMap(text => {
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
/// … [Mechaneer's Tricksleeves]"), the captions of the choices it offers (none), and its tabs; whether it is open.
/// </summary>
public sealed record ExplainPanelSnapshot(bool Open, ImmutableArray<string> Lines, ImmutableArray<string> Choices, ImmutableArray<ShownTab> Tabs);

/// <summary>A tab of the panel ("Triggers") and whether it is selected.</summary>
public sealed record ShownTab(string Name, bool Selected);

/// <summary>Where the panel, its tabs and its lines are on the screen, whether its lines scroll sideways, and the screen's and the page's width.</summary>
public sealed record ExplainPanelLayout(ScreenBox Panel, ImmutableArray<ExplainTabBox> Tabs, ImmutableArray<ExplainLineBox> Lines, bool ScrollsSideways, double ScreenWidth, double PageWidth);

/// <summary>A tab of the panel ("Outcomes") and its box.</summary>
public sealed record ExplainTabBox(string Name, ScreenBox Box);

/// <summary>A line of the panel as shown, the box of its text, how many lines it is drawn on, and whether its end is on its last one.</summary>
public sealed record ExplainLineBox(string Text, ScreenBox TextBox, int LineCount, bool EndsOnLastLine);

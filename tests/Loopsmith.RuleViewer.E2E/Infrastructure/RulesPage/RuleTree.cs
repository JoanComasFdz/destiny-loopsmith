using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Loopsmith.RuleViewer.E2E.Infrastructure.RulesPage;

/// <summary>
/// The rules folder as a tree, like a folder explorer: each item (a folder, a file, an element, a rule) is a row with an
/// arrow that folds it (a rule has none), a checkbox that shows or hides its flows and a name that selects it. An item is
/// found by the name its row shows ("arc/", "melees.yaml", "Combination Blow", "combination-blow-1").
/// </summary>
public sealed class RuleTree(IPage page)
{
    private ILocator Box => page.Locator(".rtree");

    /// <summary>Taps the item's arrow: it folds, or opens.</summary>
    public async Task ClickArrowAsync(string name)
    {
        await OnScreen.PressAsync(Box, FindRow(name).Locator(".twisty"));
        await WaitForTheNextFramesAsync();
    }

    /// <summary>Ticks the item's checkbox; the page redraws its wires on the next frame.</summary>
    public Task TickAsync(string name) => PressCheckboxAsync(name);

    /// <summary>Unticks the item's checkbox; the page redraws its wires on the next frame.</summary>
    public Task UntickAsync(string name) => PressCheckboxAsync(name);

    /// <summary>Taps the item's name: it is selected and the page scrolls (smoothly) to its flows, until it stops.</summary>
    public async Task ClickNameAsync(string name)
    {
        await OnScreen.PressAsync(Box, FindRow(name).Locator(".tname"));
        await page.EvaluateAsync(SettledScript);
    }

    /// <summary>Every item of the tree, in its order, with what its row shows.</summary>
    public Task<RuleTreeSnapshot> ReadAsync() => OnScreen.ReadAsync(Box, ReadShownAsync);

    private async Task PressCheckboxAsync(string name)
    {
        await OnScreen.PressAsync(Box, FindRow(name).Locator("input[type=checkbox]"));
        await WaitForTheNextFramesAsync();
    }

    private async Task<RuleTreeSnapshot> ReadShownAsync() =>
        new(ShownJson.Read<TreeItemShown[]>(await page.EvaluateAsync<string>(ReadScript)));

    private ILocator FindRow(string name) =>
        Box.Locator(".trow").Filter(new() { Has = page.Locator(".tname > span", new() { HasTextRegex = new Regex($"^{Regex.Escape(name)}$") }) });

    // The page shows or hides flows at once and wires them on the next animation frame.
    private Task WaitForTheNextFramesAsync() =>
        page.EvaluateAsync("() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done)))");

    // Waits until the window has stopped scrolling for five frames in a row (at most 3 s), counting only once it has moved
    // or ten frames have gone by, so a smooth scroll that starts a frame late isn't taken for one that is over.
    private const string SettledScript = """
        async () => {
          const from = scrollY;
          let last = NaN, still = 0, frames = 0;
          const until = performance.now() + 3000;
          while (still < 5 && performance.now() < until) {
            await new Promise(requestAnimationFrame);
            frames++;
            const started = scrollY !== from || frames > 10;
            still = started && scrollY === last ? still + 1 : 0;
            last = scrollY;
          }
        }
        """;

    private const string ReadScript = """
        () => {
          const nameOf = item => item.querySelector(':scope > .trow > .tname > span').textContent;
          return JSON.stringify([...document.querySelectorAll('.rtree .titem')].map(item => {
            const row = item.querySelector(':scope > .trow'), box = row.querySelector('input');
            const ancestors = [];
            for (let up = item.parentElement.closest('.titem'); up; up = up.parentElement.closest('.titem')) ancestors.push(nameOf(up));
            return {
              name: nameOf(item),
              kind: ['folder', 'file', 'element', 'rule'].find(kind => item.classList.contains(kind)),
              ancestors,
              shown: row.checkVisibility(),
              open: item.hasAttribute('aria-expanded') ? item.getAttribute('aria-expanded') === 'true' : null,
              ticked: box.checked && !box.indeterminate,
              dashed: box.indeterminate,
              selected: item.getAttribute('aria-selected') === 'true',
            };
          }));
        }
        """;
}

using Microsoft.Playwright;

namespace Loopsmith.RuleViewer.E2E.Infrastructure.RulesPage;

/// <summary>
/// Every rule's flows, beside the tree: a part per file, per element and per rule, each found by the name its tree item
/// shows ("melees.yaml", "Combination Blow", "combination-blow-1"). A flow is a row of boxes joined by wires the page
/// draws from where the boxes landed.
/// </summary>
public sealed class RuleFlows(IPage page)
{
    /// <summary>Every part, after bringing the part named <paramref name="shown"/> into view (the one the check is about).</summary>
    public async Task<RuleFlowsSnapshot> ReadAsync(string shown) => await OnScreen.ReadAsync(await FindPartAsync(shown), ReadShownAsync);

    /// <summary>Every part, read where the page is, scrolling nothing: where a name's flows were brought to.</summary>
    public Task<RuleFlowsSnapshot> ReadInPlaceAsync() => OnScreen.ReadInPlaceAsync(page, ReadShownAsync);

    private async Task<RuleFlowsSnapshot> ReadShownAsync() =>
        new(ShownJson.Read<FlowPartShown[]>(await page.EvaluateAsync<string>(ReadScript)));

    // A part is the flows' section carrying its tree item's data-item ("element:combination-blow").
    private async Task<ILocator> FindPartAsync(string name)
    {
        var item = await page.EvaluateAsync<string>(ItemScript, name);
        Assert.True(item.Length > 0, $"The tree has no item named \"{name}\".");
        return page.Locator($".rflows [data-item=\"{item.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"]");
    }

    private const string ItemScript = """
        name => [...document.querySelectorAll('.rtree .titem')]
          .find(item => item.querySelector(':scope > .trow > .tname > span').textContent === name)?.dataset.item ?? ''
        """;

    // Each tree item's part: whether it shows, where its top is, and whether every flow in it is wired: an arrow ends at
    // each result box's left edge, and at the first condition's when it has conditions (so the wires match where the
    // boxes are now, not where they were).
    private const string ReadScript = """
        () => {
          const wired = flow => {
            const o = flow.getBoundingClientRect();
            const ends = [...flow.querySelectorAll('.wires path.arrow')].map(path => {
              const n = path.getAttribute('d').match(/-?[\d.]+/g).map(Number);
              return [n[n.length - 2], n[n.length - 1]];
            });
            const reached = el => {
              const r = el.getBoundingClientRect(), x = r.left - o.left - 2, y = (r.top + r.bottom) / 2 - o.top;
              return ends.some(([ex, ey]) => Math.abs(ex - x) < 3 && Math.abs(ey - y) < 3);
            };
            const results = [...flow.querySelectorAll('.rcol .rbox')], conditions = [...flow.querySelectorAll('.ccol .rbox')];
            return results.length > 0 && results.every(reached) && (conditions.length === 0 || reached(conditions[0]));
          };
          return JSON.stringify([...document.querySelectorAll('.rtree .titem')].flatMap(item => {
            const part = document.querySelector(`.rflows [data-item="${CSS.escape(item.dataset.item)}"]`);
            if (!part) return [];
            const flows = [...part.querySelectorAll('.flow')];
            const shown = part.checkVisibility();
            return [{
              name: item.querySelector(':scope > .trow > .tname > span').textContent,
              shown,
              top: part.getBoundingClientRect().top,
              wired: shown && flows.length > 0 && flows.every(flow => flow.checkVisibility() ? wired(flow) : true) && flows.some(flow => flow.checkVisibility()),
            }];
          }));
        }
        """;
}

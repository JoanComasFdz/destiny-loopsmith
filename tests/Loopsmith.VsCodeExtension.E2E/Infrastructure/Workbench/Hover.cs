using System.Collections.Immutable;
using Microsoft.Playwright;

namespace Loopsmith.VsCodeExtension.E2E.Infrastructure.Workbench;

/// <summary>The hover VS Code shows on a name in the editor: the extension's Markdown, drawn by VS Code.</summary>
public sealed class Hover(IPage page)
{
    private ILocator Shown => page.Locator(VsCodeDom.Hover).Filter(new() { HasText = "▶" });

    /// <summary>Waits until the hover shows, its GIFs loaded.</summary>
    public async Task WaitUntilShownAsync()
    {
        await Shown.WaitForAsync();
        await Shown.EvaluateAsync("hover => Promise.all([...hover.querySelectorAll('img')].map(img => img.decode().catch(() => null)))");
    }

    /// <summary>Clicks the hover's link <paramref name="link"/> ("▶ Play mobile MP4"), then moves the mouse off it, so it closes.</summary>
    public async Task ClickLinkAsync(string link)
    {
        await OnScreen.ClickAsync(page, Shown.Locator("a").Filter(new() { HasText = link }).First);
        await OnScreen.MoveAwayAsync(page);
    }

    /// <summary>What the hover shows: its title, its lines, each GIF with its label and where it is, and its links.</summary>
    public Task<HoverSnapshot> ReadAsync() => OnScreen.ReadAsync(page, ReadShownAsync);

    private async Task<HoverSnapshot> ReadShownAsync() =>
        ShownJson.Read<HoverSnapshot>(await Shown.EvaluateAsync<string>(ReadScript));

    // The title is the first bold text; the lines are the paragraphs; the GIFs are the images of the table's first row,
    // their labels its second row's cells; a link's target is its data-href (VS Code keeps a command link's there).
    private const string ReadScript = """
        hover => {
          const box = el => { const r = el.getBoundingClientRect(); return { left: r.left, top: r.top, right: r.right, bottom: r.bottom }; };
          const asset = src => decodeURIComponent((src ?? '').split('/').pop());
          const labels = [...hover.querySelectorAll('tr:nth-child(2) td')].map(td => td.textContent.trim());
          return JSON.stringify({
            title: hover.querySelector('strong')?.textContent.trim() ?? '',
            lines: [...hover.querySelectorAll('p')].map(p => p.textContent.trim()).filter(text => text.length > 0),
            gifs: [...hover.querySelectorAll('tr:first-child td')].map((td, at) => {
              const img = td.querySelector('img');
              return { asset: img ? asset(img.getAttribute('src')) : '', label: labels[at] ?? '', box: box(img ?? td) };
            }),
            links: [...hover.querySelectorAll('a')].map(a => ({ text: a.textContent.trim(), target: a.getAttribute('data-href') ?? a.getAttribute('href') ?? '' })),
          });
        }
        """;
}

/// <summary>What the hover shows.</summary>
public sealed record HoverSnapshot(string Title, ImmutableArray<string> Lines, ImmutableArray<HoverGif> Gifs, ImmutableArray<HoverLink> Links);

/// <summary>A GIF of the hover: its asset's name, its label and where it is (CSS pixels).</summary>
public sealed record HoverGif(string Asset, string Label, ShownBox Box);

/// <summary>A link of the hover: its text and its target.</summary>
public sealed record HoverLink(string Text, string Target);

/// <summary>Where a part is on the screen, in CSS pixels.</summary>
public sealed record ShownBox(double Left, double Top, double Right, double Bottom);

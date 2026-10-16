using System.Collections.Immutable;
using Loopsmith.VsCodeExtension.E2E.Infrastructure.Vocabulary;
using Microsoft.Playwright;

namespace Loopsmith.VsCodeExtension.E2E.Infrastructure.Workbench;

/// <summary>The scenario file's editor: its lines, and the CodeLens rows VS Code draws above them.</summary>
public sealed class CodeEditor(IPage page)
{
    private const string Reading = "Reading the e2e recordings";

    private ILocator Editor => page.Locator(VsCodeDom.CodeEditor);

    private ILocator CodeLens => Editor.Locator(VsCodeDom.CodeLens);

    /// <summary>Waits until the CodeLens shows what the extension read of the release (no longer "Reading…").</summary>
    public async Task WaitUntilCodeLensReadAsync()
    {
        await CodeLens.First.WaitForAsync();
        await Editor.Locator(VsCodeDom.CodeLens, new() { HasNotText = Reading }).First.WaitForAsync();
        await Editor.Locator(VsCodeDom.CodeLens, new() { HasText = Reading }).WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    /// <summary>Waits until a CodeLens item reads <paramref name="item"/>.</summary>
    public Task WaitUntilCodeLensReadsAsync(string item) =>
        CodeLens.Locator("a, span", new() { HasText = item }).First.WaitForAsync();

    /// <summary>Clicks the CodeLens item <paramref name="item"/> ("Play desktop recording").</summary>
    public Task ClickCodeLensAsync(string item) =>
        OnScreen.ClickAsync(page, CodeLens.Locator("a").Filter(new() { HasText = item }).First);

    /// <summary>Rests the mouse on the scenario's method name, where VS Code shows its hover.</summary>
    public async Task HoverMethodNameAsync(ScenarioFile file)
    {
        var line = Editor.Locator(".view-line").Filter(new() { HasText = $"Task {file.ToMethod()}(" });
        var point = ShownJson.Read<ShownPoint>(await line.EvaluateAsync<string>(FindTextScript, file.ToMethod()));
        await OnScreen.HoverAtAsync(page, point.X, point.Y);
    }

    /// <summary>Each CodeLens row the editor shows, with its items and the line right below it.</summary>
    public Task<CodeLensSnapshot> ReadCodeLensAsync() => OnScreen.ReadAsync(page, ReadShownAsync);

    private async Task<CodeLensSnapshot> ReadShownAsync()
    {
        var json = await Editor.EvaluateAsync<string>(ReadScript, new { codeLens = VsCodeDom.CodeLens, lines = ".view-line" });
        return new CodeLensSnapshot([.. ShownJson.Read<CodeLensRow[]>(json)]);
    }

    // The middle of the first place the line shows the text: VS Code draws a line as spans of tokens, and a name can
    // share its span with what is around it.
    private const string FindTextScript = """
        (line, text) => {
          const walker = document.createTreeWalker(line, NodeFilter.SHOW_TEXT);
          const nodes = [];
          for (let node = walker.nextNode(); node; node = walker.nextNode()) nodes.push(node);
          const shown = nodes.map(node => node.textContent).join('');
          const at = shown.indexOf(text);
          const middle = at + Math.floor(text.length / 2);
          let start = 0;
          for (const node of nodes) {
            if (middle < start + node.textContent.length) {
              const range = document.createRange();
              range.setStart(node, middle - start);
              range.setEnd(node, middle - start + 1);
              const box = range.getBoundingClientRect();
              return JSON.stringify({ x: box.left + box.width / 2, y: box.top + box.height / 2 });
            }
            start += node.textContent.length;
          }
          return JSON.stringify({ x: 0, y: 0 });
        }
        """;

    // Each row's items (VS Code draws each as a link, or a span when it runs no command; their icons hold no text; the
    // "|" between them is a span of its own), and the text of the first line drawn below the row.
    private const string ReadScript = """
        (editor, { codeLens, lines }) => {
          const drawn = [...editor.querySelectorAll(lines)].map(line => ({ top: line.getBoundingClientRect().top, text: line.textContent.replace(/ /g, ' ').trim() }));
          return JSON.stringify([...editor.querySelectorAll(codeLens)].map(row => {
            const bottom = row.getBoundingClientRect().bottom;
            const below = drawn.filter(line => line.top >= bottom - 2).sort((a, b) => a.top - b.top)[0];
            return {
              items: [...row.children].map(item => item.textContent.replace(/ /g, ' ').trim()).filter(text => text.length > 0 && text !== '|'),
              lineBelow: below ? below.text : '',
            };
          }));
        }
        """;
}

/// <summary>A point on the screen, in CSS pixels.</summary>
public sealed record ShownPoint(double X, double Y);

/// <summary>The CodeLens rows the editor shows.</summary>
public sealed record CodeLensSnapshot(ImmutableArray<CodeLensRow> Rows);

/// <summary>One CodeLens row: its items, in order, and the line right below it.</summary>
public sealed record CodeLensRow(ImmutableArray<string> Items, string LineBelow);

using Loopsmith.VsCodeExtension.E2E.Infrastructure.Vocabulary;
using Microsoft.Playwright;

namespace Loopsmith.VsCodeExtension.E2E.Infrastructure.Workbench;

/// <summary>The Explorer view: the workspace's folders and files, as a tree.</summary>
public sealed class Explorer(IPage page)
{
    /// <summary>Opens <paramref name="file"/> in the editor: a click on each folder of its path, then on the file.</summary>
    public async Task OpenAsync(ScenarioFile file)
    {
        foreach (var name in file.ToPath().Split('/'))
        {
            await OnScreen.ClickAsync(page, page.GetByRole(AriaRole.Treeitem, new() { Name = name, Exact = true }));
        }

        await page.Locator(VsCodeDom.EditorTab, new() { HasText = file.ToFileName() }).WaitForAsync();
        await page.Locator(VsCodeDom.CodeEditor).Locator(VsCodeDom.Lines).WaitForAsync();
    }
}

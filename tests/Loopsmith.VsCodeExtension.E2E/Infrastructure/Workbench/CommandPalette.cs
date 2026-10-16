using Microsoft.Playwright;

namespace Loopsmith.VsCodeExtension.E2E.Infrastructure.Workbench;

/// <summary>The Command Palette: F1, a command's title, Enter.</summary>
public sealed class CommandPalette(IPage page)
{
    private ILocator Widget => page.Locator(VsCodeDom.QuickInput);

    /// <summary>Runs the command titled <paramref name="title"/> from the Command Palette.</summary>
    public async Task RunAsync(string title)
    {
        await OnScreen.KeyAsync(page, () => page.Keyboard.PressAsync("F1"));
        await Widget.Locator("input").WaitForAsync();
        await OnScreen.KeyAsync(page, () => page.Keyboard.TypeAsync(title, new() { Delay = 40 }));
        await Widget.GetByRole(AriaRole.Option, new() { Name = title }).First.WaitForAsync();
        await OnScreen.KeyAsync(page, () => page.Keyboard.PressAsync("Enter"));
        await Widget.Locator("input").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }
}

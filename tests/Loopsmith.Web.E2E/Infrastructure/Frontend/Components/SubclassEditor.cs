using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>The subclass editor (Components/SubclassEditor.razor), opened by ⋯ on the Subclass box.</summary>
public sealed class SubclassEditor(IPage page)
{
    private ILocator Editor => page.Locator("[data-testid=subclass-editor]");

    private ILocator OverflowNote => Editor.Locator("[data-testid=fragment-overflow]");

    /// <summary>Waits until the subclass editor is open.</summary>
    public Task WaitUntilShownAsync() => Editor.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>What the subclass editor shows now.</summary>
    public Task<SubclassEditorSnapshot> ReadAsync() => OnScreen.ReadAsync(Editor, ReadShownAsync);

    private async Task<SubclassEditorSnapshot> ReadShownAsync() =>
        new(await OverflowNote.CountAsync() > 0 ? (await OverflowNote.InnerTextAsync()).Trim() : "");
}

/// <summary>What the subclass editor shows: the warning under Fragments when the build holds more than its aspects open (empty when none).</summary>
public sealed record SubclassEditorSnapshot(string FragmentWarning);

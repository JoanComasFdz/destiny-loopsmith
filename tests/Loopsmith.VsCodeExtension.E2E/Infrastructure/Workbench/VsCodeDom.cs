namespace Loopsmith.VsCodeExtension.E2E.Infrastructure.Workbench;

/// <summary>
/// The parts of VS Code's own page the page objects find that have no role and accessible name to find them by
/// (E2E-TEST-CODE-RULES.md, rule c): VS Code's class names, all in this one place, so a VS Code update that renames one
/// changes one line. The extension's own panel is found by its data-testid and data-action attributes instead.
/// </summary>
public static class VsCodeDom
{
    /// <summary>The workbench, once VS Code has drawn it.</summary>
    public const string Workbench = ".monaco-workbench";

    /// <summary>The text editor of the scenario file (the editor group's editor; a webview holds none).</summary>
    public const string CodeEditor = ".editor-instance .monaco-editor";

    /// <summary>The editor's lines of code.</summary>
    public const string Lines = ".view-lines";

    /// <summary>A row of CodeLens items above a line.</summary>
    public const string CodeLens = ".codelens-decoration";

    /// <summary>A hover that shows (VS Code keeps hidden ones in the page).</summary>
    public const string Hover = ".monaco-hover:not(.hidden)";

    /// <summary>An editor tab (the file's, the panel's).</summary>
    public const string EditorTab = ".tabs-container .tab";

    /// <summary>A webview's frame (the panel), drawn over its editor group, and the frame inside it that holds its page.</summary>
    public const string Webview = "iframe.webview.ready";

    /// <summary>The frame of a webview that holds the extension's page.</summary>
    public const string WebviewContent = "#active-frame";

    /// <summary>The Command Palette's input and its list of commands.</summary>
    public const string QuickInput = ".quick-input-widget";
}

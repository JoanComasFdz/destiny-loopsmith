using System.Collections.Immutable;

namespace Loopsmith.E2ETools.Core;

/// <summary>
/// The .vsix of the VS Code extension, laid out as Loopsmith.Web.csproj's PublishVsCodeExtension target lays out the site's:
/// extension/ with package.json, extension.js, README.md and the repository's LICENSE as LICENSE.txt; then
/// extension.vsixmanifest and [Content_Types].xml at its root.
/// </summary>
public static class VsixPacking
{
    public static ImmutableArray<VsixEntry> PlanEntries(string source, string license) =>
        [
            .. new[] { "package.json", "extension.js", "README.md" }.Select(name => new VsixEntry($"{source}/{name}", $"extension/{name}")),
            new VsixEntry(license, "extension/LICENSE.txt"),
            new VsixEntry($"{source}/vsix/extension.vsixmanifest", "extension.vsixmanifest"),
            new VsixEntry($"{source}/vsix/Content_Types.xml", "[Content_Types].xml"),
        ];
}

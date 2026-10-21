using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;

namespace Loopsmith.RuleViewer.Core;

/// <summary>
/// Draws every rule in the rules folder as a tree laid out like the folder (ADRs D40): its folders and files by name, folders
/// first; in a file, its elements in the order the file has them; in an element, its rules then its passives; each rule
/// with its flows. A weapon perk's enhanced trait is an element of its own, after its perk, with its perk's rules and
/// their names. Every element is drawn as itself (<see cref="RuleFlowPicking.DrawElement"/>): what a rule needs from the
/// build stays as written ("Weapon of your Super's element"). An element with no rule or passive isn't in the tree, nor is
/// a file or folder with none. Pure: same rules ⇒ same tree.
/// </summary>
public static class RuleTreeDrawing
{
    public const string Title = "Loopsmith rules";

    public static RuleTree DrawTree(RuleCatalog catalog)
    {
        var files = catalog.Elements.Values
            .DistinctBy(element => element.Id)
            .Where(element => element.Rules.Length + element.Passives.Length > 0)
            .GroupBy(element => element.RuleFile)
            .Select(file => DrawFile(catalog, file.Key, [.. file]))
            .ToImmutableArray();
        return new RuleTree(Title, GatherFolder("", "rules", files));
    }

    private static RuleFile DrawFile(RuleCatalog catalog, string path, ImmutableArray<BuildElement> elements) =>
        new(
            path,
            path[(path.LastIndexOf('/') + 1)..],
            [.. elements
                .OrderBy(FindLine)
                .ThenBy(element => element.Id.Value, StringComparer.Ordinal)
                .Select(element => DrawElement(catalog, element))]);

    private static RuleElement DrawElement(RuleCatalog catalog, BuildElement element)
    {
        var drawn = RuleFlowPicking.DrawElement(catalog, element);
        return new RuleElement(element.Id.Value, drawn.Box, drawn.Rules);
    }

    /// <summary>Every file of a folder, in the tree's order: each of its folders' in turn, then its own.</summary>
    public static IEnumerable<RuleFile> ListFiles(RuleFolder folder) =>
        folder.Folders.SelectMany(ListFiles).Concat(folder.Files);

    /// <summary>Every flow of the tree, in its order.</summary>
    public static IEnumerable<RuleFlow> ListFlows(RuleTree tree) =>
        ListFiles(tree.Root).SelectMany(file => file.Elements).SelectMany(element => element.Rules).SelectMany(rule => rule.Flows);

    /// <summary>The line an element starts on in its file; an element no file holds (a test's) comes first.</summary>
    private static int FindLine(BuildElement element) =>
        element.Source is Provenance.Authored authored ? authored.Line : 0;

    /// <summary>A folder: the files right in it, and a folder for each first segment of the deeper files' paths, both by name.</summary>
    private static RuleFolder GatherFolder(string path, string name, ImmutableArray<RuleFile> files)
    {
        var prefix = path.Length == 0 ? "" : path + "/";
        var folders = files
            .Where(file => file.Path[prefix.Length..].Contains('/'))
            .GroupBy(file => file.Path[prefix.Length..].Split('/')[0])
            .OrderBy(folder => folder.Key, StringComparer.Ordinal)
            .Select(folder => GatherFolder(prefix + folder.Key, folder.Key, [.. folder]));
        var own = files
            .Where(file => !file.Path[prefix.Length..].Contains('/'))
            .OrderBy(file => file.Name, StringComparer.Ordinal);
        return new RuleFolder(path, name, [.. folders], [.. own]);
    }
}

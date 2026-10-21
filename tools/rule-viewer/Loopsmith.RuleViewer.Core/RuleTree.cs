using System.Collections.Immutable;

namespace Loopsmith.RuleViewer.Core;

/// <summary>
/// A rule or passive of an element: its full name (<c>hunter-arc-melees-combination-blow-2</c>, ADRs D40), its id within its
/// element (<c>combination-blow-2</c>) and its flows, one per claim (several when its trigger names several).
/// </summary>
public sealed record RuleEntry(string Name, string Id, ImmutableArray<RuleFlow> Flows);

/// <summary>
/// An element of a rule file: its id, its box (kind title, name, colour and icon, as its flows draw it "equipped") and its
/// rules, then its passives, in the file's order.
/// </summary>
public sealed record RuleElement(string Id, RuleBox Box, ImmutableArray<RuleEntry> Rules);

/// <summary>A rule file: its path under the rules folder (<c>hunter/arc/melees.yaml</c>), its name (<c>melees.yaml</c>) and its elements, in the file's order.</summary>
public sealed record RuleFile(string Path, string Name, ImmutableArray<RuleElement> Elements);

/// <summary>A folder of the rules: its path (empty for the rules folder itself), its name, then its folders and its files, each by name.</summary>
public sealed record RuleFolder(string Path, string Name, ImmutableArray<RuleFolder> Folders, ImmutableArray<RuleFile> Files);

/// <summary>Every rule in the rules folder, as it is laid out: folders, files, elements, rules, each rule with its flows.</summary>
public sealed record RuleTree(string Title, RuleFolder Root);

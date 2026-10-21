namespace Loopsmith.RuleViewer.E2E.Infrastructure.RulesPage;

/// <summary>
/// A part of the flows (a file's, an element's or a rule's) as shown: the name of its tree item, whether it shows, where
/// its top is (CSS pixels from the window's top) and whether every flow of it that shows is wired to where its boxes are.
/// </summary>
public sealed record FlowPartShown(string Name, bool Shown, double Top, bool Wired);

/// <summary>The flows as shown: a part per file, element and rule of the tree, in its order.</summary>
public sealed record RuleFlowsSnapshot(FlowPartShown[] Parts)
{
    /// <summary>The part of the tree item named <paramref name="name"/>.</summary>
    public FlowPartShown Find(string name) =>
        Parts.SingleOrDefault(part => part.Name == name) ?? throw new Xunit.Sdk.XunitException($"No flows belong to a tree item named \"{name}\".");
}

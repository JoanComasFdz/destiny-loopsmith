using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the subclass editor.</summary>
public static class SubclassEditorSnapshotAssertions
{
    /// <summary>The warning under Fragments reads <paramref name="expected"/>.</summary>
    public static AssertingThat<SubclassEditorSnapshot> Warns(this AssertingThat<SubclassEditorSnapshot> that, string expected)
    {
        var shown = that.InstanceToAssert.FragmentWarning;
        Assert.True(
            shown == expected,
            shown.Length == 0
                ? $"The subclass editor shows no warning, but \"{expected}\" was expected."
                : $"The subclass editor warns \"{shown}\", but \"{expected}\" was expected.");
        return that;
    }
}

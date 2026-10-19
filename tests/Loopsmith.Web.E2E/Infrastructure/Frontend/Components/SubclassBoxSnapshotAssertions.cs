using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the Subclass box.</summary>
public static class SubclassBoxSnapshotAssertions
{
    /// <summary>The build is of <paramref name="expected"/> class.</summary>
    public static AssertingThat<SubclassBoxSnapshot> HasClass(this AssertingThat<SubclassBoxSnapshot> that, GuardianClass expected)
    {
        var shown = that.InstanceToAssert.Class;
        Assert.True(shown == expected, $"The Subclass box shows a {shown.ToShownText()}, but a {expected.ToShownText()} was expected.");
        return that;
    }

    /// <summary>The build's subclass is <paramref name="expected"/>.</summary>
    public static AssertingThat<SubclassBoxSnapshot> HasSubclass(this AssertingThat<SubclassBoxSnapshot> that, Subclass expected)
    {
        var shown = that.InstanceToAssert.Subclass;
        Assert.True(shown == expected, $"The Subclass box shows the {shown.ToShownText()} subclass, but {expected.ToShownText()} was expected.");
        return that;
    }

    /// <summary>The box shows exactly the fragments <paramref name="expected"/>, in that order.</summary>
    public static AssertingThat<SubclassBoxSnapshot> ShowsFragments(this AssertingThat<SubclassBoxSnapshot> that, params Fragment[] expected)
    {
        var shown = that.InstanceToAssert.Fragments;
        Assert.True(
            shown.SequenceEqual(expected),
            $"The Subclass box shows the fragments {Describe(shown.Select(fragment => fragment.ToShownText()))}, but {Describe(expected.Select(fragment => fragment.ToShownText()))} was expected.");
        return that;
    }

    /// <summary>The box shows no aspect.</summary>
    public static AssertingThat<SubclassBoxSnapshot> ShowsNoAspect(this AssertingThat<SubclassBoxSnapshot> that)
    {
        var shown = that.InstanceToAssert.Aspects;
        Assert.True(shown.IsEmpty, $"The Subclass box shows the aspects {Describe(shown.Select(aspect => aspect.ToShownText()))}, but no aspect was expected.");
        return that;
    }

    private static string Describe(IEnumerable<string> names)
    {
        var quoted = names.Select(name => $"\"{name}\"").ToArray();
        return quoted.Length == 0 ? "none" : string.Join(", ", quoted);
    }
}

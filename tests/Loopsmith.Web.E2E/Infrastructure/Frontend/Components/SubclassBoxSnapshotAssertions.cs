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
}

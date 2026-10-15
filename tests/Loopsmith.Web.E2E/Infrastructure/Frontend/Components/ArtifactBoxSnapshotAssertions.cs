using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the Artifact box.</summary>
public static class ArtifactBoxSnapshotAssertions
{
    /// <summary>The build has no artifact.</summary>
    public static AssertingThat<ArtifactBoxSnapshot> IsNotSet(this AssertingThat<ArtifactBoxSnapshot> that)
    {
        var shown = that.InstanceToAssert.Artifact;
        Assert.True(shown is null, $"The Artifact box shows {shown?.ToShownText()}, but no artifact was expected.");
        return that;
    }
}

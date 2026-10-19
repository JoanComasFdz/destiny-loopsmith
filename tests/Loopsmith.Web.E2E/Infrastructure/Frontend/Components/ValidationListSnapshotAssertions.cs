using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the Validation list.</summary>
public static class ValidationListSnapshotAssertions
{
    /// <summary>One of the list's warnings (!) reads <paramref name="expected"/>.</summary>
    public static AssertingThat<ValidationListSnapshot> ShowsWarning(this AssertingThat<ValidationListSnapshot> that, string expected)
    {
        var lines = that.InstanceToAssert.Lines;
        var shown = lines.IsEmpty ? "nothing" : string.Join(", ", lines.Select(line => $"{(line.IsWarning ? "!" : "i")} \"{line.Text}\""));
        Assert.True(lines.Any(line => line.IsWarning && line.Text == expected), $"The Validation list shows {shown}, but the warning \"{expected}\" was expected.");
        return that;
    }
}

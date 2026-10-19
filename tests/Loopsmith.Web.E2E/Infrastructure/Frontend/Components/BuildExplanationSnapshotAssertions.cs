using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the "What this build does" panel.</summary>
public static class BuildExplanationSnapshotAssertions
{
    private const string Arrow = " -> ";

    /// <summary>
    /// The panel's <paramref name="trigger"/> line ("Always") shows <paramref name="effect"/> among what it sets off, each
    /// effect with its source in brackets ("Sidearms: +50 Airborne Effectiveness … [Mechaneer's Tricksleeves]").
    /// </summary>
    public static AssertingThat<BuildExplanationSnapshot> Shows(this AssertingThat<BuildExplanationSnapshot> that, string trigger, string effect)
    {
        var lines = that.InstanceToAssert.Lines.Where(line => line.StartsWith(trigger + Arrow, StringComparison.Ordinal)).ToArray();
        Assert.True(lines.Length == 1, $"The panel shows the lines {Describe(that.InstanceToAssert.Lines)}, but one \"{trigger}\" line was expected.");
        var effects = ReadEffects(lines[0][(trigger.Length + Arrow.Length)..]);
        Assert.True(effects.Contains(effect), $"The \"{trigger}\" line shows {Describe(effects)}, but \"{effect}\" was expected among them.");
        return that;
    }

    /// <summary>"A [x] + +1 B [y]" → "A [x]", "+1 B [y]": a line's effects, each ending with its source.</summary>
    private static string[] ReadEffects(string effects)
    {
        var parts = effects.Split("] + ");
        return [.. parts.Select((effect, index) => index < parts.Length - 1 ? effect + "]" : effect)];
    }

    private static string Describe(IEnumerable<string> texts)
    {
        var listed = string.Join(", ", texts.Select(text => $"\"{text}\""));
        return listed.Length == 0 ? "none" : listed;
    }
}

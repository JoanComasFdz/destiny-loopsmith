using System.Text.RegularExpressions;
using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the "What this build does" panel.</summary>
public static class ExplainPanelSnapshotAssertions
{
    /// <summary>
    /// The panel's <paramref name="trigger"/> heading ("Always") is followed by <paramref name="row"/>, exactly as shown, among
    /// its rows ("├ Sidearms: … Reload Speed  [Mechaneer's Tricksleeves]", the branch and the alignment included).
    /// </summary>
    public static AssertingThat<ExplainPanelSnapshot> HasRowUnder(this AssertingThat<ExplainPanelSnapshot> that, string trigger, string row)
    {
        var rows = ReadRows(that.InstanceToAssert, trigger);
        Assert.True(rows.Contains(row), $"Under \"{trigger}\" the panel shows {Describe(rows)}, but \"{row}\" was expected among them.");
        return that;
    }

    /// <summary>
    /// The panel's <paramref name="trigger"/> heading ("Always") is followed by a row reading <paramref name="effect"/> with its
    /// source ("Sidearms: … Reload Speed [Mechaneer's Tricksleeves]"), the row's branch and alignment left out.
    /// </summary>
    public static AssertingThat<ExplainPanelSnapshot> Shows(this AssertingThat<ExplainPanelSnapshot> that, string trigger, string effect)
    {
        var effects = ReadRows(that.InstanceToAssert, trigger).Select(ReadEffect).ToArray();
        Assert.True(effects.Contains(effect), $"Under \"{trigger}\" the panel shows {Describe(effects)}, but \"{effect}\" was expected among them.");
        return that;
    }

    /// <summary>Under the panel's <paramref name="trigger"/> heading a row's source is <paramref name="source"/> ("[Bolt Charge]").</summary>
    public static AssertingThat<ExplainPanelSnapshot> HasRowFromUnder(this AssertingThat<ExplainPanelSnapshot> that, string trigger, Keyword source)
    {
        var rows = ReadRows(that.InstanceToAssert, trigger);
        Assert.True(rows.Any(row => IsFrom(row, source)), $"Under \"{trigger}\" the panel shows {Describe(rows)}, but a row from \"[{source.ToShownText()}]\" was expected among them.");
        return that;
    }

    /// <summary>No row of the panel, under any heading, has one of <paramref name="sources"/> as its source ("[Jolt]").</summary>
    public static AssertingThat<ExplainPanelSnapshot> HasNoRowFrom(this AssertingThat<ExplainPanelSnapshot> that, params Keyword[] sources)
    {
        var found = that.InstanceToAssert.Lines.Where(IsRow).Where(row => sources.Any(source => IsFrom(row, source))).ToArray();
        Assert.True(found.Length == 0, $"The panel shows {Describe(found)}, but no row from {Describe(sources.Select(source => $"[{source.ToShownText()}]"))} was expected.");
        return that;
    }

    /// <summary>The panel offers no choice of how it is laid out ("Note", "Tree"): it is always a tree.</summary>
    public static AssertingThat<ExplainPanelSnapshot> OffersNoStyleChoice(this AssertingThat<ExplainPanelSnapshot> that)
    {
        Assert.True(that.InstanceToAssert.Choices.IsEmpty, $"The panel offers {Describe(that.InstanceToAssert.Choices)}, but no choice was expected.");
        return that;
    }

    // The rows under a trigger's heading: the lines after it that start with a branch ("├ ", "└ "), until the next heading.
    private static string[] ReadRows(ExplainPanelSnapshot shown, string trigger)
    {
        var lines = shown.Lines;
        var heading = lines.IndexOf(trigger);
        Assert.True(heading >= 0, $"The panel shows the lines {Describe(lines)}, but a \"{trigger}\" heading was expected.");
        return [.. lines.Skip(heading + 1).TakeWhile(IsRow)];
    }

    // A row's source closes it: "└ +2.5% melee energy  [Bolt Charge]".
    private static bool IsFrom(string row, Keyword source) =>
        row.EndsWith($"[{source.ToShownText()}]", StringComparison.Ordinal);

    private static bool IsRow(string line) =>
        line.StartsWith("├ ", StringComparison.Ordinal) || line.StartsWith("└ ", StringComparison.Ordinal);

    // "└ Sidearms: …  [Mechaneer's Tricksleeves]" → "Sidearms: … [Mechaneer's Tricksleeves]".
    private static string ReadEffect(string row) =>
        Regex.Replace(row[2..], " {2,}", " ");

    private static string Describe(IEnumerable<string> texts)
    {
        var listed = string.Join(", ", texts.Select(text => $"\"{text}\""));
        return listed.Length == 0 ? "none" : listed;
    }
}

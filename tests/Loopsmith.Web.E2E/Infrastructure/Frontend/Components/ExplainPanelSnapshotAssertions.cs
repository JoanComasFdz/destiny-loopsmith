using System.Collections.Immutable;
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

    /// <summary>The panel shows exactly the tabs <paramref name="tabs"/>, in order.</summary>
    public static AssertingThat<ExplainPanelSnapshot> HasTabs(this AssertingThat<ExplainPanelSnapshot> that, params string[] tabs)
    {
        var shown = that.InstanceToAssert.Tabs;
        Assert.True(shown.Select(tab => tab.Name).SequenceEqual(tabs), $"The panel shows the tabs {Describe(shown.Select(tab => tab.Name))}, but {Describe(tabs)} were expected.");
        return that;
    }

    /// <summary>The panel is open: it shows its tabs and its lines.</summary>
    public static AssertingThat<ExplainPanelSnapshot> IsOpen(this AssertingThat<ExplainPanelSnapshot> that)
    {
        Assert.True(that.InstanceToAssert.Open, "\"What this build does\" is closed, but it was expected to be open.");
        return that;
    }

    /// <summary>The panel's <paramref name="tab"/> tab is the one selected.</summary>
    public static AssertingThat<ExplainPanelSnapshot> HasSelectedTab(this AssertingThat<ExplainPanelSnapshot> that, string tab)
    {
        var selected = that.InstanceToAssert.Tabs.Where(shown => shown.Selected).Select(shown => shown.Name).ToArray();
        Assert.True(selected.SequenceEqual([tab]), $"The panel's selected tab is {Describe(selected)}, but \"{tab}\" was expected.");
        return that;
    }

    /// <summary>
    /// Under the Outcomes tab's <paramref name="outcome"/> ("Jolt · Arc debuff"), its <paramref name="block"/> ("Is given by")
    /// lists exactly <paramref name="ways"/>, in order, each read with its source ("Class ability in the air → Jolt target
    /// (10s) [Ascension]"), the row's branch and alignment left out.
    /// </summary>
    public static AssertingThat<ExplainPanelSnapshot> ListsOnlyUnder(this AssertingThat<ExplainPanelSnapshot> that, string outcome, string block, params string[] ways)
    {
        var shown = ReadBlockRows(that.InstanceToAssert, outcome, block).Select(ReadEffect).ToArray();
        Assert.True(shown.SequenceEqual(ways), $"Under \"{outcome}\", \"{block}\" lists {Describe(shown)}, but only {Describe(ways)} were expected.");
        return that;
    }

    /// <summary>The Outcomes tab lists exactly the outcomes <paramref name="outcomes"/> ("Jolt · Arc debuff"), in this order.</summary>
    public static AssertingThat<ExplainPanelSnapshot> HasOutcomesInOrder(this AssertingThat<ExplainPanelSnapshot> that, params string[] outcomes)
    {
        var lines = that.InstanceToAssert.Lines;
        var shown = lines.Where((_, index) => IsOutcome(lines, index)).ToArray();
        Assert.True(shown.SequenceEqual(outcomes), $"The Outcomes tab lists {Describe(shown)}, but {Describe(outcomes)} were expected, in that order.");
        return that;
    }

    // The rows of an outcome's block: after the outcome's line, the block's label among its blocks, then its rows.
    private static string[] ReadBlockRows(ExplainPanelSnapshot shown, string outcome, string block)
    {
        var lines = shown.Lines;
        var heading = lines.IndexOf(outcome);
        Assert.True(heading >= 0 && IsOutcome(lines, heading), $"The panel shows the lines {Describe(lines)}, but an outcome \"{outcome}\" was expected.");
        var blocks = lines.Skip(heading + 1).TakeWhile((_, offset) => !IsOutcome(lines, heading + 1 + offset)).ToArray();
        var label = Array.IndexOf(blocks, block);
        Assert.True(label >= 0, $"Under \"{outcome}\" the panel shows {Describe(blocks.Where(line => !IsRow(line)))}, but \"{block}\" was expected among them.");
        return [.. blocks.Skip(label + 1).TakeWhile(IsRow)];
    }

    // An outcome's line ("Jolt · Arc debuff") is followed by a block's label ("Is given by"), which is followed by its rows.
    private static bool IsOutcome(ImmutableArray<string> lines, int index) =>
        index + 2 < lines.Length && !IsRow(lines[index]) && !IsRow(lines[index + 1]) && IsRow(lines[index + 2]);

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

using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Web.Hosting;

namespace Loopsmith.Web.State;

/// <summary>Where a loop offered for comparison comes from; the pickers group their options by it.</summary>
public enum ChoiceOrigin { Current, Example, Imported }

/// <summary>A loop imported on the Compare screen: held in memory for this tab only, never stored.</summary>
public sealed record ImportedLoop(string Id, DesignSession Session);

/// <summary>A loop that can be picked for a side of the comparison: opened, or why it cannot be.</summary>
public sealed record ComparisonChoice(string Id, string Label, ChoiceOrigin Origin, Result<DesignSession, string> Design);

/// <summary>The loop each side shows, by <see cref="ComparisonChoice.Id"/>.</summary>
public sealed record ComparisonPicks(string LeftId, string RightId);

/// <summary>One side of a comparison: the loop (or why it cannot be opened) and its analysis.</summary>
public sealed record ComparedLoop(string Id, Result<DesignSession, string> Design, Optional<LoopReport> Report);

/// <summary>Two picked loops, analysed; the comparison exists only when both sides open.</summary>
public sealed record ComparisonView(ComparedLoop Left, ComparedLoop Right, Optional<LoopComparison> Comparison);

/// <summary>
/// Pure: what each side of the comparison can show (the open design, the bundled example loops, loops imported
/// this session), which two are shown, and the comparison itself (<see cref="LoopDesigning.CompareLoops"/>).
/// </summary>
public static class ComparisonPicking
{
    public const string CurrentDesignId = "current";
    private const string ExamplePrefix = "example:";
    private const string ImportedPrefix = "imported:";

    public static ImmutableArray<ComparisonChoice> ListChoices(
        LoopsmithBundle bundle, Optional<DesignSession> current, ImmutableArray<ImportedLoop> imported) =>
    [
        .. current.Match(
            open => [new ComparisonChoice(CurrentDesignId, $"{open.Value.Design.Name} (in the designer)", ChoiceOrigin.Current, ToOpened(open.Value))],
            _ => ImmutableArray<ComparisonChoice>.Empty),
        .. bundle.Loops.Select(DescribeExample),
        .. imported.Select(loop => new ComparisonChoice(loop.Id, $"{loop.Session.Design.Name} (imported)", ChoiceOrigin.Imported, ToOpened(loop.Session))),
    ];

    /// <summary>
    /// The previous picks that are still offered; a side whose loop is gone (or that was never picked) falls back
    /// to the first choice, the right side to the first one other than the left. None when nothing is offered.
    /// </summary>
    public static Optional<ComparisonPicks> PickIds(ImmutableArray<ComparisonChoice> choices, Optional<ComparisonPicks> previous)
    {
        if (choices.IsEmpty)
        {
            return Optional.None<ComparisonPicks>();
        }

        var left = previous.Bind(picks => FindChoice(choices, picks.LeftId)).Map(choice => choice.Id).UnwrapOr(choices[0].Id);
        var other = choices.FirstOrDefault(choice => choice.Id != left, choices[0]).Id;
        var right = previous.Bind(picks => FindChoice(choices, picks.RightId)).Map(choice => choice.Id).UnwrapOr(other);
        return Optional.Some(new ComparisonPicks(left, right));
    }

    /// <summary>The imported loops plus <paramref name="session"/>, and the id it was given.</summary>
    public static (ImmutableArray<ImportedLoop> Imported, string Id) AddImportedLoop(ImmutableArray<ImportedLoop> imported, DesignSession session)
    {
        var id = $"{ImportedPrefix}{imported.Length + 1}";
        return (imported.Add(new ImportedLoop(id, session)), id);
    }

    public static ComparisonView CompareChoices(ImmutableArray<ComparisonChoice> choices, ComparisonPicks picks)
    {
        var left = AnalyzeChoice(choices, picks.LeftId);
        var right = AnalyzeChoice(choices, picks.RightId);
        var comparison = left.Report.Bind(l => right.Report.Map(r => LoopDesigning.CompareLoops(l, r)));
        return new ComparisonView(left, right, comparison);
    }

    private static ComparedLoop AnalyzeChoice(ImmutableArray<ComparisonChoice> choices, string id)
    {
        var design = FindChoice(choices, id)
            .ToResult(() => "This loop is no longer offered.")
            .Bind(choice => choice.Design);
        var report = design.Match(
            ok => Optional.Some(LoopDesigning.AnalyzeDesign(ok.Value, LoopDesigning.DefaultMaxCycles)),
            _ => Optional.None<LoopReport>());
        return new ComparedLoop(id, design, report);
    }

    private static Optional<ComparisonChoice> FindChoice(ImmutableArray<ComparisonChoice> choices, string id) =>
        Optional.FromNullable(choices.FirstOrDefault(choice => choice.Id == id));

    private static ComparisonChoice DescribeExample(BundledLoop loop) =>
        new(
            ExamplePrefix + loop.File.Path,
            loop.Opened.Match(ok => $"{ok.Value.Design.Name} (example)", _ => $"{loop.File.Path.Split('/')[^1]} (does not import)"),
            ChoiceOrigin.Example,
            loop.Opened);

    private static Result<DesignSession, string> ToOpened(DesignSession session) =>
        new Result<DesignSession, string>.Ok(session);
}

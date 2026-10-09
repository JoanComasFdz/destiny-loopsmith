using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Web.Hosting;

namespace Loopsmith.Web.State;

/// <summary>One side of a comparison: the loop (or why it cannot be opened) and its analysis.</summary>
public sealed record ComparedLoop(string Id, Result<DesignSession, string> Design, Optional<LoopReport> Report);

/// <summary>Two picked loops, analysed; the comparison exists only when both sides open.</summary>
public sealed record ComparisonView(ComparedLoop Left, ComparedLoop Right, Optional<LoopComparison> Comparison);

/// <summary>A loop that can be picked for comparison: the open design or a library entry.</summary>
public sealed record ComparisonChoice(string Id, string Label);

/// <summary>Pure: open both picked loops, analyse each (<see cref="LoopDesigning.AnalyzeDesign"/>), compare them.</summary>
public static class EntryComparing
{
    public static ImmutableArray<ComparisonChoice> ListChoices(Optional<DesignSession> current, ImmutableArray<LibraryEntry> entries) =>
    [
        .. current.Match(
            open => [new ComparisonChoice(LibraryListing.CurrentDesignId, $"Current design — {open.Value.Design.Name}")],
            _ => ImmutableArray<ComparisonChoice>.Empty),
        .. entries.Select(entry => new ComparisonChoice(entry.Id, entry.Origin == LibraryOrigin.Bundled ? $"{entry.Name} (example)" : entry.Name)),
    ];

    public static ComparisonView CompareEntries(
        LoopsmithBundle bundle, Optional<DesignSession> current, ImmutableArray<LibraryEntry> entries, string leftId, string rightId)
    {
        var left = OpenChoice(bundle, current, entries, leftId);
        var right = OpenChoice(bundle, current, entries, rightId);
        var comparison = left.Report.Bind(l => right.Report.Map(r => LoopDesigning.CompareLoops(l, r)));
        return new ComparisonView(left, right, comparison);
    }

    private static ComparedLoop OpenChoice(LoopsmithBundle bundle, Optional<DesignSession> current, ImmutableArray<LibraryEntry> entries, string id)
    {
        var design = id == LibraryListing.CurrentDesignId
            ? current.ToResult(() => "There is no design open.")
            : LibraryListing.FindEntry(entries, id)
                .ToResult(() => "This loop is no longer in the library.")
                .Bind(entry => LibraryListing.OpenEntry(bundle.Catalog, entry));
        var report = design.Match(
            ok => Optional.Some(LoopDesigning.AnalyzeDesign(ok.Value, LoopDesigning.DefaultMaxCycles)),
            _ => Optional.None<LoopReport>());
        return new ComparedLoop(id, design, report);
    }
}

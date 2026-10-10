using System.Collections.Immutable;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Web.Hosting;

namespace Loopsmith.Web.State;

/// <summary>
/// UI state shared by the pages (one per browser tab, in memory only — nothing is stored in the browser): the
/// startup bundle, the loop being designed, what is selected, and what the Compare screen shows. It holds
/// immutable values only; every edit goes through the pure Orchestration API and the result is stored back here.
/// </summary>
public sealed class Workbench(Result<LoopsmithBundle, string> startup, bool shareLinksWork)
{
    public Result<LoopsmithBundle, string> Startup { get; } = startup;

    /// <summary>The startup self-check of the share-link codec passed.</summary>
    public bool ShareLinksWork { get; } = shareLinksWork;

    public Optional<DesignSession> Session { get; set; } = Optional.None<DesignSession>();

    /// <summary>The selected timeline card: 0 = fresh spawn, k = step #k. None = follow the end of the loop.</summary>
    public Optional<int> SelectedStep { get; set; } = Optional.None<int>();

    /// <summary>A message for the banner (an import that failed, a link that could not be opened).</summary>
    public Optional<string> Notice { get; set; } = Optional.None<string>();

    /// <summary>The designer's status line: what was just opened or exported (it survives going from Home to the designer).</summary>
    public string Status { get; set; } = "";

    /// <summary>Loops imported on the Compare screen (kept until the tab closes).</summary>
    public ImmutableArray<ImportedLoop> ImportedLoops { get; set; } = [];

    /// <summary>The loops the Compare screen showed last (None until it first opens).</summary>
    public Optional<ComparisonPicks> ComparePicks { get; set; } = Optional.None<ComparisonPicks>();

    /// <summary>Open a design: it becomes the current one, nothing is selected.</summary>
    public void OpenSession(DesignSession session)
    {
        Session = Optional.Some(session);
        SelectedStep = Optional.None<int>();
    }
}

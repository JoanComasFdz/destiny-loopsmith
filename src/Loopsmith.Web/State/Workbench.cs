using System.Collections.Immutable;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Web.Hosting;

namespace Loopsmith.Web.State;

/// <summary>
/// UI state shared by the pages (one per browser tab): the startup bundle, the loop being designed, what is
/// selected, and the saved library. It holds immutable values only; every edit goes through the pure
/// Orchestration API and the result is stored back here.
/// </summary>
public sealed class Workbench(Result<LoopsmithBundle, string> startup, bool shareLinksWork)
{
    public Result<LoopsmithBundle, string> Startup { get; } = startup;

    /// <summary>The startup self-check of the share-link codec passed.</summary>
    public bool ShareLinksWork { get; } = shareLinksWork;

    public Optional<DesignSession> Session { get; set; } = Optional.None<DesignSession>();

    /// <summary>The selected timeline card: 0 = fresh spawn, k = step #k. None = follow the end of the loop.</summary>
    public Optional<int> SelectedStep { get; set; } = Optional.None<int>();

    /// <summary>The saved loop the design was opened from or last saved as (saving again replaces it).</summary>
    public Optional<string> SavedId { get; set; } = Optional.None<string>();

    /// <summary>A message for the designer's banner (an import that failed, a link that could not be opened).</summary>
    public Optional<string> Notice { get; set; } = Optional.None<string>();

    public ImmutableArray<SavedLoop> Library { get; set; } = [];

    public bool IsLibraryLoaded { get; set; }

    public Optional<string> LibraryProblem { get; set; } = Optional.None<string>();

    /// <summary>Library entry ids picked for comparison (at most two).</summary>
    public ImmutableArray<string> CompareIds { get; set; } = [];

    /// <summary>Open a design: it becomes the current one, nothing is selected.</summary>
    public void OpenSession(DesignSession session, Optional<string> savedId)
    {
        Session = Optional.Some(session);
        SelectedStep = Optional.None<int>();
        SavedId = savedId;
    }
}

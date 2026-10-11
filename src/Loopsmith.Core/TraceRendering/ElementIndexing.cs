using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>Pure: which element a keyword is (Bolt Charge, Jolt, Orb of Power), when the catalog has one — the kernel's lookups.</summary>
internal static class ElementIndexing
{
    internal static Optional<ElementId> FindKeywordElement(this RuleCatalog catalog, KeywordRef keyword) =>
        keyword.Match(
            status => catalog.FindStatusElement(status.Id),
            pickup => catalog.FindPickupElement(pickup.Id),
            summon => catalog.FindSummonElement(summon.Id));
}

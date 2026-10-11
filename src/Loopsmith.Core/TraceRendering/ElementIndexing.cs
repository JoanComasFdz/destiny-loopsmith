using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>The catalog's elements by their id's text: a status or pickup is the element of the same id, when there is one.</summary>
internal sealed record ElementIndex(ImmutableDictionary<string, ElementId> ById);

/// <summary>Pure: which element a keyword is (Bolt Charge, Jolt, Orb of Power), when the catalog has one of its id.</summary>
internal static class ElementIndexing
{
    internal static ElementIndex IndexElements(RuleCatalog catalog) =>
        new(catalog.Elements.Keys.ToImmutableDictionary(id => id.Value, id => id));

    internal static Optional<ElementId> FindKeywordElement(this ElementIndex elements, KeywordRef keyword) =>
        elements.FindElement(keyword.DescribeKeywordId());

    private static Optional<ElementId> FindElement(this ElementIndex elements, string id) =>
        elements.ById.TryGetValue(id, out var element) ? Optional.Some(element) : Optional.None<ElementId>();
}

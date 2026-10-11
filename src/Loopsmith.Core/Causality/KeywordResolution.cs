using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Causality;

/// <summary>
/// Kernel: facts about keywords the slices rely on — what damage type a keyword strike or a summon deals, how a
/// pickup or a status is found, and which element a keyword is — so all read the same answer.
/// </summary>
public static class KeywordResolution
{
    public static DamageType ResolveStrikeDamageType(this KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition) ? definition.Affinity.ToDamageType() : DamageType.Kinetic;

    public static DamageType ResolveSummonDamageType(this KeywordGlossary glossary, SummonId summon) =>
        glossary.Summons.TryGetValue(summon, out var definition) ? definition.DamageType : DamageType.Kinetic;

    public static bool IsCollectedAutomatically(this KeywordGlossary glossary, PickupId pickup) =>
        glossary.Pickups.TryGetValue(pickup, out var definition) && definition.CollectsAutomatically;

    public static Optional<StatusDefinition> FindStatus(this KeywordGlossary glossary, StatusId status) =>
        Optional.FromNullable(glossary.Statuses.GetValueOrDefault(status));

    /// <summary>The element a status is (Bolt Charge, Woven Mail: the element of its id), when the catalog has one.</summary>
    public static Optional<ElementId> FindStatusElement(this RuleCatalog catalog, StatusId status) =>
        catalog.FindElementOfId(status.Value);

    public static Optional<ElementId> FindPickupElement(this RuleCatalog catalog, PickupId pickup) =>
        catalog.FindElementOfId(pickup.Value);

    public static Optional<ElementId> FindSummonElement(this RuleCatalog catalog, SummonId summon) =>
        catalog.FindElementOfId(summon.Value);

    /// <summary>The status an element is (the bolt-charge element: Bolt Charge), when the glossary has one of its id.</summary>
    public static Optional<StatusId> FindElementStatus(this RuleCatalog catalog, ElementId element) =>
        catalog.Glossary.Statuses.Keys.Where(status => element.IsStatusElement(status)).Take(1).Select(status => Optional.Some(status)).FindFirstSome();

    /// <summary>The pickup an element is (the ionic-trace element: Ionic Trace), when the glossary has one of its id.</summary>
    public static Optional<PickupId> FindElementPickup(this RuleCatalog catalog, ElementId element) =>
        catalog.Glossary.Pickups.Keys.Where(pickup => IsKeywordElement(element, pickup.Value)).Take(1).Select(pickup => Optional.Some(pickup)).FindFirstSome();

    /// <summary>The summon an element is, when the glossary has one of its id.</summary>
    public static Optional<SummonId> FindElementSummon(this RuleCatalog catalog, ElementId element) =>
        catalog.Glossary.Summons.Keys.Where(summon => IsKeywordElement(element, summon.Value)).Take(1).Select(summon => Optional.Some(summon)).FindFirstSome();

    /// <summary>The element a keyword is (Bolt Charge, Ionic Trace, Threadling), when the catalog has one.</summary>
    public static Optional<ElementId> FindKeywordElement(this RuleCatalog catalog, KeywordRef keyword) =>
        keyword.Match(
            status => catalog.FindStatusElement(status.Id),
            pickup => catalog.FindPickupElement(pickup.Id),
            summon => catalog.FindSummonElement(summon.Id));

    /// <summary>The keyword an element is (the bolt-charge element: Bolt Charge), when the glossary has one of its id.</summary>
    public static Optional<KeywordRef> FindElementKeyword(this RuleCatalog catalog, ElementId element) =>
        new[]
        {
            catalog.FindElementStatus(element).Map(status => (KeywordRef)new KeywordRef.Status(status)),
            catalog.FindElementPickup(element).Map(pickup => (KeywordRef)new KeywordRef.Pickup(pickup)),
            catalog.FindElementSummon(element).Map(summon => (KeywordRef)new KeywordRef.Summon(summon)),
        }.FindFirstSome();

    /// <summary>The one rule both lookups follow: a keyword's element is the element of its id — its own effects.</summary>
    public static bool IsStatusElement(this ElementId element, StatusId status) =>
        IsKeywordElement(element, status.Value);

    private static Optional<ElementId> FindElementOfId(this RuleCatalog catalog, string id) =>
        catalog.Elements.Keys.Where(element => IsKeywordElement(element, id)).Take(1).Select(element => Optional.Some(element)).FindFirstSome();

    private static bool IsKeywordElement(ElementId element, string id) =>
        element.Value == id;
}

using Loopsmith.Core.Domain;
using Loopsmith.Functional;

namespace Loopsmith.Core.Causality;

/// <summary>
/// Kernel: facts about keywords the slices rely on — what damage type a keyword strike or a summon deals, how a
/// pickup or a status is found, which element a keyword is, and which Bungie trait (the game's own keyword) — so all
/// read the same answer.
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

    /// <summary>Bungie's trait a keyword is (Jolted, for Jolt), when the glossary gives one.</summary>
    public static Optional<ItemHash> FindKeywordTrait(this KeywordGlossary glossary, KeywordRef keyword) =>
        keyword.Match(
            status => glossary.Statuses.TryGetValue(status.Id, out var found) ? found.Trait : Optional.None<ItemHash>(),
            pickup => glossary.Pickups.TryGetValue(pickup.Id, out var found) ? found.Trait : Optional.None<ItemHash>(),
            summon => glossary.Summons.TryGetValue(summon.Id, out var found) ? found.Trait : Optional.None<ItemHash>());

    /// <summary>
    /// A keyword's icon: its trait's, the game's own keyword icon; else its element's manifest item's (the element of its id:
    /// Slice's weapon perk, the Lethal Current aspect for its buff); else the shape the game draws for it (a status's
    /// <c>drawnIcon</c>, in its affinity); none when there is none of those. The designer, the trace and the dependency
    /// graph all show this one.
    /// </summary>
    public static Optional<IconRef> FindKeywordIcon(this RuleCatalog catalog, KeywordRef keyword) =>
        new[]
        {
            catalog.Glossary.FindKeywordTrait(keyword).Bind(catalog.Manifest.FindTraitIcon).Map(path => (IconRef)new IconRef.Manifest(path)),
            catalog.FindKeywordElement(keyword).Bind(catalog.FindElementItem).Bind(item => item.Icon).Map(path => (IconRef)new IconRef.Manifest(path)),
            FindDrawnIcon(catalog.Glossary, keyword),
        }.FindFirstSome();

    /// <summary>The shape the game draws for a status Bungie's data has no icon for (the glossary's <c>drawnIcon</c>), in its affinity.</summary>
    private static Optional<IconRef> FindDrawnIcon(KeywordGlossary glossary, KeywordRef keyword) =>
        keyword.Match(
            status => glossary.Statuses.TryGetValue(status.Id, out var definition)
                ? definition.DrawnIcon.Map(shape => (IconRef)new IconRef.Drawn(shape, definition.Affinity))
                : Optional.None<IconRef>(),
            _ => Optional.None<IconRef>(),
            _ => Optional.None<IconRef>());

    /// <summary>
    /// A status's own element: the element of its id, or that weapon perk's enhanced trait (the enhanced Stats for All gives
    /// Stats for All, and its stats are the status's own, not something it switches on).
    /// </summary>
    public static bool IsOwnElementOf(this RuleCatalog catalog, ElementId element, StatusId status) =>
        element.IsStatusElement(status)
        || catalog.EnhancedPerks.Any(pair => pair.Key.IsStatusElement(status) && pair.Value == element);

    /// <summary>
    /// The element a buff shows (its details, its own effects): the one that gave it when that is its own element (the
    /// enhanced trait, on the weapon that shot), else the element of its id.
    /// </summary>
    public static Optional<ElementId> FindGivingElement(this RuleCatalog catalog, StatusId status, Optional<ElementId> givenBy) =>
        new[]
        {
            givenBy.Bind(giver => catalog.IsOwnElementOf(giver, status) ? Optional.Some(giver) : Optional.None<ElementId>()),
            catalog.FindStatusElement(status),
        }.FindFirstSome();

    /// <summary>
    /// The element a keyword in an outcome shows: for a status, the element that fired the outcome when that is its own
    /// (the enhanced Stats for All's "Stats for All (11s)": the enhanced trait), else the keyword's element.
    /// </summary>
    public static Optional<ElementId> FindFiredKeywordElement(this RuleCatalog catalog, KeywordRef keyword, Optional<ElementId> firedBy) =>
        keyword.Match(
            status => catalog.FindGivingElement(status.Id, firedBy),
            _ => catalog.FindKeywordElement(keyword),
            _ => catalog.FindKeywordElement(keyword));

    /// <summary>The one rule both lookups follow: a keyword's element is the element of its id — its own effects.</summary>
    public static bool IsStatusElement(this ElementId element, StatusId status) =>
        IsKeywordElement(element, status.Value);

    private static Optional<ElementId> FindElementOfId(this RuleCatalog catalog, string id) =>
        catalog.Elements.Keys.Where(element => IsKeywordElement(element, id)).Take(1).Select(element => Optional.Some(element)).FindFirstSome();

    private static bool IsKeywordElement(ElementId element, string id) =>
        element.Value == id;
}

using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>Pure reads of the manifest excerpt (ADRs D14) that the slices and the hosts share: what a hash, an element or a subclass is.</summary>
public static class ManifestReading
{
    public static Optional<ManifestItem> FindItem(this ManifestExcerpt manifest, ItemHash hash) =>
        manifest.Items.TryGetValue(hash, out var item) ? Optional.Some(item) : Optional.None<ManifestItem>();

    /// <summary>An element's manifest item: the first of its hashes the excerpt has (an element's copies share their look).</summary>
    public static Optional<ManifestItem> FindElementItem(this RuleCatalog catalog, ElementId id) =>
        catalog.Elements.TryGetValue(id, out var element)
            ? element.Hashes.Select(hash => catalog.Manifest.FindItem(hash)).FindFirstSome()
            : Optional.None<ManifestItem>();

    /// <summary>The subclass item of a class's subclass (Arcstrider for an Arc Hunter), when the glossary and the excerpt have it.</summary>
    public static Optional<ManifestItem> FindSubclassItem(this RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass) =>
        catalog.Glossary.Subclasses
            .Where(definition => definition.Class == guardianClass && definition.Subclass == subclass)
            .SelectMany(definition => definition.Hashes)
            .Select(hash => catalog.Manifest.FindItem(hash))
            .FindFirstSome();

    /// <summary>The armor slot of an armor piece, or of a mod that only fits it; none for anything else (a general mod).</summary>
    public static Optional<ArmorSlot> ReadArmorSlot(this ManifestItem item) =>
        item.Kind switch
        {
            ManifestKind.Armor armor => Optional.Some(armor.Slot),
            ManifestKind.ArmorMod mod => mod.Slot,
            _ => Optional.None<ArmorSlot>(),
        };
}

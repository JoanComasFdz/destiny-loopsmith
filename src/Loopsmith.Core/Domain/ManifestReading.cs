using System.Collections.Immutable;
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

    /// <summary>The catalog element one of whose hashes this is (a hash names one element, the catalog checks it).</summary>
    public static Optional<BuildElement> FindElementByHash(this RuleCatalog catalog, ItemHash hash) =>
        catalog.Elements.Values
            .Where(element => element.Hashes.Contains(hash))
            .Select(Optional.Some)
            .FindFirstSome();

    /// <summary>The subclass item of a class's subclass (Arcstrider for an Arc Hunter), when the glossary and the excerpt have it.</summary>
    public static Optional<ManifestItem> FindSubclassItem(this RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass) =>
        catalog.Glossary.Subclasses
            .Where(definition => definition.Class == guardianClass && definition.Subclass == subclass)
            .SelectMany(definition => definition.Hashes)
            .Select(hash => catalog.Manifest.FindItem(hash))
            .FindFirstSome();

    /// <summary>A weapon's trait columns: its manifest item's, when the build gives its hash and the excerpt has them; else none.</summary>
    public static ImmutableArray<TraitColumn> ListTraitColumns(this ManifestExcerpt manifest, WeaponLoadout weapon) =>
        weapon.Hash
            .Bind(hash => manifest.FindItem(hash))
            .Match(item => item.Value.Kind is ManifestKind.Weapon kind ? kind.Traits : [], _ => ImmutableArray<TraitColumn>.Empty);

    /// <summary>The one of an element's hashes that is a perk of this trait column (Slice's, in Festival Flight's first), if any.</summary>
    public static Optional<ItemHash> FindColumnHash(this RuleCatalog catalog, ElementId id, TraitColumn column) =>
        catalog.Elements.TryGetValue(id, out var element)
            ? element.Hashes.Where(column.Options.Contains).Select(Optional.Some).FindFirstSome()
            : Optional.None<ItemHash>();

    /// <summary>True when one of an element's hashes is a perk of this trait column.</summary>
    public static bool IsInColumn(this RuleCatalog catalog, ElementId id, TraitColumn column) =>
        catalog.FindColumnHash(id, column).IsSome();

    /// <summary>The armor slot of an armor piece, or of a mod that only fits it; none for anything else (a general mod).</summary>
    public static Optional<ArmorSlot> ReadArmorSlot(this ManifestItem item) =>
        item.Kind switch
        {
            ManifestKind.Armor armor => Optional.Some(armor.Slot),
            ManifestKind.ArmorMod mod => mod.Slot,
            _ => Optional.None<ArmorSlot>(),
        };
}

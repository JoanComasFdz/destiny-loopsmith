using System.Collections.Immutable;

namespace Loopsmith.Core.Domain;

/// <summary>
/// Pure: a weapon perk's enhanced trait is one perk with several plugs — the one its <c>enhanced:</c> block names, and
/// others of the same name that Bungie's manifest types "Enhanced Trait" (or "Enhanced Origin Trait"): a weapon's
/// enhanced copy, a crafted one. Every such plug the excerpt has (the bundled one, or with what a Bungie login read)
/// is the enhanced trait's element's, so a DIM link or an account's weapon that rolls it equips it.
/// </summary>
public static class EnhancedPerkReading
{
    private static readonly ImmutableArray<string> EnhancedTypes = ["Enhanced Trait", "Enhanced Origin Trait"];

    /// <summary>The catalog with each enhanced trait's element holding every plug of it the excerpt has (no element's own plug is taken).</summary>
    public static RuleCatalog RecognizeEnhancedPerks(this RuleCatalog catalog)
    {
        var owned = catalog.Elements.Values.SelectMany(element => element.Hashes).ToImmutableHashSet();
        var plugsByName = catalog.Manifest.Items.Values
            .Where(item => item.Type.Match(type => EnhancedTypes.Contains(type.Value), _ => false) && !owned.Contains(item.Hash))
            .GroupBy(item => item.Name, StringComparer.Ordinal)
            .ToImmutableDictionary(group => group.Key, group => group.Select(item => item.Hash).Order().ToImmutableArray(), StringComparer.Ordinal);
        var recognized = catalog.EnhancedPerks
            .Where(pair => catalog.Elements.ContainsKey(pair.Key) && catalog.Elements.ContainsKey(pair.Value))
            .Select(pair => (Enhanced: catalog.Elements[pair.Value], Plugs: ListPlugs(catalog, catalog.Elements[pair.Key], plugsByName)))
            .Where(found => !found.Plugs.IsEmpty)
            .Select(found => found.Enhanced with { Hashes = [.. found.Enhanced.Hashes, .. found.Plugs] });
        return catalog with { Elements = catalog.Elements.SetItems(recognized.Select(element => KeyValuePair.Create(element.Id, element))) };
    }

    /// <summary>The unowned enhanced plugs named like the perk (its manifest item's name, else its own).</summary>
    private static ImmutableArray<ItemHash> ListPlugs(RuleCatalog catalog, BuildElement perk, ImmutableDictionary<string, ImmutableArray<ItemHash>> plugsByName)
    {
        var name = catalog.FindElementItem(perk.Id).Match(item => item.Value.Name, _ => perk.Name);
        return plugsByName.TryGetValue(name, out var plugs) ? plugs : [];
    }
}

using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.LoadoutImporting;

/// <summary>
/// Pure: a DIM loadout → a <see cref="Build"/> of what the rule catalog recognises by manifest hash, and the rest left
/// out (<see cref="Build.LeftOut"/>). Nothing is guessed (ADRs D12): an ability the catalog has no hash for is "?", an
/// item it doesn't know is listed, not matched by name. Weapons need the manifest (their slot, name and damage type), so
/// every weapon is left out until the manifest join.
/// </summary>
public static class DimLoadoutMapping
{
    private const string DefaultName = "DIM loadout";

    /// <summary>The loadout JSON as a build named after the loadout, with <paramref name="link"/> as its source.</summary>
    public static Result<Build, string> MapLoadout(RuleCatalog catalog, string link, string loadoutJson) =>
        DimLoadoutParsing.ParseLoadout(loadoutJson).Bind(loadout => MapLoadout(catalog, link, loadout));

    private static Result<Build, string> MapLoadout(RuleCatalog catalog, string link, DimLoadout loadout)
    {
        var byHash = catalog.Elements.Values
            .SelectMany(element => element.Hashes.Select(hash => (Hash: hash, Element: element)))
            .ToImmutableDictionary(entry => entry.Hash, entry => entry.Element);
        var subclasses = loadout.Equipped
            .Where(item => DimSubclasses.ByHash.ContainsKey(item.Hash.Value))
            .Select(item => (Item: item, Subclass: DimSubclasses.ByHash[item.Hash.Value]))
            .ToImmutableArray();
        if (subclasses.IsEmpty)
        {
            return new Result<Build, string>.Error(DescribeMissingSubclass(loadout));
        }

        var (subclassItem, subclass) = subclasses[0];
        if (loadout.ClassType.Match(type => ToClassType(subclass.Class) != type.Value && type.Value is >= 0 and <= 2, _ => false))
        {
            return new Result<Build, string>.Error(
                $"The DIM loadout is for another class than its subclass ({subclass.Name}, a {subclass.Class} subclass).");
        }

        var plugs = subclassItem.Plugs.Select(hash => Recognise(byHash, LoadoutPart.SubclassPlug, hash)).ToImmutableArray();
        var items = loadout.Equipped
            .Where(item => item != subclassItem && !DimSubclasses.ByHash.ContainsKey(item.Hash.Value))
            .Select(item => item.Hash)
            .Concat(loadout.ExoticArmor.Match(exotic => new[] { exotic.Value }, _ => []))
            .Distinct()
            .Select(hash => Recognise(byHash, LoadoutPart.Item, hash))
            .ToImmutableArray();
        var mods = loadout.Mods.Select(hash => Recognise(byHash, LoadoutPart.ArmorMod, hash)).ToImmutableArray();
        var perks = loadout.ArtifactPerks.Select(hash => Recognise(byHash, LoadoutPart.ArtifactPerk, hash)).ToImmutableArray();
        var recognised = plugs.Concat(items).Concat(mods).Concat(perks).ToImmutableArray();
        var placed = recognised.SelectMany(entry => ToSome(entry.Element).Select(element => (entry.Part, element, entry.Hash))).ToImmutableArray();
        var leftOut = recognised
            .Where(entry => !entry.Element.IsSome() || !IsPlaceable(entry.Part, entry.Element))
            .Select(entry => new LeftOutItem(entry.Part, entry.Hash))
            .ToImmutableArray();

        return new Result<Build, string>.Ok(new Build(
            loadout.Name.Trim().Length == 0 ? DefaultName : loadout.Name.Trim(),
            Optional.None<string>(),
            Optional.Some(link),
            Optional.None<CatalogVersion>(),
            subclass.Class,
            subclass.Subclass,
            new AbilityLoadout(
                FindFirst(placed, LoadoutPart.SubclassPlug, ElementKind.Super),
                FindFirst(placed, LoadoutPart.SubclassPlug, ElementKind.Grenade),
                FindFirst(placed, LoadoutPart.SubclassPlug, ElementKind.Melee),
                FindFirst(placed, LoadoutPart.SubclassPlug, ElementKind.ClassAbility)),
            ListAll(placed, LoadoutPart.SubclassPlug, ElementKind.Aspect),
            ListAll(placed, LoadoutPart.SubclassPlug, ElementKind.Fragment),
            FindFirst(placed, LoadoutPart.Item, ElementKind.ExoticArmor),
            [],
            ListAll(placed, LoadoutPart.ArmorMod, ElementKind.ArmorMod),
            ListAll(placed, LoadoutPart.ArtifactPerk, ElementKind.ArtifactPerk),
            [],
            new StatLine(
                Optional.None<StatValue>(),
                Optional.None<StatValue>(),
                Optional.None<StatValue>(),
                Optional.None<StatValue>(),
                Optional.None<StatValue>(),
                Optional.None<StatValue>()),
            leftOut));
    }

    private sealed record Recognition(LoadoutPart Part, ItemHash Hash, Optional<BuildElement> Element);

    private static Recognition Recognise(ImmutableDictionary<ItemHash, BuildElement> byHash, LoadoutPart part, ItemHash hash) =>
        new(part, hash, byHash.TryGetValue(hash, out var element) ? Optional.Some(element) : Optional.None<BuildElement>());

    /// <summary>
    /// Where a recognised element can go: a subclass plug is an ability, aspect or fragment; an item an exotic armor; a
    /// mod a mod; an artifact perk an artifact perk. Anything else (an exotic weapon's perk) needs what isn't in a
    /// loadout, and is left out.
    /// </summary>
    private static bool IsPlaceable(LoadoutPart part, Optional<BuildElement> element) =>
        element.Match(
            some => (part, some.Value.Kind) switch
            {
                (LoadoutPart.SubclassPlug, ElementKind.Super or ElementKind.Grenade or ElementKind.Melee or ElementKind.ClassAbility
                    or ElementKind.Aspect or ElementKind.Fragment) => true,
                (LoadoutPart.Item, ElementKind.ExoticArmor) => true,
                (LoadoutPart.ArmorMod, ElementKind.ArmorMod) => true,
                (LoadoutPart.ArtifactPerk, ElementKind.ArtifactPerk) => true,
                _ => false,
            },
            _ => false);

    private static Optional<ElementId> FindFirst(
        ImmutableArray<(LoadoutPart Part, BuildElement Element, ItemHash Hash)> placed, LoadoutPart part, ElementKind kind) =>
        ListAll(placed, part, kind).Select(Optional.Some).FindFirstSome();

    private static ImmutableArray<ElementId> ListAll(
        ImmutableArray<(LoadoutPart Part, BuildElement Element, ItemHash Hash)> placed, LoadoutPart part, ElementKind kind) =>
        [.. placed.Where(entry => entry.Part == part && entry.Element.Kind == kind).Select(entry => entry.Element.Id)];

    /// <summary>DIM's <c>DestinyClass</c>: Titan 0, Hunter 1, Warlock 2 (3 = any class).</summary>
    private static int ToClassType(GuardianClass guardianClass) =>
        guardianClass switch
        {
            GuardianClass.Titan => 0,
            GuardianClass.Hunter => 1,
            _ => 2,
        };

    private static string DescribeMissingSubclass(DimLoadout loadout) =>
        loadout.Equipped.IsEmpty
            ? "The DIM loadout has no equipped items, so there is no subclass to start from."
            : "Loopsmith can't tell this DIM loadout's subclass: it recognises the Arc, Solar and Void subclasses so far "
              + "(Stasis, Strand and Prismatic need the Bungie manifest, which Loopsmith doesn't read yet).";

    private static IEnumerable<T> ToSome<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);
}

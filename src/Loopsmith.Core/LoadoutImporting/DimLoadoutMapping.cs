using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.LoadoutImporting;

/// <summary>
/// Pure: a DIM loadout → a <see cref="Build"/> of what the rule catalog recognises by manifest hash, and the rest left
/// out (<see cref="Build.LeftOut"/>). Nothing is guessed (ADRs D12): an ability the catalog has no hash for is "?", a jump
/// it has no hash for and an item it doesn't know are listed, not matched by name. A weapon is equipped when the manifest excerpt knows its slot,
/// name and damage type, with the perks its socket overrides pick in its trait columns (a loadout saved with them);
/// any other weapon is left out. The armor's set
/// perks the pieces grant (2 or 4 of a set, by the excerpt) are its set bonuses, where the catalog knows them.
/// </summary>
public static class DimLoadoutMapping
{
    private const string DefaultName = "DIM loadout";

    /// <summary>Every hash the loadout wears: its equipped items and their plugs, its mods and artifact perks, the optimizer's exotic.</summary>
    public static Result<ImmutableArray<ItemHash>, string> ListLoadoutHashes(string loadoutJson) =>
        DimLoadoutParsing.ParseLoadout(loadoutJson).Map(loadout => (ImmutableArray<ItemHash>)
        [
            .. loadout.Equipped.SelectMany(item => item.Plugs.Prepend(item.Hash)),
            .. loadout.Mods,
            .. loadout.ArtifactPerks,
            .. loadout.ExoticArmor.Match(exotic => new[] { exotic.Value }, _ => []),
        ]);

    /// <summary>The loadout JSON as a build named after the loadout, with <paramref name="link"/> as its source.</summary>
    public static Result<Build, string> MapLoadout(RuleCatalog catalog, string link, string loadoutJson) =>
        DimLoadoutParsing.ParseLoadout(loadoutJson).Bind(loadout => MapLoadout(catalog, link, loadout));

    private static Result<Build, string> MapLoadout(RuleCatalog catalog, string link, DimLoadout loadout)
    {
        var byHash = catalog.Elements.Values
            .SelectMany(element => element.Hashes.Select(hash => (Hash: hash, Element: element)))
            .ToImmutableDictionary(entry => entry.Hash, entry => entry.Element);
        var subclassByHash = catalog.Glossary.Subclasses
            .SelectMany(subclass => subclass.Hashes.Select(hash => (Hash: hash, Subclass: subclass)))
            .ToImmutableDictionary(entry => entry.Hash, entry => entry.Subclass);
        var subclasses = loadout.Equipped
            .Where(item => subclassByHash.ContainsKey(item.Hash))
            .Select(item => (Item: item, Subclass: subclassByHash[item.Hash]))
            .ToImmutableArray();
        if (subclasses.IsEmpty)
        {
            return new Result<Build, string>.Error(DescribeMissingSubclass(catalog, loadout));
        }

        var (subclassItem, subclass) = subclasses[0];
        if (loadout.Class.Match(guardianClass => guardianClass.Value != subclass.Class, _ => false))
        {
            return new Result<Build, string>.Error(
                $"The DIM loadout is for another class than its subclass ({subclass.Name}, a {subclass.Class} subclass).");
        }

        var plugs = subclassItem.Plugs.Select(hash => KeepFitting(Recognise(byHash, LoadoutPart.SubclassPlug, hash), subclass.Class, subclass.Subclass)).ToImmutableArray();
        var items = loadout.Equipped
            .Where(item => !subclassByHash.ContainsKey(item.Hash))
            .Select(item => item.Hash)
            .Concat(loadout.ExoticArmor.Match(exotic => new[] { exotic.Value }, _ => []))
            .Distinct()
            .Select(hash => Recognise(byHash, LoadoutPart.Item, hash))
            .ToImmutableArray();
        var mods = loadout.Mods.Select(hash => Recognise(byHash, LoadoutPart.ArmorMod, hash)).ToImmutableArray();
        var perks = loadout.ArtifactPerks.Select(hash => Recognise(byHash, LoadoutPart.ArtifactPerk, hash)).ToImmutableArray();
        var recognised = plugs.Concat(items).Concat(mods).Concat(perks).ToImmutableArray();
        var placed = recognised.SelectMany(entry => ToSome(entry.Element).Select(element => (entry.Part, element, entry.Hash))).ToImmutableArray();
        var overrides = loadout.Equipped.GroupBy(item => item.Hash).ToImmutableDictionary(group => group.Key, group => group.First().Plugs);
        var instances = loadout.Equipped.GroupBy(item => item.Hash).ToImmutableDictionary(group => group.Key, group => group.First().Instance);
        var weapons = items
            .Where(entry => !entry.Element.IsSome())
            .SelectMany(entry => ToSome(ReadWeapon(catalog.Manifest, entry.Hash, overrides.GetValueOrDefault(entry.Hash, []), instances.GetValueOrDefault(entry.Hash, Optional.None<ItemInstanceId>()))))
            .GroupBy(weapon => weapon.Slot)
            .Select(slot => slot.First())
            .ToImmutableArray();
        var setBonuses = catalog.Manifest.CountSetPieces(items.Select(entry => entry.Hash))
            .SelectMany(worn => worn.ListActivePerks())
            .SelectMany(perk => byHash.TryGetValue(perk.Perk, out var element) && element.Kind == ElementKind.ArmorSetBonus ? new[] { element.Id } : [])
            .ToImmutableArray();
        var leftOut = recognised
            .Where(entry => !entry.Element.IsSome() || !IsPlaceable(entry.Part, entry.Element))
            .Where(entry => !weapons.Any(weapon => weapon.Hash == Optional.Some(entry.Hash)))
            .Select(entry => new LeftOutItem(entry.Part, entry.Hash))
            .ToImmutableArray();

        return new Result<Build, string>.Ok(new Build(
            loadout.Name.UnwrapOr(DefaultName),
            Optional.None<string>(),
            Optional.Some(link),
            Optional.None<CatalogVersion>(),
            subclass.Class,
            subclass.Subclass,
            new AbilityLoadout(
                FindAbility(placed, ElementKind.Super),
                FindAbility(placed, ElementKind.Grenade),
                FindAbility(placed, ElementKind.Melee),
                FindAbility(placed, ElementKind.ClassAbility),
                FindJump(placed)),
            ListAll(placed, LoadoutPart.SubclassPlug, ElementKind.Aspect),
            ListAll(placed, LoadoutPart.SubclassPlug, ElementKind.Fragment),
            FindFirst(placed, LoadoutPart.Item, ElementKind.ExoticArmor),
            setBonuses,
            ListAll(placed, LoadoutPart.ArmorMod, ElementKind.ArmorMod),
            ListAll(placed, LoadoutPart.ArtifactPerk, ElementKind.ArtifactPerk),
            weapons,
            new StatLine(
                Optional.None<StatValue>(),
                Optional.None<StatValue>(),
                Optional.None<StatValue>(),
                Optional.None<StatValue>(),
                Optional.None<StatValue>(),
                Optional.None<StatValue>()),
            leftOut,
            ListArmorCopies(catalog.Manifest, loadout),
            ListModPlugs(placed)));
    }

    /// <summary>
    /// The copies of the loadout's mods the rules know, for each element one of whose copies isn't its first hash (the
    /// cheaper artifact copy): every copy of it, in the loadout's order.
    /// </summary>
    private static ImmutableArray<ItemHash> ListModPlugs(ImmutableArray<(LoadoutPart Part, BuildElement Element, ItemHash Hash)> placed)
    {
        var mods = placed.Where(entry => entry.Part == LoadoutPart.ArmorMod && entry.Element.Kind == ElementKind.ArmorMod).ToImmutableArray();
        var mixed = mods.Where(entry => !entry.Element.Hashes.IsEmpty && entry.Element.Hashes[0] != entry.Hash).Select(entry => entry.Element.Id).ToImmutableHashSet();
        return [.. mods.Where(entry => mixed.Contains(entry.Element.Id)).Select(entry => entry.Hash)];
    }

    /// <summary>The copy of each armor piece the loadout wears, when it names one (a dim.gg share does): its instance id.</summary>
    private static ImmutableArray<ArmorCopy> ListArmorCopies(ManifestExcerpt manifest, DimLoadout loadout) =>
    [
        .. loadout.Equipped
            .Where(item => manifest.FindItem(item.Hash).Match(found => found.Value.Kind is ManifestKind.Armor, _ => false))
            .SelectMany(item => item.Instance.Match(instance => new[] { new ArmorCopy(item.Hash, instance.Value) }, _ => []))
            .DistinctBy(copy => copy.Piece),
    ];

    /// <summary>
    /// A weapon the manifest excerpt knows — its slot, name, damage type and archetype ("Grenade Launcher" →
    /// <c>grenade-launcher</c>) — and its roll: in each trait column, the loadout's socket override for it that the
    /// column rolls with — its mod (the override its mod socket takes), and the copy it is (<paramref name="instance"/>, a
    /// dim.gg share's item id). A DIM loadout names the weapon; its perks and mod only when they were saved with it (none: "?").
    /// </summary>
    private static Optional<WeaponLoadout> ReadWeapon(ManifestExcerpt manifest, ItemHash hash, ImmutableArray<ItemHash> overrides, Optional<ItemInstanceId> instance) =>
        manifest.FindItem(hash).Bind(item => item.Kind is ManifestKind.Weapon { DamageType: Optional<DamageType>.Some type } weapon
            ? Optional.Some(new WeaponLoadout(weapon.Slot, item.Name, type.Value, item.Type.Bind(ManifestReading.ToArchetype), [], Optional.Some(hash), weapon.Traits.ReadRoll(overrides), false, instance, weapon.ReadMod(overrides)))
            : Optional.None<WeaponLoadout>());

    private sealed record Recognition(LoadoutPart Part, ItemHash Hash, Optional<BuildElement> Element);

    private static Recognition Recognise(ImmutableDictionary<ItemHash, BuildElement> byHash, LoadoutPart part, ItemHash hash) =>
        new(part, hash, byHash.TryGetValue(hash, out var element) ? Optional.Some(element) : Optional.None<BuildElement>());

    /// <summary>A subclass plug whose element is another class's (an Arc fragment the rules author for the Hunter, on a Striker) is left out, not placed.</summary>
    private static Recognition KeepFitting(Recognition entry, GuardianClass guardianClass, Subclass subclass) =>
        entry with { Element = entry.Element.Bind(element => element.Fits(guardianClass, subclass) ? Optional.Some(element) : Optional.None<BuildElement>()) };

    /// <summary>
    /// Where a recognised element can go: a subclass plug is an ability, a jump, an aspect or a fragment; an item an exotic armor; a
    /// mod a mod; an artifact perk an artifact perk. Anything else (an exotic weapon's perk) needs what isn't in a
    /// loadout, and is left out.
    /// </summary>
    private static bool IsPlaceable(LoadoutPart part, Optional<BuildElement> element) =>
        element.Match(
            some => (part, some.Value.Kind) switch
            {
                (LoadoutPart.SubclassPlug, ElementKind.Super or ElementKind.Grenade or ElementKind.Melee or ElementKind.ClassAbility
                    or ElementKind.Jump or ElementKind.Aspect or ElementKind.Fragment) => true,
                (LoadoutPart.Item, ElementKind.ExoticArmor) => true,
                (LoadoutPart.ArmorMod, ElementKind.ArmorMod) => true,
                (LoadoutPart.ArtifactPerk, ElementKind.ArtifactPerk) => true,
                _ => false,
            },
            _ => false);

    private static Optional<ElementId> FindFirst(
        ImmutableArray<(LoadoutPart Part, BuildElement Element, ItemHash Hash)> placed, LoadoutPart part, ElementKind kind) =>
        ListAll(placed, part, kind).Select(Optional.Some).FindFirstSome();

    /// <summary>The loadout's ability of this kind; "?" when the rules don't know it (or the loadout names none).</summary>
    private static AbilityPlug FindAbility(ImmutableArray<(LoadoutPart Part, BuildElement Element, ItemHash Hash)> placed, ElementKind kind) =>
        FindFirst(placed, LoadoutPart.SubclassPlug, kind).Match<AbilityPlug>(found => new AbilityPlug.Known(found.Value), _ => new AbilityPlug.Unknown());

    /// <summary>
    /// The loadout's jump when the rules know it (the Arcstrider's); none otherwise: a jump the rules don't have stays in
    /// <c>leftOut</c> (it has no part in a loop, so it is no gap and no "?").
    /// </summary>
    private static AbilityPlug FindJump(ImmutableArray<(LoadoutPart Part, BuildElement Element, ItemHash Hash)> placed) =>
        FindFirst(placed, LoadoutPart.SubclassPlug, ElementKind.Jump).Match<AbilityPlug>(found => new AbilityPlug.Known(found.Value), _ => new AbilityPlug.Empty());

    private static ImmutableArray<ElementId> ListAll(
        ImmutableArray<(LoadoutPart Part, BuildElement Element, ItemHash Hash)> placed, LoadoutPart part, ElementKind kind) =>
        [.. placed.Where(entry => entry.Part == part && entry.Element.Kind == kind).Select(entry => entry.Element.Id)];

    private static string DescribeMissingSubclass(RuleCatalog catalog, DimLoadout loadout) =>
        loadout.Equipped.IsEmpty
            ? "The DIM loadout has no equipped items, so there is no subclass to start from."
            : "Loopsmith can't tell this DIM loadout's subclass: the rule catalog knows the subclass hashes of "
              + $"{DescribeSubclasses(catalog)} so far (the others need the Bungie manifest, which Loopsmith doesn't read yet).";

    /// <summary>"Arc, Solar and Void": the subclasses the glossary has a hash for, in vocabulary order.</summary>
    private static string DescribeSubclasses(RuleCatalog catalog)
    {
        var known = catalog.Glossary.Subclasses.Select(subclass => subclass.Subclass).Distinct().Order().Select(subclass => subclass.ToString()).ToImmutableArray();
        return known.Length switch
        {
            0 => "no subclass",
            1 => known[0],
            _ => $"{string.Join(", ", known[..^1])} and {known[^1]}",
        };
    }

    private static IEnumerable<T> ToSome<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);
}

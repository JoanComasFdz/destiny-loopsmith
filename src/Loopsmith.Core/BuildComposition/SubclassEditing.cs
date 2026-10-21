using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: the plugs a build's subclass holds and the ones it could (the manifest excerpt's <c>plugs</c> of the subclass
/// item), and a build with other plugs. A plug the rules know for the build's class is the build's element (its ability,
/// jump, aspect or fragment); any other is a left-out subclass plug, marked as not in the rules (an Arc fragment the rules
/// author for the Hunter is one on a Striker), except a jump the rules don't have, which has no part in a loop and isn't
/// marked. The aspects open the fragment slots: the sum of the aspects' <c>fragmentSlots</c>, or not known when any
/// aspect's isn't.
/// </summary>
public static class SubclassEditing
{
    /// <summary>The slot a plug takes, by what the manifest says it is; none for an item that isn't a subclass plug.</summary>
    public static Optional<SubclassSlot> FindSlot(RuleCatalog catalog, ItemHash plug) =>
        catalog.Manifest.FindItem(plug).Bind(item => item.Kind switch
        {
            ManifestKind.Super => Optional.Some(SubclassSlot.Super),
            ManifestKind.Grenade => Optional.Some(SubclassSlot.Grenade),
            ManifestKind.Melee => Optional.Some(SubclassSlot.Melee),
            ManifestKind.ClassAbility => Optional.Some(SubclassSlot.ClassAbility),
            ManifestKind.Movement => Optional.Some(SubclassSlot.Jump),
            ManifestKind.Aspect => Optional.Some(SubclassSlot.Aspect),
            ManifestKind.Fragment => Optional.Some(SubclassSlot.Fragment),
            _ => Optional.None<SubclassSlot>(),
        });

    /// <summary>Everything the build's subclass can hold, in the manifest's order; empty when the excerpt doesn't list its plugs (the editor is then not offered).</summary>
    public static ImmutableArray<ItemHash> ListPlugs(RuleCatalog catalog, Build build) =>
        catalog.FindSubclassItem(build.Class, build.Subclass)
            .Match(item => item.Value.Kind is ManifestKind.Subclass subclass ? subclass.Plugs : [], _ => []);

    /// <summary>What the subclass can hold in a slot.</summary>
    public static ImmutableArray<ItemHash> ListOptions(RuleCatalog catalog, Build build, SubclassSlot slot) =>
        [.. ListPlugs(catalog, build).Where(plug => FindSlot(catalog, plug) == Optional.Some(slot))];

    /// <summary>True when the build's subclass lists its plugs (so the designer can edit them).</summary>
    public static bool CanEdit(RuleCatalog catalog, Build build) =>
        !ListPlugs(catalog, build).IsEmpty;

    /// <summary>The plugs the build holds: its elements' hashes (the one this subclass lists), then its left-out subclass plugs.</summary>
    public static SubclassPlugs ReadPlugs(RuleCatalog catalog, Build build)
    {
        var listed = ListPlugs(catalog, build);
        var leftOut = build.LeftOut.Where(item => item.Part == LoadoutPart.SubclassPlug).Select(item => item.Hash).ToImmutableArray();
        Optional<ItemHash> Find(Optional<ElementId> element, SubclassSlot slot) =>
            new[] { element.Bind(id => FindHash(catalog, listed, id)), leftOut.Where(hash => FindSlot(catalog, hash) == Optional.Some(slot)).Select(Optional.Some).FindFirstSome() }
                .FindFirstSome();
        ImmutableArray<ItemHash> List(ImmutableArray<ElementId> elements, SubclassSlot slot) =>
        [
            .. elements.SelectMany(id => Flatten(FindHash(catalog, listed, id))),
            .. leftOut.Where(hash => FindSlot(catalog, hash) == Optional.Some(slot)),
        ];

        return new SubclassPlugs(
            Find(build.Abilities.Super.ToElement(), SubclassSlot.Super),
            Find(build.Abilities.Grenade.ToElement(), SubclassSlot.Grenade),
            Find(build.Abilities.Melee.ToElement(), SubclassSlot.Melee),
            Find(build.Abilities.ClassAbility.ToElement(), SubclassSlot.ClassAbility),
            Find(build.Abilities.Jump.ToElement(), SubclassSlot.Jump),
            List(build.Aspects, SubclassSlot.Aspect),
            List(build.Fragments, SubclassSlot.Fragment));
    }

    /// <summary>The element's hash this subclass lists (a class ability has a copy on each subclass), else its first.</summary>
    private static Optional<ItemHash> FindHash(RuleCatalog catalog, ImmutableArray<ItemHash> listed, ElementId id) =>
        catalog.Elements.TryGetValue(id, out var element)
            ? new[] { element.Hashes.Where(listed.Contains).Select(Optional.Some).FindFirstSome(), element.Hashes.Select(Optional.Some).FindFirstSome() }.FindFirstSome()
            : Optional.None<ItemHash>();

    /// <summary>
    /// How many fragment slots these aspects open: the sum of their elements' <c>fragmentSlots</c>; not known when an
    /// aspect isn't an element of the rules or its element doesn't say.
    /// </summary>
    public static FragmentSlotCount CountFragmentSlots(RuleCatalog catalog, ImmutableArray<ItemHash> aspects)
    {
        var slots = aspects.Select(aspect => catalog.FindElementByHash(aspect).Bind(element => element.FragmentSlots)).ToImmutableArray();
        return new FragmentSlotCount(slots.All(count => count.IsSome()) ? Optional.Some(FragmentSlots.From(slots.Sum(count => count.UnwrapOr(FragmentSlots.Zero).Value))) : Optional.None<FragmentSlots>());
    }

    /// <summary>
    /// The plugs with these aspects; the fragments the new slot count no longer holds are taken out, the last ones first,
    /// and named. A count that isn't known takes none out.
    /// </summary>
    public static SubclassTrim SetAspects(RuleCatalog catalog, SubclassPlugs plugs, ImmutableArray<ItemHash> aspects)
    {
        var slots = CountFragmentSlots(catalog, aspects).Slots;
        var kept = slots.Match(count => plugs.Fragments.Take(count.Value.Value).ToImmutableArray(), _ => plugs.Fragments);
        return new SubclassTrim(plugs with { Aspects = aspects, Fragments = kept }, [.. plugs.Fragments.Skip(kept.Length)]);
    }

    /// <summary>
    /// What the editor warns when the plugs hold more fragments than their aspects open (a build file: build
    /// validation only warns, but Save keeps the game's limit); none when they fit or the count isn't known.
    /// </summary>
    public static Optional<string> DescribeFragmentOverflow(RuleCatalog catalog, SubclassPlugs plugs) =>
        CountFragmentSlots(catalog, plugs.Aspects).Slots.Bind(count => plugs.Fragments.Length > count.Value
            ? Optional.Some($"{DescribeCount(plugs.Fragments.Length, "fragment")} equipped; the aspects open {DescribeCount(count.Value, "slot")}. Save needs aspects that open enough slots, or fewer fragments.")
            : Optional.None<string>());

    private static string DescribeCount(int count, string noun) =>
        $"{count} {noun}{(count == 1 ? "" : "s")}";

    /// <summary>The build with these plugs, or why it can't hold them (a plug the subclass doesn't list, one twice, too many aspects or fragments).</summary>
    public static Result<Build, string> ApplyPlugs(RuleCatalog catalog, Build build, SubclassPlugs plugs)
    {
        var listed = ListPlugs(catalog, build);
        var chosen = ListSingles(plugs)
            .Concat(plugs.Aspects)
            .Concat(plugs.Fragments)
            .ToImmutableArray();
        var slots = CountFragmentSlots(catalog, plugs.Aspects).Slots;
        var unlisted = chosen.Where(plug => !listed.Contains(plug)).ToImmutableArray();
        if (listed.IsEmpty)
        {
            return Refuse($"The subclass of {build.Name} can't be edited: Loopsmith doesn't list what it can hold.");
        }

        if (!unlisted.IsEmpty)
        {
            return Refuse($"{DescribePlug(catalog, unlisted[0])} isn't something this subclass can hold.");
        }

        if (chosen.Distinct().Count() != chosen.Length)
        {
            return Refuse("A plug can be equipped once.");
        }

        if (plugs.Aspects.Length > 2)
        {
            return Refuse("A subclass holds two aspects.");
        }

        if (slots.Match(count => plugs.Fragments.Length > count.Value.Value, _ => false))
        {
            return Refuse($"{plugs.Fragments.Length} fragments don't fit the {slots.UnwrapOr(FragmentSlots.Zero)} slots the aspects open.");
        }

        return new Result<Build, string>.Ok(Write(catalog, build, plugs));
    }

    /// <summary>
    /// The build holding the plugs: a plug the rules know is its element, any other a left-out plug ("?" in an ability
    /// slot; a jump the rules don't have leaves the jump slot empty, since it has no part in a loop). An ability slot with
    /// no plug is empty, except a "?" the build holds without a plug (a build file's), which the editor shows empty but
    /// keeps.
    /// </summary>
    private static Build Write(RuleCatalog catalog, Build build, SubclassPlugs plugs)
    {
        Optional<ElementId> FindId(ItemHash hash) => FindElement(catalog, build, hash).Map(element => element.Id);
        AbilityPlug FindAbility(Optional<ItemHash> plug, AbilityPlug held, AbilityPlug unknown) =>
            plug.Match(
                hash => FindId(hash.Value).Match<AbilityPlug>(id => new AbilityPlug.Known(id.Value), _ => unknown),
                _ => held is AbilityPlug.Unknown ? held : new AbilityPlug.Empty());
        ImmutableArray<ElementId> ListElements(ImmutableArray<ItemHash> hashes) => [.. hashes.SelectMany(hash => Flatten(FindId(hash)))];
        var unknown = ListSingles(plugs).Concat(plugs.Aspects).Concat(plugs.Fragments).Where(hash => !FindId(hash).IsSome());
        return build with
        {
            Abilities = new AbilityLoadout(
                FindAbility(plugs.Super, build.Abilities.Super, new AbilityPlug.Unknown()),
                FindAbility(plugs.Grenade, build.Abilities.Grenade, new AbilityPlug.Unknown()),
                FindAbility(plugs.Melee, build.Abilities.Melee, new AbilityPlug.Unknown()),
                FindAbility(plugs.ClassAbility, build.Abilities.ClassAbility, new AbilityPlug.Unknown()),
                FindAbility(plugs.Jump, build.Abilities.Jump, new AbilityPlug.Empty())),
            Aspects = ListElements(plugs.Aspects),
            Fragments = ListElements(plugs.Fragments),
            LeftOut = [.. build.LeftOut.Where(item => item.Part != LoadoutPart.SubclassPlug), .. unknown.Select(hash => new LeftOutItem(LoadoutPart.SubclassPlug, hash))],
        };
    }

    private static IEnumerable<ItemHash> ListSingles(SubclassPlugs plugs) =>
        new[] { plugs.Super, plugs.Grenade, plugs.Melee, plugs.ClassAbility, plugs.Jump }.SelectMany(Flatten);

    private static IEnumerable<T> Flatten<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);

    /// <summary>The rules' element a plug is on this build: an ability, jump, aspect or fragment that fits its class and subclass; none: a left-out plug.</summary>
    public static Optional<BuildElement> FindElement(RuleCatalog catalog, Build build, ItemHash hash) =>
        catalog.FindElementByHash(hash).Bind(element => IsSubclassPlug(element.Kind) && element.Fits(build.Class, build.Subclass) ? Optional.Some(element) : Optional.None<BuildElement>());

    private static bool IsSubclassPlug(ElementKind kind) =>
        kind is ElementKind.Super or ElementKind.Grenade or ElementKind.Melee or ElementKind.ClassAbility or ElementKind.Jump
            or ElementKind.Aspect or ElementKind.Fragment;

    private static string DescribePlug(RuleCatalog catalog, ItemHash hash) =>
        catalog.Manifest.FindItem(hash).Match(item => item.Value.Name, _ => $"Item {hash.Value}");

    private static Result<Build, string> Refuse(string why) =>
        new Result<Build, string>.Error(why);
}

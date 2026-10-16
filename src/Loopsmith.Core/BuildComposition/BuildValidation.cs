using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// The sole constructor of <see cref="ValidatedBuild"/> (architecture test). Blocking issues put the
/// build on the failure track; warnings and info ride along on the validated build. What the rules don't know yet (a
/// "?" ability, a perk, mod or catalyst, what a DIM link left out) is no issue: its square carries the ⊘. An element
/// the rules mark <c>inert</c> is outside the loop on purpose, and every other element has rules or passives (a test
/// over <c>rules/</c> holds the catalog to it).
/// </summary>
public static class BuildValidation
{
    private const int MaxAspects = 2;

    /// <summary>A slot of the build; <see cref="Holder"/>: the weapon holding what it holds (a weapon's perk, catalyst or mod).</summary>
    private sealed record Slot(ElementId Id, string SlotName, ImmutableArray<ElementKind> Accepts, Optional<WeaponLoadout> Holder);

    public static Result<ValidatedBuild, string> ValidateBuild(Build build, RuleCatalog catalog)
    {
        var slots = ListSlots(build, catalog);
        var resolved = slots.Select(slot => ResolveSlot(build, catalog, slot)).ToImmutableArray();
        var elements = resolved
            .SelectMany(r => r.Element.Match(some => new[] { (r.Slot, Element: BindToHolder(some.Value, r.Slot.Holder), BoundTo: FindBinding(some.Value, r.Slot.Holder)) }, _ => []))
            .ToImmutableArray();
        var issues = resolved.SelectMany(r => r.Issues)
            .Concat(CheckRolls(build, catalog))
            .Concat(CheckAspects(build, catalog))
            .Concat(CheckFragmentSlots(build, catalog))
            .Concat(CheckStats(build, elements.Select(e => e.Element)))
            .Concat(CheckUnreachedWeapons(build, elements.Select(e => e.Element)))
            .Concat(CheckNotStacking(catalog, build.ReadSuperType(catalog), elements.Select(e => e.Element).Concat(ListKeywordElements(catalog).Select(e => e.Element))))
            .Concat(CheckPinnedCatalog(build, catalog))
            .Concat(CheckIgnoredMods(build, catalog))
            .ToImmutableArray();

        var blocking = issues.Where(issue => issue.Severity == Severity.Blocking).ToImmutableArray();
        if (!blocking.IsEmpty)
        {
            var message = string.Join(Environment.NewLine, blocking.Select(issue => $"✗ {issue.Message}"));
            return new Result<ValidatedBuild, string>.Error($"Build '{build.Name}' is invalid:{Environment.NewLine}{message}");
        }

        var equipped = GroupCopies(elements).AddRange(ListKeywordElements(catalog));
        return new Result<ValidatedBuild, string>.Ok(new ValidatedBuild(build, catalog, equipped, issues));
    }

    /// <summary>
    /// Every slot the build fills, in the format's order. A weapon's perks are what <see cref="WeaponRolling.ListPerkSlots"/>
    /// places: each column's perk the catalog knows by hash, and the named perks in no column (a perk the catalog doesn't
    /// know sets nothing off, and its square carries the ⊘).
    /// </summary>
    private static ImmutableArray<Slot> ListSlots(Build build, RuleCatalog catalog)
    {
        static IEnumerable<Slot> CreateSlots(IEnumerable<ElementId> ids, string name, params ElementKind[] accepts) =>
            ids.Select(id => new Slot(id, name, [.. accepts], Optional.None<WeaponLoadout>()));
        static IEnumerable<Slot> CreateOptionalSlot(Optional<ElementId> id, string name, ElementKind accepts) =>
            CreateSlots(id.Match(some => new[] { some.Value }, _ => []), name, accepts);
        static IEnumerable<Slot> CreateWeaponSlots(WeaponLoadout weapon, IEnumerable<ElementId> ids, string name, params ElementKind[] accepts) =>
            ids.Select(id => new Slot(id, $"{weapon.Name} {name}", [.. accepts], Optional.Some(weapon)));

        var exotic = CreateOptionalSlot(build.ExoticArmor, "exotic armor", ElementKind.ExoticArmor);
        var weaponPerks = build.Weapons.SelectMany(w =>
            CreateWeaponSlots(w, ListWeaponPerks(catalog, w), "perk", ElementKind.WeaponPerk, ElementKind.ExoticWeapon)
                .Concat(CreateWeaponSlots(w, ListWeaponCatalysts(catalog, w), "catalyst", ElementKind.Catalyst))
                .Concat(CreateWeaponSlots(w, ListWeaponMods(catalog, w), "mod", ElementKind.WeaponMod)));
        return
        [
            .. CreateOptionalSlot(build.Abilities.Super.ToElement(), "super", ElementKind.Super),
            .. CreateOptionalSlot(build.Abilities.Grenade.ToElement(), "grenade", ElementKind.Grenade),
            .. CreateOptionalSlot(build.Abilities.Melee.ToElement(), "melee", ElementKind.Melee),
            .. CreateOptionalSlot(build.Abilities.ClassAbility.ToElement(), "class ability", ElementKind.ClassAbility),
            .. CreateOptionalSlot(build.Abilities.Jump.ToElement(), "jump", ElementKind.Jump),
            .. CreateSlots(build.Aspects, "aspect", ElementKind.Aspect),
            .. CreateSlots(build.Fragments, "fragment", ElementKind.Fragment),
            .. exotic,
            .. CreateSlots(build.ArmorSetBonuses, "armor set bonus", ElementKind.ArmorSetBonus),
            .. CreateSlots(build.ArmorMods, "armor mod", ElementKind.ArmorMod),
            .. CreateSlots(build.ArtifactPerks, "artifact perk", ElementKind.ArtifactPerk),
            .. weaponPerks,
        ];
    }

    /// <summary>The weapons (by index, in the build's order) whose perks, turned-on catalyst or mod are this element, as the build check equips them.</summary>
    public static ImmutableArray<int> ListWeaponsHolding(RuleCatalog catalog, Build build, ElementId element) =>
    [
        .. build.Weapons
            .Select((weapon, index) => (Index: index, Holds: ListHeldElements(catalog, weapon).Contains(element)))
            .Where(weapon => weapon.Holds)
            .Select(weapon => weapon.Index),
    ];

    /// <summary>
    /// The weapons (by index) one of whose perks, turned-on catalyst or mod has a rule that gives this buff (Slice: the weapon
    /// that rolls Slice). None: no weapon's own perk gives it (Unraveling Rounds comes from an artifact perk).
    /// </summary>
    public static ImmutableArray<int> ListWeaponsGiving(RuleCatalog catalog, Build build, StatusId buff) =>
    [
        .. build.Weapons
            .Select((weapon, index) => (Index: index, Gives: ListHeldElements(catalog, weapon).Any(element => IsGiving(catalog, element, buff))))
            .Where(weapon => weapon.Gives)
            .Select(weapon => weapon.Index),
    ];

    private static IEnumerable<ElementId> ListHeldElements(RuleCatalog catalog, WeaponLoadout weapon) =>
        ListWeaponPerks(catalog, weapon).Concat(ListWeaponCatalysts(catalog, weapon)).Concat(ListWeaponMods(catalog, weapon));

    /// <summary>The element of the weapon's mod, when the catalog knows it by hash.</summary>
    private static IEnumerable<ElementId> ListWeaponMods(RuleCatalog catalog, WeaponLoadout weapon) =>
        ListElement(catalog, weapon.Mod);

    /// <summary>
    /// An element as its weapon holds it: "this weapon" is that weapon (<see cref="WeaponBinding"/>); an element no weapon
    /// holds, or one that names no "this weapon", as the rules author it.
    /// </summary>
    private static BuildElement BindToHolder(BuildElement element, Optional<WeaponLoadout> holder) =>
        holder.Match(
            weapon => element.IsNamingThisWeapon() ? element.BindToWeapon(weapon.Value) : element,
            _ => element);

    /// <summary>The slot of the weapon an element is bound to (<see cref="BindToHolder"/>); none when it isn't bound.</summary>
    private static Optional<WeaponSlot> FindBinding(BuildElement element, Optional<WeaponLoadout> holder) =>
        holder.Bind(weapon => element.IsNamingThisWeapon() ? Optional.Some(weapon.Slot) : Optional.None<WeaponSlot>());

    private static bool IsGiving(RuleCatalog catalog, ElementId element, StatusId buff) =>
        catalog.Elements.TryGetValue(element, out var found)
        && found.Rules.Any(rule => rule.Then.Any(outcome => outcome is Outcome.ApplyBuff applied && applied.Status == buff));

    private static IEnumerable<ElementId> ListWeaponPerks(RuleCatalog catalog, WeaponLoadout weapon) =>
        WeaponRolling.ListPerkSlots(catalog, weapon).SelectMany(slot => slot.Match(
            intrinsic => ListElement(catalog, Optional.Some(intrinsic.Perk)),
            named => [named.Perk],
            fixedPerk => ListElement(catalog, Optional.Some(fixedPerk.Perk)),
            rolling => ListElement(catalog, rolling.Perk),
            _ => []));

    /// <summary>The catalyst element of a weapon whose build turns it on, when the catalog knows it by hash.</summary>
    private static IEnumerable<ElementId> ListWeaponCatalysts(RuleCatalog catalog, WeaponLoadout weapon) =>
        WeaponRolling.ListPerkSlots(catalog, weapon)
            .OfType<WeaponPerkSlot.Catalyst>()
            .Where(catalyst => catalyst.On)
            .SelectMany(catalyst => ListElement(catalog, Optional.Some(catalyst.Plug)));

    private static IEnumerable<ElementId> ListElement(RuleCatalog catalog, Optional<ItemHash> hash) =>
        hash.Bind(catalog.FindElementByHash).Match(element => new[] { element.Value.Id }, _ => []);

    private sealed record RollPick(int Column, ItemHash Hash);

    private static IEnumerable<RollPick> ListPicks(WeaponLoadout weapon) =>
        weapon.Roll.SelectMany((pick, column) => pick.Match(hash => new[] { new RollPick(column, hash.Value) }, _ => []));

    private sealed record ResolvedSlot(Slot Slot, Optional<BuildElement> Element, ImmutableArray<BuildIssue> Issues);

    private static ResolvedSlot ResolveSlot(Build build, RuleCatalog catalog, Slot slot)
    {
        if (!catalog.Elements.TryGetValue(slot.Id, out var element))
        {
            return new ResolvedSlot(slot, Optional.None<BuildElement>(),
                [new BuildIssue(Severity.Blocking, $"Unknown {slot.SlotName} '{slot.Id}' — no such element in the rule catalog.")]);
        }

        ImmutableArray<BuildIssue> issues =
        [
            .. slot.Accepts.Contains(element.Kind)
                ? []
                : new[] { new BuildIssue(Severity.Blocking, $"'{element.Name}' ({element.Kind}) can't go in the {slot.SlotName} slot.") },
            .. element.FitsClass(build.Class)
                ? []
                : new[] { new BuildIssue(Severity.Blocking, $"'{element.Name}' belongs to another class, not {build.Class}.") },
            .. element.FitsSubclass(build.Subclass)
                ? []
                : new[] { new BuildIssue(Severity.Blocking, $"'{element.Name}' ({element.Affinity}) does not fit the {build.Subclass} subclass.") },
        ];
        return new ResolvedSlot(slot, Optional.Some(element), issues);
    }

    /// <summary>
    /// A passive that changes weapon stats for archetypes none of the build's weapons is ("Ionic Overclock changes Linear
    /// Fusion Rifles, Fusion Rifles and Heat Weapons"): it does nothing here. Not said while a weapon's archetype is unknown.
    /// </summary>
    private static IEnumerable<BuildIssue> CheckUnreachedWeapons(Build build, IEnumerable<BuildElement> elements) =>
        build.HasKnownArchetypes()
            ? elements
                .DistinctBy(element => element.Id)
                .SelectMany(element => element.Passives
                    .Select(passive => passive.Modifier)
                    .OfType<Passive.ModifyWeaponStats>()
                    .Where(weapons => PassiveReaching.FindReach(build, weapons) is PassiveReach.NoWeapon)
                    .SelectMany(weapons => weapons.Weapons.Match<ImmutableArray<ImmutableArray<string>>>(
                        ofArchetypes => [ofArchetypes.Archetypes],
                        _ => [],
                        _ => []))
                    .Select(archetypes => new BuildIssue(
                        Severity.Info,
                        $"{element.Name} changes {DomainPhrasing.DescribeArchetypes(archetypes)}, and no weapon in this build is one, so it does nothing here.")))
            : [];

    /// <summary>
    /// A weapon's roll, against the manifest excerpt: a pick outside its column's perks (or past its last column) is a
    /// warning, and so is a named perk whose column holds another; a pick the catalog doesn't know is part of the
    /// weapon, not of the trace.
    /// </summary>
    private static IEnumerable<BuildIssue> CheckRolls(Build build, RuleCatalog catalog) =>
        build.Weapons.SelectMany(weapon => CheckRoll(catalog, weapon));

    private static IEnumerable<BuildIssue> CheckRoll(RuleCatalog catalog, WeaponLoadout weapon)
    {
        var columns = catalog.Manifest.ListTraitColumns(weapon);
        var picks = ListPicks(weapon).ToImmutableArray();
        var misplaced = columns.IsEmpty
            ? []
            : picks
                .Where(pick => pick.Column >= columns.Length || !columns[pick.Column].Options.Contains(pick.Hash))
                .Select(pick => new BuildIssue(Severity.Warning,
                    $"{weapon.Name}'s roll: {catalog.Manifest.DescribeTypedItemHash(pick.Hash)} isn't a perk of its column {pick.Column + 1} in the manifest excerpt."));
        var displaced = WeaponRolling.ListDisplacedPerks(catalog, weapon)
            .Select(id => new BuildIssue(Severity.Warning,
                $"{weapon.Name}: '{(catalog.Elements.TryGetValue(id, out var element) ? element.Name : id.Value)}' gives way to the other perk of its column — a column holds one perk."));
        var catalyst = CheckCatalyst(catalog, weapon);
        var mod = CheckMod(catalog, weapon);
        return
        [
            .. misplaced,
            .. displaced,
            .. catalyst,
            .. mod,
        ];
    }

    /// <summary>
    /// A catalyst turned on that the excerpt doesn't give the weapon is a warning. One the catalog doesn't know says
    /// nothing here: its square carries the ⊘.
    /// </summary>
    private static IEnumerable<BuildIssue> CheckCatalyst(RuleCatalog catalog, WeaponLoadout weapon) =>
        !weapon.Catalyst
            ? []
            : catalog.Manifest.FindWeaponKind(weapon).Bind(kind => kind.Catalyst).Match(
                _ => [],
                _ => new[] { new BuildIssue(Severity.Warning, $"{weapon.Name} has no catalyst in the manifest excerpt, so catalyst: true does nothing.") });

    /// <summary>
    /// A weapon's mod: one its mod socket doesn't take (only when the excerpt lists what it takes) is a warning. One the
    /// catalog doesn't know says nothing here: its square carries the ⊘.
    /// </summary>
    private static IEnumerable<BuildIssue> CheckMod(RuleCatalog catalog, WeaponLoadout weapon) =>
        weapon.Mod.Match(
            mod =>
            {
                var takes = catalog.Manifest.FindWeaponKind(weapon).Map(kind => kind.Mods).UnwrapOr([]);
                return !takes.IsEmpty && !takes.Contains(mod.Value)
                    ? new[] { new BuildIssue(Severity.Warning, $"{weapon.Name}'s mod: {catalog.Manifest.DescribeTypedItemHash(mod.Value)} isn't a mod its mod socket takes in the manifest excerpt.") }
                    : [];
            },
            _ => []);

    private static IEnumerable<BuildIssue> CheckAspects(Build build, RuleCatalog catalog)
    {
        if (build.Aspects.Length > MaxAspects)
        {
            yield return new BuildIssue(Severity.Blocking, $"{build.Aspects.Length} aspects equipped; at most {MaxAspects}.");
        }

        foreach (var duplicate in build.Aspects.Concat(build.Fragments).GroupBy(id => id).Where(g => g.Count() > 1))
        {
            var name = catalog.Elements.TryGetValue(duplicate.Key, out var element) ? element.Name : duplicate.Key.Value;
            yield return new BuildIssue(Severity.Blocking, $"'{name}' is equipped {duplicate.Count()} times; aspects and fragments are unique.");
        }
    }

    /// <summary>
    /// The DIM loadout's raid, activity and retired seasonal mods the build ignores (ADRs D35): one info line naming
    /// them, each once ("Herd Thinner ×2").
    /// </summary>
    private static IEnumerable<BuildIssue> CheckIgnoredMods(Build build, RuleCatalog catalog)
    {
        var ignored = LeftOutClassifying.ListIgnored(catalog, build)
            .GroupBy(item => item.Hash)
            .Select(copies => $"{catalog.Manifest.DescribeItemHash(copies.Key)}{(copies.Count() > 1 ? $" ×{copies.Count()}" : "")}")
            .ToImmutableArray();
        return ignored.IsEmpty
            ? []
            : [new BuildIssue(Severity.Info, $"Ignored from the DIM loadout: {string.Join(", ", ignored)} — {LeftOutClassifying.ActivityModsIgnored}.")];
    }

    private static IEnumerable<BuildIssue> CheckFragmentSlots(Build build, RuleCatalog catalog)
    {
        var aspects = build.Aspects
            .Select(id => catalog.Elements.GetValueOrDefault(id))
            .OfType<BuildElement>()
            .ToImmutableArray();
        var leftOutAspects = build.LeftOut
            .Where(item => item.Part == LoadoutPart.SubclassPlug)
            .Select(item => catalog.Manifest.FindItem(item.Hash))
            .SelectMany(item => item.Match(found => found.Value.Kind is ManifestKind.Aspect ? new[] { found.Value.Name } : [], _ => []));
        var unknown = aspects.Where(a => !a.FragmentSlots.IsSome()).Select(a => a.Name).Concat(leftOutAspects).ToImmutableArray();
        if (!unknown.IsEmpty)
        {
            yield return new BuildIssue(Severity.Info, $"Fragment slots unknown for {string.Join(", ", unknown)} — fragment count not checked.");
            yield break;
        }

        var slots = aspects.Sum(a => a.FragmentSlots.UnwrapOr(0));
        if (build.Fragments.Length > slots)
        {
            yield return new BuildIssue(Severity.Blocking, $"{build.Fragments.Length} fragments equipped; the aspects grant {slots} slots.");
        }
    }

    /// <summary>
    /// What an equipped element's stat checks say at the build's stats (Gambler's Dodge below 50 Melee): each check
    /// whose stat the build knows and whose range holds it, once per element. An unknown stat says nothing.
    /// </summary>
    private static IEnumerable<BuildIssue> CheckStats(Build build, IEnumerable<BuildElement> elements) =>
        elements
            .DistinctBy(e => e.Id)
            .SelectMany(e => e.StatChecks)
            .SelectMany(check => build.Stats.ReadStat(check.Stat).Match(
                known => IsInRange(check, known.Value.Value)
                    ? new[] { new BuildIssue(ToSeverity(check.Severity), check.Says.Replace("{value}", known.Value.Value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)) }
                    : [],
                _ => []));

    private static bool IsInRange(StatCheck check, int value) =>
        value < check.Below.Value && check.From.Match(from => value >= from.Value.Value, _ => true);

    private static Severity ToSeverity(StatCheckSeverity severity) =>
        severity == StatCheckSeverity.Warning ? Severity.Warning : Severity.Info;

    /// <summary>
    /// A rule that doesn't stack with another equipped element gives nothing when both fire: a note for the build
    /// crafter, who may keep both for what else each gives (not a mistake, so not a warning).
    /// </summary>
    private static IEnumerable<BuildIssue> CheckNotStacking(RuleCatalog catalog, Optional<DamageType> superType, IEnumerable<BuildElement> elements)
    {
        var distinct = elements.DistinctBy(e => e.Id).ToImmutableArray();
        return distinct.SelectMany(element => element.Rules
            .SelectMany(rule => distinct
                .Where(partner => partner.Id != element.Id && rule.DoesNotStackWith.Contains(partner.Id))
                .Select(partner => new BuildIssue(
                    Severity.Info,
                    $"'{element.Name}': {catalog.Glossary.DescribeOutcomes(rule.Then.Select(o => new OutcomeMention(o, 1)), superType)} on "
                    + $"\"{catalog.Glossary.DescribeTrigger(rule.On, superType)}\" doesn't stack with '{partner.Name}'."))));
    }

    private static IEnumerable<BuildIssue> CheckPinnedCatalog(Build build, RuleCatalog catalog) =>
        build.PinnedCatalog.Match(
            pinned => pinned.Value == catalog.Version
                ? []
                : new[] { new BuildIssue(Severity.Info, $"Build pinned catalog {pinned.Value}; using {catalog.Version}.") },
            _ => []);

    /// <summary>
    /// Copies of one element stack (armor mods: the count picks the value by copies). An element bound to the weapon
    /// holding it (its "this weapon") is that weapon's: two weapons holding it are two elements, each with its own rules.
    /// </summary>
    private static ImmutableArray<EquippedElement> GroupCopies(IEnumerable<(Slot Slot, BuildElement Element, Optional<WeaponSlot> BoundTo)> elements) =>
        elements
            .GroupBy(e => (e.Element.Id, e.BoundTo))
            .Select(group => new EquippedElement(group.First().Element, group.Count()))
            .ToImmutableArray();

    private static ImmutableArray<EquippedElement> ListKeywordElements(RuleCatalog catalog) =>
        catalog.Elements.Values
            .Where(e => e.Kind == ElementKind.Keyword)
            .OrderBy(e => e.Id.Value, StringComparer.Ordinal)
            .Select(e => new EquippedElement(e, 1))
            .ToImmutableArray();
}

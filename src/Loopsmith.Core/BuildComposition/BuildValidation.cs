using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// The sole constructor of <see cref="ValidatedBuild"/> (architecture test). Blocking issues put the
/// build on the failure track; warnings and info ride along on the validated build.
/// </summary>
public static class BuildValidation
{
    private const int MaxAspects = 2;

    private sealed record Slot(ElementId Id, string SlotName, ImmutableArray<ElementKind> Accepts);

    public static Result<ValidatedBuild, string> ValidateBuild(Build build, RuleCatalog catalog)
    {
        var slots = ListSlots(build);
        var resolved = slots.Select(slot => ResolveSlot(build, catalog, slot)).ToImmutableArray();
        var elements = resolved
            .SelectMany(r => r.Element.Match(some => new[] { (r.Slot, Element: some.Value) }, _ => []))
            .ToImmutableArray();
        var issues = resolved.SelectMany(r => r.Issues)
            .Concat(CheckAspects(build, catalog))
            .Concat(CheckFragmentSlots(build, catalog))
            .Concat(CheckInertElements(elements.Select(e => e.Element)))
            .Concat(CheckPinnedCatalog(build, catalog))
            .ToImmutableArray();

        var blocking = issues.Where(issue => issue.Severity == Severity.Blocking).ToImmutableArray();
        if (!blocking.IsEmpty)
        {
            var message = string.Join(Environment.NewLine, blocking.Select(issue => $"✗ {issue.Message}"));
            return new Result<ValidatedBuild, string>.Error($"Build '{build.Name}' is invalid:{Environment.NewLine}{message}");
        }

        var equipped = GroupCopies(elements.Select(e => e.Element)).AddRange(ListKeywordElements(catalog));
        return new Result<ValidatedBuild, string>.Ok(new ValidatedBuild(build, catalog, equipped, issues));
    }

    private static ImmutableArray<Slot> ListSlots(Build build)
    {
        static Slot Single(ElementId id, string name, params ElementKind[] accepts) => new(id, name, [.. accepts]);
        static IEnumerable<Slot> Many(IEnumerable<ElementId> ids, string name, params ElementKind[] accepts) =>
            ids.Select(id => new Slot(id, name, [.. accepts]));

        var exotic = build.ExoticArmor.Match(
            some => new[] { Single(some.Value, "exotic armor", ElementKind.ExoticArmor) },
            _ => []);
        var weaponPerks = build.Weapons.SelectMany(w =>
            Many(w.Perks, $"{w.Name} perk", ElementKind.WeaponPerk, ElementKind.ExoticWeapon));
        return
        [
            Single(build.Abilities.Super, "super", ElementKind.Super),
            Single(build.Abilities.Grenade, "grenade", ElementKind.Grenade),
            Single(build.Abilities.Melee, "melee", ElementKind.Melee),
            Single(build.Abilities.ClassAbility, "class ability", ElementKind.ClassAbility),
            .. Many(build.Aspects, "aspect", ElementKind.Aspect),
            .. Many(build.Fragments, "fragment", ElementKind.Fragment),
            .. exotic,
            .. Many(build.ArmorSetBonuses, "armor set bonus", ElementKind.ArmorSetBonus),
            .. Many(build.ArmorMods, "armor mod", ElementKind.ArmorMod),
            .. Many(build.ArtifactPerks, "artifact perk", ElementKind.ArtifactPerk),
            .. weaponPerks,
        ];
    }

    private sealed record ResolvedSlot(Slot Slot, Optional<BuildElement> Element, ImmutableArray<BuildIssue> Issues);

    private static ResolvedSlot ResolveSlot(Build build, RuleCatalog catalog, Slot slot)
    {
        if (!catalog.Elements.TryGetValue(slot.Id, out var element))
        {
            return new ResolvedSlot(slot, Optional.None<BuildElement>(),
                [new BuildIssue(Severity.Blocking, $"Unknown {slot.SlotName} '{slot.Id}' — no such element in the rule catalog.")]);
        }

        var issues = new[]
            {
                slot.Accepts.Contains(element.Kind)
                    ? null
                    : new BuildIssue(Severity.Blocking, $"'{element.Name}' is a {element.Kind}, not a {slot.SlotName}."),
                element.Class.Match(c => c.Value == build.Class, _ => true)
                    ? null
                    : new BuildIssue(Severity.Blocking, $"'{element.Name}' belongs to another class, not {build.Class}."),
                IsSubclassCompatible(build.Subclass, element)
                    ? null
                    : new BuildIssue(Severity.Blocking, $"'{element.Name}' ({element.Affinity}) does not fit a {build.Subclass} subclass."),
            }
            .OfType<BuildIssue>()
            .ToImmutableArray();
        return new ResolvedSlot(slot, Optional.Some(element), issues);
    }

    private static bool IsSubclassCompatible(Subclass subclass, BuildElement element)
    {
        var isSubclassBound = element.Kind is ElementKind.Super or ElementKind.Grenade or ElementKind.Melee
            or ElementKind.Aspect or ElementKind.Fragment;
        var isNeutral = element.Affinity is Affinity.Neutral or Affinity.Kinetic;
        return !isSubclassBound || isNeutral || subclass == Subclass.Prismatic || element.Affinity.ToString() == subclass.ToString();
    }

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

    private static IEnumerable<BuildIssue> CheckFragmentSlots(Build build, RuleCatalog catalog)
    {
        var aspects = build.Aspects
            .Select(id => catalog.Elements.GetValueOrDefault(id))
            .OfType<BuildElement>()
            .ToImmutableArray();
        var unknown = aspects.Where(a => !a.FragmentSlots.IsSome()).Select(a => a.Name).ToImmutableArray();
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

    private static IEnumerable<BuildIssue> CheckInertElements(IEnumerable<BuildElement> elements) =>
        elements
            .DistinctBy(e => e.Id)
            .Where(e => e.Rules.IsEmpty && e.Passives.IsEmpty)
            .Select(e => new BuildIssue(Severity.Warning, $"'{e.Name}' has no authored rules yet — it is inert in the trace."));

    private static IEnumerable<BuildIssue> CheckPinnedCatalog(Build build, RuleCatalog catalog) =>
        build.PinnedCatalog.Match(
            pinned => pinned.Value == catalog.Version
                ? []
                : new[] { new BuildIssue(Severity.Info, $"Build pinned catalog {pinned.Value}; simulating with {catalog.Version}.") },
            _ => []);

    private static ImmutableArray<EquippedElement> GroupCopies(IEnumerable<BuildElement> elements) =>
        elements
            .GroupBy(e => e.Id)
            .Select(group => new EquippedElement(group.First(), group.Count()))
            .ToImmutableArray();

    private static ImmutableArray<EquippedElement> ListKeywordElements(RuleCatalog catalog) =>
        catalog.Elements.Values
            .Where(e => e.Kind == ElementKind.Keyword)
            .OrderBy(e => e.Id.Value, StringComparer.Ordinal)
            .Select(e => new EquippedElement(e, 1))
            .ToImmutableArray();
}

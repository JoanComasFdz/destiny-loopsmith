using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

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
            .Concat(CheckUnknownAbilities(build))
            .Concat(CheckLeftOut(build))
            .Concat(CheckAspects(build, catalog))
            .Concat(CheckFragmentSlots(build, catalog))
            .Concat(CheckInertElements(elements.Select(e => e.Element)))
            .Concat(CheckNotStacking(catalog, elements.Select(e => e.Element).Concat(ListKeywordElements(catalog).Select(e => e.Element))))
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
        static IEnumerable<Slot> CreateSlots(IEnumerable<ElementId> ids, string name, params ElementKind[] accepts) =>
            ids.Select(id => new Slot(id, name, [.. accepts]));
        static IEnumerable<Slot> CreateOptionalSlot(Optional<ElementId> id, string name, ElementKind accepts) =>
            CreateSlots(id.Match(some => new[] { some.Value }, _ => []), name, accepts);

        var exotic = CreateOptionalSlot(build.ExoticArmor, "exotic armor", ElementKind.ExoticArmor);
        var weaponPerks = build.Weapons.SelectMany(w =>
            CreateSlots(w.Perks, $"{w.Name} perk", ElementKind.WeaponPerk, ElementKind.ExoticWeapon));
        return
        [
            .. CreateOptionalSlot(build.Abilities.Super, "super", ElementKind.Super),
            .. CreateOptionalSlot(build.Abilities.Grenade, "grenade", ElementKind.Grenade),
            .. CreateOptionalSlot(build.Abilities.Melee, "melee", ElementKind.Melee),
            .. CreateOptionalSlot(build.Abilities.ClassAbility, "class ability", ElementKind.ClassAbility),
            .. CreateSlots(build.Aspects, "aspect", ElementKind.Aspect),
            .. CreateSlots(build.Fragments, "fragment", ElementKind.Fragment),
            .. exotic,
            .. CreateSlots(build.ArmorSetBonuses, "armor set bonus", ElementKind.ArmorSetBonus),
            .. CreateSlots(build.ArmorMods, "armor mod", ElementKind.ArmorMod),
            .. CreateSlots(build.ArtifactPerks, "artifact perk", ElementKind.ArtifactPerk),
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

        ImmutableArray<BuildIssue> issues =
        [
            .. slot.Accepts.Contains(element.Kind)
                ? []
                : new[] { new BuildIssue(Severity.Blocking, $"'{element.Name}' ({element.Kind}) can't go in the {slot.SlotName} slot.") },
            .. element.Class.Match(c => c.Value == build.Class, _ => true)
                ? []
                : new[] { new BuildIssue(Severity.Blocking, $"'{element.Name}' belongs to another class, not {build.Class}.") },
            .. IsSubclassCompatible(build.Subclass, element)
                ? []
                : new[] { new BuildIssue(Severity.Blocking, $"'{element.Name}' ({element.Affinity}) does not fit the {build.Subclass} subclass.") },
        ];
        return new ResolvedSlot(slot, Optional.Some(element), issues);
    }

    private static bool IsSubclassCompatible(Subclass subclass, BuildElement element)
    {
        var isSubclassBound = element.Kind is ElementKind.Super or ElementKind.Grenade or ElementKind.Melee
            or ElementKind.Aspect or ElementKind.Fragment;
        var isNeutral = element.Affinity is Affinity.Neutral or Affinity.Kinetic;
        return !isSubclassBound || isNeutral || subclass == Subclass.Prismatic || element.Affinity == subclass.ToAffinity();
    }

    /// <summary>An ability given as "?" is part of the build but not of the trace: it sets nothing off.</summary>
    private static IEnumerable<BuildIssue> CheckUnknownAbilities(Build build)
    {
        var abilities = build.Abilities;
        var unknown = new[]
            {
                ("super", abilities.Super), ("grenade", abilities.Grenade), ("melee", abilities.Melee),
                ("class ability", abilities.ClassAbility),
            }
            .Where(slot => !slot.Item2.IsSome())
            .Select(slot => slot.Item1)
            .ToImmutableArray();
        return unknown.IsEmpty
            ? []
            : [new BuildIssue(Severity.Info, $"Unknown ({DomainPhrasing.Unknown}): {string.Join(", ", unknown)} — not in the rule catalog yet, so {(unknown.Length == 1 ? "it sets" : "they set")} nothing off.")];
    }

    private static IEnumerable<BuildIssue> CheckLeftOut(Build build) =>
        build.LeftOut.IsEmpty
            ? []
            : [new BuildIssue(Severity.Info, $"Left out of the build: {build.LeftOut.DescribeLeftOut()} from the DIM loadout — not in the rule catalog yet.")];

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

    /// <summary>
    /// A rule that doesn't stack with another equipped element gives nothing when both fire: wasted potential the
    /// build crafter should see before playing it.
    /// </summary>
    private static IEnumerable<BuildIssue> CheckNotStacking(RuleCatalog catalog, IEnumerable<BuildElement> elements)
    {
        var distinct = elements.DistinctBy(e => e.Id).ToImmutableArray();
        return distinct.SelectMany(element => element.Rules
            .SelectMany(rule => distinct
                .Where(partner => partner.Id != element.Id && rule.DoesNotStackWith.Contains(partner.Id))
                .Select(partner => new BuildIssue(
                    Severity.Warning,
                    $"'{element.Name}': {catalog.Glossary.DescribeOutcomes(rule.Then.Select(o => new OutcomeMention(o, 1)))} on "
                    + $"\"{catalog.Glossary.DescribeTrigger(rule.On)}\" doesn't stack with '{partner.Name}' — with both equipped, it is wasted."))));
    }

    private static IEnumerable<BuildIssue> CheckPinnedCatalog(Build build, RuleCatalog catalog) =>
        build.PinnedCatalog.Match(
            pinned => pinned.Value == catalog.Version
                ? []
                : new[] { new BuildIssue(Severity.Info, $"Build pinned catalog {pinned.Value}; using {catalog.Version}.") },
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

using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.RepoFileChecking;

/// <summary>
/// Where an element's keys say it goes: one file, anywhere outside the class and subclass folders, or nowhere (its keys
/// contradict each other); <c>Says</c> is what the test says when the element is elsewhere.
/// </summary>
[Union]
public partial record RuleFilePlace
{
    partial record InFile(string File, string Says);
    partial record Outside(string Says);
    partial record Nowhere(string Says);
}

/// <summary>
/// Every element of <c>rules/</c> is in the file its own keys name (docs/rule-format.md, "Files"; ADRs D40): class, then
/// subclass, then area (<c>hunter/arc/melees.yaml</c>), what every class gets on a subclass in <c>all-classes/</c>, and
/// the rest outside them. The grouping is what names the rules, their e2e tests and their recordings, so it is held here.
/// Run it alone: <c>dotnet test --filter RuleFileGroupingTests</c>.
/// </summary>
public sealed class RuleFileGroupingTests
{
    private const string AllClasses = "all-classes";

    private static readonly ImmutableArray<string> ClassFolders = [.. Enum.GetValues<GuardianClass>().Select(ToFolder), AllClasses];

    [Fact]
    public void Every_element_is_in_the_file_its_keys_name()
    {
        var catalog = RepoCatalog.Catalog;                                                        // impure
        var misplaced = catalog.Elements.Values                                                   // pure
            .Where(element => element.RuleFile.Length > 0)
            .Select(element => (Element: element, Place: FindPlace(element)))
            .Where(found => !IsIn(found.Place, found.Element.RuleFile))
            .Select(found => $"'{found.Element.Id}' is in {found.Element.RuleFile}: {DescribePlace(found.Place)}")
            .Order(StringComparer.Ordinal);

        Assert.Empty(misplaced);
    }

    [Theory]
    [InlineData(ElementKind.Melee, "hunter", Affinity.Arc, "hunter/arc/melees.yaml")]
    [InlineData(ElementKind.Super, "titan", Affinity.Solar, "titan/solar/supers.yaml")]
    [InlineData(ElementKind.Aspect, "warlock", Affinity.Prismatic, "warlock/prismatic/aspects.yaml")]
    [InlineData(ElementKind.Jump, "hunter", Affinity.Arc, "hunter/arc/jumps.yaml")]
    [InlineData(ElementKind.Fragment, "", Affinity.Arc, "all-classes/arc/fragments.yaml")]
    [InlineData(ElementKind.Grenade, "", Affinity.Strand, "all-classes/strand/grenades.yaml")]
    [InlineData(ElementKind.Grenade, "hunter", Affinity.Arc, "hunter/arc/grenades.yaml")]
    [InlineData(ElementKind.ClassAbility, "hunter", Affinity.Arc, "hunter/class-abilities.yaml")]
    [InlineData(ElementKind.ExoticArmor, "hunter", Affinity.Arc, "hunter/exotic-armor.yaml")]
    public void A_subclass_or_class_element_has_one_file(ElementKind kind, string guardianClass, Affinity affinity, string file)
    {
        var place = FindPlace(ToElement(kind, guardianClass, affinity));

        Assert.Equal(new RuleFilePlace.InFile(file, $"belongs in {file}"), place);
        Assert.True(IsIn(place, file));
    }

    [Theory]
    [InlineData(ElementKind.ArmorMod, "mods/arms.yaml", true)]
    [InlineData(ElementKind.Keyword, "keywords/arc.yaml", true)]
    [InlineData(ElementKind.WeaponPerk, "hunter/weapon-perks.yaml", false)]
    [InlineData(ElementKind.ArtifactPerk, "all-classes/arc/artifact.yaml", false)]
    public void Any_other_element_is_outside_the_class_and_subclass_folders(ElementKind kind, string file, bool fits)
    {
        var place = FindPlace(ToElement(kind, "", Affinity.Neutral));

        Assert.Equal(fits, IsIn(place, file));
    }

    [Theory]
    [InlineData(ElementKind.Melee, Affinity.Arc, "is one class's melee: it needs a class")]
    [InlineData(ElementKind.ClassAbility, Affinity.Arc, "is a class ability: it needs a class")]
    [InlineData(ElementKind.Fragment, Affinity.Neutral, "is a subclass's fragment: its affinity (neutral) is no subclass's")]
    public void An_element_whose_keys_name_no_file_is_misplaced_anywhere(ElementKind kind, Affinity affinity, string says)
    {
        var place = FindPlace(ToElement(kind, "", affinity));

        Assert.Equal(new RuleFilePlace.Nowhere(says), place);
        Assert.False(IsIn(place, "hunter/arc/melees.yaml"));
    }

    /// <summary>The element's file, from its kind, its class and its affinity (docs/rule-format.md, "Files").</summary>
    private static RuleFilePlace FindPlace(BuildElement element) =>
        element.Kind switch
        {
            ElementKind.Super or ElementKind.Melee or ElementKind.Aspect or ElementKind.Jump =>
                element.Class.Match(
                    owner => ToSubclassFile(ToFolder(owner.Value), element),
                    _ => new RuleFilePlace.Nowhere($"is one class's {ToWord(element.Kind)}: it needs a class")),
            ElementKind.Grenade or ElementKind.Fragment =>
                ToSubclassFile(element.Class.Match(owner => ToFolder(owner.Value), _ => AllClasses), element),
            ElementKind.ClassAbility =>
                element.Class.Match(
                    owner => ToFilePlace($"{ToFolder(owner.Value)}/class-abilities.yaml"),
                    _ => new RuleFilePlace.Nowhere("is a class ability: it needs a class")),
            ElementKind.ExoticArmor =>
                element.Class.Match(owner => ToFilePlace($"{ToFolder(owner.Value)}/exotic-armor.yaml"), _ => ToOutside()),
            _ => ToOutside(),
        };

    private static RuleFilePlace ToSubclassFile(string folder, BuildElement element) =>
        ToSubclassFolder(element.Affinity).Match(
            subclass => ToFilePlace($"{folder}/{subclass.Value}/{ToArea(element.Kind)}.yaml"),
            _ => new RuleFilePlace.Nowhere($"is a subclass's {ToWord(element.Kind)}: its affinity ({element.Affinity.ToString().ToLowerInvariant()}) is no subclass's"));

    /// <summary>In its one file, or, for an element with none, in no class or subclass folder.</summary>
    private static bool IsIn(RuleFilePlace place, string file) =>
        place.Match(
            inFile => inFile.File == file,
            _ => !ClassFolders.Any(folder => file.StartsWith(folder + "/", StringComparison.Ordinal)),
            _ => false);

    private static string DescribePlace(RuleFilePlace place) =>
        place.Match(inFile => inFile.Says, outside => outside.Says, nowhere => nowhere.Says);

    private static RuleFilePlace ToFilePlace(string file) => new RuleFilePlace.InFile(file, $"belongs in {file}");

    private static RuleFilePlace ToOutside() =>
        new RuleFilePlace.Outside($"belongs outside {string.Join(", ", ClassFolders.Select(folder => folder + "/"))}");

    private static Optional<string> ToSubclassFolder(Affinity affinity) =>
        affinity is Affinity.Neutral or Affinity.Kinetic
            ? Optional.None<string>()
            : Optional.Some(affinity.ToString().ToLowerInvariant());

    private static string ToArea(ElementKind kind) =>
        kind switch
        {
            ElementKind.Super => "supers",
            ElementKind.Grenade => "grenades",
            ElementKind.Melee => "melees",
            ElementKind.Aspect => "aspects",
            ElementKind.Jump => "jumps",
            ElementKind.Fragment => "fragments",
            _ => kind.ToString().ToLowerInvariant(),
        };

    private static string ToWord(ElementKind kind) =>
        kind == ElementKind.ClassAbility ? "class ability" : kind.ToString().ToLowerInvariant();

    private static string ToFolder(GuardianClass guardianClass) => guardianClass.ToString().ToLowerInvariant();

    private static BuildElement ToElement(ElementKind kind, string guardianClass, Affinity affinity) =>
        new(
            ElementId.From("probe"), "Probe", kind, affinity,
            guardianClass.Length == 0 ? Optional.None<GuardianClass>() : Optional.Some(Enum.Parse<GuardianClass>(guardianClass, ignoreCase: true)),
            [], Optional.None<string>(), [], [], Optional.None<AbilityProfile>(), Optional.None<FragmentSlots>(),
            new Provenance.Authored("probe.yaml", 1), Optional.None<string>(), []);
}

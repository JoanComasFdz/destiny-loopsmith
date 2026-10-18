using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using YamlDotNet.RepresentationModel;
using static Loopsmith.Core.RuleParsing.ResultAccumulation;
using static Loopsmith.Core.RuleParsing.ValueReading;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary>
/// The elements of one rules file that parsed (a weapon perk's enhanced trait among them, after its perk), each weapon
/// perk with an enhanced trait → that trait's element, and the errors of those that did not parse.
/// </summary>
internal sealed record ElementsFile(ImmutableArray<Located<BuildElement>> Elements, ImmutableArray<(ElementId Perk, ElementId Enhanced)> EnhancedPerks, Errors Errors);

/// <summary>A <c>rules/**/*.yaml</c> file other than the glossary: <c>elements: [ … ]</c>.</summary>
internal static class ElementParsing
{
    private static readonly ImmutableArray<string> FileKeys = ["elements"];

    private static readonly ImmutableArray<string> ElementKeys =
    [
        "id", "name", "kind", "affinity", "class", "hash", "fragmentSlots", "ability", "description", "source",
        "rules", "passives", "inert", "statChecks", "enhanced",
    ];

    private static readonly ImmutableArray<string> StatCheckKeys = ["stat", "from", "below", "severity", "says"];

    private static readonly ImmutableArray<string> AbilityKeys = ["kind", "charges", "chunkScalar", "baseCooldown"];
    private static readonly ImmutableArray<string> CreatorClaimKeys = ["creator", "quote"];

    /// <summary>Elements are parsed one by one, so one broken element does not hide the others' errors.</summary>
    internal static ElementsFile ReadElementsFile(SourceText file, ReferenceScope scope) =>
        YamlReading.LoadDocument(file, "rules file")
            .Bind(root => root.ToMap())
            .Bind(map => Combine(
                map.CheckKeys(FileKeys),
                map.ReadRequired("elements", elements => elements.ToItems("element")),
                (_, items) => items))
            .Match(
                ok => ToElementsFile(ok.Value.Select(item => ReadElement(scope, item))),
                error => new ElementsFile([], [], error.Failure));

    private static ElementsFile ToElementsFile(IEnumerable<Result<(Located<BuildElement> Element, Optional<Located<BuildElement>> Enhanced), Errors>> elements)
    {
        var (parsed, errors) = elements.Partition();
        return new ElementsFile(
            [.. parsed.SelectMany(read => read.Enhanced.Match(enhanced => new[] { read.Element, enhanced.Value }, _ => [read.Element]))],
            [.. parsed.SelectMany(read => read.Enhanced.Match(enhanced => new[] { (read.Element.Value.Id, enhanced.Value.Value.Id) }, _ => []))],
            errors);
    }

    /// <summary>An element and, for a weapon perk with an <c>enhanced:</c> block, its enhanced trait's (EnhancedPerkParsing).</summary>
    private static Result<(Located<BuildElement> Element, Optional<Located<BuildElement>> Enhanced), Errors> ReadElement(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
                map.CheckKeys(ElementKeys),
                map.ReadRequired("id", ReadElementId),
                map.ReadRequired("name", ReadName),
                map.ReadRequired("kind", ReadVocabularyWord<ElementKind>),
                map.ReadRequired("affinity", ReadVocabularyWord<Affinity>),
                map.ReadOptional("class", ReadVocabularyWord<GuardianClass>),
                map.ReadOrDefault("hash", ReadItemHashes, []),
                map.ReadOptional("description", YamlReading.ToText),
                map.ReadOrDefault("rules", rules => rules.ReadEach("rule", rule => RuleBodyParsing.ReadRule(scope, rule)), []),
                map.ReadOrDefault(
                    "passives",
                    passives => passives.ReadEach("passive", passive => RuleBodyParsing.ReadPassiveRule(scope, passive)),
                    []),
                map.ReadOptional("ability", ReadAbilityProfile),
                map.ReadOptional("fragmentSlots", ReadFragmentSlots),
                map.ReadOrDefault("source", ReadProvenance, ToAuthored(map)),
                map.ReadOptional("inert", YamlReading.ToText),
                map.ReadOrDefault("statChecks", checks => checks.ReadEach("stat check", ReadStatCheck), []),
                (_, id, name, kind, affinity, guardianClass, hashes, description, rules, passives, ability, fragmentSlots, source, inert, statChecks) =>
                    new BuildElement(
                        id, name, kind, affinity, guardianClass, hashes, description, rules, passives, ability, fragmentSlots, source, inert, statChecks, map.Path))
            .Bind(element => Combine(
                CheckFragmentSlots(map, element),
                CheckAbilityProfile(map, element),
                CheckInert(map, element),
                CheckThisWeapon(map, element),
                EnhancedPerkParsing.ReadEnhanced(scope, map, element),
                (_, _, _, _, enhanced) => (map.ToLocated(element), enhanced.Map(map.ToLocated)))));

    /// <summary>
    /// <c>{ stat: melee, from: 50, below: 100, severity: info, says: "With {value} Melee, …" }</c>: <c>from</c> is optional,
    /// both are stat values (0..200) and <c>from</c> is below <c>below</c>; <c>severity</c> is <c>warning</c> or <c>info</c>.
    /// </summary>
    private static Result<StatCheck, Errors> ReadStatCheck(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
                map.CheckKeys(StatCheckKeys),
                map.ReadRequired("stat", ReadVocabularyWord<StatKind>),
                map.ReadOptional("from", ReadStatValue),
                map.ReadRequired("below", ReadStatValue),
                map.ReadRequired("severity", ReadVocabularyWord<StatCheckSeverity>),
                map.ReadRequired("says", ReadName),
                (_, stat, from, below, severity, says) => new StatCheck(stat, from, below, severity, says))
            .Bind(check => CheckStatRange(map, check)));

    private static Result<StatCheck, Errors> CheckStatRange(YamlMap map, StatCheck check) =>
        check.From.Match(from => from.Value.Value >= check.Below.Value, _ => false)
            ? map.FailAtKey<StatCheck>("below", "a stat check's range starts below its end (from < below)")
            : Succeed(check);

    /// <summary>An element with a reason to have no rules has none: <c>inert</c> with rules or passives would hide a real gap.</summary>
    private static Result<Unit, Errors> CheckInert(YamlMap map, BuildElement element) =>
        element.Inert.IsSome() && (!element.Rules.IsEmpty || !element.Passives.IsEmpty)
            ? map.FailAtKey<Unit>("inert", "inert is only for an element with no rules and no passives")
            : Succeed<Unit>(new Unit.Value());

    /// <summary>"This weapon" (<c>weapon:this</c>) is the weapon holding the element: only an element a weapon holds has one.</summary>
    private static Result<Unit, Errors> CheckThisWeapon(YamlMap map, BuildElement element) =>
        element.IsNamingThisWeapon() && !element.Kind.IsHeldByWeapon()
            ? map.FailAtKey<Unit>(
                "rules",
                "weapon:this is the weapon holding the element: only a weaponPerk, weaponMod, exoticWeapon or catalyst has one, "
                + $"not a {GameNotationParsing.ToVocabularyWord(element.Kind)}")
            : Succeed<Unit>(new Unit.Value());

    /// <summary>An omitted <c>source</c> means "authored here": the file and the element's first line.</summary>
    private static Provenance ToAuthored(YamlMap map) =>
        new Provenance.Authored(map.Path, (int)Math.Min(map.Node.Start.Line, int.MaxValue));

    private static Result<Provenance, Errors> ReadProvenance(YamlValue value) =>
        value.Node is YamlMappingNode
            ? value.ToMap().Bind(map => Combine(
                    map.CheckKeys(CreatorClaimKeys),
                    map.ReadRequired("creator", YamlReading.ToText),
                    map.ReadRequired("quote", YamlReading.ToText),
                    (_, creator, quote) => (creator, quote))
                .Bind(claim => GameNotationParsing.ParseCreatorClaim(claim.creator, claim.quote)
                    .MapError(message => value.ToErrors($"{value.Label}: {message}"))))
            : value.ParseWith(GameNotationParsing.ParseProvenance);

    /// <summary><c>ability: { kind, charges = 1, chunkScalar = "?", baseCooldown = "?" }</c>.</summary>
    private static Result<AbilityProfile, Errors> ReadAbilityProfile(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(AbilityKeys),
            map.ReadRequired("kind", ReadVocabularyWord<AbilityKind>),
            map.ReadOrDefault("charges", ReadChargeCount, ChargeCount.One),
            map.ReadOrDefault("chunkScalar", ReadGameValue, new GameValue.Unknown()),
            map.ReadOrDefault("baseCooldown", ReadGameValue, new GameValue.Unknown()),
            (_, kind, charges, chunkScalar, baseCooldown) => new AbilityProfile(kind, charges, chunkScalar, baseCooldown)));

    private static Result<Unit, Errors> CheckFragmentSlots(YamlMap map, BuildElement element) =>
        element.FragmentSlots.IsSome() && element.Kind != ElementKind.Aspect
            ? map.FailAtKey<Unit>(
                "fragmentSlots",
                $"fragmentSlots is only allowed on aspects, not on a {GameNotationParsing.ToVocabularyWord(element.Kind)}")
            : Succeed<Unit>(new Unit.Value());

    /// <summary><c>ability</c> belongs to super/grenade/melee/classAbility elements, and its kind is the element's.</summary>
    private static Result<Unit, Errors> CheckAbilityProfile(YamlMap map, BuildElement element) =>
        element.Ability.Match(
            ability => ToAbilityKind(element.Kind).Match(
                elementAbility => elementAbility.Value == ability.Value.Kind
                    ? Succeed<Unit>(new Unit.Value())
                    : map.FailAtKey<Unit>(
                        "ability",
                        $"ability.kind '{GameNotationParsing.ToVocabularyWord(ability.Value.Kind)}' does not match "
                        + $"the element kind '{GameNotationParsing.ToVocabularyWord(element.Kind)}'"),
                _ => map.FailAtKey<Unit>(
                    "ability",
                    "ability is only allowed on super, grenade, melee and classAbility elements, "
                    + $"not on a {GameNotationParsing.ToVocabularyWord(element.Kind)}")),
            _ => Succeed<Unit>(new Unit.Value()));

    private static Optional<AbilityKind> ToAbilityKind(ElementKind kind) =>
        kind switch
        {
            ElementKind.Super => Optional.Some(AbilityKind.Super),
            ElementKind.Grenade => Optional.Some(AbilityKind.Grenade),
            ElementKind.Melee => Optional.Some(AbilityKind.Melee),
            ElementKind.ClassAbility => Optional.Some(AbilityKind.ClassAbility),
            _ => Optional.None<AbilityKind>(),
        };
}

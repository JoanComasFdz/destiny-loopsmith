using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using YamlDotNet.RepresentationModel;
using static Loopsmith.Core.RuleParsing.ResultAccumulation;
using static Loopsmith.Core.RuleParsing.ValueReading;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary>The elements of one rules file that parsed, and the errors of those that did not.</summary>
internal sealed record ElementsFile(ImmutableArray<Located<BuildElement>> Elements, Errors Errors);

/// <summary>A <c>rules/**/*.yaml</c> file other than the glossary: <c>elements: [ … ]</c>.</summary>
internal static class ElementParsing
{
    private static readonly ImmutableArray<string> FileKeys = ["elements"];

    private static readonly ImmutableArray<string> ElementKeys =
    [
        "id", "name", "kind", "affinity", "class", "hash", "fragmentSlots", "ability", "description", "source",
        "rules", "passives",
    ];

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
                error => new ElementsFile([], error.Failure));

    private static ElementsFile ToElementsFile(IEnumerable<Result<Located<BuildElement>, Errors>> elements)
    {
        var (parsed, errors) = elements.Partition();
        return new ElementsFile(parsed, errors);
    }

    private static Result<Located<BuildElement>, Errors> ReadElement(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
                map.CheckKeys(ElementKeys),
                map.ReadRequired("id", ReadElementId),
                map.ReadRequired("name", ReadName),
                map.ReadRequired("kind", ReadVocabularyWord<ElementKind>),
                map.ReadRequired("affinity", ReadVocabularyWord<Affinity>),
                map.ReadOptional("class", ReadVocabularyWord<GuardianClass>),
                map.ReadOptional("hash", ReadItemHash),
                map.ReadOptional("description", YamlReading.ToText),
                map.ReadOrDefault("rules", rules => rules.ReadEach("rule", rule => RuleBodyParsing.ReadRule(scope, rule)), []),
                map.ReadOrDefault(
                    "passives",
                    passives => passives.ReadEach("passive", passive => RuleBodyParsing.ReadPassiveRule(scope, passive)),
                    []),
                map.ReadOptional("ability", ReadAbilityProfile),
                map.ReadOptional("fragmentSlots", ReadNonNegativeInteger),
                map.ReadOrDefault("source", ReadProvenance, ToAuthored(map)),
                (_, id, name, kind, affinity, guardianClass, hash, description, rules, passives, ability, fragmentSlots, source) =>
                    new BuildElement(
                        id, name, kind, affinity, guardianClass, hash, description, rules, passives, ability, fragmentSlots, source))
            .Bind(element => Combine(
                CheckFragmentSlots(map, element),
                CheckAbilityProfile(map, element),
                (_, _) => map.ToLocated(element))));

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
            map.ReadOrDefault("charges", ReadPositiveInteger, 1),
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

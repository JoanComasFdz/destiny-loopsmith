using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.BuildExplanation;

/// <summary>
/// How an element of the build deals with a keyword: it gives it (a buff, a debuff, a pickup, a summon), reacts to it
/// (its trigger names it), needs it (its guard reads it), uses it up, collects it (a pickup, from the ground: Powerful
/// Attraction), strikes with it, changes it (a passive on it), or — a passive whose condition names it — is switched on by it.
/// </summary>
public enum InteractionRole { Gives, ReactsTo, UsesUp, Collects, Needs, Strikes, Changes, SwitchesOn }

/// <summary>
/// One way an element of the build deals with a keyword: the element (its name, kind and affinity), what it does
/// ("Grenade thrown → spends Armor Charge → +?% grenade energy per stack"), and where it reaches when that is weapons.
/// </summary>
public sealed record KeywordInteraction(ElementId Source, string SourceName, ElementKind SourceKind, Affinity Affinity, string Text, Optional<string> Note);

/// <summary>The interactions of one role, under its heading ("Gives Armor Charge", "Uses up Armor Charge", "While Amplified").</summary>
public sealed record InteractionGroup(InteractionRole Role, string Heading, ImmutableArray<KeywordInteraction> Interactions);

/// <summary>
/// Pure: what in a build interacts with a keyword (Armor Charge: Elemental Charge gives it, Grenade Kickstart uses it
/// up), role by role, each rule once — under the first role it has: reacts to it, uses it up, collects it, gives it,
/// needs it, strikes with it. The keyword's own element (its own rules, in its details already) is left out, and so is a guard on
/// the keyword itself in a rule's text ("while Armor Charge" under Armor Charge).
/// </summary>
public static class KeywordInteracting
{
    public static ImmutableArray<InteractionGroup> ExplainInteractions(ValidatedBuild build, KeywordRef keyword)
    {
        var glossary = build.Catalog.Glossary;
        var own = build.Catalog.FindKeywordElement(keyword);
        var others = build.Equipped.Where(equipped => own != Optional.Some(equipped.Element.Id)).ToImmutableArray();
        var fromRules = others.SelectMany(equipped => equipped.Element.Rules
            .SelectMany(rule => FindRuleRole(keyword, rule).Select(role => (Role: role, Interaction: DescribeRule(build, keyword, equipped, rule)))));
        var fromPassives = others.SelectMany(equipped => equipped.Element.Passives
            .SelectMany(passive => FindPassiveRole(keyword, passive).Select(role => (Role: role, Interaction: DescribePassive(build, keyword, equipped, passive, role)))));
        var name = DescribeKeywordName(glossary, keyword);
        return
        [
            .. fromRules.Concat(fromPassives)
                .GroupBy(found => found.Role)
                .OrderBy(group => group.Key)
                .Select(group => new InteractionGroup(group.Key, DescribeRoleHeading(glossary, keyword, name, group.Key), [.. group.Select(found => found.Interaction)])),
        ];
    }

    /// <summary>
    /// What an element is, as a label: a keyword by what it is in the game ("Arc buff", "Arc elemental pickup", "Pickup"),
    /// anything else by its kind ("Fragment", "Armor mod").
    /// </summary>
    public static string DescribeElementType(RuleCatalog catalog, ElementId element) =>
        catalog.FindElementKeyword(element).Match(
            keyword => catalog.Glossary.DescribeKeywordKind(keyword.Value),
            _ => catalog.Elements.TryGetValue(element, out var found) ? found.Kind.DescribeElementKind() : "Element");

    /// <summary>The first role a rule has toward the keyword: what fires it, what it needs, what it does.</summary>
    private static IEnumerable<InteractionRole> FindRuleRole(KeywordRef keyword, Rule rule) =>
        new (InteractionRole Role, bool Has)[]
        {
            (InteractionRole.ReactsTo, IsNamedBy(keyword, rule.On)),
            (InteractionRole.UsesUp, rule.Then.Any(outcome => IsUsingUp(keyword, outcome))),
            (InteractionRole.Collects, rule.Then.Any(outcome => IsCollecting(keyword, outcome))),
            (InteractionRole.Gives, rule.Then.Any(outcome => IsGiving(keyword, outcome))),
            (InteractionRole.Needs, rule.When.Any(condition => IsNamedBy(keyword, condition))),
            (InteractionRole.Strikes, rule.Then.Any(outcome => IsStriking(keyword, outcome))),
        }.Where(role => role.Has).Take(1).Select(role => role.Role);

    /// <summary>A passive switched on by the keyword (the kernel's answer, as the state's), else one that changes it.</summary>
    private static IEnumerable<InteractionRole> FindPassiveRole(KeywordRef keyword, PassiveRule passive) =>
        new (InteractionRole Role, bool Has)[]
        {
            (InteractionRole.SwitchesOn, keyword is KeywordRef.Status status && passive.IsSwitchedOnBy(status.Id)),
            (InteractionRole.Changes, IsChanging(keyword, passive.Modifier)),
        }.Where(role => role.Has).Take(1).Select(role => role.Role);

    private static bool IsNamedBy(KeywordRef keyword, Trigger trigger) =>
        trigger.Match(
            _ => false,
            kill => IsVia(keyword, kill.Via),
            tier => IsVia(keyword, tier.Via),
            debuffed => IsVia(keyword, debuffed.Via) || IsAnyStatus(keyword, debuffed.TargetHas),
            multiple => IsVia(keyword, multiple.Via),
            damage => IsVia(keyword, damage.Via),
            debuffed => IsVia(keyword, debuffed.Via) || IsAnyStatus(keyword, debuffed.TargetHas),
            multiple => IsVia(keyword, multiple.Via),
            pickUp => keyword is KeywordRef.Pickup pickup && pickup.Id == pickUp.Pickup,
            gained => IsStatus(keyword, gained.Status),
            maxed => IsStatus(keyword, maxed.Status),
            _ => false,
            _ => false,
            fewer => IsVia(keyword, fewer.Via),
            _ => false,
            broken => IsVia(keyword, broken.Via),
            _ => false,
            fewerHit => IsVia(keyword, fewerHit.Via),
            between => IsVia(keyword, between.Via),
            reload => IsVia(keyword, reload.Via),
            _ => false,
            _ => false);

    /// <summary>A guard the keyword meets by being there (the kernel's: a "without it" guard doesn't need it).</summary>
    private static bool IsNamedBy(KeywordRef keyword, Condition condition) =>
        keyword is KeywordRef.Status status && condition.IsNaming(status.Id);

    private static bool IsVia(KeywordRef keyword, DamageSource via) =>
        via.Match(
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            keywordOf => IsStatus(keyword, keywordOf.Status),
            summonOf => keyword is KeywordRef.Summon summon && summon.Id == summonOf.Summon,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false);

    private static bool IsUsingUp(KeywordRef keyword, Outcome outcome) =>
        outcome.Match(
            _ => false,
            convert => IsStatus(keyword, convert.Consumed),
            _ => false,
            remove => IsStatus(keyword, remove.Status),
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            spend => IsStatus(keyword, spend.Status),
            _ => false,
            _ => false);

    private static bool IsGiving(KeywordRef keyword, Outcome outcome) =>
        outcome.Match(
            _ => false,
            _ => false,
            apply => IsStatus(keyword, apply.Status),
            _ => false,
            debuff => IsStatus(keyword, debuff.Status),
            spawn => keyword is KeywordRef.Pickup pickup && pickup.Id == spawn.Pickup,
            spawn => keyword is KeywordRef.Summon summon && summon.Id == spawn.Summon,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false);

    private static bool IsCollecting(KeywordRef keyword, Outcome outcome) =>
        outcome.Match(
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            collect => keyword is KeywordRef.Pickup pickup && pickup.Id == collect.Pickup,
            _ => false,
            _ => false,
            _ => false,
            _ => false);

    private static bool IsStriking(KeywordRef keyword, Outcome outcome) =>
        outcome.Match(
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            strike => IsStatus(keyword, strike.Via),
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false,
            _ => false);

    private static bool IsChanging(KeywordRef keyword, Passive passive) =>
        passive.Match(
            extra => IsStatus(keyword, extra.Status),
            _ => false,
            modify => IsVia(keyword, modify.Against),
            _ => false,
            _ => false,
            raise => IsStatus(keyword, raise.Status),
            _ => false,
            _ => false,
            _ => false);

    private static bool IsAnyStatus(KeywordRef keyword, ImmutableArray<StatusId> statuses) =>
        statuses.Any(status => IsStatus(keyword, status));

    private static bool IsStatus(KeywordRef keyword, StatusId status) =>
        keyword is KeywordRef.Status named && named.Id == status;

    /// <summary>"Grenade thrown → spends Armor Charge → +?% grenade energy per stack (chance)", with its copies counted.</summary>
    private static KeywordInteraction DescribeRule(ValidatedBuild build, KeywordRef keyword, EquippedElement equipped, Rule rule)
    {
        var glossary = build.Catalog.Glossary;
        var superType = build.ReadSuperType();
        var outcomes = glossary.DescribeOutcomes(rule.Then.Select(outcome => new OutcomeMention(outcome, equipped.Count)), superType);
        var others = rule.When.Where(condition => !IsNamedBy(keyword, condition)).ToImmutableArray();
        var condition = others.IsEmpty ? "" : $" ({glossary.DescribeConditions(others)})";
        var chance = rule.Likelihood == Likelihood.Chance ? " (chance)" : "";
        return new KeywordInteraction(
            equipped.Element.Id,
            equipped.Element.Name,
            equipped.Element.Kind,
            equipped.Element.Affinity,
            $"{glossary.DescribeTrigger(rule.On, superType)} → {outcomes}{condition}{chance}",
            Optional.None<string>());
    }

    /// <summary>
    /// A passive: what it does, its conditions but the keyword's own when the keyword switches it on (Flow State's "during
    /// your class ability" under "While Amplified"), and where a weapon change reaches.
    /// </summary>
    private static KeywordInteraction DescribePassive(ValidatedBuild build, KeywordRef keyword, EquippedElement equipped, PassiveRule passive, InteractionRole role)
    {
        var glossary = build.Catalog.Glossary;
        var shown = role == InteractionRole.SwitchesOn ? passive.When.Where(when => !IsNamedBy(keyword, when)).ToImmutableArray() : passive.When;
        var condition = shown.IsEmpty ? "" : $" ({glossary.DescribeConditions(shown)})";
        return new KeywordInteraction(
            equipped.Element.Id,
            equipped.Element.Name,
            equipped.Element.Kind,
            equipped.Element.Affinity,
            glossary.DescribePassive(passive.Modifier, equipped.Count, build.ReadSuperType()) + condition,
            DescribeReach(build.Build, PassiveReaching.FindReach(build.Build, passive.Modifier)));
    }

    private static Optional<string> DescribeReach(Build build, PassiveReach reach) =>
        reach.Match(
            _ => Optional.None<string>(),
            weapons => Optional.Some($"On {string.Join(", ", weapons.Indices.Select(index => build.Weapons[index].Name))}."),
            _ => Optional.Some(build.HasKnownArchetypes()
                ? "No weapon in this build is one of them, so it does nothing here."
                : "No weapon in this build is known to be one of them."));

    private static string DescribeKeywordName(KeywordGlossary glossary, KeywordRef keyword) =>
        keyword.Match(
            status => glossary.DescribeStatus(status.Id),
            pickup => glossary.DescribePickup(pickup.Id),
            summon => glossary.DescribeSummon(summon.Id));

    /// <summary>"Gives Armor Charge", "Applies Jolt", "Creates Orb of Power", "Reacts to Bolt Charge", "While Amplified".</summary>
    private static string DescribeRoleHeading(KeywordGlossary glossary, KeywordRef keyword, string name, InteractionRole role) =>
        role switch
        {
            InteractionRole.Gives => $"{DescribeGivingVerb(glossary, keyword)} {name}",
            InteractionRole.ReactsTo => $"Reacts to {name}",
            InteractionRole.Needs => $"Needs {name}",
            InteractionRole.UsesUp => $"Uses up {name}",
            InteractionRole.Collects => $"Collects {name}",
            InteractionRole.Strikes => $"Strikes with {name}",
            InteractionRole.Changes => $"Changes {name}",
            _ => $"While {name}",
        };

    private static string DescribeGivingVerb(KeywordGlossary glossary, KeywordRef keyword) =>
        keyword.Match(
            status => glossary.Statuses.TryGetValue(status.Id, out var definition) && definition.Kind == KeywordKind.Debuff ? "Applies" : "Gives",
            _ => "Creates",
            _ => "Summons");
}

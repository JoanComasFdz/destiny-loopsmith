using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.BuildExplanation;

/// <summary>
/// The groups of the build's outcomes, in the order the Outcomes tab lists them: what lives on you, on your weapons and on
/// the target, the strikes, what drops and what is summoned, then energy, health, damage, resistance, weapons and stats.
/// </summary>
public enum OutcomeGroup { Buff, WeaponBuff, Debuff, Strike, Pickup, Summon, Energy, Health, DamageBonus, DamageResistance, Weapons, Stats }

/// <summary>How a way deals with its outcome: gives it, picks it up from the ground, ends or uses it up, or changes it.</summary>
public enum OutcomeWayRole { GivenBy, PickedUpBy, EndedOrUsedUpBy, ChangedBy }

/// <summary>One way to get (or end, or change) an outcome: "Class ability in the air → Jolt target (10s)" and its source.</summary>
public sealed record OutcomeWay(string Text, string SourceName, ElementId Source, Affinity Affinity);

/// <summary>The ways of one role under an outcome ("Is given by").</summary>
public sealed record OutcomeWayBlock(OutcomeWayRole Role, ImmutableArray<OutcomeWay> Ways);

/// <summary>Something the build can get you ("Jolt", "Grenade energy"), what it is ("Arc debuff"), and every way to it.</summary>
public sealed record OutcomeHeading(OutcomeGroup Group, string Name, Optional<string> Kind, ImmutableArray<OutcomeWayBlock> Blocks);

/// <summary>
/// Pure: the build by outcome — for everything its rules and passives can get you, every direct way the build gets it
/// (the trigger, or the passive's condition, the outcome with its number as a fact, and the source), then what ends or uses
/// it up and what changes it. No chain is followed: the trigger names what a way needs (ADRs D1). The same elements as
/// <see cref="BuildExplaining.ExplainBuild"/>: a keyword the build doesn't use is left out.
/// </summary>
public static class OutcomeExplaining
{
    private const string Arrow = " → ";

    public static ImmutableArray<OutcomeHeading> ExplainOutcomes(ValidatedBuild build)
    {
        var glossary = build.Catalog.Glossary;
        var superType = build.ReadSuperType();
        var equipped = BuildExplaining.ListUsedElements(build);
        var fromRules = equipped.SelectMany(element => element.Element.Rules.SelectMany(rule => rule.Then.SelectMany(outcome =>
            ListOutcomeTargets(glossary, superType, outcome).Select(target => (target.Key, target.Role, Way: DescribeRuleWay(build, element, rule, outcome))))));
        var fromPassives = equipped.SelectMany(element => element.Element.Passives.SelectMany(passive =>
            ListPassiveTargets(glossary, passive.Modifier).Select(target => (target.Key, target.Role, Way: DescribePassiveWay(build, element, passive)))));
        return
        [
            .. fromRules.Concat(fromPassives)
                .GroupBy(found => found.Key)
                .OrderBy(group => group.Key.Group)
                .ThenBy(group => group.Key.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => new OutcomeHeading(
                    group.Key.Group,
                    group.Key.Name,
                    group.Key.Kind,
                    [
                        .. group.GroupBy(found => found.Role)
                            .OrderBy(role => role.Key)
                            .Select(role => new OutcomeWayBlock(role.Key, [.. role.Select(found => found.Way).Distinct()])),
                    ])),
        ];
    }

    /// <summary>
    /// The build by outcome as lines: each outcome with what it is, then each block ("Is given by") and its ways as a tree, the
    /// sources aligned; every keyword in its affinity's tone.
    /// </summary>
    public static ImmutableArray<StyledLine> RenderOutcomes(ValidatedBuild build, ImmutableArray<OutcomeHeading> headings) =>
        [.. headings.SelectMany(heading => RenderHeading(build, heading))];

    /// <summary>"Is given by", "Is picked up by", "Is ended or used up by", "Is changed by".</summary>
    public static string DescribeRole(this OutcomeWayRole role) =>
        role switch
        {
            OutcomeWayRole.GivenBy => "Is given by",
            OutcomeWayRole.PickedUpBy => "Is picked up by",
            OutcomeWayRole.EndedOrUsedUpBy => "Is ended or used up by",
            _ => "Is changed by",
        };

    private static IEnumerable<StyledLine> RenderHeading(ValidatedBuild build, OutcomeHeading heading)
    {
        var title = new StyledLine(0,
        [
            .. BuildExplaining.SplitOpeningKeywords(build, heading.Name, Tone.Strong, Optional.None<ElementId>()),
            .. heading.Kind.Match(kind => [$" · {kind.Value}".ToSpan(Tone.Muted)], _ => ImmutableArray<StyledSpan>.Empty),
        ]);
        return [title, .. heading.Blocks.SelectMany(block => RenderBlock(build, block))];
    }

    private static IEnumerable<StyledLine> RenderBlock(ValidatedBuild build, OutcomeWayBlock block)
    {
        var width = block.Ways.Max(way => way.Text.Length);
        var label = StyledText.ToLine(1, block.Role.DescribeRole().ToSpan(Tone.Muted));
        var rows = block.Ways.Select((way, index) => new StyledLine(2,
        [
            (index == block.Ways.Length - 1 ? "└ " : "├ ").ToSpan(Tone.Muted),
            .. BuildExplaining.SplitOpeningKeywords(build, way.Text, Tone.Plain, Optional.Some(way.Source)),
            new string(' ', width - way.Text.Length).ToSpan(Tone.Plain),
            .. BuildExplaining.RenderSource(way.SourceName, way.Source, way.Affinity),
        ]));
        return [label, .. rows];
    }

    /// <summary>"Class ability in the air → Jolt target (10s) (while Amplified) (chance)": the trigger as the Triggers tab heads it.</summary>
    private static OutcomeWay DescribeRuleWay(ValidatedBuild build, EquippedElement equipped, Rule rule, Outcome outcome)
    {
        var glossary = build.Catalog.Glossary;
        var phrase = glossary.DescribeOutcomes([new OutcomeMention(outcome, equipped.Count)], build.ReadSuperType());
        var condition = BuildExplaining.DescribeRuleCondition(build, rule).Match(c => $" ({c.Value})", _ => "");
        var chance = rule.Likelihood == Likelihood.Chance ? " (chance)" : "";
        var trigger = BuildExplaining.DescribeHeading(build, rule.On);
        return new OutcomeWay($"{trigger}{Arrow}{phrase}{condition}{chance}", equipped.Element.Name, equipped.Element.Id, equipped.Element.Affinity);
    }

    /// <summary>"While Amplified → 25% damage resistance", "Always → +1 grenade charge": the passive's condition as the Triggers tab heads it.</summary>
    private static OutcomeWay DescribePassiveWay(ValidatedBuild build, EquippedElement equipped, PassiveRule passive)
    {
        var glossary = build.Catalog.Glossary;
        var phrase = glossary.DescribePassive(passive.Modifier, equipped.Count, build.ReadSuperType());
        var when = BuildExplaining.DescribePassiveHeading(glossary, passive);
        return new OutcomeWay($"{when}{Arrow}{phrase}", equipped.Element.Name, equipped.Element.Id, equipped.Element.Affinity);
    }

    /// <summary>The headings an outcome lands under, each with its role: spending stacks for energy is under both.</summary>
    private static ImmutableArray<(OutcomeKey Key, OutcomeWayRole Role)> ListOutcomeTargets(KeywordGlossary glossary, Optional<DamageType> superType, Outcome outcome) =>
        outcome.Match<ImmutableArray<(OutcomeKey, OutcomeWayRole)>>(
            grant => [(ToEnergyKey(grant.To), OutcomeWayRole.GivenBy)],
            convert => [(ToEnergyKey(convert.To), OutcomeWayRole.GivenBy), (ToStatusKey(glossary, convert.Consumed), OutcomeWayRole.EndedOrUsedUpBy)],
            apply => [(ToStatusKey(glossary, apply.Status), OutcomeWayRole.GivenBy)],
            remove => [(ToStatusKey(glossary, remove.Status), OutcomeWayRole.EndedOrUsedUpBy)],
            debuff => [(ToStatusKey(glossary, debuff.Status), OutcomeWayRole.GivenBy)],
            spawn => [(ToPickupKey(glossary, spawn.Pickup), OutcomeWayRole.GivenBy)],
            summon => [(ToSummonKey(glossary, summon.Summon), OutcomeWayRole.GivenBy)],
            strike => [(new OutcomeKey(OutcomeGroup.Strike, $"{glossary.DescribeStatus(strike.Via)} strike", Optional.Some("Strike")), OutcomeWayRole.GivenBy)],
            _ => [(ToPlainKey(OutcomeGroup.DamageBonus, "Damage bonus"), OutcomeWayRole.GivenBy)],
            _ => [(ToPlainKey(OutcomeGroup.Health, "Health"), OutcomeWayRole.GivenBy)],
            reset => [(ToEnergyKey(reset.Which), OutcomeWayRole.GivenBy)],
            collect => [(ToPickupKey(glossary, collect.Pickup), OutcomeWayRole.PickedUpBy)],
            _ => [(ToPlainKey(OutcomeGroup.Energy, "Least-charged ability energy"), OutcomeWayRole.GivenBy)],
            spend => [(ToStatusKey(glossary, spend.Status), OutcomeWayRole.EndedOrUsedUpBy)],
            weapon => [(new OutcomeKey(OutcomeGroup.Strike, $"{DomainPhrasing.Capitalize(glossary.DescribeDamageSource(weapon.Weapon, superType))} strike", Optional.Some("Strike")), OutcomeWayRole.GivenBy)],
            _ => [(ToPlainKey(OutcomeGroup.Weapons, "Reload"), OutcomeWayRole.GivenBy)],
            _ => [(ToPlainKey(OutcomeGroup.Weapons, "Magazine refill"), OutcomeWayRole.GivenBy)],
            _ => [(ToPlainKey(OutcomeGroup.Weapons, "Ammo progress"), OutcomeWayRole.GivenBy)]);

    /// <summary>The headings a passive lands under: a status it changes (extra stacks, a higher cap), else what it gives.</summary>
    private static ImmutableArray<(OutcomeKey Key, OutcomeWayRole Role)> ListPassiveTargets(KeywordGlossary glossary, Passive passive) =>
        passive.Match<ImmutableArray<(OutcomeKey, OutcomeWayRole)>>(
            extra => [(ToStatusKey(glossary, extra.Status), OutcomeWayRole.ChangedBy)],
            charges => [(ToEnergyKey(charges.Ability), OutcomeWayRole.GivenBy)],
            _ => [(ToPlainKey(OutcomeGroup.DamageBonus, "Damage bonus"), OutcomeWayRole.GivenBy)],
            _ => [(ToPlainKey(OutcomeGroup.DamageResistance, "Damage resistance"), OutcomeWayRole.GivenBy)],
            _ => [(ToPlainKey(OutcomeGroup.Weapons, "Weapon stats"), OutcomeWayRole.GivenBy)],
            raise => [(ToStatusKey(glossary, raise.Status), OutcomeWayRole.ChangedBy)],
            _ => [(ToPlainKey(OutcomeGroup.Stats, "Stats"), OutcomeWayRole.GivenBy)],
            _ => [(ToPlainKey(OutcomeGroup.DamageResistance, "Damage resistance"), OutcomeWayRole.GivenBy)],
            regeneration => [.. regeneration.Abilities.Select(ability => (ToEnergyKey(ability), OutcomeWayRole.GivenBy))]);

    private static OutcomeKey ToStatusKey(KeywordGlossary glossary, StatusId status) =>
        new(ReadStatusGroup(glossary, status), glossary.DescribeStatus(status), Optional.Some(glossary.DescribeKeywordKind(new KeywordRef.Status(status))));

    private static OutcomeGroup ReadStatusGroup(KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition)
            ? definition.Kind switch
            {
                KeywordKind.Debuff => OutcomeGroup.Debuff,
                KeywordKind.WeaponBuff => OutcomeGroup.WeaponBuff,
                _ => OutcomeGroup.Buff,
            }
            : OutcomeGroup.Buff;

    private static OutcomeKey ToPickupKey(KeywordGlossary glossary, PickupId pickup) =>
        new(OutcomeGroup.Pickup, glossary.DescribePickup(pickup), Optional.Some(glossary.DescribeKeywordKind(new KeywordRef.Pickup(pickup))));

    private static OutcomeKey ToSummonKey(KeywordGlossary glossary, SummonId summon) =>
        new(OutcomeGroup.Summon, glossary.DescribeSummon(summon), Optional.Some(glossary.DescribeKeywordKind(new KeywordRef.Summon(summon))));

    /// <summary>"Grenade energy", "Class ability energy": one heading per ability, what gives its energy, charges or regeneration.</summary>
    private static OutcomeKey ToEnergyKey(AbilityKind ability) =>
        ToPlainKey(OutcomeGroup.Energy, DomainPhrasing.Capitalize($"{ability.DescribeAbility()} energy"));

    private static OutcomeKey ToPlainKey(OutcomeGroup group, string name) =>
        new(group, name, Optional.None<string>());

    /// <summary>What a way lands under: its group, its name, and what it is when it is a keyword or a strike.</summary>
    private sealed record OutcomeKey(OutcomeGroup Group, string Name, Optional<string> Kind);
}

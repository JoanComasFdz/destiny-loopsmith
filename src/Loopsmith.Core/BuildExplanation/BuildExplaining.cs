using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.BuildExplanation;

/// <summary>One "outcome [source]" bullet of a trigger line.</summary>
public sealed record ExplanationItem(
    string Outcomes,
    string SourceName,
    ElementKind SourceKind,
    Affinity Affinity,
    Likelihood Likelihood,
    Optional<string> Condition,
    Optional<string> Reason);

/// <summary>A trigger ("Kill Jolted target") and everything in the build it sets off.</summary>
public sealed record ExplanationGroup(string Heading, Affinity Affinity, ImmutableArray<ExplanationItem> Items);

public enum ExplanationStyle
{
    Note,   // one line per trigger, like a build note: "Trigger -> outcome [source] + outcome [source]"
    Tree,   // one line per bullet, sources aligned
}

/// <summary>The static view of a build: what each trigger sets off, without simulating.</summary>
public static class BuildExplaining
{
    public static ImmutableArray<ExplanationGroup> ExplainBuild(ValidatedBuild build)
    {
        var glossary = build.Catalog.Glossary;
        var ruleItems = build.Equipped
            .SelectMany(equipped => equipped.Element.Rules.Select(rule => (equipped.Element, equipped.Count, Rule: rule)))
            .Select((x, order) => (
                Heading: DescribeHeading(glossary, x.Rule.On),
                Rank: RankTrigger(x.Rule.On),
                Order: order,
                Affinity: ReadTriggerAffinity(build, x.Rule.On),
                Item: new ExplanationItem(
                    glossary.DescribeOutcomes(x.Rule.Then.Select(o => new OutcomeMention(o, x.Count))),
                    x.Element.Name,
                    x.Element.Kind,
                    x.Element.Affinity,
                    x.Rule.Likelihood,
                    DescribeRuleCondition(build, x.Rule),
                    x.Rule.Reason)));
        var passiveItems = build.Equipped
            .SelectMany(equipped => equipped.Element.Passives.Select(passive => (equipped.Element, Passive: passive)))
            .Select((x, order) => (
                Heading: x.Passive.When.IsEmpty ? "Always" : DomainPhrasing.Capitalize(glossary.DescribeConditions(x.Passive.When)),
                Rank: x.Passive.When.IsEmpty ? 70 : 60,
                Order: 10_000 + order,
                Affinity: x.Passive.When.Select(c => ReadConditionAffinity(glossary, c)).FirstOrDefault(Affinity.Neutral),
                Item: new ExplanationItem(
                    glossary.DescribePassive(x.Passive.Modifier),
                    x.Element.Name,
                    x.Element.Kind,
                    x.Element.Affinity,
                    Likelihood.Always,
                    Optional.None<string>(),
                    x.Passive.Reason)));
        return ruleItems.Concat(passiveItems)
            .GroupBy(x => x.Heading)
            .Select(group => (
                Rank: group.Min(x => x.Rank),
                Order: group.Min(x => x.Order),
                Group: new ExplanationGroup(group.Key, group.First().Affinity, group.Select(x => x.Item).ToImmutableArray())))
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Order)
            .Select(x => x.Group)
            .ToImmutableArray();
    }

    public static ImmutableArray<StyledLine> RenderExplanation(ImmutableArray<ExplanationGroup> groups, ExplanationStyle style) =>
        style == ExplanationStyle.Note
            ? groups.Select(RenderNoteLine).ToImmutableArray()
            : groups.SelectMany(RenderTree).ToImmutableArray();

    /// <summary>DIM-like header: what is equipped, plus validation warnings.</summary>
    public static ImmutableArray<StyledLine> RenderBuildSummary(ValidatedBuild build)
    {
        var b = build.Build;
        var tone = b.Subclass.ToAffinity().ToTone();
        string DescribeElement(ElementId id) => build.Catalog.Elements.TryGetValue(id, out var e) ? e.Name : id.Value;
        string DescribeElements(IEnumerable<ElementId> ids) => string.Join(", ", ids
            .GroupBy(id => id)
            .Select(g => g.Count() > 1 ? $"{DescribeElement(g.Key)} ×{g.Count()}" : DescribeElement(g.Key)));
        StyledLine RenderRow(string label, string value) => StyledText.ToLine(1, $"{label,-10}".ToSpan(Tone.Muted), value.ToSpan(Tone.Plain));

        var source = b.Author.Match(a => a.Value, _ => "") + b.SourceUrl.Match(u => $"  {u.Value}", _ => "");
        var weapons = b.Weapons.Select(w =>
            $"{w.Name} ({w.Type}{w.Archetype.Match(a => $" {a.Value}", _ => "")})" + (w.Perks.IsEmpty ? "" : $": {DescribeElements(w.Perks)}"));
        var stats = new (string Name, Optional<StatValue> Value)[]
            {
                ("Weapons", b.Stats.Weapons), ("Health", b.Stats.Health), ("Class", b.Stats.Class),
                ("Grenade", b.Stats.Grenade), ("Super", b.Stats.Super), ("Melee", b.Stats.Melee),
            }
            .SelectMany(s => s.Value.Match(v => new[] { $"{s.Name} {v.Value.Value}" }, _ => []));
        var issues = build.Issues.Select(issue => StyledText.ToLine(1,
            $"{(issue.Severity == Severity.Warning ? "! " : "i ")}{issue.Message}".ToSpan(issue.Severity == Severity.Warning ? Tone.Warning : Tone.Muted)));
        return
        [
            StyledText.ToLine(0, b.Name.ToSpan(Tone.Strong), $"  {b.Class} · {b.Subclass}".ToSpan(tone)),
            .. source.Length > 0 ? [StyledText.ToLine(1, source.Trim().ToSpan(Tone.Muted))] : ImmutableArray<StyledLine>.Empty,
            RenderRow("Abilities", $"{DescribeElement(b.Abilities.Super)} · {DescribeElement(b.Abilities.Grenade)} · {DescribeElement(b.Abilities.Melee)} · {DescribeElement(b.Abilities.ClassAbility)}"),
            RenderRow("Aspects", DescribeElements(b.Aspects)),
            RenderRow("Fragments", DescribeElements(b.Fragments)),
            RenderRow("Exotic", b.ExoticArmor.Match(e => DescribeElement(e.Value), _ => "—")),
            .. b.ArmorSetBonuses.IsEmpty ? ImmutableArray<StyledLine>.Empty : [RenderRow("Set", DescribeElements(b.ArmorSetBonuses))],
            RenderRow("Mods", DescribeElements(b.ArmorMods)),
            RenderRow("Artifact", DescribeElements(b.ArtifactPerks)),
            RenderRow("Weapons", string.Join(" · ", weapons)),
            RenderRow("Stats", string.Join(" · ", stats)),
            .. issues,
        ];
    }

    /// <summary>The trigger; a stacking buff's max also names its cap from the glossary ("Max Bolt Charge (x10)"), a fact.</summary>
    private static string DescribeHeading(KeywordGlossary glossary, Trigger trigger) =>
        trigger is Trigger.StacksMaxed maxed && glossary.Statuses.TryGetValue(maxed.Status, out var definition)
            ? glossary.DescribeTrigger(trigger) + definition.MaxStacks.Match(cap => $" (x{cap.Value.Value})", _ => "")
            : glossary.DescribeTrigger(trigger);

    /// <summary>The rule's guards, plus "doesn't stack with X" for each equipped element it gives way to.</summary>
    private static Optional<string> DescribeRuleCondition(ValidatedBuild build, Rule rule)
    {
        var guards = rule.When.IsEmpty ? [] : new[] { build.Catalog.Glossary.DescribeConditions(rule.When) };
        var partners = build.Equipped
            .Where(equipped => rule.DoesNotStackWith.Contains(equipped.Element.Id))
            .Select(equipped => $"doesn't stack with {equipped.Element.Name}");
        var parts = guards.Concat(partners).ToImmutableArray();
        return parts.IsEmpty ? Optional.None<string>() : Optional.Some(string.Join("; ", parts));
    }

    private static StyledLine RenderNoteLine(ExplanationGroup group)
    {
        var bullets = group.Items.SelectMany((item, index) => (ImmutableArray<StyledSpan>)
            [
                .. index > 0 ? [" + ".ToSpan(Tone.Muted)] : ImmutableArray<StyledSpan>.Empty,
                item.Outcomes.ToSpan(Tone.Plain),
                .. item.Condition.Match(c => [$" ({c.Value})".ToSpan(Tone.Muted)], _ => ImmutableArray<StyledSpan>.Empty),
                " [".ToSpan(Tone.Muted),
                item.SourceName.ToSpan(item.Affinity.ToTone()),
                "]".ToSpan(Tone.Muted),
                .. item.Likelihood == Likelihood.Chance ? [" (chance)".ToSpan(Tone.Muted)] : ImmutableArray<StyledSpan>.Empty,
            ]);
        return new StyledLine(0, [group.Heading.ToSpan(Tone.Strong), " -> ".ToSpan(Tone.Muted), .. bullets]);
    }

    private static IEnumerable<StyledLine> RenderTree(ExplanationGroup group)
    {
        var texts = group.Items
            .Select(item => item.Outcomes + item.Condition.Match(c => $" ({c.Value})", _ => "") + (item.Likelihood == Likelihood.Chance ? " (chance)" : ""))
            .ToImmutableArray();
        var width = texts.Max(text => text.Length);
        var header = StyledText.ToLine(0, group.Heading.ToSpan(group.Affinity == Affinity.Neutral ? Tone.Strong : group.Affinity.ToTone()));
        var rows = group.Items.Select((item, index) => StyledText.ToLine(1,
            (index == group.Items.Length - 1 ? "└ " : "├ ").ToSpan(Tone.Muted),
            texts[index].PadRight(width).ToSpan(Tone.Plain),
            "  [".ToSpan(Tone.Muted),
            item.SourceName.ToSpan(item.Affinity.ToTone()),
            "]".ToSpan(Tone.Muted)));
        return [header, .. rows];
    }

    private static int RankTrigger(Trigger trigger) =>
        trigger.Match(
            abilityCast: cast => cast.Kind switch
            {
                AbilityKind.ClassAbility => 0,
                AbilityKind.Grenade => 1,
                AbilityKind.Melee => 2,
                _ => 3,
            },
            killAny: _ => 20,
            killOfTier: _ => 21,
            killDebuffed: _ => 22,
            killMultiple: _ => 23,
            damage: _ => 10,
            damageDebuffed: _ => 11,
            damageMultiple: _ => 12,
            pickUp: _ => 30,
            buffGained: _ => 40,
            stacksMaxed: _ => 50);

    private static Affinity ReadTriggerAffinity(ValidatedBuild build, Trigger trigger)
    {
        var glossary = build.Catalog.Glossary;
        var statuses = trigger.Match(
            abilityCast: _ => ImmutableArray<StatusId>.Empty,
            killAny: _ => [],
            killOfTier: _ => [],
            killDebuffed: killDebuffed => killDebuffed.TargetHas,
            killMultiple: _ => [],
            damage: _ => [],
            damageDebuffed: damageDebuffed => damageDebuffed.TargetHas,
            damageMultiple: _ => [],
            pickUp: _ => [],
            buffGained: buffGained => [buffGained.Status],
            stacksMaxed: stacksMaxed => [stacksMaxed.Status]);
        var pickup = trigger is Trigger.PickUp pickUp && glossary.Pickups.TryGetValue(pickUp.Pickup, out var definition)
            ? definition.Affinity
            : Affinity.Neutral;
        return statuses.Select(glossary.ReadStatusAffinity).FirstOrDefault(pickup);
    }

    private static Affinity ReadConditionAffinity(KeywordGlossary glossary, Condition condition) =>
        condition.Match(
            hasBuff => glossary.ReadStatusAffinity(hasBuff.Status),
            lacksBuff => glossary.ReadStatusAffinity(lacksBuff.Status),
            targetHas => glossary.ReadStatusAffinity(targetHas.Status),
            atMax => glossary.ReadStatusAffinity(atMax.Status));
}

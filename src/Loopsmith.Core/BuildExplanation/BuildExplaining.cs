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
    public static ImmutableArray<ExplanationGroup> ExplainBuild(ValidatedBuild build) =>
        ExplainEquipped(build, build.Equipped);

    /// <summary>
    /// What one element sets off in this build (its copies counted), grouped by trigger: an item's details. An element
    /// the build doesn't equip is explained as one copy — what it would set off if it were.
    /// </summary>
    public static ImmutableArray<ExplanationGroup> ExplainElement(ValidatedBuild build, BuildElement element) =>
        ExplainEquipped(build, [build.Equipped.Where(equipped => equipped.Element.Id == element.Id).Select(Optional.Some).FindFirstSome()
            .UnwrapOr(new EquippedElement(element, 1))]);

    /// <summary>One element's explanation as lines: each trigger, then what it sets off, its condition and its reason.</summary>
    public static ImmutableArray<StyledLine> RenderElementExplanation(KeywordGlossary glossary, ImmutableArray<ExplanationGroup> groups) =>
    [
        .. groups.SelectMany(group => (ImmutableArray<StyledLine>)
        [
            StyledText.ToLine(0, group.Heading.ToSpan(group.Affinity.ToHighlightTone())),
            .. group.Items.SelectMany(item => (ImmutableArray<StyledLine>)
            [
                new StyledLine(1,
                [
                    .. SplitKeywords(glossary, item.Outcomes, Tone.Plain),
                    .. item.Condition.Match(c => SplitKeywords(glossary, $" ({c.Value})", Tone.Muted), _ => ImmutableArray<StyledSpan>.Empty),
                    .. item.Likelihood == Likelihood.Chance ? [" (chance)".ToSpan(Tone.Muted)] : ImmutableArray<StyledSpan>.Empty,
                ]),
                .. item.Reason.Match(reason => [new StyledLine(2, SplitKeywords(glossary, reason.Value, Tone.Muted))], _ => ImmutableArray<StyledLine>.Empty),
            ]),
        ]),
    ];

    /// <summary>A text as spans: each keyword the glossary knows in its affinity's tone, the rest in the given one.</summary>
    private static ImmutableArray<StyledSpan> SplitKeywords(KeywordGlossary glossary, string text, Tone rest) =>
        [.. glossary.SplitKeywords(text).Select(part => part.Text.ToSpan(part.Keyword.Match(keyword => keyword.Value.Affinity.ToHighlightTone(), _ => rest)))];

    private static ImmutableArray<ExplanationGroup> ExplainEquipped(ValidatedBuild build, ImmutableArray<EquippedElement> equippedElements)
    {
        var glossary = build.Catalog.Glossary;
        var superType = build.ReadSuperType();
        var ruleItems = equippedElements
            .SelectMany(equipped => equipped.Element.Rules.Select(rule => (equipped.Element, equipped.Count, Rule: rule)))
            .Select((x, order) => (
                Heading: DescribeHeading(build, x.Rule.On),
                Rank: RankTrigger(x.Rule.On),
                Order: order,
                Affinity: ReadTriggerAffinity(build, x.Rule.On),
                Item: new ExplanationItem(
                    glossary.DescribeOutcomes(x.Rule.Then.Select(o => new OutcomeMention(o, x.Count)), superType),
                    x.Element.Name,
                    x.Element.Kind,
                    x.Element.Affinity,
                    x.Rule.Likelihood,
                    DescribeRuleCondition(build, x.Rule),
                    x.Rule.Reason)));
        var passiveItems = equippedElements
            .SelectMany(equipped => equipped.Element.Passives.Select(passive => (equipped.Element, equipped.Count, Passive: passive)))
            .Select((x, order) => (
                Heading: x.Passive.When.IsEmpty ? "Always" : DomainPhrasing.Capitalize(glossary.DescribeConditions(x.Passive.When)),
                Rank: x.Passive.When.IsEmpty ? 70 : 60,
                Order: 10_000 + order,
                Affinity: x.Passive.When.Select(c => ReadConditionAffinity(glossary, c)).FirstOrDefault(Affinity.Neutral),
                Item: new ExplanationItem(
                    glossary.DescribePassive(x.Passive.Modifier, x.Count, superType),
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

    /// <summary>A build's explanation as a build note or an aligned tree; every keyword it names in its affinity's tone.</summary>
    public static ImmutableArray<StyledLine> RenderExplanation(KeywordGlossary glossary, ImmutableArray<ExplanationGroup> groups, ExplanationStyle style) =>
        style == ExplanationStyle.Note
            ? groups.Select(group => RenderNoteLine(glossary, group)).ToImmutableArray()
            : groups.SelectMany(group => RenderTree(glossary, group)).ToImmutableArray();

    /// <summary>DIM-like header: what is equipped, plus validation warnings.</summary>
    public static ImmutableArray<StyledLine> RenderBuildSummary(ValidatedBuild build)
    {
        var b = build.Build;
        var tone = b.Subclass.ToAffinity().ToTone();
        string DescribeElement(ElementId id) => build.Catalog.Elements.TryGetValue(id, out var e) ? e.Name : id.Value;
        string DescribeAbility(AbilityPlug ability) => ability.Match(known => DescribeElement(known.Element), _ => DomainPhrasing.Unknown, _ => "—");
        string DescribeElements(IEnumerable<ElementId> ids) => string.Join(", ", ids
            .GroupBy(id => id)
            .Select(g => g.Count() > 1 ? $"{DescribeElement(g.Key)} ×{g.Count()}" : DescribeElement(g.Key)));
        StyledLine RenderRow(string label, string value) => StyledText.ToLine(1, $"{label,-10}".ToSpan(Tone.Muted), value.ToSpan(Tone.Plain));

        var source = b.Author.Match(a => a.Value, _ => "") + b.SourceUrl.Match(u => $"  {u.Value}", _ => "");
        string DescribeMod(ItemHash mod) => build.Catalog.FindElementByHash(mod).Match(e => e.Value.Name, _ => build.Catalog.Manifest.DescribeItemHash(mod));
        var weapons = b.Weapons.Select(w =>
            $"{w.Name} ({w.Type}{w.Archetype.Match(a => $" {a.Value}", _ => "")})" + (w.Perks.IsEmpty ? "" : $": {DescribeElements(w.Perks)}")
            + w.Mod.Match(mod => $" — mod {DescribeMod(mod.Value)}", _ => ""));
        var stats = StatReading.GameOrder
            .SelectMany(stat => b.Stats.ReadStat(stat).Match(v => new[] { $"{stat} {v.Value.Value}" }, _ => []));
        var issues = build.Issues.Select(issue => StyledText.ToLine(1,
            $"{(issue.Severity == Severity.Warning ? "! " : "i ")}{issue.Message}".ToSpan(issue.Severity == Severity.Warning ? Tone.Warning : Tone.Muted)));
        return
        [
            StyledText.ToLine(0, b.Name.ToSpan(Tone.Strong), $"  {b.Class} · {b.Subclass}".ToSpan(tone)),
            .. source.Length > 0 ? [StyledText.ToLine(1, source.Trim().ToSpan(Tone.Muted))] : ImmutableArray<StyledLine>.Empty,
            RenderRow("Abilities", $"{DescribeAbility(b.Abilities.Super)} · {DescribeAbility(b.Abilities.Grenade)} · {DescribeAbility(b.Abilities.Melee)} · {DescribeAbility(b.Abilities.ClassAbility)}"),
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

    /// <summary>The trigger; a stacking buff's max also names the build's cap ("Max Bolt Charge (x10)", Charged Up raises it), a fact.</summary>
    private static string DescribeHeading(ValidatedBuild build, Trigger trigger) =>
        build.Catalog.Glossary.DescribeTrigger(trigger, build.ReadSuperType()) + (trigger is Trigger.StacksMaxed maxed
            ? build.DescribeStackCap(maxed.Status).Match(cap => $" ({cap.Value})", _ => "")
            : "");

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

    private static StyledLine RenderNoteLine(KeywordGlossary glossary, ExplanationGroup group)
    {
        var bullets = group.Items.SelectMany((item, index) => (ImmutableArray<StyledSpan>)
            [
                .. index > 0 ? [" + ".ToSpan(Tone.Muted)] : ImmutableArray<StyledSpan>.Empty,
                .. SplitKeywords(glossary, item.Outcomes, Tone.Plain),
                .. item.Condition.Match(c => SplitKeywords(glossary, $" ({c.Value})", Tone.Muted), _ => ImmutableArray<StyledSpan>.Empty),
                " [".ToSpan(Tone.Muted),
                item.SourceName.ToSpan(item.Affinity.ToTone()),
                "]".ToSpan(Tone.Muted),
                .. item.Likelihood == Likelihood.Chance ? [" (chance)".ToSpan(Tone.Muted)] : ImmutableArray<StyledSpan>.Empty,
            ]);
        return new StyledLine(0, [group.Heading.ToSpan(Tone.Strong), " -> ".ToSpan(Tone.Muted), .. bullets]);
    }

    private static IEnumerable<StyledLine> RenderTree(KeywordGlossary glossary, ExplanationGroup group)
    {
        var texts = group.Items
            .Select(item => item.Outcomes + item.Condition.Match(c => $" ({c.Value})", _ => "") + (item.Likelihood == Likelihood.Chance ? " (chance)" : ""))
            .ToImmutableArray();
        var width = texts.Max(text => text.Length);
        var header = StyledText.ToLine(0, group.Heading.ToSpan(group.Affinity.ToHighlightTone()));
        var rows = group.Items.Select((item, index) => new StyledLine(1,
        [
            (index == group.Items.Length - 1 ? "└ " : "├ ").ToSpan(Tone.Muted),
            .. SplitKeywords(glossary, texts[index], Tone.Plain),
            new string(' ', width - texts[index].Length).ToSpan(Tone.Plain),
            "  [".ToSpan(Tone.Muted),
            item.SourceName.ToSpan(item.Affinity.ToTone()),
            "]".ToSpan(Tone.Muted),
        ]));
        return [header, .. rows];
    }

    private static int RankTrigger(Trigger trigger) =>
        trigger.Match(
            abilityCast: cast => RankAbility(cast.Kind),
            killAny: _ => 20,
            killOfTier: _ => 21,
            killDebuffed: _ => 22,
            killMultiple: _ => 23,
            damage: _ => 10,
            damageDebuffed: _ => 11,
            damageMultiple: _ => 12,
            pickUp: _ => 30,
            buffGained: _ => 40,
            stacksMaxed: _ => 50,
            finisher: _ => 25,
            abilityCastAs: cast => RankAbility(cast.Kind),
            killFewer: _ => 24,
            criticalHealth: _ => 60,
            shieldBroken: _ => 26,
            energyExpended: expended => RankAbility(expended.Kind));

    private static int RankAbility(AbilityKind kind) =>
        kind switch
        {
            AbilityKind.ClassAbility => 0,
            AbilityKind.Grenade => 1,
            AbilityKind.Melee => 2,
            _ => 3,
        };

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
            stacksMaxed: stacksMaxed => [stacksMaxed.Status],
            finisher: _ => [],
            abilityCastAs: _ => [],
            killFewer: _ => [],
            criticalHealth: _ => [],
            shieldBroken: _ => [],
            energyExpended: _ => []);
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
            atMax => glossary.ReadStatusAffinity(atMax.Status),
            _ => Affinity.Neutral,
            belowMax => glossary.ReadStatusAffinity(belowMax.Status),
            holds => glossary.ReadStatusAffinity(holds.Status));
}

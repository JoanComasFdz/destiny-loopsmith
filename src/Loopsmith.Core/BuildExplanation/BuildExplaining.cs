using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
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

/// <summary>The static view of a build: what each trigger sets off, without simulating.</summary>
public static class BuildExplaining
{
    /// <summary>What the build sets off: its own elements, and of the keywords every build equips only those it uses.</summary>
    public static ImmutableArray<ExplanationGroup> ExplainBuild(ValidatedBuild build) =>
        ExplainEquipped(build, ListUsedElements(build));

    /// <summary>
    /// The build's elements without the keywords it doesn't use. A keyword is used when an element already listed names it
    /// (gives it, reacts to it, needs it, uses it up, strikes with it, changes it), or gives a keyword one of them reads
    /// (Orb of Power, for a build that reads Armor Charge); a keyword listed so lists the keywords it names in turn.
    /// </summary>
    private static ImmutableArray<EquippedElement> ListUsedElements(ValidatedBuild build)
    {
        var catalog = build.Catalog;
        var keywords = ListGlossaryKeywords(catalog.Glossary);
        var used = AddUsedKeywords(catalog, keywords, build.Equipped, [.. build.Equipped.Where(equipped => equipped.Element.Kind != ElementKind.Keyword).Select(equipped => equipped.Element.Id)]);
        return [.. build.Equipped.Where(equipped => used.Contains(equipped.Element.Id))];
    }

    private static ImmutableArray<KeywordRef> ListGlossaryKeywords(KeywordGlossary glossary) =>
    [
        .. glossary.Statuses.Keys.Select(status => (KeywordRef)new KeywordRef.Status(status)),
        .. glossary.Pickups.Keys.Select(pickup => (KeywordRef)new KeywordRef.Pickup(pickup)),
        .. glossary.Summons.Keys.Select(summon => (KeywordRef)new KeywordRef.Summon(summon)),
    ];

    /// <summary>The used elements, with every keyword the ones listed so far use, until a round adds none.</summary>
    private static ImmutableHashSet<ElementId> AddUsedKeywords(
        RuleCatalog catalog, ImmutableArray<KeywordRef> keywords, ImmutableArray<EquippedElement> equipped, ImmutableHashSet<ElementId> used)
    {
        var listed = equipped.Where(e => used.Contains(e.Element.Id)).Select(e => e.Element).ToImmutableArray();
        var added = equipped.Where(e => !used.Contains(e.Element.Id) && IsKeywordUsed(catalog, keywords, listed, e.Element)).Select(e => e.Element.Id).ToImmutableArray();
        return added.IsEmpty ? used : AddUsedKeywords(catalog, keywords, equipped, used.Union(added));
    }

    private static bool IsKeywordUsed(RuleCatalog catalog, ImmutableArray<KeywordRef> keywords, ImmutableArray<BuildElement> listed, BuildElement keywordElement) =>
        catalog.FindElementKeyword(keywordElement.Id).Match(own => listed.Any(element => KeywordInteracting.IsNamedBy(own.Value, element)), _ => false)
        || keywords.Any(keyword => KeywordInteracting.IsGivenBy(keyword, keywordElement) && listed.Any(element => KeywordInteracting.IsReadBy(keyword, element)));

    /// <summary>
    /// What one element sets off in this build (its copies counted), grouped by trigger: an item's details. An element
    /// the build doesn't equip is explained as one copy — what it would set off if it were.
    /// </summary>
    public static ImmutableArray<ExplanationGroup> ExplainElement(ValidatedBuild build, BuildElement element) =>
        ExplainEquipped(build, [build.Equipped.Where(equipped => equipped.Element.Id == element.Id).Select(Optional.Some).FindFirstSome()
            .UnwrapOr(new EquippedElement(element, CopyCount.One))]);

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

    /// <summary>
    /// A build's explanation as a tree: each trigger, then one line per outcome with its source, the sources aligned; every
    /// keyword it names in its affinity's tone.
    /// </summary>
    public static ImmutableArray<StyledLine> RenderExplanation(KeywordGlossary glossary, ImmutableArray<ExplanationGroup> groups) =>
        [.. groups.SelectMany(group => RenderTree(glossary, group))];

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
            energyExpended: expended => RankAbility(expended.Kind),
            damageFewer: _ => 13,
            killBetween: _ => 23,
            reload: _ => 27,
            hitAtCriticalHealth: _ => 61,
            pickUpAmmo: _ => 31,
            readyWeapon: _ => 26);

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
            energyExpended: _ => [],
            damageFewer: _ => [],
            killBetween: _ => [],
            reload: _ => [],
            hitAtCriticalHealth: _ => [],
            pickUpAmmo: _ => [],
            readyWeapon: _ => []);
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

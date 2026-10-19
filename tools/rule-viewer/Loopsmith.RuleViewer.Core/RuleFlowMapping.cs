using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.RuleViewer.Core;

/// <summary>A flow and the trigger group it goes in: its key (the trigger's node, or its text) and name.</summary>
internal sealed record MappedFlow(RuleFlow Flow, string GroupKey, string GroupName);

/// <summary>A flow's trigger before the split: its node (none: "Any", "This weapon", "Always"), its text and its group's name.</summary>
internal sealed record TriggerPart(Optional<GraphNode> Node, string Text, string GroupName);

/// <summary>
/// Reads an element's rules and passives as flows, one claim each: what sets it off ➜ what must be in place ➜ what it
/// gives. A rule whose trigger names several (any ability) becomes one flow per trigger; the rules file keeps one rule.
/// The boxes are the graph's nodes (<see cref="DependencyMapping"/>), so a flow names, colours and draws a node as the
/// graph does.
/// </summary>
internal static class RuleFlowMapping
{
    private const string AlwaysKey = "always";

    /// <summary>What the manifest has no icon for, drawn as an emoji (Orb of Power, Armor Charge), by node.</summary>
    private static readonly ImmutableDictionary<string, string> EmojiIcons = new Dictionary<string, string>
    {
        ["orb-of-power"] = "⚪",
        ["rule:orb-of-power"] = "⚪",
        ["armor-charge"] = "🟨🔳🔳",
        ["rule:armor-charge"] = "🟨🔳🔳",
    }.ToImmutableDictionary();

    public static ImmutableArray<MappedFlow> MapFlows(GraphContext context, BuildElement element)
    {
        var owner = DependencyMapping.ToElementNode(context, element);
        var equipped = element.Kind == ElementKind.Keyword ? [] : ImmutableArray.Create(ToBox(context, owner, "equipped", BoxRole.Condition));
        var facts = owner.Element.Bind(found => found.File);
        var rules = element.Rules.SelectMany((rule, place) => MapRule(context, element, owner, equipped, facts, rule, place));
        var passives = element.Passives.Select((passive, place) => MapPassive(context, element, owner, equipped, facts, passive, place));
        return [.. rules, .. passives];
    }

    /// <summary>Whether a group is the flows that are always on (the passives'), which comes last.</summary>
    public static bool IsAlwaysGroup(string key) => key == AlwaysKey;

    private static IEnumerable<MappedFlow> MapRule(
        GraphContext context, BuildElement element, GraphNode owner, ImmutableArray<RuleBox> equipped, Optional<string> file, Rule rule, int place)
    {
        var triggers = ListTriggerParts(context, rule.On);
        var onTarget = rule.On.ListRequiredTargetStatuses().Select(status => ToBox(context, DependencyMapping.ToStatusNode(context, status), "on the target", BoxRole.Condition));
        var conditions = equipped.Concat(onTarget).Concat(rule.When.Select(condition => ToConditionBox(context, condition))).ToImmutableArray();
        var results = OrderResults(rule.Then.SelectMany(outcome => DependencyMapping.ListOutcomeEffects(context, outcome)).Select(effect => ToResultBox(context, effect)));
        var id = $"{owner.Id}#{place.ToString(CultureInfo.InvariantCulture)}";
        return triggers.Select((trigger, at) => new MappedFlow(
            new RuleFlow(
                triggers.Length == 1 ? id : $"{id}.{at.ToString(CultureInfo.InvariantCulture)}",
                owner.Id,
                element.ToRuleName(rule.Id),
                ToTriggerBox(context, trigger),
                conditions,
                results,
                rule.Likelihood == Likelihood.Chance,
                file,
                rule.Reason),
            trigger.Node.Map(node => node.Id).UnwrapOr($"text:{trigger.GroupName}"),
            trigger.GroupName));
    }

    private static MappedFlow MapPassive(
        GraphContext context, BuildElement element, GraphNode owner, ImmutableArray<RuleBox> equipped, Optional<string> file, PassiveRule passive, int place) =>
        new(
            new RuleFlow(
                $"{owner.Id}#p{place.ToString(CultureInfo.InvariantCulture)}",
                owner.Id,
                element.ToRuleName(passive.Id),
                ToTriggerBox(context, new TriggerPart(Optional.None<GraphNode>(), "Always", "Always on")),
                [.. equipped, .. passive.When.Select(condition => ToConditionBox(context, condition))],
                OrderResults(DependencyMapping.ListPassiveEffects(context, passive.Modifier).Select(effect => ToResultBox(context, effect))),
                false,
                file,
                passive.Reason),
            AlwaysKey,
            "Always on");

    /// <summary>
    /// What sets a rule off, as the player says it: an ability cast ("Grenade cast", "Class ability"), a hit or kill by each source
    /// it names ("Melee hit", "Sliding melee hit", "Arc weapon 3+ kills"), a pickup, a buff gained or maxed, a finisher…
    /// </summary>
    private static ImmutableArray<TriggerPart> ListTriggerParts(GraphContext context, Trigger trigger) =>
        trigger.Match(
            abilityCast: cast => ListCastParts(context, trigger, cast.Kind),
            killAny: kill => ListHitParts(context, kill.Via, DependencyMapping.DescribeKillLabel(kill.Needs, "kill")),
            killOfTier: kill => ListHitParts(context, kill.Via, DependencyMapping.DescribeKillLabel(kill.Needs, $"kill ({DomainPhrasing.DescribeTierNeed(kill.Tier)})")),
            killDebuffed: kill => ListHitParts(context, kill.Via, DependencyMapping.DescribeKillLabel(kill.Needs, "kill")),
            killMultiple: kill => ListHitParts(context, kill.Via, DependencyMapping.DescribeKillLabel(kill.Needs, $"{FormatCount(kill.AtLeast)}+ kills{DescribeWithin(kill.Within)}")),
            damage: damage => ListHitParts(context, damage.Via, DescribeHit(damage.Needs)),
            damageDebuffed: damage => ListHitParts(context, damage.Via, DescribeHit(damage.Needs)),
            damageMultiple: damage => ListHitParts(context, damage.Via, $"{FormatCount(damage.AtLeast)}+ hits{DescribeWithin(damage.Within)}"),
            pickUp: pickUp => ListActionParts(context, trigger, name => $"Pick up {name}"),
            buffGained: _ => ListActionParts(context, trigger, name => $"{name} gained"),
            stacksMaxed: _ => ListActionParts(context, trigger, name => $"{name} at max"),
            finisher: _ => ListActionParts(context, trigger, name => name),
            abilityCastAs: cast => ListCastParts(context, trigger, cast.Kind),
            killFewer: kill => ListHitParts(context, kill.Via, DependencyMapping.DescribeKillLabel(kill.Needs, $"fewer than {FormatCount(kill.FewerThan)} kills")),
            criticalHealth: _ => ListActionParts(context, trigger, name => name),
            shieldBroken: _ => ListActionParts(context, trigger, name => name),
            energyExpended: _ => ListActionParts(context, trigger, name => $"{name} (last charge)"),
            damageFewer: damage => ListHitParts(context, damage.Via, $"fewer than {FormatCount(damage.FewerThan)} hits"),
            killBetween: kill => ListHitParts(context, kill.Via, DependencyMapping.DescribeKillLabel(kill.Needs, $"{FormatCount(kill.AtLeast)}+ kills, fewer than {FormatCount(kill.FewerThan)}")),
            reload: _ => ListActionParts(context, trigger, name => name),
            hitAtCriticalHealth: _ => ListActionParts(context, trigger, name => name),
            pickUpAmmo: _ => ListActionParts(context, trigger, name => name),
            readyWeapon: _ => ListActionParts(context, trigger, name => name));

    /// <summary>A cast: "Grenade cast", "Super cast"; the class ability is the cast itself ("Class ability", "Air class ability").</summary>
    private static ImmutableArray<TriggerPart> ListCastParts(GraphContext context, Trigger trigger, AbilityKind kind) =>
        ListActionParts(context, trigger, name => kind == AbilityKind.ClassAbility ? name : $"{name} cast");

    private static ImmutableArray<TriggerPart> ListActionParts(GraphContext context, Trigger trigger, Func<string, string> describe) =>
        [.. DependencyMapping.ListActionNodes(context, trigger).Select(node => new TriggerPart(Optional.Some(node), describe(node.Name), node.Name))];

    /// <summary>
    /// A hit or kill by a source: one part per node the source stands for (any ability: each ability that deals damage;
    /// any melee: the punch and the sliding melee); a source with no node is "Any" or "This weapon".
    /// </summary>
    private static ImmutableArray<TriggerPart> ListHitParts(GraphContext context, DamageSource via, string word)
    {
        var nodes = DependencyMapping.ListSourceNodes(context, via);
        var unnamed = via is DamageSource.AnySource ? "Any" : "This weapon";
        return nodes.IsEmpty
            ? [new TriggerPart(Optional.None<GraphNode>(), $"{unnamed} {word}", unnamed)]
            : [.. nodes.Select(node => new TriggerPart(Optional.Some(node), $"{node.Name} {word}", node.Name))];
    }

    private static string DescribeHit(DamageSpan span) => span == DamageSpan.Sustained ? "sustained hit" : "hit";

    private static string DescribeWithin(Optional<Seconds> within) =>
        within.Map(seconds => $" within {seconds.FormatSeconds()}").UnwrapOr("");

    private static string FormatCount(TargetCount count) => count.Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A trigger box: its words as the player says them; a class ability's caption is the class's own word ("Dodge").</summary>
    private static RuleBox ToTriggerBox(GraphContext context, TriggerPart trigger) =>
        trigger.Node.Match(
            node => ToBox(context, node.Value, DependencyMapping.FindClassMove(context, node.Value).UnwrapOr(""), BoxRole.Trigger) with { Title = "Trigger", Name = trigger.Text },
            _ => new RuleBox(Optional.None<string>(), "Trigger", trigger.Text, "", BoxTone.Trigger, new RuleIcon.Blank(), BoxRole.Trigger));

    /// <summary>A state a rule or passive needs, with a short caption: "on you", "at max stacks", "3+ stacks", "on the target"…</summary>
    private static RuleBox ToConditionBox(GraphContext context, Condition condition) =>
        condition.Match(
            hasBuff: has => ToStatusBox(context, has.Status, "on you"),
            lacksBuff: lacks => ToStatusBox(context, lacks.Status, "not on you"),
            targetHas: target => ToStatusBox(context, target.Status, "on the target"),
            atMax: atMax => ToStatusBox(context, atMax.Status, "at max stacks"),
            during: during => ToBox(context, DependencyMapping.ToTriggerNode(context, during.Ability, AbilityInput.Press), "under way", BoxRole.Condition),
            belowMax: below => ToStatusBox(context, below.Status, "below max stacks"),
            holds: holds => ToStatusBox(context, holds.Status, $"{holds.AtLeast.Value.ToString(CultureInfo.InvariantCulture)}+ stacks"));

    private static RuleBox ToStatusBox(GraphContext context, StatusId status, string caption) =>
        ToBox(context, DependencyMapping.ToStatusNode(context, status), caption, BoxRole.Condition);

    /// <summary>A result: what it gives with its amount as authored ("×2", "100%", "8.5s"), or what it ends, collects or adds to.</summary>
    private static RuleBox ToResultBox(GraphContext context, Effect effect)
    {
        var role = effect.Kind switch
        {
            EdgeKind.Ends => BoxRole.Ends,
            EdgeKind.Collects => BoxRole.Collects,
            EdgeKind.Boosts => BoxRole.Boosts,
            _ => BoxRole.Gives,
        };
        var word = role switch
        {
            BoxRole.Ends => "ends",
            BoxRole.Collects => "collects",
            BoxRole.Boosts => "adds to",
            _ => "",
        };
        return ToBox(context, effect.Node, string.Join(" · ", new[] { word, effect.Label }.Where(part => part.Length > 0)), role);
    }

    /// <summary>Strikes, other effects (energy, health, resistance…), buffs, debuffs, pickups, summons; then what the flow ends, collects or adds to.</summary>
    private static ImmutableArray<RuleBox> OrderResults(IEnumerable<RuleBox> results) =>
        [.. results.OrderBy(RankResult)];

    private static int RankResult(RuleBox box) =>
        box.Role != BoxRole.Gives
            ? 10
            : box.Tone switch
            {
                BoxTone.Strike => 0,
                BoxTone.Effect => 1,
                BoxTone.Buff => 2,
                BoxTone.Debuff => 3,
                BoxTone.Pickup => 4,
                BoxTone.Summon => 5,
                _ => 9,
            };

    private static RuleBox ToBox(GraphContext context, GraphNode node, string caption, BoxRole role) =>
        new(Optional.Some(node.Id), ToTitle(node.Type), node.Name, caption, ToTone(node.Type), ToRuleIcon(context, node), role);

    /// <summary>A box's kind title: "Subclass ability · Grenade", "Subclass aspect", "Buff", "Strike", "Armor mod"…</summary>
    private static string ToTitle(GraphNodeType type) =>
        type.Match(
            actionTrigger: _ => "Trigger",
            neededWeapon: _ => "Weapon",
            subclassAbility: ability => $"Subclass ability · {ToWords(ability.Slot.ToString())}",
            subclassSuper: _ => "Subclass super",
            subclassAspect: _ => "Subclass aspect",
            subclassFragment: _ => "Subclass fragment",
            buffStatus: _ => "Buff",
            debuffStatus: _ => "Debuff",
            pickupDrop: _ => "Pickup",
            summonedAlly: _ => "Summon",
            unlistedKeyword: _ => "Keyword",
            keywordRule: _ => "Keyword rule",
            shownEffect: _ => "Effect",
            keywordStrike: _ => "Strike",
            outsideElement: outside => ToWords(outside.Kind.ToString()));

    private static BoxTone ToTone(GraphNodeType type) =>
        type.Match(
            actionTrigger: _ => BoxTone.Trigger,
            neededWeapon: _ => BoxTone.Weapon,
            subclassAbility: _ => BoxTone.Ability,
            subclassSuper: _ => BoxTone.Super,
            subclassAspect: _ => BoxTone.Aspect,
            subclassFragment: _ => BoxTone.Fragment,
            buffStatus: _ => BoxTone.Buff,
            debuffStatus: _ => BoxTone.Debuff,
            pickupDrop: _ => BoxTone.Pickup,
            summonedAlly: _ => BoxTone.Summon,
            unlistedKeyword: _ => BoxTone.Keyword,
            keywordRule: _ => BoxTone.Keyword,
            shownEffect: _ => BoxTone.Effect,
            keywordStrike: _ => BoxTone.Strike,
            outsideElement: _ => BoxTone.Outside);

    /// <summary>
    /// A node's icon: an emoji for what the manifest has none of; a buff with no keyword icon of its own (the game's
    /// green diamond) is its picture — its element's, or the stat icon its id names (the Super) — clipped to a diamond;
    /// else its picture, or nothing.
    /// </summary>
    private static RuleIcon ToRuleIcon(GraphContext context, GraphNode node) =>
        EmojiIcons.TryGetValue(node.Id, out var emoji)
            ? new RuleIcon.Emoji(emoji)
            : node.Type is GraphNodeType.BuffStatus && !HasKeywordTrait(context, node)
                ? FindBuffPicture(context, node).Match<RuleIcon>(url => new RuleIcon.MaskedBuff(url.Value), _ => new RuleIcon.Blank())
                : node.Icon.Match<RuleIcon>(url => new RuleIcon.Picture(url.Value), _ => new RuleIcon.Blank());

    private static bool HasKeywordTrait(GraphContext context, GraphNode node) =>
        context.Catalog.Glossary.Statuses.TryGetValue(ToStatusId(node), out var status) && status.Trait.IsSome();

    private static Optional<string> FindBuffPicture(GraphContext context, GraphNode node) =>
        node.Icon.IsSome()
            ? node.Icon
            : Enum.TryParse<StatKind>(ToStatusId(node).Value, ignoreCase: true, out var stat)
                ? DependencyMapping.FindStatIcon(context, stat)
                : Optional.None<string>();

    /// <summary>The glossary status of a status node: its id, without the <c>status:</c> its node may carry.</summary>
    private static StatusId ToStatusId(GraphNode node) =>
        StatusId.From(node.Id.StartsWith("status:", StringComparison.Ordinal) ? node.Id["status:".Length..] : node.Id);

    /// <summary>"ArmorSetBonus" → "Armor set bonus", "ClassAbility" → "Class ability".</summary>
    private static string ToWords(string pascal) =>
        string.Concat(pascal.Select((letter, at) => at > 0 && char.IsUpper(letter) ? " " + char.ToLowerInvariant(letter) : letter.ToString()));
}

using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.DependencyGraphing;

/// <summary>The rules a graph is drawn from, and the class and subclass it is drawn for.</summary>
internal sealed record GraphContext(RuleCatalog Catalog, GuardianClass Class, Subclass Subclass);

/// <summary>
/// What one part of a rule needs: one node, or one of several (<c>via: ability</c>: any ability trigger), with the edge's
/// label.
/// </summary>
internal sealed record Need(ImmutableArray<GraphNode> Alternatives, string Label);

/// <summary>
/// One way an element fires (a rule, or a passive): every node of <see cref="AllOf"/>, and one node of each group of
/// <see cref="AnyOf"/>.
/// </summary>
internal sealed record FiringWay(ImmutableArray<string> AllOf, ImmutableArray<ImmutableArray<string>> AnyOf);

/// <summary>
/// An element as the graph reads it: its node, the ways it fires, its edges (none to itself), what it gives (itself
/// included: a keyword that gives its own status) and every other node it touches.
/// </summary>
internal sealed record ElementLinks(
    GraphNode Node,
    bool IsKeyword,
    ImmutableArray<FiringWay> Ways,
    ImmutableArray<GraphEdge> Edges,
    ImmutableHashSet<string> Gives,
    ImmutableArray<GraphNode> Touched);

/// <summary>
/// Reads an element's rules as the graph's edges: what each trigger and guard needs (Causality reads the trigger's
/// damage source and target statuses the way the engine does) and what each outcome gives.
/// </summary>
internal static class DependencyMapping
{
    private const string RulesFolder = "rules";

    /// <summary>
    /// The ability uses <c>via: ability</c> stands for, each its own trigger node: only those whose step deals damage. A
    /// class ability is used, not aimed (<see cref="OffensiveAbility"/>; StepPlaying's <c>ActionResolution</c> resolves
    /// <c>PlayerAction.UseClassAbility</c> to a cast and no hit), so neither dodge ever matches a hit or kill by any ability.
    /// </summary>
    private static readonly ImmutableArray<(AbilityKind Kind, AbilityInput Input)> DamagingAbilityUses =
    [
        .. new[]
            {
                (AbilityKind.Grenade, AbilityInput.Press),
                (AbilityKind.Melee, AbilityInput.Press),
                (AbilityKind.Melee, AbilityInput.Slide),
                (AbilityKind.ClassAbility, AbilityInput.Press),
                (AbilityKind.ClassAbility, AbilityInput.Air),
                (AbilityKind.Super, AbilityInput.Press),
            }
            .Where(use => Enum.GetValues<OffensiveAbility>().Any(offensive => offensive.ToAbilityKind() == use.Item1)),
    ];

    public static ElementLinks MapElement(GraphContext context, BuildElement element)
    {
        var node = ToElementNode(context, element);
        var ruleNeeds = element.Rules.Select(rule => ListRuleNeeds(context, rule)).ToImmutableArray();
        var passiveNeeds = element.Passives.Select(passive => ListConditionNeeds(context, passive.When)).ToImmutableArray();
        var wayNeeds = ruleNeeds.Concat(passiveNeeds).ToImmutableArray();
        var given = element.Rules.SelectMany(rule => rule.Then).SelectMany(outcome => ListGivenNodes(context, outcome)).ToImmutableArray();
        var needs = wayNeeds.SelectMany(way => way).ToImmutableArray();
        var needEdges = needs.SelectMany(need => need.Alternatives.Select(needed => new GraphEdge(needed.Id, node.Id, EdgeKind.Needs, need.Label)));
        var giveEdges = given.Select(gift => new GraphEdge(node.Id, gift.Id, EdgeKind.Gives, ""));
        return new ElementLinks(
            node,
            element.Kind == ElementKind.Keyword,
            wayNeeds.IsEmpty ? [new FiringWay([], [])] : [.. wayNeeds.Select(ToFiringWay)],
            [.. needEdges.Concat(giveEdges).Where(edge => edge.From != edge.To).Distinct()],
            [.. given.Select(gift => gift.Id)],
            [.. needs.SelectMany(need => need.Alternatives).Concat(given).Where(touched => touched.Id != node.Id)]);
    }

    /// <summary>
    /// A super, grenade, melee, class ability, aspect or fragment of the class and the subclass's element: the subclass's
    /// own elements (jumps are left out: movement has no part in a loop).
    /// </summary>
    public static bool IsSubclassPart(GraphContext context, BuildElement element) =>
        element.Kind is ElementKind.Super or ElementKind.Grenade or ElementKind.Melee or ElementKind.ClassAbility
            or ElementKind.Aspect or ElementKind.Fragment
        && element.Class == Optional.Some(context.Class)
        && element.Affinity == context.Subclass.ToAffinity();

    /// <summary>
    /// The elements a graph can hold: not inert, no jump, fitting the class and subclass, and a keyword only of no element
    /// or the subclass's (a Strand keyword has no part in an Arc graph).
    /// </summary>
    public static bool IsDrawn(GraphContext context, BuildElement element) =>
        !element.Inert.IsSome()
        && element.Kind != ElementKind.Jump
        && element.Fits(context.Class, context.Subclass)
        && (element.Kind != ElementKind.Keyword
            || element.Affinity is Affinity.Neutral or Affinity.Kinetic
            || element.Affinity == context.Subclass.ToAffinity());

    private static FiringWay ToFiringWay(ImmutableArray<Need> needs) =>
        new(
            [.. needs.Where(need => need.Alternatives.Length == 1).Select(need => need.Alternatives[0].Id)],
            [.. needs.Where(need => need.Alternatives.Length > 1).Select(need => need.Alternatives.Select(node => node.Id).ToImmutableArray())]);

    private static ImmutableArray<Need> ListRuleNeeds(GraphContext context, Rule rule)
    {
        var label = DescribeTriggerLabel(rule.On);
        var action = ListActionNodes(context, rule.On).Select(node => ToNeed(node, label));
        var via = rule.On.FindVia().Map(source => ListSourceNodes(context, source)).UnwrapOr([]);
        var viaNeeds = via.IsEmpty ? [] : new[] { new Need(via, label) };
        var targets = rule.On.ListRequiredTargetStatuses()
            .Select(status => ToNeed(ToStatusNode(context, status), $"{label} on {context.Catalog.Glossary.DescribeDebuffedAdjective(status)}"));
        return [.. action, .. viaNeeds, .. targets, .. ListConditionNeeds(context, rule.When)];
    }

    private static ImmutableArray<Need> ListConditionNeeds(GraphContext context, ImmutableArray<Condition> conditions) =>
        [.. conditions.SelectMany(condition => condition.Match<IEnumerable<Need>>(
            hasBuff: has => [ToNeed(ToStatusNode(context, has.Status), "while")],
            lacksBuff: _ => [],
            targetHas: target => [ToNeed(ToStatusNode(context, target.Status), $"on {context.Catalog.Glossary.DescribeDebuffedAdjective(target.Status)}")],
            atMax: atMax => [ToNeed(ToStatusNode(context, atMax.Status), "at max")],
            during: _ => [],
            belowMax: below => [ToNeed(ToStatusNode(context, below.Status), "below max")],
            holds: holds => [ToNeed(ToStatusNode(context, holds.Status), $"{holds.AtLeast.Value.ToString(CultureInfo.InvariantCulture)}+ stacks")]))];

    private static Need ToNeed(GraphNode node, string label) => new([node], label);

    /// <summary>The edge label of a trigger: what happens to set it off ("kill", "3+ kills", "sustained hit", "cast").</summary>
    private static string DescribeTriggerLabel(Trigger trigger) =>
        trigger.Match(
            abilityCast: _ => "cast",
            killAny: _ => "kill",
            killOfTier: kill => $"{kill.Tier.ToString().ToLowerInvariant()} kill",
            killDebuffed: _ => "kill",
            killMultiple: kill => $"{kill.AtLeast.Value.ToString(CultureInfo.InvariantCulture)}+ kills",
            damage: damage => DescribeHitLabel(damage.Needs),
            damageDebuffed: damage => DescribeHitLabel(damage.Needs),
            damageMultiple: damage => $"{damage.AtLeast.Value.ToString(CultureInfo.InvariantCulture)}+ hits",
            pickUp: _ => "pick up",
            buffGained: _ => "gained",
            stacksMaxed: _ => "at max",
            finisher: _ => "finisher",
            abilityCastAs: _ => "cast",
            killFewer: _ => "kill",
            criticalHealth: _ => "critical health",
            shieldBroken: _ => "shield broken",
            energyExpended: _ => "last charge",
            damageFewer: _ => "hit",
            killBetween: kill => $"{kill.AtLeast.Value.ToString(CultureInfo.InvariantCulture)}+ kills",
            reload: _ => "reload");

    private static string DescribeHitLabel(DamageSpan span) =>
        span == DamageSpan.Sustained ? "sustained hit" : "hit";

    /// <summary>What a trigger needs besides its damage source: the player's action, or the pickup or buff it reacts to.</summary>
    private static ImmutableArray<GraphNode> ListActionNodes(GraphContext context, Trigger trigger) =>
        trigger.Match<ImmutableArray<GraphNode>>(
            abilityCast: cast => [ToTriggerNode(context, cast.Kind, AbilityInput.Press)],
            killAny: _ => [],
            killOfTier: _ => [],
            killDebuffed: _ => [],
            killMultiple: _ => [],
            damage: _ => [],
            damageDebuffed: _ => [],
            damageMultiple: _ => [],
            pickUp: pickUp => [ToPickupNode(context, pickUp.Pickup)],
            buffGained: gained => [ToStatusNode(context, gained.Status)],
            stacksMaxed: maxed => [ToStatusNode(context, maxed.Status)],
            finisher: _ => [ToTriggerNode("Finisher")],
            abilityCastAs: cast => [ToTriggerNode(context, cast.Kind, cast.Input)],
            killFewer: _ => [],
            criticalHealth: _ => [ToTriggerNode("Critical health")],
            shieldBroken: _ => [ToTriggerNode("Shield broken")],
            energyExpended: expended => [ToTriggerNode(context, expended.Kind, AbilityInput.Press)],
            damageFewer: _ => [],
            killBetween: _ => [],
            reload: _ => [ToTriggerNode("Reload")]);

    /// <summary>
    /// The nodes a damage source needs: the weapon or ability trigger, the strike or summon dealing it; any ability is one
    /// of each ability trigger; any source and "this weapon" (the element sits on it) need none.
    /// </summary>
    private static ImmutableArray<GraphNode> ListSourceNodes(GraphContext context, DamageSource source) =>
        source.Match<ImmutableArray<GraphNode>>(
            anySource: _ => [],
            anyWeapon: _ => [ToWeaponNode("Weapon")],
            weaponOfType: weapon => [ToWeaponNode($"{weapon.Type} weapon")],
            weaponInSlot: weapon => [ToWeaponNode($"{weapon.Slot} weapon")],
            anyAbility: _ => [.. DamagingAbilityUses.Select(use => ToTriggerNode(context, use.Kind, use.Input))],
            abilityOf: ability => [ToTriggerNode(context, ability.Kind, AbilityInput.Press)],
            ofType: type => [ToWeaponNode($"{type.Type} damage")],
            keywordOf: keyword => [ToStrikeNode(context, keyword.Status)],
            summonOf: summon => [ToSummonNode(context, summon.Summon)],
            weaponOfSuperType: _ => [ToWeaponNode(context.Subclass == Subclass.Prismatic ? "Weapon of your Super's element" : $"{context.Subclass.ToDamageType()} weapon")],
            abilityUsedAs: used => [ToTriggerNode(context, used.Kind, used.Input)],
            thisWeapon: _ => [],
            thisWeaponOfType: _ => []);

    /// <summary>The statuses, pickups, summons and strikes an outcome gives; energy, health, damage and the rest give no node.</summary>
    private static ImmutableArray<GraphNode> ListGivenNodes(GraphContext context, Outcome outcome) =>
        outcome.Match<ImmutableArray<GraphNode>>(
            grantEnergy: _ => [],
            convertStacksToEnergy: _ => [],
            applyBuff: apply => [ToStatusNode(context, apply.Status)],
            removeBuff: _ => [],
            debuffTarget: debuff => [ToStatusNode(context, debuff.Status)],
            spawn: spawn => [ToPickupNode(context, spawn.Pickup)],
            spawnSummon: summon => [ToSummonNode(context, summon.Summon)],
            strikeTarget: strike => [ToStrikeNode(context, strike.Via)],
            modifyDamage: _ => [],
            restoreHealth: _ => [],
            resetCooldown: _ => [],
            collect: _ => [],
            grantLeastChargedEnergy: _ => [],
            spendStacks: _ => [],
            weaponStrike: _ => [],
            reloadWeapon: _ => []);

    private static GraphNode ToElementNode(GraphContext context, BuildElement element) =>
        new(element.Id.Value, element.Name, ToElementType(context, element), element.Kind == ElementKind.Keyword, Optional.Some(ToFacts(element)));

    private static GraphNodeType ToElementType(GraphContext context, BuildElement element) =>
        element.Kind switch
        {
            ElementKind.Keyword => ToKeywordType(context.Catalog.Glossary, element.Id.Value),
            _ when !IsSubclassPart(context, element) => new GraphNodeType.OutsideElement(element.Kind),
            ElementKind.Super => new GraphNodeType.SubclassSuper(),
            ElementKind.Grenade => new GraphNodeType.SubclassAbility(AbilityKind.Grenade),
            ElementKind.Melee => new GraphNodeType.SubclassAbility(AbilityKind.Melee),
            ElementKind.ClassAbility => new GraphNodeType.SubclassAbility(AbilityKind.ClassAbility),
            ElementKind.Aspect => new GraphNodeType.SubclassAspect(),
            _ => new GraphNodeType.SubclassFragment(),
        };

    /// <summary>A keyword is the glossary's status, pickup or summon of its id; none of them: a keyword of its own (Guardian).</summary>
    private static GraphNodeType ToKeywordType(KeywordGlossary glossary, string id) =>
        glossary.Statuses.TryGetValue(StatusId.From(id), out var status)
            ? ToStatusType(status.Kind)
            : glossary.Pickups.ContainsKey(PickupId.From(id))
                ? new GraphNodeType.PickupDrop()
                : glossary.Summons.ContainsKey(SummonId.From(id))
                    ? new GraphNodeType.SummonedAlly()
                    : new GraphNodeType.UnlistedKeyword();

    private static GraphNodeType ToStatusType(KeywordKind kind) =>
        kind.IsBuff() ? new GraphNodeType.BuffStatus() : new GraphNodeType.DebuffStatus();

    private static ElementFacts ToFacts(BuildElement element) =>
        new(
            $"{RulesFolder}/{element.RuleFile}",
            CiteSource(element.Source),
            element.Description,
            [
                .. element.Rules.SelectMany(rule => rule.Reason.Match(reason => new[] { reason.Value }, _ => [])),
                .. element.Passives.SelectMany(passive => passive.Reason.Match(reason => new[] { reason.Value }, _ => [])),
            ]);

    /// <summary>The source as the rules cite it (<c>compendium/2026-10-09/Arc#44</c>); none when the rule file is its own source.</summary>
    private static Optional<string> CiteSource(Provenance source) =>
        source.Match(
            compendium => Optional.Some(
                $"compendium/{compendium.Snapshot.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}/{compendium.Tab}#{compendium.Row.ToString(CultureInfo.InvariantCulture)}"),
            clarity => Optional.Some($"clarity/{clarity.Hash.Value.ToString(CultureInfo.InvariantCulture)}@{clarity.ClarityVersion}"),
            _ => Optional.None<string>(),
            creator => Optional.Some(creator.SourceUrl));

    /// <summary>A status's node: the keyword element of its id when there is one (one node), else the glossary's status.</summary>
    private static GraphNode ToStatusNode(GraphContext context, StatusId status) =>
        FindKeywordNode(context, status.Value).UnwrapOr(new GraphNode(
            ToGlossaryNodeId(context, "status", status.Value),
            context.Catalog.Glossary.DescribeStatus(status),
            context.Catalog.Glossary.Statuses.TryGetValue(status, out var definition) ? ToStatusType(definition.Kind) : new GraphNodeType.BuffStatus(),
            false,
            Optional.None<ElementFacts>()));

    private static GraphNode ToPickupNode(GraphContext context, PickupId pickup) =>
        FindKeywordNode(context, pickup.Value).UnwrapOr(new GraphNode(
            ToGlossaryNodeId(context, "pickup", pickup.Value), context.Catalog.Glossary.DescribePickup(pickup), new GraphNodeType.PickupDrop(), false, Optional.None<ElementFacts>()));

    private static GraphNode ToSummonNode(GraphContext context, SummonId summon) =>
        FindKeywordNode(context, summon.Value).UnwrapOr(new GraphNode(
            ToGlossaryNodeId(context, "summon", summon.Value), context.Catalog.Glossary.DescribeSummon(summon), new GraphNodeType.SummonedAlly(), false, Optional.None<ElementFacts>()));

    /// <summary>
    /// A glossary entry's node id: its own, or with its kind in front when an element that isn't its keyword has the same
    /// id (the Lethal Current aspect and the buff it gives: <c>status:lethal-current</c>).
    /// </summary>
    private static string ToGlossaryNodeId(GraphContext context, string kind, string id) =>
        context.Catalog.Elements.ContainsKey(ElementId.From(id)) ? $"{kind}:{id}" : id;

    private static Optional<GraphNode> FindKeywordNode(GraphContext context, string id) =>
        context.Catalog.Elements.TryGetValue(ElementId.From(id), out var element) && element.Kind == ElementKind.Keyword
            ? Optional.Some(ToElementNode(context, element))
            : Optional.None<GraphNode>();

    /// <summary>The strike a keyword deals (<c>strike:bolt-charge</c>, "Bolt Charge strike").</summary>
    private static GraphNode ToStrikeNode(GraphContext context, StatusId via) =>
        new($"strike:{via.Value}", $"{context.Catalog.Glossary.DescribeStatus(via)} strike", new GraphNodeType.KeywordStrike(), false, Optional.None<ElementFacts>());

    private static GraphNode ToTriggerNode(GraphContext context, AbilityKind kind, AbilityInput input) =>
        ToTriggerNode(NameAbilityTrigger(context.Class, kind, input));

    private static GraphNode ToTriggerNode(string name) =>
        new($"trigger:{ToKebab(name)}", name, new GraphNodeType.ActionTrigger(), false, Optional.None<ElementFacts>());

    private static GraphNode ToWeaponNode(string name) =>
        new($"weapon:{ToKebab(name)}", name, new GraphNodeType.NeededWeapon(), false, Optional.None<ElementFacts>());

    /// <summary>An ability use as the player says it: a Hunter's class ability is their dodge.</summary>
    private static string NameAbilityTrigger(GuardianClass guardianClass, AbilityKind kind, AbilityInput input) =>
        (kind, input) switch
        {
            (AbilityKind.Grenade, AbilityInput.Press) => "Grenade",
            (AbilityKind.Grenade, AbilityInput.Hold) => "Held grenade",
            (AbilityKind.Melee, AbilityInput.Press) => "Melee",
            (AbilityKind.Melee, AbilityInput.Slide) => "Sliding melee",
            (AbilityKind.Melee, AbilityInput.Air) => "Air melee",
            (AbilityKind.ClassAbility, AbilityInput.Press) => guardianClass == GuardianClass.Hunter ? "Dodge" : "Class ability",
            (AbilityKind.ClassAbility, AbilityInput.Air) => guardianClass == GuardianClass.Hunter ? "Air dodge" : "Air class ability",
            (AbilityKind.Super, AbilityInput.Press) => "Super",
            _ => $"{kind} ({input.ToString().ToLowerInvariant()})",
        };

    private static string ToKebab(string name) =>
        string.Join('-', name.ToLowerInvariant().Replace("'", "", StringComparison.Ordinal).Replace("(", "", StringComparison.Ordinal)
            .Replace(")", "", StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

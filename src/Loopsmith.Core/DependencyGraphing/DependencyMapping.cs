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

/// <summary>What an outcome or a passive does to a node: gives it, ends it, collects it or boosts it, with the edge's label.</summary>
internal sealed record Effect(GraphNode Node, EdgeKind Kind, string Label);

/// <summary>
/// An element as the graph reads it: its node (a keyword's rule node, <c>rule:jolt</c>), its keyword state's id when it has
/// one (<c>jolt</c>), the ways it fires, its edges (none to itself), what it gives (the only edges the columns count) and
/// every other node it touches.
/// </summary>
internal sealed record ElementLinks(
    GraphNode Node,
    Optional<string> StateId,
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

    /// <summary>Where a manifest icon path is served, as the web app reads it (its <c>LoadoutShaping.ToIconUrl</c>).</summary>
    private const string IconHost = "https://www.bungie.net";

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
        var effects = element.Rules.SelectMany(rule => rule.Then).SelectMany(outcome => ListOutcomeEffects(context, outcome))
            .Concat(element.Passives.SelectMany(passive => ListPassiveEffects(context, passive.Modifier)))
            .Where(effect => effect.Node.Id != node.Id)
            .ToImmutableArray();
        var needs = wayNeeds.SelectMany(way => way).ToImmutableArray();
        var needEdges = needs.SelectMany(need => need.Alternatives.Select(needed => new GraphEdge(needed.Id, node.Id, EdgeKind.Needs, need.Label)));
        var effectEdges = effects.Select(effect => new GraphEdge(node.Id, effect.Node.Id, effect.Kind, effect.Label));
        return new ElementLinks(
            node,
            FindKeywordState(context, element),
            wayNeeds.IsEmpty ? [new FiringWay([], [])] : [.. wayNeeds.Select(ToFiringWay)],
            [.. needEdges.Concat(effectEdges).Where(edge => edge.From != edge.To).Distinct()],
            [.. effects.Where(effect => effect.Kind == EdgeKind.Gives).Select(effect => effect.Node.Id)],
            [.. needs.SelectMany(need => need.Alternatives).Concat(effects.Select(effect => effect.Node)).Where(touched => touched.Id != node.Id)]);
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
            finisher: _ => [ToTriggerNode("Finisher", context.Catalog.FindClassFinisher(context.Class).Bind(item => item.Icon).Map(ToIconUrl))],
            abilityCastAs: cast => [ToTriggerNode(context, cast.Kind, cast.Input)],
            killFewer: _ => [],
            criticalHealth: _ => [ToTriggerNode("Critical health", FindStatIcon(context, StatKind.Health))],
            shieldBroken: _ => [ToTriggerNode("Shield broken", Optional.None<string>())],
            energyExpended: expended => [ToTriggerNode(context, expended.Kind, AbilityInput.Press)],
            damageFewer: _ => [],
            killBetween: _ => [],
            reload: _ => [ToTriggerNode("Reload", Optional.None<string>())]);

    /// <summary>
    /// The nodes a damage source needs: the weapon or ability trigger, the strike or summon dealing it; any ability is one
    /// of each ability trigger; any source and "this weapon" (the element sits on it) need none.
    /// </summary>
    private static ImmutableArray<GraphNode> ListSourceNodes(GraphContext context, DamageSource source) =>
        source.Match<ImmutableArray<GraphNode>>(
            anySource: _ => [],
            anyWeapon: _ => [ToWeaponNode("Weapon", FindStatIcon(context, StatKind.Weapons))],
            weaponOfType: weapon => [ToWeaponNode($"{weapon.Type} weapon", FindDamageTypeIcon(context, weapon.Type))],
            weaponInSlot: weapon => [ToWeaponNode($"{weapon.Slot} weapon", Optional.None<string>())],   // a slot has no damage type
            anyAbility: _ => [.. DamagingAbilityUses.Select(use => ToTriggerNode(context, use.Kind, use.Input))],
            abilityOf: ability => [ToTriggerNode(context, ability.Kind, AbilityInput.Press)],
            ofType: type => [ToWeaponNode($"{type.Type} damage", FindDamageTypeIcon(context, type.Type))],
            keywordOf: keyword => [ToStrikeNode(context, keyword.Status)],
            summonOf: summon => [ToSummonNode(context, summon.Summon)],
            weaponOfSuperType: _ => [context.Subclass == Subclass.Prismatic
                ? ToWeaponNode("Weapon of your Super's element", Optional.None<string>())
                : ToWeaponNode($"{context.Subclass.ToDamageType()} weapon", FindDamageTypeIcon(context, context.Subclass.ToDamageType()))],
            abilityUsedAs: used => [ToTriggerNode(context, used.Kind, used.Input)],
            thisWeapon: _ => [],
            thisWeaponOfType: _ => []);

    /// <summary>
    /// What an outcome does: gives a status, pickup, summon, strike or effect (energy, health, a damage bonus, an extra
    /// strike, a reload), ends a status (removes or spends it) or collects a pickup. Every case is drawn.
    /// </summary>
    private static ImmutableArray<Effect> ListOutcomeEffects(GraphContext context, Outcome outcome) =>
        outcome.Match<ImmutableArray<Effect>>(
            grantEnergy: grant => [Give(ToEnergyNode(context, grant.To), DescribeEnergy(grant.Amount))],
            convertStacksToEnergy: convert =>
            [
                new Effect(ToStatusNode(context, convert.Consumed), EdgeKind.Ends, "spends all"),
                Give(ToEnergyNode(context, convert.To), DescribeStackEnergy(convert.Energy)),
            ],
            applyBuff: apply => [Give(ToStatusNode(context, apply.Status), "")],
            removeBuff: remove => [new Effect(ToStatusNode(context, remove.Status), EdgeKind.Ends, "ends")],
            debuffTarget: debuff => [Give(ToStatusNode(context, debuff.Status), "")],
            spawn: spawn => [Give(ToPickupNode(context, spawn.Pickup), "")],
            spawnSummon: summon => [Give(ToSummonNode(context, summon.Summon), "")],
            strikeTarget: strike => [Give(ToStrikeNode(context, strike.Via), "")],
            modifyDamage: modify => [Give(ToEffectNode("damage-bonus", "Damage bonus"), DescribeDamageChange(context, modify.Against, modify.Change))],
            restoreHealth: heal => [Give(ToEffectNode("health", "Health", FindStatIcon(context, StatKind.Health)), DescribeHeal(heal))],
            resetCooldown: reset => [Give(ToEnergyNode(context, reset.Which), "full")],
            collect: collect => [new Effect(ToPickupNode(context, collect.Pickup), EdgeKind.Collects, "collects")],
            grantLeastChargedEnergy: least => [Give(ToEffectNode("energy-least-charged", "Least-charged ability energy"), DescribeEnergy(least.Amount))],
            spendStacks: spend => [new Effect(ToStatusNode(context, spend.Status), EdgeKind.Ends, $"spends {spend.Count.Value.ToString(CultureInfo.InvariantCulture)}")],
            weaponStrike: strike => [Give(ToEffectNode("extra-strike", "Extra strike"), strike.Hit == HitOutcome.Kill ? "kill" : "hit")],
            reloadWeapon: _ => [Give(ToEffectNode("reload", "Reload"), "")]);

    /// <summary>What a passive does while it holds: an effect it gives (resistance, a damage bonus, stats, a charge), or the status it boosts.</summary>
    private static ImmutableArray<Effect> ListPassiveEffects(GraphContext context, Passive passive) =>
        passive.Match<ImmutableArray<Effect>>(
            extraStacks: extra => [new Effect(ToStatusNode(context, extra.Status), EdgeKind.Boosts, $"+{extra.Extra.Value.ToString(CultureInfo.InvariantCulture)} stack")],
            extraCharges: charges => [Give(
                ToEffectNode($"extra-charge-{ToKebab(charges.Ability.DescribeAbility())}", $"Extra {charges.Ability.DescribeAbility()} charge"),
                $"+{charges.Extra.Value.ToString(CultureInfo.InvariantCulture)}")],
            modifyDamage: modify => [Give(ToEffectNode("damage-bonus", "Damage bonus"), DescribeDamageChange(context, modify.Against, modify.Change))],
            resistDamage: resist => [Give(
                ToEffectNode("damage-resistance", "Damage resistance"),
                resist.Amount.Pve.ToPercentText() + resist.Amount.Pvp.Map(pvp => $" [PvP {pvp.ToPercentText()}]").UnwrapOr(""))],
            modifyWeaponStats: weapons => [Give(
                ToEffectNode("weapon-stats", "Weapon stats"),
                string.Join(", ", weapons.Changes.Select(change => $"{change.Stat} {FormatSigned(change.Change)}")))],
            raiseMaxStacks: raise => [new Effect(ToStatusNode(context, raise.Status), EdgeKind.Boosts, $"max {raise.Max.ToNumberText()}")],
            modifyStat: stat => [Give(ToEffectNode($"stat-{ToKebab(stat.Stat.ToString())}", $"{stat.Stat} stat", FindStatIcon(context, stat.Stat)), FormatSigned(stat.Change))],
            resistCombatantDamage: resist => [Give(
                ToEffectNode("damage-resistance", "Damage resistance"),
                $"{resist.Amount.ToPercentText()} vs {resist.Against.DescribeCombatantDamage(Optional.None<DamageType>())}")]);

    private static Effect Give(GraphNode node, string label) => new(node, EdgeKind.Gives, label);

    private static string DescribeEnergy(EnergyGrant grant) =>
        grant.Match(
            fraction => fraction.Amount.ToPercentText(),
            _ => "full",
            byArchetype => $"{byArchetype.Otherwise.ToPercentText()} ({byArchetype.OnArchetypes.ToPercentText()} on {string.Join(", ", byArchetype.Archetypes)})");

    private static string DescribeStackEnergy(StackEnergy energy) =>
        energy.Match(
            perStack => $"{perStack.Amount.ToPercentText()} per stack",
            bySpent => $"{DescribeTable(bySpent.Table)} by stacks spent from x{bySpent.Table.From.Value.ToString(CultureInfo.InvariantCulture)}",
            byCharges => $"{DescribeTable(byCharges.Table)} by charges from {byCharges.Table.From.Value.ToString(CultureInfo.InvariantCulture)}");

    private static string DescribeTable(StackTable table) =>
        string.Join(" | ", table.Values.Select(value => new GameValue.Known(value).ToPercentText()));

    private static string DescribeDamageChange(GraphContext context, DamageSource against, GameValue change) =>
        $"{FormatSigned(change, percent: true)} vs {context.Catalog.Glossary.DescribeDamageSource(against, Optional.None<DamageType>())}";

    private static string DescribeHeal(Outcome.RestoreHealth heal) =>
        heal.Amount.Match(share => share.Value.ToPercentText(), points => $"{points.Value.ToNumberText()} HP")
        + (heal.IncludesAllies ? " + allies" : "");

    /// <summary>"+20", "-10", "+12%|17%|20%", "?": a change as the game shows it.</summary>
    private static string FormatSigned(GameValue value, bool percent = false)
    {
        var text = percent ? value.ToPercentText() : value.ToNumberText();
        return text.StartsWith('-') || text.StartsWith('?') ? text : "+" + text;
    }

    /// <summary>The energy an ability gets (<c>effect:energy-grenade</c>, "Grenade energy").</summary>
    private static GraphNode ToEnergyNode(GraphContext context, AbilityKind ability) =>
        ToEffectNode($"energy-{ToKebab(ability.DescribeAbility())}", $"{ToSentenceStart(ability.DescribeAbility())} energy", FindStatIcon(context, ToStat(ability)));

    private static GraphNode ToEffectNode(string id, string name) => ToEffectNode(id, name, Optional.None<string>());

    private static GraphNode ToEffectNode(string id, string name, Optional<string> icon) =>
        new($"effect:{id}", name, new GraphNodeType.ShownEffect(), false, Optional.None<ElementFacts>(), icon);

    /// <summary>A fraction as the rules author it: 0.1125 → "11.25%", "12% | 17% | 20%" by copies, "~25%", "?".</summary>
    private static string ToPercentText(this GameValue value) =>
        value.Match(
            known => FormatPercentNumber(known.Value),
            perModCount => string.Join(" | ", perModCount.Values.Select(FormatPercentNumber)),
            approximate => "~" + FormatPercentNumber(approximate.Value),
            _ => "?");

    private static string ToNumberText(this GameValue value) =>
        value.Match(
            known => known.Value.ToString("0.###", CultureInfo.InvariantCulture),
            perModCount => string.Join(" | ", perModCount.Values.Select(number => number.ToString("0.###", CultureInfo.InvariantCulture))),
            approximate => "~" + approximate.Value.ToString("0.###", CultureInfo.InvariantCulture),
            _ => "?");

    private static string FormatPercentNumber(decimal fraction) =>
        (fraction * 100m).ToString("0.###", CultureInfo.InvariantCulture) + "%";

    private static string ToSentenceStart(string text) => string.Concat(text[..1].ToUpperInvariant(), text[1..]);

    /// <summary>An element's node; a keyword with a glossary state is its rule node (<c>rule:jolt</c>), the state a node of its own.</summary>
    private static GraphNode ToElementNode(GraphContext context, BuildElement element) =>
        FindKeywordState(context, element).Match(
            state => new GraphNode(
                $"rule:{state.Value}", element.Name, new GraphNodeType.KeywordRule(), true, Optional.Some(ToFacts(element)), FindGlossaryIcon(context, state.Value)),
            _ => new GraphNode(
                element.Id.Value,
                element.Name,
                ToElementType(context, element),
                element.Kind == ElementKind.Keyword,
                Optional.Some(ToFacts(element)),
                FindElementIcon(context, element.Id)));

    /// <summary>The glossary status, pickup or summon a keyword element is the rules of; none for any other element (and for Guardian).</summary>
    private static Optional<string> FindKeywordState(GraphContext context, BuildElement element) =>
        element.Kind == ElementKind.Keyword && ToKeywordType(context.Catalog.Glossary, element.Id.Value) is not GraphNodeType.UnlistedKeyword
            ? Optional.Some(element.Id.Value)
            : Optional.None<string>();

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

    /// <summary>A status's node, the glossary's (its state when a keyword element has its rules: <see cref="GraphNode.IsKeyword"/>).</summary>
    private static GraphNode ToStatusNode(GraphContext context, StatusId status) =>
        new(
            ToGlossaryNodeId(context, "status", status.Value),
            context.Catalog.Glossary.DescribeStatus(status),
            context.Catalog.Glossary.Statuses.TryGetValue(status, out var definition) ? ToStatusType(definition.Kind) : new GraphNodeType.BuffStatus(),
            HasKeywordRules(context, status.Value),
            Optional.None<ElementFacts>(),
            FindStateIcon(context, status.Value));

    private static GraphNode ToPickupNode(GraphContext context, PickupId pickup) =>
        new(
            ToGlossaryNodeId(context, "pickup", pickup.Value),
            context.Catalog.Glossary.DescribePickup(pickup),
            new GraphNodeType.PickupDrop(),
            HasKeywordRules(context, pickup.Value),
            Optional.None<ElementFacts>(),
            FindStateIcon(context, pickup.Value));

    private static GraphNode ToSummonNode(GraphContext context, SummonId summon) =>
        new(
            ToGlossaryNodeId(context, "summon", summon.Value),
            context.Catalog.Glossary.DescribeSummon(summon),
            new GraphNodeType.SummonedAlly(),
            HasKeywordRules(context, summon.Value),
            Optional.None<ElementFacts>(),
            FindStateIcon(context, summon.Value));

    private static bool HasKeywordRules(GraphContext context, string id) =>
        context.Catalog.Elements.TryGetValue(ElementId.From(id), out var element) && element.Kind == ElementKind.Keyword;

    /// <summary>
    /// A glossary entry's node id: its own, or with its kind in front when an element that isn't its keyword has the same
    /// id (the Lethal Current aspect and the buff it gives: <c>status:lethal-current</c>).
    /// </summary>
    private static string ToGlossaryNodeId(GraphContext context, string kind, string id) =>
        context.Catalog.Elements.ContainsKey(ElementId.From(id)) && !HasKeywordRules(context, id) ? $"{kind}:{id}" : id;

    /// <summary>The strike a keyword deals (<c>strike:bolt-charge</c>, "Bolt Charge strike").</summary>
    private static GraphNode ToStrikeNode(GraphContext context, StatusId via) =>
        new($"strike:{via.Value}", $"{context.Catalog.Glossary.DescribeStatus(via)} strike", new GraphNodeType.KeywordStrike(), false, Optional.None<ElementFacts>(),
            FindGlossaryIcon(context, via.Value));

    /// <summary>An ability trigger, with the icon of the ability's armor stat (the dodge: Class).</summary>
    private static GraphNode ToTriggerNode(GraphContext context, AbilityKind kind, AbilityInput input) =>
        ToTriggerNode(NameAbilityTrigger(context.Class, kind, input), FindStatIcon(context, ToStat(kind)));

    private static GraphNode ToTriggerNode(string name, Optional<string> icon) =>
        new($"trigger:{ToKebab(name)}", name, new GraphNodeType.ActionTrigger(), false, Optional.None<ElementFacts>(), icon);

    private static GraphNode ToWeaponNode(string name, Optional<string> icon) =>
        new($"weapon:{ToKebab(name)}", name, new GraphNodeType.NeededWeapon(), false, Optional.None<ElementFacts>(), icon);

    /// <summary>The armor stat an ability's energy goes by.</summary>
    private static StatKind ToStat(AbilityKind kind) =>
        kind switch
        {
            AbilityKind.Grenade => StatKind.Grenade,
            AbilityKind.Melee => StatKind.Melee,
            AbilityKind.ClassAbility => StatKind.Class,
            _ => StatKind.Super,
        };

    private static Optional<string> FindStatIcon(GraphContext context, StatKind stat) =>
        context.Catalog.Manifest.FindArmorStat(stat).Map(found => ToIconUrl(found.Icon));

    private static Optional<string> FindDamageTypeIcon(GraphContext context, DamageType type) =>
        context.Catalog.Manifest.DamageTypeIcons.TryGetValue(type, out var icon) ? Optional.Some(ToIconUrl(icon)) : Optional.None<string>();

    /// <summary>An element's icon: its manifest item's.</summary>
    private static Optional<string> FindElementIcon(GraphContext context, ElementId element) =>
        context.Catalog.FindElementItem(element).Bind(item => item.Icon).Map(ToIconUrl);

    /// <summary>
    /// A state's icon: its glossary trait's; one without a trait whose id an element shares (the Lethal Current aspect's buff)
    /// takes that element's.
    /// </summary>
    private static Optional<string> FindStateIcon(GraphContext context, string id) =>
        new[]
        {
            FindGlossaryIcon(context, id),
            HasKeywordRules(context, id) ? Optional.None<string>() : FindElementIcon(context, ElementId.From(id)),
        }.FindFirstSome();

    /// <summary>The icon of the trait the glossary gives a status, pickup or summon of this id.</summary>
    private static Optional<string> FindGlossaryIcon(GraphContext context, string id)
    {
        var glossary = context.Catalog.Glossary;
        var trait = glossary.Statuses.TryGetValue(StatusId.From(id), out var status)
            ? status.Trait
            : glossary.Pickups.TryGetValue(PickupId.From(id), out var pickup)
                ? pickup.Trait
                : glossary.Summons.TryGetValue(SummonId.From(id), out var summon) ? summon.Trait : Optional.None<ItemHash>();
        return trait.Bind(context.Catalog.Manifest.FindTraitIcon).Map(ToIconUrl);
    }

    /// <summary>A manifest icon path as the address the web app shows it at.</summary>
    private static string ToIconUrl(string path) => IconHost + path;

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

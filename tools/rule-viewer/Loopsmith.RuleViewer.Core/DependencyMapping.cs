using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.RuleViewer.Core;

/// <summary>
/// The rules a graph is drawn from, and the class (none: a rule of every class, drawn as itself) and subclass it is drawn
/// for.
/// </summary>
internal sealed record GraphContext(RuleCatalog Catalog, Optional<GuardianClass> Class, Subclass Subclass);

/// <summary>
/// What one part of a rule needs: one node, or one of several (<c>via: ability</c>: any ability trigger), with the edge's
/// label.
/// </summary>
internal sealed record Need(ImmutableArray<GraphNode> Alternatives, string Label);

/// <summary>What an outcome or a passive does to a node: gives it, ends it, collects it or boosts it, with the edge's label.</summary>
internal sealed record Effect(GraphNode Node, EdgeKind Kind, string Label);

/// <summary>
/// An element as the graph reads it: its node (a keyword's rule node, <c>rule:jolt</c>), its keyword state's id when it has
/// one (<c>jolt</c>), its edges (none to itself) and every other node it touches.
/// </summary>
internal sealed record ElementLinks(
    GraphNode Node,
    Optional<string> StateId,
    ImmutableArray<GraphEdge> Edges,
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
        var needs = element.Rules.SelectMany(rule => ListRuleNeeds(context, rule))
            .Concat(element.Passives.SelectMany(passive => ListConditionNeeds(context, passive.When)))
            .ToImmutableArray();
        var effects = element.Rules.SelectMany(rule => rule.Then).SelectMany(outcome => ListOutcomeEffects(context, outcome))
            .Concat(element.Passives.SelectMany(passive => ListPassiveEffects(context, passive.Modifier)))
            .Where(effect => effect.Node.Id != node.Id)
            .ToImmutableArray();
        var needEdges = needs.SelectMany(need => need.Alternatives.Select(needed => new GraphEdge(needed.Id, node.Id, EdgeKind.Needs, need.Label)));
        var effectEdges = effects.Select(effect => new GraphEdge(node.Id, effect.Node.Id, effect.Kind, effect.Label));
        return new ElementLinks(
            node,
            FindKeywordState(context, element),
            [.. needEdges.Concat(effectEdges).Where(edge => edge.From != edge.To).Distinct()],
            [.. needs.SelectMany(need => need.Alternatives).Concat(effects.Select(effect => effect.Node)).Where(touched => touched.Id != node.Id)]);
    }

    /// <summary>
    /// A super, grenade, melee, class ability, aspect or fragment of the class (or of every class: the subclass's grenades
    /// and fragments) and the subclass's element: the subclass's own elements (jumps are left out: movement has no part in a
    /// loop).
    /// </summary>
    public static bool IsSubclassPart(GraphContext context, BuildElement element) =>
        element.Kind is ElementKind.Super or ElementKind.Grenade or ElementKind.Melee or ElementKind.ClassAbility
            or ElementKind.Aspect or ElementKind.Fragment
        && FitsClass(context, element)
        && element.Affinity == context.Subclass.ToAffinity();

    /// <summary>
    /// The elements a graph can hold: not inert, no jump, fitting the class and subclass, and a keyword only of no element
    /// or the subclass's (a Strand keyword has no part in an Arc graph).
    /// </summary>
    public static bool IsDrawn(GraphContext context, BuildElement element) =>
        !element.Inert.IsSome()
        && element.Kind != ElementKind.Jump
        && FitsClass(context, element)
        && element.FitsSubclass(context.Subclass)
        && (element.Kind != ElementKind.Keyword
            || element.Affinity is Affinity.Neutral or Affinity.Kinetic
            || element.Affinity == context.Subclass.ToAffinity());

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
    internal static string DescribeTriggerLabel(Trigger trigger) =>
        trigger.Match(
            abilityCast: _ => "cast",
            killAny: kill => DescribeKillLabel(kill.Needs, "kill"),
            killOfTier: kill => DescribeKillLabel(kill.Needs, $"{kill.Tier.ToString().ToLowerInvariant()} kill"),
            killDebuffed: kill => DescribeKillLabel(kill.Needs, "kill"),
            killMultiple: kill => DescribeKillLabel(kill.Needs, $"{kill.AtLeast.Value.ToString(CultureInfo.InvariantCulture)}+ kills"),
            damage: damage => DescribeHitLabel(damage.Needs),
            damageDebuffed: damage => DescribeHitLabel(damage.Needs),
            damageMultiple: damage => $"{damage.AtLeast.Value.ToString(CultureInfo.InvariantCulture)}+ hits",
            pickUp: _ => "pick up",
            buffGained: _ => "gained",
            stacksMaxed: _ => "at max",
            finisher: _ => "finisher",
            abilityCastAs: _ => "cast",
            killFewer: kill => DescribeKillLabel(kill.Needs, "kill"),
            criticalHealth: _ => "critical health",
            shieldBroken: _ => "shield broken",
            energyExpended: _ => "last charge",
            damageFewer: _ => "hit",
            killBetween: kill => DescribeKillLabel(kill.Needs, $"{kill.AtLeast.Value.ToString(CultureInfo.InvariantCulture)}+ kills"),
            reload: _ => "reload",
            hitAtCriticalHealth: _ => "hit at critical health",
            slideOverAmmo: _ => "slide over ammo",
            readyWeapon: _ => "ready");

    /// <summary>"precision kill", "body kill": where a source needs its kill to land (ADRs D36).</summary>
    internal static string DescribeKillLabel(ZoneNeed needs, string kill) =>
        needs.Match(
            _ => kill,
            _ => $"body {kill}",
            _ => $"precision {kill}");

    private static string DescribeHitLabel(DamageSpan span) =>
        span == DamageSpan.Sustained ? "sustained hit" : "hit";

    /// <summary>What a trigger needs besides its damage source: the player's action, or the pickup or buff it reacts to.</summary>
    internal static ImmutableArray<GraphNode> ListActionNodes(GraphContext context, Trigger trigger) =>
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
            finisher: _ => [ToTriggerNode("Finisher", context.Class.Bind(context.Catalog.FindClassFinisher).Bind(item => item.Icon).Map(ToIconUrl))],
            abilityCastAs: cast => [ToTriggerNode(context, cast.Kind, cast.Input)],
            killFewer: _ => [],
            criticalHealth: _ => [ToTriggerNode("Critical health", FindStatIcon(context, StatKind.Health))],
            shieldBroken: _ => [ToTriggerNode("Shield broken", Optional.None<string>())],
            energyExpended: expended => [ToTriggerNode(context, expended.Kind, AbilityInput.Press)],
            damageFewer: _ => [],
            killBetween: _ => [],
            reload: _ => [ToTriggerNode("Reload", Optional.None<string>())],
            hitAtCriticalHealth: _ => [ToTriggerNode("Hit at critical health", FindStatIcon(context, StatKind.Health))],
            slideOverAmmo: _ => [ToTriggerNode(DomainPhrasing.SlideOverAmmo, Optional.None<string>())],
            readyWeapon: ready => [ToTriggerNode(context.Catalog.Glossary.DescribeTrigger(ready, Optional.None<DamageType>()), Optional.None<string>())]);   // "Ready Sidearm": the weapon it names

    /// <summary>
    /// The nodes a damage source needs: the weapon or ability trigger, the strike or summon dealing it; any ability is one
    /// of each ability trigger; any source and "this weapon" (the element sits on it) need none.
    /// </summary>
    internal static ImmutableArray<GraphNode> ListSourceNodes(GraphContext context, DamageSource source) =>
        source.Match<ImmutableArray<GraphNode>>(
            anySource: _ => [],
            anyWeapon: _ => [ToWeaponNode("Weapon", FindStatIcon(context, StatKind.Weapons))],
            weaponOfType: weapon => [ToWeaponNode($"{weapon.Type} weapon", FindDamageTypeIcon(context, weapon.Type))],
            weaponInSlot: weapon => [ToWeaponNode($"{weapon.Slot} weapon", Optional.None<string>())],   // a slot has no damage type
            anyAbility: _ => [.. DamagingAbilityUses.Select(use => ToTriggerNode(context, use.Kind, use.Input))],
            abilityOf: ability => [.. ListDamagingUses(ability.Kind).Select(use => ToTriggerNode(context, use.Kind, use.Input))],
            ofType: type => [ToWeaponNode($"{type.Type} damage", FindDamageTypeIcon(context, type.Type))],
            keywordOf: keyword => [ToStrikeNode(context, keyword.Status)],
            summonOf: summon => [ToSummonNode(context, summon.Summon)],
            weaponOfSuperType: _ => [context.Subclass == Subclass.Prismatic
                ? ToWeaponNode("Weapon of your Super's element", Optional.None<string>())
                : ToWeaponNode($"{context.Subclass.ToDamageType()} weapon", FindDamageTypeIcon(context, context.Subclass.ToDamageType()))],
            abilityUsedAs: used => [ToTriggerNode(context, used.Kind, used.Input)],
            thisWeapon: _ => [],
            thisWeaponOfType: _ => [],
            weaponOfAmmo: weapon => [ToWeaponNode($"{weapon.Ammo} weapon", FindStatIcon(context, StatKind.Weapons))],
            weaponOfAmmoAndType: weapon => [ToWeaponNode($"{weapon.Ammo} {weapon.Type} weapon", FindDamageTypeIcon(context, weapon.Type))],
            weaponOfArchetype: weapon => [ToWeaponNode(DomainPhrasing.DescribeArchetype(weapon.Archetype), FindStatIcon(context, StatKind.Weapons))]);

    /// <summary>
    /// The uses of one ability a hit or kill "by that ability" matches, however it's used (Causality matches any input):
    /// the melee's press and its slide; a grenade only thrown (a held grenade is a cast, not a hit).
    /// </summary>
    private static IEnumerable<(AbilityKind Kind, AbilityInput Input)> ListDamagingUses(AbilityKind kind) =>
        DamagingAbilityUses.Where(use => use.Kind == kind).DefaultIfEmpty((kind, AbilityInput.Press));

    /// <summary>
    /// What an outcome does: gives a status, pickup, summon, strike or effect (energy, health, a damage bonus, an extra
    /// strike, a reload), ends a status (removes or spends it) or collects a pickup. Every case is drawn.
    /// </summary>
    internal static ImmutableArray<Effect> ListOutcomeEffects(GraphContext context, Outcome outcome) =>
        outcome.Match<ImmutableArray<Effect>>(
            grantEnergy: grant => [Give(ToEnergyNode(context, grant.To), DescribeEnergy(grant.Amount))],
            convertStacksToEnergy: convert =>
            [
                new Effect(ToStatusNode(context, convert.Consumed), EdgeKind.Ends, "spends all"),
                Give(ToEnergyNode(context, convert.To), DescribeStackEnergy(convert.Energy)),
            ],
            applyBuff: apply => [Give(ToStatusNode(context, apply.Status), DescribeBuffGrant(apply))],
            removeBuff: remove => [new Effect(ToStatusNode(context, remove.Status), EdgeKind.Ends, "")],
            debuffTarget: debuff => [Give(ToStatusNode(context, debuff.Status), debuff.Duration.Map(duration => duration.FormatSeconds()).UnwrapOr(""))],
            spawn: spawn => [Give(ToPickupNode(context, spawn.Pickup), DescribeCount(spawn.Count))],
            spawnSummon: summon => [Give(ToSummonNode(context, summon.Summon), DescribeCount(summon.Count))],
            strikeTarget: strike => [Give(ToStrikeNode(context, strike.Via), "")],
            modifyDamage: modify => [Give(ToEffectNode("damage-bonus", "Damage bonus"), DescribeDamageChange(context, modify.Against, modify.Change, modify.Pvp))],
            restoreHealth: heal => [Give(ToEffectNode("health", "Health", FindStatIcon(context, StatKind.Health)), DescribeHeal(heal))],
            resetCooldown: reset => [Give(ToEnergyNode(context, reset.Which), "full")],
            collect: collect => [new Effect(ToPickupNode(context, collect.Pickup), EdgeKind.Collects, "")],
            grantLeastChargedEnergy: least => [Give(ToEffectNode("energy-least-charged", "Least-charged ability energy"), DescribeEnergy(least.Amount))],
            spendStacks: spend => [new Effect(ToStatusNode(context, spend.Status), EdgeKind.Ends, $"spends {spend.Count.Value.ToString(CultureInfo.InvariantCulture)}")],
            weaponStrike: strike => [Give(ToEffectNode("extra-strike", "Extra strike"), strike.Hit == HitOutcome.Kill ? "kill" : "hit")],
            reloadWeapon: _ => [Give(ToEffectNode("reload", "Reload"), "")],
            refillWeapon: _ => [Give(ToEffectNode("refill", "Refill"), "")]);

    /// <summary>What a passive does while it holds: an effect it gives (resistance, a damage bonus, stats, a charge), or the status it boosts.</summary>
    internal static ImmutableArray<Effect> ListPassiveEffects(GraphContext context, Passive passive) =>
        passive.Match<ImmutableArray<Effect>>(
            extraStacks: extra => [new Effect(ToStatusNode(context, extra.Status), EdgeKind.Boosts, $"+{extra.Extra.Value.ToString(CultureInfo.InvariantCulture)} stack")],
            extraCharges: charges => [Give(
                ToEffectNode($"extra-charge-{ToKebab(charges.Ability.DescribeAbility())}", $"Extra {charges.Ability.DescribeAbility()} charge"),
                $"+{charges.Extra.Value.ToString(CultureInfo.InvariantCulture)}")],
            modifyDamage: modify => [Give(ToEffectNode("damage-bonus", "Damage bonus"), DescribeDamageChange(context, modify.Against, modify.Change, modify.Pvp))],
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
                $"{resist.Amount.ToPercentText()} vs {resist.Against.DescribeCombatantDamage(Optional.None<DamageType>())}")],
            fasterRegeneration: regeneration => [.. regeneration.Abilities.Select(ability => Give(
                ToEnergyNode(context, ability),
                regeneration.Rate.Pve.ToPercentText() + regeneration.Rate.Pvp.Map(pvp => $" [PvP {pvp.ToPercentText()}]").UnwrapOr("") + " faster regeneration"))]);

    private static Effect Give(GraphNode node, string label) => new(node, EdgeKind.Gives, label);

    /// <summary>A buff's grant as authored: its stacks when not one ("×2", "×1 | ×2 | ×3" by copies) and its duration ("8.5s").</summary>
    private static string DescribeBuffGrant(Outcome.ApplyBuff apply) =>
        string.Join(' ', new[] { DescribeStacks(apply.Stacks), DescribeDuration(apply.Duration) }.Where(part => part.Length > 0));

    private static string DescribeStacks(StackGrant stacks) =>
        stacks.Match(
            fixedCount => fixedCount.Count.Value == 1 ? "" : $"×{fixedCount.Count.Value.ToString(CultureInfo.InvariantCulture)}",
            perModCount => string.Join(" | ", perModCount.Counts.Select(count => $"×{count.Value.ToString(CultureInfo.InvariantCulture)}")));

    private static string DescribeDuration(BuffDuration duration) =>
        duration.Match(
            _ => "",
            fixedTime => fixedTime.Time.FormatSeconds(),
            bySpent => $"{string.Join(" | ", bySpent.Table.Values.Select(seconds => seconds.ToString("0.#", CultureInfo.InvariantCulture) + "s"))} by stacks spent");

    private static string DescribeCount(int count) =>
        count == 1 ? "" : $"×{count.ToString(CultureInfo.InvariantCulture)}";

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

    private static string DescribeDamageChange(GraphContext context, DamageSource against, GameValue change, Optional<GameValue> pvp) =>
        $"{FormatSigned(change, percent: true)}{pvp.Map(value => $" [PvP {value.ToPercentText()}]").UnwrapOr("")} vs {context.Catalog.Glossary.DescribeDamageSource(against, Optional.None<DamageType>())}";

    private static string DescribeHeal(Outcome.RestoreHealth heal) =>
        heal.Amount.Match(share => share.Value.ToPercentText(), points => $"{points.Value.ToNumberText()} HP")
        + (heal.IncludesAllies ? " + allies" : "");

    /// <summary>"+20", "-10", "+12% | +17% | +20%", "?": a change as the game shows it, each copy's figure signed.</summary>
    private static string FormatSigned(GameValue value, bool percent = false)
    {
        var text = percent ? value.ToPercentText() : value.ToNumberText();
        return string.Join(" | ", text.Split(" | ").Select(part => part.StartsWith('-') || part.StartsWith('?') ? part : "+" + part));
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
    internal static GraphNode ToElementNode(GraphContext context, BuildElement element) =>
        FindKeywordState(context, element).Match(
            state => new GraphNode(
                $"rule:{state.Value}", element.Name, new GraphNodeType.KeywordRule(), true, Optional.Some(ToFacts(element)), FindKeywordIcon(context, ToKeywordRef(context, state.Value))),
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
            element.RuleFile.Length == 0 ? Optional.None<string>() : Optional.Some($"{RulesFolder}/{element.RuleFile}"),
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
    internal static GraphNode ToStatusNode(GraphContext context, StatusId status) =>
        new(
            ToGlossaryNodeId(context, "status", status.Value),
            context.Catalog.Glossary.DescribeStatus(status),
            context.Catalog.Glossary.Statuses.TryGetValue(status, out var definition) ? ToStatusType(definition.Kind) : new GraphNodeType.BuffStatus(),
            HasKeywordRules(context, status.Value),
            Optional.None<ElementFacts>(),
            FindKeywordIcon(context, new KeywordRef.Status(status)));

    private static GraphNode ToPickupNode(GraphContext context, PickupId pickup) =>
        new(
            ToGlossaryNodeId(context, "pickup", pickup.Value),
            context.Catalog.Glossary.DescribePickup(pickup),
            new GraphNodeType.PickupDrop(),
            HasKeywordRules(context, pickup.Value),
            Optional.None<ElementFacts>(),
            FindKeywordIcon(context, new KeywordRef.Pickup(pickup)));

    private static GraphNode ToSummonNode(GraphContext context, SummonId summon) =>
        new(
            ToGlossaryNodeId(context, "summon", summon.Value),
            context.Catalog.Glossary.DescribeSummon(summon),
            new GraphNodeType.SummonedAlly(),
            HasKeywordRules(context, summon.Value),
            Optional.None<ElementFacts>(),
            FindKeywordIcon(context, new KeywordRef.Summon(summon)));

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
            FindKeywordIcon(context, new KeywordRef.Status(via)));

    /// <summary>
    /// An ability trigger, with the icon of the ability's armor stat; the class ability's is the icon of the subclass's
    /// first class ability in the rules, by id (a dodge, a barricade or a rift), else the Class stat's.
    /// </summary>
    internal static GraphNode ToTriggerNode(GraphContext context, AbilityKind kind, AbilityInput input) =>
        ToTriggerNode(
            NameAbilityTrigger(kind, input),
            kind == AbilityKind.ClassAbility ? new[] { FindClassAbilityIcon(context), FindStatIcon(context, StatKind.Class) }.FindFirstSome() : FindStatIcon(context, ToStat(kind)));

    /// <summary>
    /// The class's own word for a class ability trigger: a Hunter's dodge, a Titan's barricade, a Warlock's rift ("Air
    /// dodge" in the air); none for any other node.
    /// </summary>
    internal static Optional<string> FindClassMove(GraphContext context, GraphNode node) =>
        context.Class.Bind(guardianClass => FindClassMove(guardianClass, node));

    private static Optional<string> FindClassMove(GuardianClass guardianClass, GraphNode node)
    {
        var move = guardianClass switch
        {
            GuardianClass.Hunter => "dodge",
            GuardianClass.Titan => "barricade",
            _ => "rift",
        };
        return node.Id == ToTriggerNodeId(NameAbilityTrigger(AbilityKind.ClassAbility, AbilityInput.Press))
            ? Optional.Some(ToSentenceStart(move))
            : node.Id == ToTriggerNodeId(NameAbilityTrigger(AbilityKind.ClassAbility, AbilityInput.Air))
                ? Optional.Some($"Air {move}")
                : Optional.None<string>();
    }

    /// <summary>True when the element is the context's class's, or any class's; every element fits a context of no class.</summary>
    private static bool FitsClass(GraphContext context, BuildElement element) =>
        context.Class.Match(guardianClass => element.FitsClass(guardianClass.Value), _ => true);

    private static Optional<string> FindClassAbilityIcon(GraphContext context) =>
        context.Catalog.Elements.Values
            .Where(element => element.Kind == ElementKind.ClassAbility && context.Class.Match(guardianClass => element.Fits(guardianClass.Value, context.Subclass), _ => false))
            .OrderBy(element => element.Id.Value, StringComparer.Ordinal)
            .Select(element => FindElementIcon(context, element.Id))
            .FindFirstSome();

    private static GraphNode ToTriggerNode(string name, Optional<string> icon) =>
        new(ToTriggerNodeId(name), name, new GraphNodeType.ActionTrigger(), false, Optional.None<ElementFacts>(), icon);

    private static string ToTriggerNodeId(string name) => $"trigger:{ToKebab(name)}";

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

    internal static Optional<string> FindStatIcon(GraphContext context, StatKind stat) =>
        context.Catalog.Manifest.FindArmorStat(stat).Map(found => ToIconUrl(found.Icon));

    private static Optional<string> FindDamageTypeIcon(GraphContext context, DamageType type) =>
        context.Catalog.Manifest.DamageTypeIcons.TryGetValue(type, out var icon) ? Optional.Some(ToIconUrl(icon)) : Optional.None<string>();

    /// <summary>An element's icon: its manifest item's.</summary>
    private static Optional<string> FindElementIcon(GraphContext context, ElementId element) =>
        context.Catalog.FindElementItem(element).Bind(item => item.Icon).Map(ToIconUrl);

    /// <summary>
    /// A keyword's icon as the web app finds it (Causality's <c>FindKeywordIcon</c>): its trait's, else the item of the
    /// element of its id (Orb of Power's, the Lethal Current aspect's for its buff and its strike); a shape the game draws
    /// has no address, so none.
    /// </summary>
    private static Optional<string> FindKeywordIcon(GraphContext context, KeywordRef keyword) =>
        context.Catalog.FindKeywordIcon(keyword).Bind(icon => icon is IconRef.Manifest manifest ? Optional.Some(ToIconUrl(manifest.Path)) : Optional.None<string>());

    /// <summary>The glossary keyword of an id: its status, else its pickup, else its summon (a status when the glossary has none).</summary>
    private static KeywordRef ToKeywordRef(GraphContext context, string id) =>
        context.Catalog.Glossary.Pickups.ContainsKey(PickupId.From(id)) && !context.Catalog.Glossary.Statuses.ContainsKey(StatusId.From(id))
            ? new KeywordRef.Pickup(PickupId.From(id))
            : context.Catalog.Glossary.Summons.ContainsKey(SummonId.From(id)) && !context.Catalog.Glossary.Statuses.ContainsKey(StatusId.From(id))
                ? new KeywordRef.Summon(SummonId.From(id))
                : new KeywordRef.Status(StatusId.From(id));

    /// <summary>A manifest icon path as the address the web app shows it at.</summary>
    private static string ToIconUrl(string path) => IconHost + path;

    /// <summary>An ability use as the player says it; the class ability is named the same for every class (its own word is <see cref="FindClassMove"/>).</summary>
    private static string NameAbilityTrigger(AbilityKind kind, AbilityInput input) =>
        (kind, input) switch
        {
            (AbilityKind.Grenade, AbilityInput.Press) => "Grenade",
            (AbilityKind.Grenade, AbilityInput.Hold) => "Held grenade",
            (AbilityKind.Melee, AbilityInput.Press) => "Melee",
            (AbilityKind.Melee, AbilityInput.Slide) => "Sliding melee",
            (AbilityKind.Melee, AbilityInput.Air) => "Air melee",
            (AbilityKind.ClassAbility, AbilityInput.Press) => "Class ability",
            (AbilityKind.ClassAbility, AbilityInput.Air) => "Air class ability",
            (AbilityKind.Super, AbilityInput.Press) => "Super",
            (AbilityKind.Super, AbilityInput.Air) => "Airborne Super heavy",
            (AbilityKind.Super, AbilityInput.Combo) => "Super heavy combo",
            (AbilityKind.Super, AbilityInput.Guard) => "Super guard",
            _ => $"{kind} ({input.ToString().ToLowerInvariant()})",
        };

    private static string ToKebab(string name) =>
        string.Join('-', name.ToLowerInvariant().Replace("'", "", StringComparison.Ordinal).Replace("(", "", StringComparison.Ordinal)
            .Replace(")", "", StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

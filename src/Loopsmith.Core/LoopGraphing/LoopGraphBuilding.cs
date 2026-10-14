using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.LoopGraphing;

/// <summary>
/// Builds the static cause → effect graph of a build: nodes are triggers, ability energy and player
/// actions; an edge exists when an outcome can produce an event that fires another rule. A grant of a
/// stacking buff leads to "Gain X" only; reaching its max is the player's call, one declared edge
/// "Gain X → Max X" (ADRs D3).
/// </summary>
public static class LoopGraphBuilding
{
    private const string You = "you";
    private const string YouDeclare = "you declare";

    private sealed record RawEdge(string From, string To, string Source, EdgeKind Kind);

    /// <summary>A rule's edge, with the element it belongs to and the elements that rule gives way to.</summary>
    private sealed record RuleEdge(RawEdge Edge, ElementId Element, ImmutableArray<ElementId> DoesNotStackWith);

    public static LoopGraph BuildLoopGraph(ValidatedBuild build)
    {
        var glossary = build.Catalog.Glossary;
        var superType = build.ReadSuperType();
        string ToKey(Trigger trigger) => ToTriggerKey(glossary, trigger, superType);
        var rules = build.Equipped
            .SelectMany(e => e.Element.Rules.Select(rule => (e.Element, Rule: rule, Key: ToKey(rule.On))))
            .ToImmutableArray();
        var maxedStatuses = rules
            .SelectMany(r => ListMaxedStatuses(r.Rule))
            .Where(glossary.IsStacking)
            .Distinct()
            .ToImmutableArray();
        // A buff some rule reacts to at its max is gained, then declared at max: both are nodes, even with no rule on the gain.
        var triggers = rules
            .Select(r => r.Rule.On)
            .Concat(maxedStatuses.SelectMany(ListDeclaredMaxTriggers))
            .Select(on => (Key: ToKey(on), On: on))
            .DistinctBy(t => t.Key)
            .ToImmutableArray();
        var triggerNodes = triggers
            .Select(t => new GraphNode(t.Key, glossary.DescribeTrigger(t.On, superType), NodeKind.Trigger, ReadTriggerAffinity(build, t.On)))
            .ToImmutableArray();
        var appliedDebuffs = rules
            .SelectMany(r => r.Rule.Then.OfType<Outcome.DebuffTarget>().Select(d => d.Status))
            .Distinct()
            .ToImmutableArray();
        var subclass = build.Build.Subclass.ToDamageType();
        var subclassAffinity = build.Build.Subclass.ToAffinity();

        var abilityKinds = new[] { AbilityKind.Grenade, AbilityKind.Melee, AbilityKind.ClassAbility, AbilityKind.Super };
        var energyNodes = abilityKinds.Select(k => new GraphNode(ToEnergyKey(k), $"{DomainPhrasing.Capitalize(k.DescribeAbility())} energy", NodeKind.Energy, subclassAffinity));
        var castNodes = abilityKinds.Select(k => new GraphNode(ToCastKey(k), DescribeCastAction(k), NodeKind.Action, subclassAffinity));
        var weaponNodes = build.Build.Weapons.Select(w => new GraphNode(ToWeaponKey(w.Slot), $"Shoot {w.Name}", NodeKind.Action, w.Type.ToAffinity()));

        // Which trigger nodes does a concrete event reach?
        IEnumerable<string> Reach(GameEvent gameEvent) =>
            triggers.Where(t => t.On.IsTriggeredBy(gameEvent, superType)).Select(t => t.Key).Distinct();

        // A finisher is always possible (the designer's palette offers it in every build); the graph draws it where it
        // sets something off: a rule reacting to a finisher, or a damage or kill rule from any source.
        var finisherReach = ListFinisherEvents(appliedDebuffs).SelectMany(Reach).Distinct().ToImmutableArray();
        GraphNode[] finisherNodes = finisherReach.IsEmpty ? [] : [new GraphNode(FinisherKey, DomainPhrasing.Finisher, NodeKind.Action, Affinity.Neutral)];

        var playerEdges = abilityKinds
            .SelectMany(kind =>
            {
                var castEvents = ListCastEvents(kind, subclass, appliedDebuffs);
                return castEvents.SelectMany(Reach).Select(to => new RawEdge(ToCastKey(kind), to, You, EdgeKind.Player))
                    .Prepend(new RawEdge(ToEnergyKey(kind), ToCastKey(kind), You, EdgeKind.Player));
            })
            .Concat(build.Build.Weapons.SelectMany(w =>
                ListHitEvents(new DamageOrigin.Weapon(w.Slot, w.Type), appliedDebuffs)
                    .Concat(ListVolleyEvents(new DamageOrigin.Weapon(w.Slot, w.Type)))
                    .Append(new GameEvent.ShieldBroken(new DamageOrigin.Weapon(w.Slot, w.Type)))   // the player may say the step breaks a shield (ADRs D36)
                    .SelectMany(Reach)
                    .Select(to => new RawEdge(ToWeaponKey(w.Slot), to, You, EdgeKind.Player))))
            .Concat(finisherReach.Select(to => new RawEdge(FinisherKey, to, You, EdgeKind.Player)));

        var ruleEdges = rules
            .SelectMany(r => r.Rule.Then.SelectMany(outcome =>
                ListOutcomeEdges(build, r.Key, DescribeEdgeSource(glossary, r.Element, r.Rule), outcome, appliedDebuffs, Reach, triggers)
                    .Select(edge => new RuleEdge(edge, r.Element.Id, r.Rule.DoesNotStackWith))))
            .ToImmutableArray();
        var triggeredElements = rules.Select(r => (r.Key, r.Element.Id)).ToImmutableHashSet();
        var routed = RouteThroughStackingFilters(ruleEdges, triggeredElements);

        // Reaching the max is the player's call (ADRs D3): one declared link per buff, never a grant's.
        var declaredEdges = maxedStatuses.Select(status => new RawEdge(
            ToKey(new Trigger.BuffGained(status)), ToKey(new Trigger.StacksMaxed(status)), YouDeclare, EdgeKind.Declared));
        // Reaching critical health is the player's call too (ADRs D3): drawn where a rule reacts to it.
        var criticalReach = Reach(new GameEvent.CriticalHealth()).ToImmutableArray();
        GraphNode[] criticalNodes = criticalReach.IsEmpty ? [] : [new GraphNode(CriticalHealthKey, DomainPhrasing.CriticalHealth, NodeKind.Action, Affinity.Neutral)];
        var criticalEdges = criticalReach.Select(to => new RawEdge(CriticalHealthKey, to, YouDeclare, EdgeKind.Declared));
        // A rule guarded "at max" leads from its own trigger ("while Bolt Charge at max"); the declared max is what it needs.
        var guardEdges = rules.SelectMany(r => r.Rule.When
            .OfType<Condition.AtMax>()
            .Where(atMax => glossary.IsStacking(atMax.Status))
            .Select(atMax => new RawEdge(ToKey(new Trigger.StacksMaxed(atMax.Status)), r.Key, r.Element.Name, EdgeKind.Enables)));

        var edges = playerEdges.Concat(routed.Edges).Concat(declaredEdges).Concat(criticalEdges).Concat(guardEdges)
            .GroupBy(e => (e.From, e.To, e.Kind))
            .Select(g => new GraphEdge(g.Key.From, g.Key.To, g.Select(e => e.Source).Distinct().ToImmutableArray(), g.Key.Kind))
            .ToImmutableArray();
        var used = edges.SelectMany(e => new[] { e.From, e.To }).ToImmutableHashSet();
        var nodes = triggerNodes.Concat(energyNodes).Concat(castNodes).Concat(weaponNodes).Concat(finisherNodes).Concat(criticalNodes).Concat(routed.Filters)
            .Where(n => used.Contains(n.Key))
            .ToImmutableArray();
        return new LoopGraph(nodes, edges);
    }

    private sealed record RoutedEdges(ImmutableArray<RawEdge> Edges, ImmutableArray<GraphNode> Filters);

    /// <summary>
    /// A rule that doesn't stack (<see cref="Rule.DoesNotStackWith"/>) keeps its arrows, but where it leads from a trigger
    /// that also fires a rule it gives way to, its arrow goes into a "doesn't stack" node instead of the target, and so
    /// does the partner's arrow to that target; only the partner's arrow leaves the node — one of them applies. Rules of
    /// other elements on the same trigger and target keep their direct arrow.
    /// </summary>
    private static RoutedEdges RouteThroughStackingFilters(
        ImmutableArray<RuleEdge> ruleEdges, ImmutableHashSet<(string Key, ElementId Element)> triggeredElements)
    {
        var yielding = ruleEdges
            .Where(y => y.Edge.Kind == EdgeKind.Rule && y.DoesNotStackWith.Any(partner => triggeredElements.Contains((y.Edge.From, partner))))
            .ToImmutableArray();
        var filtered = yielding
            .GroupBy(y => (y.Edge.From, y.Edge.To))
            .ToImmutableDictionary(
                group => group.Key,
                group => (Yielders: group.Select(y => y.Element).ToImmutableHashSet(), Partners: group.SelectMany(y => y.DoesNotStackWith).ToImmutableHashSet()));

        IEnumerable<RawEdge> Route(RuleEdge rule)
        {
            var pair = (rule.Edge.From, rule.Edge.To);
            if (rule.Edge.Kind != EdgeKind.Rule || !filtered.TryGetValue(pair, out var filter))
            {
                return [rule.Edge];
            }

            var key = ToFilterKey(pair.From, pair.To);
            return (filter.Yielders.Contains(rule.Element), filter.Partners.Contains(rule.Element)) switch
            {
                (true, _) => [rule.Edge with { To = key }],
                (_, true) => [rule.Edge with { To = key }, rule.Edge with { From = key }],
                _ => [rule.Edge],
            };
        }

        var filters = filtered.Keys
            .OrderBy(pair => ToFilterKey(pair.From, pair.To), StringComparer.Ordinal)
            .Select(pair => new GraphNode(ToFilterKey(pair.From, pair.To), "Doesn't stack", NodeKind.Filter, Affinity.Neutral))
            .ToImmutableArray();
        return new RoutedEdges([.. ruleEdges.SelectMany(Route)], filters);
    }

    private static IEnumerable<RawEdge> ListOutcomeEdges(
        ValidatedBuild build,
        string from,
        string source,
        Outcome outcome,
        ImmutableArray<StatusId> appliedDebuffs,
        Func<GameEvent, IEnumerable<string>> reach,
        IEnumerable<(string Key, Trigger On)> triggers)
    {
        var glossary = build.Catalog.Glossary;
        IEnumerable<RawEdge> LinkToTriggers(IEnumerable<GameEvent> events) =>
            events.SelectMany(reach).Distinct().Select(to => new RawEdge(from, to, source, EdgeKind.Rule));
        RawEdge[] LinkToEnergy(AbilityKind kind) => [new RawEdge(from, ToEnergyKey(kind), source, EdgeKind.Rule)];

        return outcome.Match(
            grant => LinkToEnergy(grant.To),
            convert => LinkToEnergy(convert.To),
            apply => LinkToTriggers([new GameEvent.BuffGained(apply.Status)]),   // a grant is a gain, never the max (ADRs D3)
            _ => [],
            debuff => triggers
                .Where(t => t.On.ListRequiredTargetStatuses().Contains(debuff.Status))
                .Select(t => new RawEdge(from, t.Key, source, EdgeKind.Enables)),
            spawn => LinkToTriggers([new GameEvent.PickedUp(spawn.Pickup)]),
            summon => LinkToTriggers(ListHitEvents(
                new DamageOrigin.Summoned(summon.Summon, glossary.ResolveSummonDamageType(summon.Summon)),
                appliedDebuffs)),
            strike => LinkToTriggers(ListHitEvents(
                new DamageOrigin.Keyword(strike.Via, glossary.ResolveStrikeDamageType(strike.Via)),
                appliedDebuffs,
                strike.Hit == HitOutcome.Kill)),
            _ => [],
            _ => [],
            reset => LinkToEnergy(reset.Which),
            collect => LinkToTriggers([new GameEvent.PickedUp(collect.Pickup)]),   // what is on the ground, picked up
            _ => [],   // the least-charged ability: the game picks it, so it leads to no one ability's energy
            _ => []);   // spending stacks sets nothing off
    }

    private static string DescribeCastAction(AbilityKind kind) =>
        kind switch
        {
            AbilityKind.Grenade => "Throw grenade",
            AbilityKind.Melee => "Melee",
            AbilityKind.ClassAbility => "Use class ability",
            _ => "Cast super",
        };

    /// <summary>"Grenade Kickstart (while Armor Charge)" — a conditional link must not look unconditional.</summary>
    private static string DescribeEdgeSource(KeywordGlossary glossary, BuildElement element, Rule rule) =>
        element.Name
        + (rule.When.IsEmpty ? "" : $" ({glossary.DescribeConditions(rule.When)})")
        + (rule.Likelihood == Likelihood.Chance ? " (chance)" : "");

    /// <summary>The buffs a rule reacts to at their max: its <c>stacksMaxed</c> trigger and its <c>atMax</c> guards.</summary>
    private static IEnumerable<StatusId> ListMaxedStatuses(Rule rule)
    {
        if (rule.On is Trigger.StacksMaxed maxed)
        {
            yield return maxed.Status;
        }

        foreach (var atMax in rule.When.OfType<Condition.AtMax>())
        {
            yield return atMax.Status;
        }
    }

    /// <summary>"Gain X" and "Max X": the two ends of the link the player declares.</summary>
    private static IEnumerable<Trigger> ListDeclaredMaxTriggers(StatusId status)
    {
        yield return new Trigger.BuffGained(status);
        yield return new Trigger.StacksMaxed(status);
    }

    private static IEnumerable<GameEvent> ListCastEvents(AbilityKind kind, DamageType subclass, ImmutableArray<StatusId> debuffs)
    {
        // its last charge, which the player may spend (ADRs D36): a Super has no such trigger
        if (kind != AbilityKind.Super)
        {
            yield return new GameEvent.EnergyExpended(kind);
        }

        // every use of the ability: its press and each secondary (the air move reaches Ascension's rules, the sliding
        // melee Tempest Strike's), as the player may use any of them
        foreach (var input in kind.ListSecondaryInputs().Prepend(AbilityInput.Press))
        {
            yield return new GameEvent.AbilityCast(kind, input);
            if (kind == AbilityKind.ClassAbility)
            {
                continue;
            }

            var origin = new DamageOrigin.Ability(kind, subclass, input);
            foreach (var hit in ListHitEvents(origin, debuffs).Concat(ListVolleyEvents(origin)))
            {
                yield return hit;
            }
        }
    }

    /// <summary>The finisher, then its hit and kill: on a target that may carry any debuff some rule can apply, of every tier.</summary>
    private static IEnumerable<GameEvent> ListFinisherEvents(ImmutableArray<StatusId> debuffs) =>
        ListHitEvents(new DamageOrigin.Finisher(), debuffs).Prepend(new GameEvent.Finisher());

    /// <summary>
    /// Optimistic: a player action may hit (or kill) as many enemies as the player says, so it can reach every
    /// "hit / kill at least N in one action" trigger. Strikes and summons hit one enemy and never do.
    /// </summary>
    private static IEnumerable<GameEvent> ListVolleyEvents(DamageOrigin origin)
    {
        yield return new GameEvent.TargetsHit(origin, TargetCount.Most, HitOutcome.Damage);
        yield return new GameEvent.TargetsHit(origin, TargetCount.Most, HitOutcome.Kill);
    }

    /// <summary>
    /// Optimistic: the target may carry any debuff some rule can apply, and every tier; a weapon's or an ability's hits may
    /// be a hit step's sustained damage (ADRs D34), a strike's, a summon's or a finisher's are one blow.
    /// </summary>
    private static IEnumerable<GameEvent> ListHitEvents(DamageOrigin origin, ImmutableArray<StatusId> debuffs, bool kills = true)
    {
        var span = origin is DamageOrigin.Weapon or DamageOrigin.Ability ? DamageSpan.Sustained : DamageSpan.Blow;
        foreach (var tier in Enum.GetValues<EnemyTier>())
        {
            yield return new GameEvent.Damaged(origin, tier, debuffs, span);
            if (kills)
            {
                yield return new GameEvent.Killed(origin, tier, debuffs);
            }
        }
    }

    private static Affinity ReadTriggerAffinity(ValidatedBuild build, Trigger trigger)
    {
        var glossary = build.Catalog.Glossary;
        var status = trigger.ListRequiredTargetStatuses()
            .Select(Optional.Some)
            .Append(trigger switch
            {
                Trigger.BuffGained gained => Optional.Some(gained.Status),
                Trigger.StacksMaxed maxed => Optional.Some(maxed.Status),
                _ => Optional.None<StatusId>(),
            })
            .FindFirstSome();
        var pickup = trigger is Trigger.PickUp pickUp && glossary.Pickups.TryGetValue(pickUp.Pickup, out var definition)
            ? Optional.Some(definition.Affinity)
            : Optional.None<Affinity>();
        return status
            .Map(glossary.ReadStatusAffinity)
            .UnwrapOr(pickup.UnwrapOr(build.Build.Subclass.ToAffinity()));
    }

    private static string ToTriggerKey(KeywordGlossary glossary, Trigger trigger, Optional<DamageType> superType) =>
        "t:" + glossary.DescribeTrigger(trigger, superType);

    private const string FinisherKey = "a:Finisher";

    private const string CriticalHealthKey = "a:CriticalHealth";

    private static string ToEnergyKey(AbilityKind kind) => $"e:{kind}";

    private static string ToCastKey(AbilityKind kind) => $"a:{kind}";

    private static string ToWeaponKey(WeaponSlot slot) => $"w:{slot}";

    private static string ToFilterKey(string from, string to) => $"f:{from}>{to}";
}

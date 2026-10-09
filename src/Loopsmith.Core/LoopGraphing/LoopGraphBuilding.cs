using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.LoopGraphing;

/// <summary>
/// Builds the static cause → effect graph of a build: nodes are triggers, ability energy and player
/// actions; an edge exists when an outcome can produce an event that fires another rule.
/// </summary>
public static class LoopGraphBuilding
{
    private const string You = "you";

    private sealed record RawEdge(string From, string To, string Source, EdgeKind Kind);

    /// <summary>A rule's edge, with the element it belongs to and the elements that rule gives way to.</summary>
    private sealed record RuleEdge(RawEdge Edge, ElementId Element, ImmutableArray<ElementId> DoesNotStackWith);

    public static LoopGraph BuildLoopGraph(ValidatedBuild build)
    {
        var glossary = build.Catalog.Glossary;
        var rules = build.Equipped
            .SelectMany(e => e.Element.Rules.Select(rule => (e.Element, Rule: rule, Key: ToTriggerKey(glossary, rule.On))))
            .ToImmutableArray();
        var triggerNodes = rules
            .DistinctBy(r => r.Key)
            .Select(r => new GraphNode(r.Key, glossary.DescribeTrigger(r.Rule.On), NodeKind.Trigger, ReadTriggerAffinity(build, r.Rule.On)))
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
            rules.Where(r => r.Rule.On.IsTriggeredBy(gameEvent)).Select(r => r.Key).Distinct();

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
                    .SelectMany(Reach)
                    .Select(to => new RawEdge(ToWeaponKey(w.Slot), to, You, EdgeKind.Player))));

        var ruleEdges = rules
            .SelectMany(r => r.Rule.Then.SelectMany(outcome =>
                ListOutcomeEdges(build, r.Key, DescribeEdgeSource(glossary, r.Element, r.Rule), outcome, appliedDebuffs, Reach, rules.Select(x => (x.Key, x.Rule.On)))
                    .Select(edge => new RuleEdge(edge, r.Element.Id, r.Rule.DoesNotStackWith))))
            .ToImmutableArray();
        var triggeredElements = rules.Select(r => (r.Key, r.Element.Id)).ToImmutableHashSet();
        var routed = RouteThroughStackingFilters(ruleEdges, triggeredElements);

        var edges = playerEdges.Concat(routed.Edges)
            .GroupBy(e => (e.From, e.To, e.Kind))
            .Select(g => new GraphEdge(g.Key.From, g.Key.To, g.Select(e => e.Source).Distinct().ToImmutableArray(), g.Key.Kind))
            .ToImmutableArray();
        var used = edges.SelectMany(e => new[] { e.From, e.To }).ToImmutableHashSet();
        var nodes = triggerNodes.Concat(energyNodes).Concat(castNodes).Concat(weaponNodes).Concat(routed.Filters)
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
            apply => LinkToTriggers(ListBuffEvents(glossary, apply.Status)),
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
            reset => LinkToEnergy(reset.Which));
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

    private static IEnumerable<GameEvent> ListBuffEvents(KeywordGlossary glossary, StatusId status)
    {
        yield return new GameEvent.BuffGained(status, StackCount.From(1));
        if (glossary.CanReachMaxStacks(status))
        {
            yield return new GameEvent.StacksMaxed(status);
        }
    }

    private static IEnumerable<GameEvent> ListCastEvents(AbilityKind kind, DamageType subclass, ImmutableArray<StatusId> debuffs)
    {
        yield return new GameEvent.AbilityCast(kind);
        if (kind == AbilityKind.ClassAbility)
        {
            yield return new GameEvent.AbilityCast(kind, Airborne: true);   // reaches airborne-only rules (Ascension)
            yield break;
        }

        var origin = new DamageOrigin.Ability(kind, subclass);
        foreach (var hit in ListHitEvents(origin, debuffs).Concat(ListVolleyEvents(origin)))
        {
            yield return hit;
        }
    }

    /// <summary>
    /// Optimistic: a player action may hit (or kill) as many enemies as the player says, so it can reach every
    /// "hit / kill at least N in one action" trigger. Strikes and summons hit one enemy and never do.
    /// </summary>
    private static IEnumerable<GameEvent> ListVolleyEvents(DamageOrigin origin)
    {
        yield return new GameEvent.TargetsHit(origin, TargetCount.Most, HitOutcome.Damage);
        yield return new GameEvent.TargetsHit(origin, TargetCount.Most, HitOutcome.Kill);
    }

    /// <summary>Optimistic: the target may carry any debuff some rule can apply, and every tier.</summary>
    private static IEnumerable<GameEvent> ListHitEvents(DamageOrigin origin, ImmutableArray<StatusId> debuffs, bool kills = true)
    {
        foreach (var tier in Enum.GetValues<EnemyTier>())
        {
            yield return new GameEvent.Damaged(origin, tier, debuffs);
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

    private static string ToTriggerKey(KeywordGlossary glossary, Trigger trigger) => "t:" + glossary.DescribeTrigger(trigger);

    private static string ToEnergyKey(AbilityKind kind) => $"e:{kind}";

    private static string ToCastKey(AbilityKind kind) => $"a:{kind}";

    private static string ToWeaponKey(WeaponSlot slot) => $"w:{slot}";

    private static string ToFilterKey(string from, string to) => $"f:{from}>{to}";
}

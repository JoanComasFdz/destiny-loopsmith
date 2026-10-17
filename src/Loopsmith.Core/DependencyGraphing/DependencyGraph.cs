using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.DependencyGraphing;

/// <summary>
/// What a node of the dependency graph is. A player action (<see cref="ActionTrigger"/>: a grenade, a dodge, a
/// finisher) or the equipment a trigger names (<see cref="NeededWeapon"/>: an Arc weapon) needs nothing; the
/// subclass's parts, the outside elements and the keywords fire on what they need; the statuses, pickups, summons and
/// strikes are what the elements give.
/// </summary>
[Union]
public partial record GraphNodeType
{
    partial record ActionTrigger();
    partial record NeededWeapon();
    partial record SubclassAbility(AbilityKind Slot);   // grenade, melee or class ability
    partial record SubclassSuper();
    partial record SubclassAspect();
    partial record SubclassFragment();
    partial record BuffStatus();                        // on you or your weapons
    partial record DebuffStatus();                      // on the target
    partial record PickupDrop();
    partial record SummonedAlly();
    partial record UnlistedKeyword();                   // a keyword element the glossary has no status, pickup or summon for (Guardian)
    partial record KeywordStrike();                     // the damage a keyword deals (Bolt Charge's lightning strike)
    partial record OutsideElement(ElementKind Kind);    // a mod, perk, exotic, artifact perk, catalyst or armor set bonus
}

/// <summary>An element's facts the graph shows: its rule file, its source's citation, its description and its rules' reasons.</summary>
public sealed record ElementFacts(string File, Optional<string> Source, Optional<string> Description, ImmutableArray<string> Reasons);

/// <summary>
/// A node: an element (<see cref="Element"/> holds its facts), a status, pickup, summon, strike, trigger or weapon. A keyword
/// element whose id is a glossary status, pickup or summon is that one node (<see cref="IsKeyword"/>).
/// </summary>
public sealed record GraphNode(string Id, string Name, GraphNodeType Type, bool IsKeyword, Optional<ElementFacts> Element);

public enum EdgeKind { Gives, Needs }

/// <summary>
/// "<see cref="From"/> gives <see cref="To"/>" (an element → the status, pickup, summon or strike it gives) or
/// "<see cref="To"/> needs <see cref="From"/>" (what is needed → the element), with a short <see cref="Label"/>
/// ("kill on Jolted", "while", "cast").
/// </summary>
public sealed record GraphEdge(string From, string To, EdgeKind Kind, string Label);

public enum ScopeKind { Subclass, WithNeeds, WithDependents }

/// <summary>
/// One view of the graph: its nodes, the edges between them and each node's column, left to right in the order to test
/// them (none: it needs something outside this scope).
/// </summary>
public sealed record GraphScope(
    ScopeKind Kind,
    string Name,
    ImmutableArray<string> Nodes,
    ImmutableArray<GraphEdge> Edges,
    ImmutableDictionary<string, Optional<int>> Columns);

/// <summary>The dependency graph of a class's subclass ("Arc Hunter"): every node any scope holds, and the three scopes.</summary>
public sealed record DependencyGraph(string Title, ImmutableSortedDictionary<string, GraphNode> Nodes, ImmutableArray<GraphScope> Scopes);

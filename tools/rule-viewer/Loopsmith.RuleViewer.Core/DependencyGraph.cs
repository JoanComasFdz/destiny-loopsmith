using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;

namespace Loopsmith.RuleViewer.Core;

/// <summary>
/// What a node of the dependency graph is. A player action (<see cref="ActionTrigger"/>: a grenade, a dodge, a
/// finisher) or the equipment a trigger names (<see cref="NeededWeapon"/>: an Arc weapon) needs nothing; the
/// subclass's parts, the outside elements and the keywords fire on what they need; the statuses, pickups, summons and
/// strikes are what the elements give, and the effects what they change without the graph counting it.
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
    partial record KeywordRule();                       // a keyword's rules (rule:jolt), apart from its state (jolt)
    partial record ShownEffect();                       // what Loopsmith shows but doesn't count: energy, health, damage, resistance…
    partial record KeywordStrike();                     // the damage a keyword deals (Bolt Charge's lightning strike)
    partial record OutsideElement(ElementKind Kind);    // a mod, perk, exotic, artifact perk, catalyst or armor set bonus
}

/// <summary>An element's facts: its rule file (none for an element no file holds), its source's citation, its description and its rules' reasons.</summary>
public sealed record ElementFacts(Optional<string> File, Optional<string> Source, Optional<string> Description, ImmutableArray<string> Reasons);

/// <summary>
/// A node: an element (<see cref="Element"/> holds its facts), a status, pickup, summon, strike, effect, trigger or weapon. A
/// keyword element whose id is a glossary status, pickup or summon is two nodes: its state (that id, <see cref="IsKeyword"/>,
/// no facts) and its rules (<c>rule:&lt;id&gt;</c>, with the facts). <see cref="Icon"/>: the address of the icon the web app
/// would show for it (a bungie.net URL); none when there is none.
/// </summary>
public sealed record GraphNode(string Id, string Name, GraphNodeType Type, bool IsKeyword, Optional<ElementFacts> Element, Optional<string> Icon);

/// <summary>
/// <see cref="Gives"/>: an element → the status, pickup, summon, strike or effect it gives. <see cref="Needs"/>: what is needed → the element. <see cref="Ends"/>: an element → the status it removes or
/// spends. <see cref="Collects"/>: an element → the pickup it picks up from the ground. <see cref="Boosts"/>: an element →
/// the status it gives more stacks of, or a higher cap.
/// </summary>
public enum EdgeKind { Gives, Needs, Ends, Collects, Boosts }

/// <summary>An edge from <see cref="From"/> to <see cref="To"/> of that kind, with a short <see cref="Label"/> ("kill on Jolted", "while", "12%", "×2").</summary>
public sealed record GraphEdge(string From, string To, EdgeKind Kind, string Label);

public enum ScopeKind { Subclass, WithNeeds, WithDependents }

/// <summary>One view of the graph: its nodes and the edges between them.</summary>
public sealed record GraphScope(ScopeKind Kind, string Name, ImmutableArray<string> Nodes, ImmutableArray<GraphEdge> Edges);

/// <summary>The dependency graph of a class's subclass ("Arc Hunter"): every node any scope holds, and the three scopes.</summary>
public sealed record DependencyGraph(string Title, ImmutableSortedDictionary<string, GraphNode> Nodes, ImmutableArray<GraphScope> Scopes);

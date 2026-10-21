using System.Collections.Immutable;
using Dunet;
using Loopsmith.Functional;

namespace Loopsmith.RuleViewer.Core;

/// <summary>
/// What a flow box shows as its icon: a picture at its address; a buff's picture that isn't drawn as one (its element's,
/// or an ability's stat icon), clipped to a diamond with a buff's border; an emoji for what the manifest has no icon of
/// (Orb of Power, Armor Charge); or nothing.
/// </summary>
[Union]
public partial record RuleIcon
{
    partial record Picture(string Url);
    partial record MaskedBuff(string Url);
    partial record Emoji(string Text);
    partial record Blank();
}

/// <summary>The colour family of a box: what it is (a trigger, a subclass part, a buff, an element from outside the subclass…).</summary>
public enum BoxTone { Trigger, Weapon, Ability, Super, Aspect, Fragment, Buff, Debuff, Pickup, Strike, Summon, Keyword, Effect, Outside }

/// <summary>How a box relates to its flow: a trigger, a condition, or a result that gives, ends, collects or adds to it.</summary>
public enum BoxRole { Trigger, Condition, Gives, Ends, Collects, Boosts }

/// <summary>
/// A box of a flow: the node it stands for (none for a trigger the graph has no node for: "Any kill"), its kind title
/// ("Subclass aspect", "Buff"), its name, a short caption ("equipped", "on the target", "×2", "ends · spends 3"), its
/// colour family, its icon and its role.
/// </summary>
public sealed record RuleBox(Optional<string> Node, string Title, string Name, string Caption, BoxTone Tone, RuleIcon Icon, BoxRole Role);

/// <summary>
/// One claim of the rules, read left to right: <see cref="Trigger"/> sets it off, every <see cref="Conditions"/> box
/// must be in place (the element itself, "equipped", unless it is a keyword every build has; then each state it needs),
/// and it gives every <see cref="Results"/> box, in the order strikes, other effects, buffs, debuffs, pickups, summons,
/// then what it ends, collects or adds to. <see cref="Id"/>: the element's node, the rule's place (<c>#0</c>, a
/// passive's <c>#p0</c>) and, for a rule split by its trigger, the trigger's place (<c>.1</c>). <see cref="Rule"/>: the
/// rule's full name (<c>hunter-arc-melees-combination-blow-1</c>, ADRs D40), the same on every flow of a split rule.
/// <see cref="ByChance"/>: the rule only may fire (its <c>chance</c>).
/// </summary>
public sealed record RuleFlow(
    string Id,
    string Element,
    string Rule,
    RuleBox Trigger,
    ImmutableArray<RuleBox> Conditions,
    ImmutableArray<RuleBox> Results,
    bool ByChance,
    Optional<string> File,
    Optional<string> Reason);

/// <summary>The flows one trigger sets off ("Melee", "Bolt Charge", "Always on"), with its icon.</summary>
public sealed record RuleGroup(string Key, string Name, RuleIcon Icon, ImmutableArray<RuleFlow> Flows);

/// <summary>The flows of one scope of the graph (the subclass alone, with its needs, with what builds on it), grouped by trigger.</summary>
public sealed record RuleScope(ScopeKind Kind, string Name, ImmutableArray<RuleGroup> Groups);

/// <summary>Every flow of a class's subclass ("Arc Hunter"), per scope.</summary>
public sealed record SubclassRules(string Title, ImmutableArray<RuleScope> Scopes);

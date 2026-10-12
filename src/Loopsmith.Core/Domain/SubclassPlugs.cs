using System.Collections.Immutable;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>
/// What a subclass holds, by manifest hash — the rules' elements and the plugs they don't know alike: its super,
/// grenade, melee, class ability, jump (a jump has no part in a loop), aspects and fragments. The designer's subclass
/// editor reads a build's as these, and a saved edit writes them back.
/// </summary>
public sealed record SubclassPlugs(
    Optional<ItemHash> Super,
    Optional<ItemHash> Grenade,
    Optional<ItemHash> Melee,
    Optional<ItemHash> ClassAbility,
    Optional<ItemHash> Jump,
    ImmutableArray<ItemHash> Aspects,
    ImmutableArray<ItemHash> Fragments);

/// <summary>A place on a subclass that takes one kind of plug.</summary>
public enum SubclassSlot { Super, Grenade, Melee, ClassAbility, Jump, Aspect, Fragment }

/// <summary>
/// How many fragment slots the picked aspects open: a number when every aspect's is known, else not known (the editor
/// shows "?" and doesn't limit the fragments).
/// </summary>
public sealed record FragmentSlotCount(Optional<int> Slots);

/// <summary>A plug an aspect change took out because its fragment slot closed.</summary>
public sealed record SubclassTrim(SubclassPlugs Plugs, ImmutableArray<ItemHash> Removed);

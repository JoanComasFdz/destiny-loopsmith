using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>A step the designer chose, with an optional note ("arm Slice + Reaper").</summary>
public sealed record LoopStep(PlayerAction Action, Optional<string> Note);

/// <summary>
/// The product: a loop someone designed. Self-contained — it carries its build file's text — so it
/// can be shared, imported, replayed and compared anywhere (docs/loop-format.md).
/// </summary>
public sealed record LoopDesign(
    string Name,
    Optional<string> Author,
    Optional<string> Description,
    Optional<CatalogVersion> Catalog,
    SourceText Build,
    ImmutableArray<LoopStep> Steps);

/// <summary>
/// A number of enemies a step's ability or weapon can hit or kill (ADRs D4), with what it is for: the equipped elements
/// whose rules need that many in one action from that ability or weapon, and what that many fills up (Bolt Charge to its
/// cap, the grenade's energy); none of either for a plain count.
/// </summary>
public sealed record TargetCountChoice(TargetCount Count, ImmutableArray<CountNeed> NamedBy, ImmutableArray<CountFill> Fills);

/// <summary>What an action's enemies fill up at a count: a stacking buff reaches the build's cap, an ability a full charge.</summary>
[Union]
public partial record CountFill
{
    partial record StatusAtCap(StatusId Status, int Cap);
    partial record AbilityFull(AbilityKind Ability);
}

/// <summary>An element whose rule needs a count, and the window its source gives the kills (Arc Siphon: within 3 s), if any.</summary>
public sealed record CountNeed(string Name, Optional<Seconds> Within);

using System.Collections.Immutable;
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
/// whose rules need that many in one action from that ability or weapon (Arc Siphon: two kills, within 3 s), none for a
/// plain count.
/// </summary>
public sealed record TargetCountChoice(TargetCount Count, Optional<Seconds> Within, ImmutableArray<string> NamedBy);

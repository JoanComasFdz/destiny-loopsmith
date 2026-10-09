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

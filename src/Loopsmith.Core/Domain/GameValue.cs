using System.Collections.Immutable;
using Dunet;

namespace Loopsmith.Core.Domain;

/// <summary>A number from a source. A Compendium "?" is data (<see cref="Unknown"/>), never 0.</summary>
[Union]
public partial record GameValue
{
    partial record Known(decimal Value);
    partial record PerModCount(ImmutableArray<decimal> Values);   // "12% | 17% | 20%"
    partial record Approximate(decimal Value);                    // "~25%", "1%?"
    partial record Unknown();                                     // "?%"
}

/// <summary>"300% [20%]" — PvE value with an optional PvP value.</summary>
public sealed record PveAndPvp(GameValue Pve, Functional.Optional<GameValue> Pvp);

/// <summary>Where a number or rule came from — shown in the trace.</summary>
[Union]
public partial record Provenance
{
    partial record Compendium(SnapshotDate Snapshot, string Tab, int Row);
    partial record Clarity(ItemHash Hash, string ClarityVersion);
    partial record Authored(string RuleFile, int Line);
    partial record CreatorClaim(string SourceUrl, string Quote);   // a guide/video says so; unverified in game data
}

public sealed record Sourced<T>(T Value, Provenance Source);

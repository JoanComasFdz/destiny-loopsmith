using System.Collections.Immutable;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>Pure reads of the keyword glossary (and of a build's stack caps) that both kernel modules and the slices share.</summary>
public static class GlossaryReading
{
    /// <summary>
    /// A status stacks when the glossary gives it a <c>maxStacks</c> (at least 2): its grants read "+1 Bolt Charge" and the
    /// player can declare it at its maximum (ADRs D3). The cap is a fact, never counted toward.
    /// </summary>
    public static bool IsStacking(this KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition) && definition.MaxStacks.IsSome();

    /// <summary>The engine counts this status's stacks (<c>counted: true</c> in the glossary: Armor Charge, ADRs D33).</summary>
    public static bool IsCounted(this KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition) && definition.Counted;

    /// <summary>
    /// The most stacks of a status this build holds, a fact the player reaches and declares, never counted toward (ADRs D3):
    /// the cap an equipped <c>raiseMaxStacks</c> states for its copies (Charged Up: Armor Charge "4 | 5 | 6", so x4 with one
    /// copy), else the glossary's (x3). Two elements raising it: no source says how they combine, so it is unknown ("?").
    /// None when the status doesn't stack.
    /// </summary>
    public static Optional<GameValue> ReadStackCap(this ValidatedBuild build, StatusId status) =>
        build.Catalog.Glossary.Statuses.TryGetValue(status, out var definition)
            ? definition.MaxStacks.Map(cap => ListRaisedCaps(build, status) switch
            {
                [] => new GameValue.Known(cap.Value),
                [var raised] => raised,
                _ => new GameValue.Unknown(),
            })
            : Optional.None<GameValue>();

    /// <summary>The caps the equipped <c>raiseMaxStacks</c> of a status state, each for its element's copies.</summary>
    private static ImmutableArray<GameValue> ListRaisedCaps(ValidatedBuild build, StatusId status) =>
    [
        .. build.Equipped.SelectMany(equipped => equipped.Element.Passives
            .Select(passive => passive.Modifier)
            .OfType<Passive.RaiseMaxStacks>()
            .Where(raise => raise.Status == status)
            .Select(raise => raise.Max.NarrowToCopies(equipped.Count))),
    ];

    /// <summary>A buff on you or on your weapons: the engine keeps both on the player (only the state shows them apart).</summary>
    public static bool IsBuff(this KeywordKind kind) =>
        kind is KeywordKind.Buff or KeywordKind.WeaponBuff;
}

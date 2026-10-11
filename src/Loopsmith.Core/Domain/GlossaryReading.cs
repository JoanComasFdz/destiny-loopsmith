using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>Pure reads of the keyword glossary that both kernel modules and the slices share.</summary>
public static class GlossaryReading
{
    /// <summary>
    /// A status stacks when the glossary gives it a <c>maxStacks</c> (at least 2): its grants read "+1 Bolt Charge" and the
    /// player can declare it at its maximum (ADRs D3). The cap is a fact, never counted toward.
    /// </summary>
    public static bool IsStacking(this KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition) && definition.MaxStacks.IsSome();

    /// <summary>A buff on you or on your weapons: the engine keeps both on the player (only the state shows them apart).</summary>
    public static bool IsBuff(this KeywordKind kind) =>
        kind is KeywordKind.Buff or KeywordKind.WeaponBuff;
}

using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>A chip of the state panel: text in the CLI's wording, the affinity that colours it, and the source's facts (its tooltip).</summary>
public sealed record StateChip(string Text, Affinity Affinity, string Facts);

/// <summary>Pure: a <see cref="GameState"/> → what the state panel draws, phrased with the kernel's Phrasing.</summary>
public static class StateShaping
{
    public static ImmutableArray<StateChip> ShapeBuffs(KeywordGlossary glossary, ImmutableArray<ActiveBuff> buffs) =>
        [.. buffs.Select(buff => new StateChip(glossary.DescribeActiveBuff(buff), glossary.ReadStatusAffinity(buff.Status), glossary.DescribeStatusFacts(buff.Status)))];

    public static ImmutableArray<StateChip> ShapeDebuffs(KeywordGlossary glossary, ImmutableArray<StatusId> debuffs) =>
        [.. debuffs.Select(debuff => new StateChip(glossary.DescribeStatus(debuff), glossary.ReadStatusAffinity(debuff), glossary.DescribeStatusFacts(debuff)))];

    public static ImmutableArray<StateChip> ShapePickups(KeywordGlossary glossary, ImmutableArray<PickupId> pickups) =>
    [
        .. pickups.Select(pickup => new StateChip(
            glossary.DescribePickup(pickup),
            glossary.Pickups.TryGetValue(pickup, out var definition) ? definition.Affinity : Affinity.Neutral,
            "")),
    ];

    /// <summary>Passives whose conditions hold (always-on ones are part of the build, not of the moment — as in the CLI).</summary>
    public static ImmutableArray<ActivePassive> ListConditionalPassives(ImmutableArray<ActivePassive> passives) =>
        [.. passives.Where(passive => !passive.Passive.When.IsEmpty)];
}

using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Causality;

/// <summary>
/// Kernel: facts about keywords both Simulation and LoopGraphing rely on — what damage type a keyword
/// strike or a summon deals, and whether a status can max out — so both read the same answer.
/// </summary>
public static class KeywordResolution
{
    public static DamageType ResolveStrikeDamageType(this KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition) ? definition.Affinity.ToDamageType() : DamageType.Kinetic;

    public static DamageType ResolveSummonDamageType(this KeywordGlossary glossary, SummonId summon) =>
        glossary.Summons.TryGetValue(summon, out var definition) ? definition.DamageType : DamageType.Kinetic;

    public static bool CanReachMaxStacks(this KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition)
        && definition.MaxStacks.Match(max => max.Value.Value > 1, _ => false);

    public static bool IsCollectedAutomatically(this KeywordGlossary glossary, PickupId pickup) =>
        glossary.Pickups.TryGetValue(pickup, out var definition) && definition.CollectsAutomatically;

    public static Optional<StatusDefinition> FindStatus(this KeywordGlossary glossary, StatusId status) =>
        Optional.FromNullable(glossary.Statuses.GetValueOrDefault(status));
}

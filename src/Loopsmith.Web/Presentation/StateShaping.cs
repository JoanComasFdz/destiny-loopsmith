using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>A chip of the state panel: text in the CLI's wording and the affinity that colours it.</summary>
public sealed record StateChip(string Text, Affinity Affinity);

/// <summary>Pure: a <see cref="GameState"/> → what the state panel draws, phrased with the kernel's Phrasing.</summary>
public static class StateShaping
{
    public static ImmutableArray<StateChip> ShapeStatuses(KeywordGlossary glossary, ImmutableArray<ActiveStatus> statuses) =>
        [.. statuses.Select(status => new StateChip(DescribeActiveStatus(glossary, status), glossary.ReadStatusAffinity(status.Status)))];

    public static ImmutableArray<StateChip> ShapePickups(KeywordGlossary glossary, ImmutableArray<GroundPickup> pickups) =>
    [
        .. pickups
            .Where(pickup => pickup.Count > 0)
            .Select(pickup => new StateChip(
                $"{pickup.Count}× {glossary.DescribePickup(pickup.Pickup)}",
                glossary.Pickups.TryGetValue(pickup.Pickup, out var definition) ? definition.Affinity : Affinity.Neutral)),
    ];

    /// <summary>Passives whose conditions hold (always-on ones are part of the build, not of the moment — as in the CLI).</summary>
    public static ImmutableArray<ActivePassive> ListConditionalPassives(ImmutableArray<ActivePassive> passives) =>
        [.. passives.Where(passive => !passive.Passive.When.IsEmpty)];

    public static string DescribeAbilityLabel(AbilityKind kind) =>
        DomainPhrasing.Capitalize(kind.DescribeAbility());

    private static string DescribeActiveStatus(KeywordGlossary glossary, ActiveStatus status)
    {
        var name = glossary.DescribeStatus(status.Status);
        var stacks = glossary.IsStacking(status.Status) ? $" ×{status.Stacks.Value}" : "";
        var remaining = status.Remaining.Match(r => $" {r.Value.FormatSeconds()}", _ => "");
        return name + stacks + remaining;
    }
}

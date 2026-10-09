using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>A chip of the state panel: text in the CLI's wording and the affinity that colours it.</summary>
public sealed record StateChip(string Text, Affinity Affinity);

/// <summary>An ability gauge drawn as one segment per charge (each 0..1 full).</summary>
public sealed record GaugeView(string Label, ImmutableArray<decimal> Segments, string Charges);

/// <summary>Pure: a <see cref="GameState"/> → what the state panel draws, phrased with the kernel's Phrasing.</summary>
public static class StateShaping
{
    public static ImmutableArray<GaugeView> ShapeGauges(GameState state)
    {
        var gauges = ImmutableArray.Create(state.Abilities.Grenade, state.Abilities.Melee, state.Abilities.ClassAbility, state.Abilities.Super);
        return [.. gauges.Select(ShapeGauge)];
    }

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

    public static string FormatCharges(decimal charges) =>
        charges.ToString("0.##", CultureInfo.InvariantCulture);

    public static string FormatSignedCharges(decimal charges) =>
        (charges > 0 ? "+" : "") + FormatCharges(charges);

    public static string DescribeAbilityLabel(AbilityKind kind) =>
        DomainPhrasing.Capitalize(kind.DescribeAbility());

    private static GaugeView ShapeGauge(AbilityGauge gauge)
    {
        var count = Math.Max(gauge.MaxCharges, 1);
        var segments = Enumerable.Range(0, count)
            .Select(index => Math.Clamp(gauge.Energy.Value - index, 0m, 1m))
            .ToImmutableArray();
        return new GaugeView(DescribeAbilityLabel(gauge.Kind), segments, $"{FormatCharges(gauge.Energy.Value)}/{gauge.MaxCharges}");
    }

    private static string DescribeActiveStatus(KeywordGlossary glossary, ActiveStatus status)
    {
        var name = glossary.DescribeStatus(status.Status);
        var stacks = glossary.IsStacking(status.Status) ? $" ×{status.Stacks.Value}" : "";
        var remaining = status.Remaining.Match(r => $" {r.Value.FormatSeconds()}", _ => "");
        return name + stacks + remaining;
    }
}

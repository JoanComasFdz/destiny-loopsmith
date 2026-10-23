using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>An ability's energy right before a step and right after it ("Grenade", "100%", "≥ 8.44%"; ADRs D48).</summary>
public sealed record EnergyLine(AbilityKind Ability, string Name, string Before, string After);

/// <summary>
/// A step's energy (ADRs D48): the grenade's, melee's and class ability's before and after it, and — for a step whose
/// ability had no full charge before it — whether surely or maybe not, with its note and its badge
/// ("Grenade not ready: 0% before #2", "Not ready").
/// </summary>
public sealed record StepEnergy(ImmutableArray<EnergyLine> Lines, Optional<ReadinessFlag> Flag, Optional<string> Badge);

/// <summary>
/// An ability's energy as its HUD square fills (ADRs D48): the share of its first charge surely there (0 to 1: an "at
/// least" fills to its floor, "?" to nothing), as a CSS percentage ("8.44"), whether it is empty (all gray), how it reads
/// ("62.5%", "≥ 8.44%", "1 charge + 25%"), and a bar under the square for each further charge, as the game's HUD shows
/// Shinobu's Vow's second grenade.
/// </summary>
public sealed record EnergyGauge(decimal Fill, string FillPercent, bool IsEmpty, string Text, ImmutableArray<ChargeBar> ExtraCharges);

/// <summary>A further charge's bar under its ability's square: how much of it is surely there, as a CSS percentage ("40"), and whether it is full.</summary>
public sealed record ChargeBar(string FillPercent, bool IsFull);

/// <summary>Pure: a step's resolution → its energy, in the designer's words.</summary>
public static class StepEnergyShaping
{
    /// <summary>The grenade's, melee's or class ability's energy in <paramref name="state"/>, as its HUD square fills; none for a Super (its energy isn't kept).</summary>
    public static Optional<EnergyGauge> ShapeGauge(ValidatedBuild build, GameState state, AbilityKind ability)
    {
        if (!GameStateReading.EnergyAbilities.Contains(ability))
        {
            return Optional.None<EnergyGauge>();
        }

        var charges = build.CountCharges(ability);
        var energy = state.ReadEnergy(ability, charges);
        var total = energy.ReadTotal();
        var fill = ReadChargeFill(total, 0);
        return Optional.Some(new EnergyGauge(
            fill,
            FormatPercent(fill),
            fill <= 0m,
            DomainPhrasing.DescribeEnergy(energy, charges),
            [.. Enumerable.Range(1, charges.Value - 1).Select(charge => ReadChargeFill(total, charge)).Select(bar => new ChargeBar(FormatPercent(bar), bar >= 1m))]));
    }

    /// <summary>How much of the charge numbered <paramref name="charge"/> (0: the first) energy of <paramref name="total"/> charges fills: 0 to 1.</summary>
    private static decimal ReadChargeFill(decimal total, int charge) =>
        Math.Clamp(total - charge, 0m, 1m);

    private static string FormatPercent(decimal share) =>
        (share * 100m).ToString("0.##", CultureInfo.InvariantCulture);

    public static StepEnergy ShapeStepEnergy(ValidatedBuild build, Resolution resolution)
    {
        var flag = resolution.Readiness.Bind(ready => DomainPhrasing.DescribeReadiness(ready, build.CountCharges(ready.Ability), resolution.State.Step));
        return new(
            [
                .. GameStateReading.EnergyAbilities.Select(ability => new EnergyLine(
                    ability,
                    DomainPhrasing.Capitalize(ability.DescribeAbility()),
                    DomainPhrasing.DescribeEnergy(resolution.Before.ReadEnergy(ability, build.CountCharges(ability)), build.CountCharges(ability)),
                    DomainPhrasing.DescribeEnergy(resolution.State.ReadEnergy(ability, build.CountCharges(ability)), build.CountCharges(ability)))),
            ],
            flag,
            flag.Map(found => found.DescribeReadinessBadge()));
    }
}

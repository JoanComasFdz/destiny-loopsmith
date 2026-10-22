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
/// An ability's energy as its HUD square fills (ADRs D48): the share of all its charges surely there (0 to 1: an "at
/// least" fills to its floor, "?" to nothing), as a CSS percentage ("8.44"), whether it is empty (all gray), and how it
/// reads ("62.5%", "≥ 8.44%", "1 charge + 25%").
/// </summary>
public sealed record EnergyGauge(decimal Fill, string FillPercent, bool IsEmpty, string Text);

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
        var fill = Math.Min(energy.ReadTotal() / charges.Value, 1m);
        return Optional.Some(new EnergyGauge(
            fill,
            (fill * 100m).ToString("0.##", CultureInfo.InvariantCulture),
            fill <= 0m,
            DomainPhrasing.DescribeEnergy(energy, charges)));
    }

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

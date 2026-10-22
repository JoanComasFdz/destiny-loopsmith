using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>An ability's energy right before a step and right after it ("Grenade", "100%", "≥ 8.44%"; ADRs D48).</summary>
public sealed record EnergyLine(AbilityKind Ability, string Name, string Before, string After);

/// <summary>
/// A step's energy (ADRs D48): the grenade's, melee's and class ability's before and after it, and — for a step that uses
/// one of them — whether it was ready, with why not ("Grenade not ready: 0% before #2").
/// </summary>
public sealed record StepEnergy(ImmutableArray<EnergyLine> Lines, Readiness Readiness, Optional<string> NotReadyText);

/// <summary>Pure: a step's resolution → its energy, in the designer's words.</summary>
public static class StepEnergyShaping
{
    public static StepEnergy ShapeStepEnergy(ValidatedBuild build, Resolution resolution) =>
        new(
            [
                .. GameStateReading.EnergyAbilities.Select(ability => new EnergyLine(
                    ability,
                    DomainPhrasing.Capitalize(ability.DescribeAbility()),
                    DomainPhrasing.DescribeEnergy(resolution.Before.ReadEnergy(ability, build.CountCharges(ability)), build.CountCharges(ability)),
                    DomainPhrasing.DescribeEnergy(resolution.State.ReadEnergy(ability, build.CountCharges(ability)), build.CountCharges(ability)))),
            ],
            resolution.Readiness.Match(ready => ready.Value.Readiness, _ => Readiness.Ready),
            resolution.Readiness.Bind(ready => DomainPhrasing.DescribeReadiness(ready, build.CountCharges(ready.Ability), resolution.State.Step)));
}

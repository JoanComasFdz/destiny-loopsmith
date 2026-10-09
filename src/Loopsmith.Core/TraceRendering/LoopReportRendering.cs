using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>A <see cref="LoopReport"/> as styled lines: does the loop sustain, where it breaks, energy per cycle, steady state.</summary>
public static class LoopReportRendering
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly ImmutableArray<string> EnergyColumns = ["Grenade", "Melee", "Class", "Super"];

    private const int LabelWidth = 18;
    private const int ColumnWidth = 9;

    public static ImmutableArray<StyledLine> RenderLoopReport(LoopReport report) =>
        report.Cycles.IsEmpty
            ? [RenderTitle(report), StyledText.ToLine(1, "The loop has no steps — nothing to run (0 cycles completed).".ToSpan(Tone.Warning))]
            :
            [
                .. RenderHeading(report),
                RenderVerdict(report),
                Blank,
                .. RenderEnergy(report),
                Blank,
                .. RenderSteadyState(report),
            ];

    private static readonly StyledLine Blank = StyledText.ToLine(0, "".ToSpan());

    private static StyledLine RenderTitle(LoopReport report) =>
        StyledText.ToLine(0, report.LoopName.ToSpan(Tone.Strong), $"  {report.BuildName}".ToSpan(Tone.Muted));

    private static ImmutableArray<StyledLine> RenderHeading(LoopReport report) =>
    [
        RenderTitle(report),
        StyledText.ToLine(1,
            $"{DescribeCount(report.StepCount, "step")} per cycle, played back to back from a fresh spawn for up to {DescribeCount(report.MaxCycles, "cycle")}"
                .ToSpan(Tone.Muted)),
    ];

    private static StyledLine RenderVerdict(LoopReport report)
    {
        if (report.IsSustainable())
        {
            return StyledText.ToLine(1,
                "✓ Sustainable".ToSpan(Tone.Strong),
                $" — all {DescribeCount(report.MaxCycles, "cycle")} completed, the loop feeds itself".ToSpan(Tone.Plain));
        }

        var broken = report.Cycles.Where(cycle => cycle.Blocked.IsSome()).ToImmutableArray();
        var where = broken.SelectMany(cycle => cycle.Blocked.Match(
            blocked => new[]
            {
                $"✗ Breaks in cycle {cycle.Number} at step {blocked.Value.StepIndex + 1} ({blocked.Value.Action.ToActionToken()}): {blocked.Value.Reason}",
            },
            _ => []));
        return StyledText.ToLine(1,
            string.Concat(where).ToSpan(Tone.Warning),
            $" {report.CompletedCycles} of {DescribeCount(report.MaxCycles, "cycle")} completed.".ToSpan(Tone.Plain));
    }

    private static ImmutableArray<StyledLine> RenderEnergy(LoopReport report)
    {
        var header = StyledText.ToLine(0,
            Pad("Energy (charges)", LabelWidth + 2).ToSpan(Tone.Strong),
            JoinColumns(EnergyColumns).ToSpan(Tone.Muted));
        var start = RenderEnergyRow("Fresh spawn", ListCharges(report.EnergyAtStart), Tone.Plain);
        var cycles = report.Cycles.Select(cycle => RenderEnergyRow(
            $"After cycle {cycle.Number}{(cycle.Blocked.IsSome() ? " ✗" : "")}",
            ListCharges(cycle.EnergyAtEnd),
            cycle.Blocked.IsSome() ? Tone.Warning : Tone.Plain));
        var net = report.ComputeNetEnergy();
        var netRow = RenderEnergyRow(
            "Net per cycle",
            [FormatSigned(net.Grenade), FormatSigned(net.Melee), FormatSigned(net.ClassAbility), FormatSigned(net.Super)],
            Tone.Strong);
        return [header, start, .. cycles, netRow];
    }

    private static StyledLine RenderEnergyRow(string label, ImmutableArray<string> values, Tone tone) =>
        StyledText.ToLine(1,
            Pad(label, LabelWidth).ToSpan(Tone.Muted),
            JoinColumns(values).ToSpan(tone));

    /// <summary>Fixed-width columns, the last one unpadded (no trailing spaces).</summary>
    private static string JoinColumns(ImmutableArray<string> values) =>
        string.Concat(values.Select((value, index) => index < values.Length - 1 ? Pad(value, ColumnWidth) : value));

    private static ImmutableArray<string> ListCharges(EnergySnapshot energy) =>
        [FormatCharges(energy.Grenade), FormatCharges(energy.Melee), FormatCharges(energy.ClassAbility), FormatCharges(energy.Super)];

    private static ImmutableArray<StyledLine> RenderSteadyState(LoopReport report)
    {
        var steady = report.FindSteadyCycle().Match(cycle => cycle.Value.Number, _ => 0);
        var basis = report.CompletedCycles > 0 ? $"cycle {steady}, the last completed" : "cycle 1, which did not complete";
        var outcomeWidth = report.Outcomes.Select(o => o.Label.Length).DefaultIfEmpty(0).Max() + 2;
        var outcomes = report.Outcomes.Select(outcome => StyledText.ToLine(1,
            Pad(outcome.Label, outcomeWidth).ToSpan(Tone.Muted),
            outcome.Count.ToString(Invariant).ToSpan(Tone.Strong)));
        var countWidth = report.Sources.Select(s => s.Fired.ToString(Invariant).Length).DefaultIfEmpty(1).Max();
        var sources = report.Sources.Select(source => StyledText.ToLine(2,
            $"{source.Fired.ToString(Invariant).PadLeft(countWidth)}× ".ToSpan(Tone.Muted),
            source.SourceName.ToSpan(source.Affinity.ToTone())));
        var uptimeWidth = report.Uptime.Select(u => u.Name.Length).DefaultIfEmpty(0).Max() + 2;
        var uptime = report.Uptime.Select(buff => StyledText.ToLine(2,
            Pad(buff.Name, uptimeWidth).ToSpan(buff.Affinity.ToTone()),
            $"{buff.StepsActive}/{buff.Steps} steps".ToSpan(buff.StepsActive == buff.Steps ? Tone.Strong : Tone.Plain)));
        return
        [
            StyledText.ToLine(0, "Steady state ".ToSpan(Tone.Strong), $"({basis})".ToSpan(Tone.Muted)),
            .. outcomes,
            StyledText.ToLine(1, "Fired per cycle".ToSpan(Tone.Strong)),
            .. report.Sources.IsEmpty ? [StyledText.ToLine(2, "nothing".ToSpan(Tone.Muted))] : sources,
            StyledText.ToLine(1, "Buff uptime ".ToSpan(Tone.Strong), "(active after how many of the cycle's steps)".ToSpan(Tone.Muted)),
            .. report.Uptime.IsEmpty ? [StyledText.ToLine(2, "no buffs".ToSpan(Tone.Muted))] : uptime,
            RenderCaveat(report.UnknownValues, "?", "unknown value not applied", "unknown values not applied", " — the real loop is stronger"),
            RenderCaveat(report.ChanceRules, "(chance)", "bullet", "bullets", " fired in v1 but not guaranteed in game"),
        ];
    }

    /// <summary>The design itself: author, description and the numbered steps with their notes.</summary>
    public static ImmutableArray<StyledLine> RenderLoopDesign(LoopDesign design)
    {
        var author = design.Author.Match(a => $"  by {a.Value}", _ => "");
        var description = design.Description.Match(
            d => d.Value.TrimEnd('\n').Split('\n').Select(line => StyledText.ToLine(1, line.ToSpan(Tone.Muted))).ToImmutableArray(),
            _ => []);
        var numberWidth = design.Steps.Length.ToString(Invariant).Length;
        var tokenWidth = design.Steps.Select(step => step.Action.ToActionToken().Length).DefaultIfEmpty(0).Max() + 2;
        var steps = design.Steps.Select((step, index) => StyledText.ToLine(1,
            $"{(index + 1).ToString(Invariant).PadLeft(numberWidth)}. ".ToSpan(Tone.Muted),
            (step.Note.IsSome() ? Pad(step.Action.ToActionToken(), tokenWidth) : step.Action.ToActionToken()).ToSpan(Tone.Strong),
            step.Note.Match(note => note.Value, _ => "").ToSpan(Tone.Plain)));
        return
        [
            StyledText.ToLine(0, "Steps".ToSpan(Tone.Strong), author.ToSpan(Tone.Muted)),
            .. description,
            .. design.Steps.IsEmpty ? [StyledText.ToLine(1, "none yet".ToSpan(Tone.Muted))] : steps.ToImmutableArray(),
        ];
    }

    private static StyledLine RenderCaveat(int count, string marker, string singular, string plural, string consequence) =>
        StyledText.ToLine(1,
            $"{marker} ".ToSpan(Tone.Muted),
            $"{count} {(count == 1 ? singular : plural)}".ToSpan(count == 0 ? Tone.Muted : Tone.Warning),
            (count == 0 ? "" : consequence).ToSpan(Tone.Muted));

    private static string DescribeCount(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static string FormatCharges(EnergyAmount amount) => amount.Value.ToString("0.##", Invariant);

    private static string FormatSigned(decimal charges) =>
        charges switch
        {
            > 0m => "+" + charges.ToString("0.##", Invariant),
            < 0m => charges.ToString("0.##", Invariant),
            _ => "0",
        };

    private static string Pad(string text, int width) => text.PadRight(width);
}

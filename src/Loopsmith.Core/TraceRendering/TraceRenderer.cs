using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

public sealed record TraceOptions(bool ShowState, bool ShowReasons, bool ShowCaveats);

/// <summary>Renders simulation steps the way build notes read: event → outcome [source] + outcome [source].</summary>
public static class TraceRenderer
{
    private const int BarWidth = 10;

    public static ImmutableArray<StyledLine> RenderSequence(
        ValidatedBuild build, GameState initial, ImmutableArray<Resolution> resolutions, TraceOptions options)
    {
        var opening = options.ShowState
            ? [StyledText.ToLine(0, "Fresh spawn".ToSpan(Tone.Strong)), .. RenderState(build, initial, [])]
            : ImmutableArray<StyledLine>.Empty;
        var steps = resolutions.SelectMany(resolution => RenderResolution(build, resolution, options));
        var legend = resolutions.SelectMany(r => r.Fired).SelectMany(f => f.Outcomes).Any(o => o.Certainty == Certainty.Assumed)
            ? [StyledText.ToLine(0, "* chunk energy scalar unknown — 1× assumed (import the Compendium for real values)".ToSpan(Tone.Muted))]
            : ImmutableArray<StyledLine>.Empty;
        return [.. opening, .. steps, .. legend];
    }

    public static ImmutableArray<StyledLine> RenderResolution(ValidatedBuild build, Resolution resolution, TraceOptions options)
    {
        var glossary = build.Catalog.Glossary;
        var header = StyledText.ToLine(0,
            $"#{resolution.State.Step} ".ToSpan(Tone.Muted),
            glossary.DescribeAction(resolution.Action, build.Build).ToSpan(Tone.Strong));
        var fired = GroupByEvent(resolution.Fired).SelectMany(group => RenderGroup(build, group, options));
        var nothing = resolution.Fired.IsEmpty && resolution.Notes.IsEmpty
            ? [StyledText.ToLine(1, "nothing triggers".ToSpan(Tone.Muted))]
            : ImmutableArray<StyledLine>.Empty;
        var notes = resolution.Notes.Select(note => StyledText.ToLine(1, "! ".ToSpan(Tone.Warning), note.ToSpan(Tone.Warning)));
        var state = options.ShowState ? RenderState(build, resolution.State, resolution.ActivePassives) : [];
        return [header, .. fired, .. nothing, .. notes, .. state];
    }

    public static ImmutableArray<StyledLine> RenderState(ValidatedBuild build, GameState state, ImmutableArray<ActivePassive> passives)
    {
        var glossary = build.Catalog.Glossary;
        var tone = ToSubclassTone(build.Build.Subclass);
        var gauges = state.Abilities.Select(gauge => StyledText.ToLine(1,
            $"{DomainPhrasing.Capitalize(gauge.Kind.DescribeAbility()),-14}".ToSpan(Tone.Muted),
            RenderBar(gauge).ToSpan(tone),
            $" {FormatCharges(gauge.Energy.Value)}/{gauge.MaxCharges}".ToSpan(Tone.Plain)));
        var buffs = state.Buffs.IsEmpty
            ? "none".ToSpan(Tone.Muted).ToSingleSpanList()
            : JoinSpans(state.Buffs.Select(buff => DescribeActiveStatus(glossary, buff).ToSpan(glossary.ReadStatusAffinity(buff.Status).ToTone())));
        var debuffs = state.Target.Debuffs.IsEmpty
            ? "none".ToSpan(Tone.Muted).ToSingleSpanList()
            : JoinSpans(state.Target.Debuffs.Select(d => DescribeActiveStatus(glossary, d).ToSpan(glossary.ReadStatusAffinity(d.Status).ToTone())));
        var ground = state.Pickups.Where(p => p.Count > 0).ToImmutableArray();
        var pickups = ground.IsEmpty
            ? "none".ToSpan(Tone.Muted).ToSingleSpanList()
            : JoinSpans(ground.Select(p => $"{p.Count}× {glossary.DescribePickup(p.Pickup)}".ToSpan(Tone.Plain)));
        var passiveLines = passives
            .Where(p => !p.Passive.When.IsEmpty)
            .Select(p => StyledText.ToLine(1,
                "Active      ".ToSpan(Tone.Muted),
                glossary.DescribePassive(p.Passive.Effect).ToSpan(Tone.Plain),
                " [".ToSpan(Tone.Muted), p.SourceName.ToSpan(p.Affinity.ToTone()), "]".ToSpan(Tone.Muted)));
        return
        [
            .. gauges,
            new StyledLine(1, ["Buffs         ".ToSpan(Tone.Muted), .. buffs]),
            new StyledLine(1, ["Target        ".ToSpan(Tone.Muted), .. debuffs]),
            new StyledLine(1, ["Ground        ".ToSpan(Tone.Muted), .. pickups]),
            .. passiveLines,
        ];
    }

    public static ImmutableArray<StyledLine> RenderAvailableActions(ValidatedBuild build, ImmutableArray<PlayerAction> actions)
    {
        var glossary = build.Catalog.Glossary;
        return actions
            .Select((action, index) => StyledText.ToLine(1,
                $"[{index + 1,2}] ".ToSpan(Tone.Muted),
                $"{action.ToActionToken(),-22}".ToSpan(Tone.Strong),
                glossary.DescribeAction(action, build.Build).ToSpan(Tone.Plain)))
            .ToImmutableArray();
    }

    private static IEnumerable<ImmutableArray<FiredRule>> GroupByEvent(ImmutableArray<FiredRule> fired)
    {
        var current = ImmutableArray.CreateBuilder<FiredRule>();
        foreach (var rule in fired)
        {
            if (current.Count > 0 && !IsSameEvent(current[0], rule))
            {
                yield return current.ToImmutable();
                current.Clear();
            }

            current.Add(rule);
        }

        if (current.Count > 0)
        {
            yield return current.ToImmutable();
        }
    }

    private static bool IsSameEvent(FiredRule left, FiredRule right) =>
        left.Depth == right.Depth && ReferenceEquals(left.Trigger, right.Trigger);

    private static IEnumerable<StyledLine> RenderGroup(ValidatedBuild build, ImmutableArray<FiredRule> group, TraceOptions options)
    {
        var glossary = build.Catalog.Glossary;
        var first = group[0];
        var arrow = first.Depth > 0 ? "↳ " : "";
        var lead = new[]
        {
            arrow.ToSpan(Tone.Muted),
            glossary.DescribeEvent(first.Trigger, build.Build).ToSpan(Tone.Strong),
            " → ".ToSpan(Tone.Muted),
        };
        var bullets = group.SelectMany((rule, index) => RenderFiredRule(glossary, rule, index > 0));
        yield return new StyledLine(1 + first.Depth, [.. lead, .. bullets]);

        var detailIndent = 2 + first.Depth;
        foreach (var rule in group)
        {
            if (options.ShowReasons)
            {
                foreach (var reason in rule.Reason.Match(some => new[] { some.Value }, _ => []))
                {
                    yield return StyledText.ToLine(detailIndent, $"why [{rule.SourceName}]: {reason}".ToSpan(Tone.Muted));
                }
            }

            if (options.ShowCaveats)
            {
                foreach (var outcome in rule.Outcomes)
                {
                    foreach (var caveat in outcome.Caveat.Match(some => new[] { some.Value }, _ => []))
                    {
                        yield return StyledText.ToLine(detailIndent,
                            $"· {glossary.DescribeOutcome(outcome.Outcome)}: {caveat}".ToSpan(Tone.Muted));
                    }
                }
            }
        }
    }

    private static IEnumerable<StyledSpan> RenderFiredRule(KeywordGlossary glossary, FiredRule rule, bool isContinuation)
    {
        var joiner = isContinuation ? new[] { " + ".ToSpan(Tone.Muted) } : [];
        var outcomes = rule.Outcomes.IsEmpty
            ? "(no effect)"
            : string.Join(" and ", rule.Outcomes.Select(o => glossary.DescribeOutcome(o.Outcome) + ToCertaintyMarker(o.Certainty)));
        var chance = rule.Likelihood == Likelihood.Chance ? new[] { " (chance)".ToSpan(Tone.Muted) } : [];
        return
        [
            .. joiner,
            outcomes.ToSpan(Tone.Plain),
            " [".ToSpan(Tone.Muted),
            rule.SourceName.ToSpan(rule.Affinity.ToTone()),
            "]".ToSpan(Tone.Muted),
            .. chance,
        ];
    }

    private static string ToCertaintyMarker(Certainty certainty) =>
        certainty == Certainty.Assumed ? "*" : "";

    private static string RenderBar(AbilityGauge gauge)
    {
        var ratio = gauge.MaxCharges == 0 ? 0m : gauge.Energy.Value / gauge.MaxCharges;
        var filled = (int)Math.Round(ratio * BarWidth, MidpointRounding.AwayFromZero);
        return new string('▰', filled) + new string('▱', BarWidth - filled);
    }

    private static string FormatCharges(decimal charges) =>
        charges.ToString("0.##", CultureInfo.InvariantCulture);

    private static string DescribeActiveStatus(KeywordGlossary glossary, ActiveStatus status)
    {
        var name = glossary.DescribeStatus(status.Status);
        var stacks = glossary.IsStacking(status.Status) ? $" ×{status.Stacks.Value}" : "";
        var remaining = status.Remaining.Match(r => $" {r.Value.FormatSeconds()}", _ => "");
        return name + stacks + remaining;
    }

    private static ImmutableArray<StyledSpan> ToSingleSpanList(this StyledSpan span) => [span];

    private static ImmutableArray<StyledSpan> JoinSpans(IEnumerable<StyledSpan> spans) =>
        spans.SelectMany((span, index) => index == 0 ? new[] { span } : [" · ".ToSpan(Tone.Muted), span]).ToImmutableArray();

    private static Tone ToSubclassTone(Subclass subclass) =>
        subclass switch
        {
            Subclass.Arc => Tone.Arc,
            Subclass.Solar => Tone.Solar,
            Subclass.Void => Tone.Void,
            Subclass.Stasis => Tone.Stasis,
            Subclass.Strand => Tone.Strand,
            _ => Tone.Prismatic,
        };
}

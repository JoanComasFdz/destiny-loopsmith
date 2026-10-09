using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

public sealed record TraceOptions(bool ShowState, bool ShowReasons, bool ShowCaveats);

/// <summary>Renders simulation steps the way build notes read: event → outcome [source] + outcome [source].</summary>
public static class TraceRenderer
{
    public static ImmutableArray<StyledLine> RenderSequence(
        ValidatedBuild build, GameState initial, ImmutableArray<Resolution> resolutions, TraceOptions options)
    {
        var opening = options.ShowState
            ? [StyledText.ToLine(0, "Fresh spawn".ToSpan(Tone.Strong)), .. RenderState(build, initial, [])]
            : ImmutableArray<StyledLine>.Empty;
        var steps = resolutions.SelectMany(resolution => RenderResolution(build, resolution, options));
        return [.. opening, .. steps];
    }

    public static ImmutableArray<StyledLine> RenderResolution(ValidatedBuild build, Resolution resolution, TraceOptions options)
    {
        var glossary = build.Catalog.Glossary;
        var header = StyledText.ToLine(0,
            $"#{resolution.State.Step} ".ToSpan(Tone.Muted),
            glossary.DescribeAction(resolution.Action, build.Build).ToSpan(Tone.Strong));
        var fired = CollapseRepeats(GroupByEvent(resolution.Fired).Select(group => RenderGroup(build, group, options).ToImmutableArray()));
        var nothing = resolution.Fired.IsEmpty && resolution.Notes.IsEmpty
            ? [StyledText.ToLine(1, "nothing triggers".ToSpan(Tone.Muted))]
            : ImmutableArray<StyledLine>.Empty;
        var notes = resolution.Notes.Select(note => StyledText.ToLine(1, "! ".ToSpan(Tone.Warning), note.ToSpan(Tone.Warning)));
        var state = options.ShowState ? RenderState(build, resolution.State, resolution.ActivePassives) : [];
        return [header, .. fired, .. nothing, .. notes, .. state];
    }

    /// <summary>Buffs, target debuffs, ground pickups and active passives — no energy bars: abilities are always available (ADRs D21).</summary>
    public static ImmutableArray<StyledLine> RenderState(ValidatedBuild build, GameState state, ImmutableArray<ActivePassive> passives)
    {
        var glossary = build.Catalog.Glossary;
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
                glossary.DescribePassive(p.Passive.Modifier).ToSpan(Tone.Plain),
                " [".ToSpan(Tone.Muted), p.SourceName.ToSpan(p.Affinity.ToTone()), "]".ToSpan(Tone.Muted)));
        return
        [
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
        left.EventIndex == right.EventIndex;

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
        var bullets = group.SelectMany((rule, index) => RenderFiredRule(build, rule, index > 0));
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

    /// <summary>Identical consecutive event blocks (six orbs picked up) become one line marked ×N.</summary>
    private static ImmutableArray<StyledLine> CollapseRepeats(IEnumerable<ImmutableArray<StyledLine>> blocks)
    {
        var result = ImmutableArray.CreateBuilder<StyledLine>();
        var pending = ImmutableArray<StyledLine>.Empty;
        var count = 0;
        foreach (var block in blocks.Append([]))
        {
            if (count > 0 && block.Select(l => l.ToPlainText()).SequenceEqual(pending.Select(l => l.ToPlainText())))
            {
                count++;
                continue;
            }

            if (count > 0)
            {
                result.AddRange(count == 1 ? pending : [MarkRepeat(pending[0], count), .. pending.Skip(1)]);
            }

            pending = block;
            count = block.IsEmpty ? 0 : 1;
        }

        return result.ToImmutable();
    }

    private static StyledLine MarkRepeat(StyledLine line, int count) =>
        line with { Spans = line.Spans.Insert(2, $" ×{count}".ToSpan(Tone.Strong)) };

    private static IEnumerable<StyledSpan> RenderFiredRule(ValidatedBuild build, FiredRule rule, bool isContinuation)
    {
        var glossary = build.Catalog.Glossary;
        var copies = build.Equipped.Where(e => e.Element.Id == rule.Source).Select(e => e.Count).DefaultIfEmpty(1).First();
        ImmutableArray<StyledSpan> joiner = isContinuation ? [" + ".ToSpan(Tone.Muted)] : [];
        var outcomes = rule.Outcomes.IsEmpty
            ? "(no effect)"
            : glossary.DescribeOutcomes(rule.Outcomes.Select(o => new OutcomeMention(o.Outcome, copies)));
        ImmutableArray<StyledSpan> chance = rule.Likelihood == Likelihood.Chance ? [" (chance)".ToSpan(Tone.Muted)] : [];
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
}

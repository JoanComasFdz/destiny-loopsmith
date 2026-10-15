using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
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
        var header = StyledText.ToLine(0,
            $"#{resolution.State.Step} ".ToSpan(Tone.Muted),
            build.DescribeAction(resolution.Action).ToSpan(Tone.Strong));
        var fired = CollapseRepeats([.. GroupByEvent(resolution.Fired).Select(group => new RenderedEvent(group[0].Depth, DescribeEventKind(build, group[0].Trigger), ListFacts(build, group, options), [.. RenderGroup(build, group, options)], 1))]);
        var nothing = resolution.Fired.IsEmpty && resolution.Notes.IsEmpty
            ? [StyledText.ToLine(1, "nothing triggers".ToSpan(Tone.Muted))]
            : ImmutableArray<StyledLine>.Empty;
        var notes = resolution.Notes.Select(note => StyledText.ToLine(1, "! ".ToSpan(Tone.Warning), note.ToSpan(Tone.Warning)));
        var state = options.ShowState ? RenderState(build, resolution.State, resolution.ActivePassives) : [];
        return [header, .. fired, .. nothing, .. notes, .. state];
    }

    /// <summary>
    /// What is present — buffs on you ("at max" once declared), buffs on your weapons and the changes to their stats,
    /// debuffs on the pack, pickups on the ground, your damage resistance (PvE and PvP, its sources multiplied; a line for
    /// each resistance against some damage only) — then, for each status that switches other elements' passives on,
    /// what it switches on (<see cref="StepStateShaping"/>, as the designer shows it under each step).
    /// </summary>
    public static ImmutableArray<StyledLine> RenderState(ValidatedBuild build, GameState state, ImmutableArray<ActivePassive> passives)
    {
        var shaped = StepStateShaping.ShapeStepState(build, state, passives);
        var weaponEffects = shaped.WeaponEffects.Select(effect =>
            $"{effect.SourceName} on {string.Join(", ", effect.Weapons.Select(index => build.Build.Weapons[index].Name))}".ToSpan(effect.Affinity.ToTone()));
        var switchedOn = shaped.Buffs.Concat(shaped.Weapons).Concat(shaped.Debuffs)
            .Select(entry => (Entry: entry, Effects: ListEntryEffects(entry)))
            .Where(entry => !entry.Effects.IsEmpty)
            .Select(entry => new StyledLine(1,
            [
                $"While {entry.Entry.Text}  ".ToSpan(Tone.Muted),
                .. entry.Effects.SelectMany((effect, index) => (ImmutableArray<StyledSpan>)
                [
                    .. index > 0 ? [" · ".ToSpan(Tone.Muted)] : ImmutableArray<StyledSpan>.Empty,
                    effect.Text.ToSpan(Tone.Plain),
                    " [".ToSpan(Tone.Muted), effect.SourceName.ToSpan(effect.Affinity.ToTone()), "]".ToSpan(Tone.Muted),
                ]),
            ]));
        return
        [
            new StyledLine(1, ["Buffs         ".ToSpan(Tone.Muted), .. RenderEntries(shaped.Buffs)]),
            new StyledLine(1, ["Weapons       ".ToSpan(Tone.Muted), .. RenderWeaponEntries(shaped.Weapons, weaponEffects)]),
            new StyledLine(1, ["Debuffs       ".ToSpan(Tone.Muted), .. RenderEntries(shaped.Debuffs)]),
            new StyledLine(1, ["Ground        ".ToSpan(Tone.Muted), .. RenderEntries(shaped.Ground)]),
            .. shaped.Counts.Select(RenderCount),
            .. shaped.Resistance.Map(RenderResistance).UnwrapOr([]),
            .. switchedOn,
        ];
    }

    /// <summary>What a status does right then: its own weapon changes (Stats for All's stats), then what it switches on.</summary>
    private static ImmutableArray<(string Text, string SourceName, Affinity Affinity)> ListEntryEffects(StateEntry entry) =>
    [
        .. entry.OwnWeaponEffects.Select(text => (text, entry.Text, entry.Affinity)),
        .. entry.SwitchesOn.Select(effect => (effect.Text, effect.SourceName, effect.Affinity)),
    ];

    /// <summary>
    /// A counted buff's stacks and where they came from (ADRs D33): "Armor Charge  x1 · +1 (chance)  (#3 Elemental Charge: +1
    /// (chance) → +1 (chance) · #6 Orb of Power: +1 → x1 · +1 (chance))".
    /// </summary>
    private static StyledLine RenderCount(StackCountEntry count) =>
        new(1,
        [
            $"{count.Name}  ".ToSpan(Tone.Muted),
            count.Count.ToSpan(count.Affinity.ToTone()),
            $"  ({string.Join(" · ", count.Changes)})".ToSpan(Tone.Muted),
        ]);

    /// <summary>
    /// "Resistance    PvE 73% · PvP 32.5%  (Guardian 34.5% × Woven Mail 45% [PvP 25%])", then a line for each resistance
    /// against some combatants only: "  against Arc combatants  PvE 81.1%  (× Arc Resistance 30%)".
    /// </summary>
    private static ImmutableArray<StyledLine> RenderResistance(DamageResistance resistance) =>
    [
        RenderResistanceLine("Resistance    ", $"PvE {resistance.AllDamage.Pve.FormatPercent()} · PvP {resistance.AllDamage.Pvp.FormatPercent()}", "", resistance.AllDamage.Sources),
        .. resistance.AgainstSome.Select(line => RenderResistanceLine($"  against {line.Against}  ", $"PvE {line.Pve.FormatPercent()}", "× ", line.Sources)),
    ];

    private static StyledLine RenderResistanceLine(string label, string totals, string lead, ImmutableArray<ResistanceSource> sources) =>
        new(1,
        [
            label.ToSpan(Tone.Muted),
            totals.ToSpan(Tone.Strong),
            $"  ({lead}".ToSpan(Tone.Muted),
            .. sources.SelectMany((source, index) => (ImmutableArray<StyledSpan>)
            [
                .. index > 0 ? [" × ".ToSpan(Tone.Muted)] : ImmutableArray<StyledSpan>.Empty,
                source.Name.ToSpan(source.Affinity.ToTone()),
                (" " + source.Pve.FormatPercent() + source.Pvp.Map(pvp => $" [PvP {pvp.FormatPercent()}]").UnwrapOr("")).ToSpan(Tone.Plain),
            ]),
            ")".ToSpan(Tone.Muted),
        ]);

    private static ImmutableArray<StyledSpan> RenderEntries(ImmutableArray<StateEntry> entries) =>
        entries.IsEmpty
            ? "none".ToSpan(Tone.Muted).ToSingleSpanList()
            : JoinSpans(entries.Select(entry => entry.Text.ToSpan(entry.Affinity.ToTone())));

    private static ImmutableArray<StyledSpan> RenderWeaponEntries(ImmutableArray<StateEntry> entries, IEnumerable<StyledSpan> effects)
    {
        var spans = entries.Select(entry => entry.Text.ToSpan(entry.Affinity.ToTone())).Concat(effects).ToImmutableArray();
        return spans.IsEmpty ? "none".ToSpan(Tone.Muted).ToSingleSpanList() : JoinSpans(spans);
    }

    public static ImmutableArray<StyledLine> RenderAvailableActions(ValidatedBuild build, ImmutableArray<PlayerAction> actions) =>
        actions
            .Select((action, index) => StyledText.ToLine(1,
                $"[{index + 1,2}] ".ToSpan(Tone.Muted),
                $"{action.ToActionToken(),-22}".ToSpan(Tone.Strong),
                build.DescribeAction(action).ToSpan(Tone.Plain)))
            .ToImmutableArray();

    /// <summary>The fired rules, each run of the same event together (an event and the rules it set off).</summary>
    internal static IEnumerable<ImmutableArray<FiredRule>> GroupByEvent(ImmutableArray<FiredRule> fired)
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
            glossary.DescribeFiredEvent(group, build.Build).ToSpan(Tone.Strong),
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
                            $"· {glossary.DescribeAppliedOutcome(outcome.Outcome, outcome.Spend, build.ReadSuperType())}: {caveat}".ToSpan(Tone.Muted));
                    }
                }
            }
        }
    }

    /// <summary>
    /// An event's lines at its cascade depth, the event without the statuses on its target (what alike events share), what
    /// it shows (each rule's outcomes, each reason and caveat line), and how many times it happened in a row alike.
    /// </summary>
    private sealed record RenderedEvent(int Depth, string Kind, ImmutableArray<string> Facts, ImmutableArray<StyledLine> Lines, int Repeats);

    /// <summary>
    /// The event without the statuses on its target: "Grenade kill" for a kill on a Jolted target and one on a Jolted +
    /// Blinded target, which fold when the second sets off nothing the first didn't.
    /// </summary>
    internal static string DescribeEventKind(ValidatedBuild build, GameEvent gameEvent) =>
        build.Catalog.Glossary.DescribeEvent(
            gameEvent switch
            {
                GameEvent.Damaged damaged => damaged with { TargetHas = [] },
                GameEvent.Killed killed => killed with { TargetHas = [] },
                _ => gameEvent,
            },
            build.Build);

    private static ImmutableArray<string> ListFacts(ValidatedBuild build, ImmutableArray<FiredRule> group, TraceOptions options) =>
    [
        .. group.Select(rule => string.Concat(RenderFiredRule(build, rule, false).Select(span => span.Text))),
        .. RenderGroup(build, group, options).Skip(1).Select(line => line.ToPlainText()),
    ];

    /// <summary>
    /// Alike events in a row become one line marked ×N — with what the first set off: an event and the deeper events under
    /// it are a block, and a block that sets off nothing the block before it of the same event didn't shows in it. Identical
    /// blocks fold (five jolted hits of one grenade, each with its "Bolt Charge gained"; six orbs picked up), and so does a
    /// later one with less (the second kill of one action, after the first consumed Reaper). The statuses on the target
    /// don't keep blocks apart when they change nothing they set off (the kills after the first one's Photonic Flare
    /// blinded the pack). Blocks collapse at every depth.
    /// </summary>
    private static ImmutableArray<StyledLine> CollapseRepeats(ImmutableArray<RenderedEvent> events) =>
        [.. CollapseRepeatedBlocks(events).SelectMany(rendered => rendered.Repeats == 1 ? rendered.Lines : [MarkRepeat(rendered.Lines[0], rendered.Repeats), .. rendered.Lines[1..]])];

    private static ImmutableArray<RenderedEvent> CollapseRepeatedBlocks(ImmutableArray<RenderedEvent> events)
    {
        if (events.IsEmpty)
        {
            return events;
        }

        var depth = events.Min(rendered => rendered.Depth);
        var blocks = events
            .Aggregate(ImmutableArray<ImmutableArray<RenderedEvent>>.Empty, (split, rendered) =>
                split.IsEmpty || rendered.Depth <= depth ? split.Add([rendered]) : split.SetItem(split.Length - 1, split[^1].Add(rendered)))
            .Select(block => (ImmutableArray<RenderedEvent>)[block[0], .. CollapseRepeatedBlocks(block[1..])]);
        return
        [
            .. blocks
                .Aggregate(ImmutableArray<ImmutableArray<RenderedEvent>>.Empty, (collapsed, next) =>
                    !collapsed.IsEmpty && IsFoldedInto(collapsed[^1], next)
                        ? collapsed.SetItem(collapsed.Length - 1, collapsed[^1].SetItem(0, collapsed[^1][0] with { Repeats = collapsed[^1][0].Repeats + next[0].Repeats }))
                        : collapsed.Add(next))
                .SelectMany(block => block),
        ];
    }

    /// <summary>A block folds into the one before it when both start with the same event and it shows nothing that one didn't.</summary>
    private static bool IsFoldedInto(ImmutableArray<RenderedEvent> earlier, ImmutableArray<RenderedEvent> later) =>
        earlier[0].Depth == later[0].Depth
        && earlier[0].Kind == later[0].Kind
        && ListBlockFacts(later).IsSubsetOf(ListBlockFacts(earlier));

    /// <summary>What a block shows: each event (a deeper one with its count) and each fact under it.</summary>
    private static ImmutableHashSet<string> ListBlockFacts(ImmutableArray<RenderedEvent> block) =>
        [.. block.SelectMany((rendered, index) => ListEventFacts(rendered, $"{rendered.Depth}|{rendered.Kind}{(index == 0 ? "" : $"×{rendered.Repeats}")}"))];

    private static IEnumerable<string> ListEventFacts(RenderedEvent rendered, string key) =>
        rendered.Facts.Select(fact => $"{key}|{fact}").Prepend(key);

    private static StyledLine MarkRepeat(StyledLine line, int count) =>
        line with { Spans = line.Spans.Insert(2, $" ×{count}".ToSpan(Tone.Strong)) };

    private static IEnumerable<StyledSpan> RenderFiredRule(ValidatedBuild build, FiredRule rule, bool isContinuation)
    {
        var glossary = build.Catalog.Glossary;
        var copies = CountCopies(build, rule.Source);
        ImmutableArray<StyledSpan> joiner = isContinuation ? [" + ".ToSpan(Tone.Muted)] : [];
        var outcomes = rule.NotStackedWith.Match(
            partner => $"doesn't stack with {partner.Value}".ToSpan(Tone.Warning),
            _ => (rule.Outcomes.IsEmpty
                ? "(no effect)"
                : glossary.DescribeOutcomes(rule.Outcomes.Select(o => new OutcomeMention(o.Outcome, copies, o.Spend)), build.ReadSuperType())).ToSpan(Tone.Plain));
        ImmutableArray<StyledSpan> chance = rule.Likelihood == Likelihood.Chance ? [" (chance)".ToSpan(Tone.Muted)] : [];
        return
        [
            .. joiner,
            outcomes,
            " [".ToSpan(Tone.Muted),
            rule.SourceName.ToSpan(rule.Affinity.ToTone()),
            "]".ToSpan(Tone.Muted),
            .. chance,
        ];
    }

    /// <summary>The copies of an element equipped (one for an element every build has: a keyword).</summary>
    internal static int CountCopies(ValidatedBuild build, ElementId source) =>
        build.Equipped.Where(e => e.Element.Id == source).Select(e => e.Count).DefaultIfEmpty(1).First();

    private static ImmutableArray<StyledSpan> ToSingleSpanList(this StyledSpan span) => [span];

    private static ImmutableArray<StyledSpan> JoinSpans(IEnumerable<StyledSpan> spans) =>
        spans.SelectMany((span, index) => index == 0 ? new[] { span } : [" · ".ToSpan(Tone.Muted), span]).ToImmutableArray();
}

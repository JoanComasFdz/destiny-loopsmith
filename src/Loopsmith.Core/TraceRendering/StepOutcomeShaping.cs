using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>
/// One outcome on a line of its own ("+1 Bolt Charge"), or why its element gave none ("doesn't stack with …"); the
/// keyword it names when the catalog has one (Bolt Charge, Jolt, Ionic Trace: something to show the details of).
/// </summary>
public sealed record OutcomeLine(string Text, Tone Tone, Optional<ElementId> Keyword, bool Chance);

/// <summary>An element an event set off, its outcomes line by line; its rules' reasons and its outcomes' caveats when asked for.</summary>
public sealed record SourceOutcomes(
    ElementId Source,
    string Name,
    ElementKind Kind,
    Affinity Affinity,
    ImmutableArray<OutcomeLine> Outcomes,
    ImmutableArray<string> Reasons,
    ImmutableArray<string> Caveats);

/// <summary>
/// An event of a step (a hit, a kill, a buff gained) at its cascade depth (0: what the action itself did), the keyword
/// it names (a buff gained, a pickup picked up), how many times it happened in a row alike, and the elements it set off.
/// </summary>
public sealed record EventOutcomes(int Depth, string Event, Optional<ElementId> Keyword, int Repeats, ImmutableArray<SourceOutcomes> Sources);

/// <summary>A step's outcomes as a tree: event → element → outcome, and the step's notes (warnings).</summary>
public sealed record StepOutcomes(ImmutableArray<EventOutcomes> Events, ImmutableArray<string> Notes);

/// <summary>
/// Pure: a step's outcomes shaped for a host that draws them line by line (the web designer) — the same facts and
/// wording as <see cref="TraceRenderer"/>'s one line per event, each element on a line of its own and each of its
/// outcomes under it.
/// </summary>
public static class StepOutcomeShaping
{
    public static StepOutcomes ShapeStepOutcomes(ValidatedBuild build, Resolution resolution, TraceOptions options)
    {
        var events = TraceRenderer.GroupByEvent(resolution.Fired).Select(group => ShapeEvent(build, group, options));
        return new StepOutcomes(CollapseRepeats(events), resolution.Notes);
    }

    private static EventOutcomes ShapeEvent(ValidatedBuild build, ImmutableArray<FiredRule> group, TraceOptions options)
    {
        var first = group[0];
        var sources = group.GroupBy(rule => rule.Source).Select(rules => ShapeSource(build, [.. rules], options));
        return new EventOutcomes(
            first.Depth,
            build.Catalog.Glossary.DescribeEvent(first.Trigger, build.Build),
            FindEventKeyword(build.Catalog, first.Trigger),
            1,
            [.. sources]);
    }

    private static SourceOutcomes ShapeSource(ValidatedBuild build, ImmutableArray<FiredRule> rules, TraceOptions options)
    {
        var first = rules[0];
        var glossary = build.Catalog.Glossary;
        var copies = TraceRenderer.CountCopies(build, first.Source);
        var lines = rules.SelectMany(rule => ShapeOutcomeLines(build.Catalog, rule, copies));
        var reasons = options.ShowReasons ? rules.SelectMany(rule => ToSequence(rule.Reason)) : [];
        var caveats = options.ShowCaveats
            ? rules.SelectMany(rule => rule.Outcomes.SelectMany(outcome => ToSequence(outcome.Caveat).Select(caveat => $"{glossary.DescribeOutcome(outcome.Outcome)}: {caveat}")))
            : [];
        return new SourceOutcomes(first.Source, first.SourceName, first.SourceKind, first.Affinity, [.. lines], [.. reasons], [.. caveats]);
    }

    private static IEnumerable<OutcomeLine> ShapeOutcomeLines(RuleCatalog catalog, FiredRule rule, int copies)
    {
        var chance = rule.Likelihood == Likelihood.Chance;
        return rule.NotStackedWith.Match(
            partner => [new OutcomeLine($"doesn't stack with {partner.Value}", Tone.Warning, Optional.None<ElementId>(), chance)],
            _ => rule.Outcomes.IsEmpty
                ? [new OutcomeLine("(no effect)", Tone.Muted, Optional.None<ElementId>(), chance)]
                : catalog.Glossary.ListOutcomePhrases(rule.Outcomes.Select(outcome => new OutcomeMention(outcome.Outcome, copies)))
                    .Select(phrase => new OutcomeLine(phrase.Text, Tone.Plain, FindOutcomeKeyword(catalog, phrase.Outcome), chance)));
    }

    /// <summary>Identical events in a row (six orbs picked up) become one, counted.</summary>
    private static ImmutableArray<EventOutcomes> CollapseRepeats(IEnumerable<EventOutcomes> events) =>
        events.Aggregate(ImmutableArray<EventOutcomes>.Empty, (collapsed, next) =>
            !collapsed.IsEmpty && DescribeRepeatKey(collapsed[^1]) == DescribeRepeatKey(next)
                ? collapsed.SetItem(collapsed.Length - 1, collapsed[^1] with { Repeats = collapsed[^1].Repeats + 1 })
                : collapsed.Add(next));

    private static string DescribeRepeatKey(EventOutcomes shaped) =>
        $"{shaped.Depth}|{shaped.Event}|" + string.Join("|", shaped.Sources.Select(source =>
            $"{source.Source.Value}:{string.Join(";", source.Outcomes.Select(line => $"{line.Text}{(line.Chance ? "?" : "")}"))}:{string.Join(";", source.Reasons)}:{string.Join(";", source.Caveats)}"));

    private static Optional<ElementId> FindOutcomeKeyword(RuleCatalog catalog, Outcome outcome) =>
        outcome switch
        {
            Outcome.ConvertStacksToEnergy convert => FindKeyword(catalog, convert.Consumed.Value),
            Outcome.ApplyBuff apply => FindKeyword(catalog, apply.Status.Value),
            Outcome.RemoveBuff remove => FindKeyword(catalog, remove.Status.Value),
            Outcome.DebuffTarget debuff => FindKeyword(catalog, debuff.Status.Value),
            Outcome.Spawn spawn => FindKeyword(catalog, spawn.Pickup.Value),
            Outcome.StrikeTarget strike => FindKeyword(catalog, strike.Via.Value),
            _ => Optional.None<ElementId>(),
        };

    private static Optional<ElementId> FindEventKeyword(RuleCatalog catalog, GameEvent gameEvent) =>
        gameEvent switch
        {
            GameEvent.PickedUp picked => FindKeyword(catalog, picked.Pickup.Value),
            GameEvent.BuffGained gained => FindKeyword(catalog, gained.Status.Value),
            GameEvent.StacksMaxed maxed => FindKeyword(catalog, maxed.Status.Value),
            _ => Optional.None<ElementId>(),
        };

    /// <summary>The element a status or pickup is, when the catalog has one by that id (a keyword: Bolt Charge is <c>bolt-charge</c>).</summary>
    private static Optional<ElementId> FindKeyword(RuleCatalog catalog, string id) =>
        ElementId.TryFrom(id, out var element) && catalog.Elements.ContainsKey(element) ? Optional.Some(element) : Optional.None<ElementId>();

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(found => new[] { found.Value }, _ => []);
}

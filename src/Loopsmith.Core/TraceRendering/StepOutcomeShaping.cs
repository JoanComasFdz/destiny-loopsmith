using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>
/// One outcome on a line of its own ("+1 Bolt Charge"), or why its element gave none ("doesn't stack with …"); the
/// element the status or pickup it names is, when the catalog has one by that id (Bolt Charge, Jolt, Ionic Trace,
/// Combination Blow: something to show the details of).
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
/// An event of a step (a hit, a kill, a buff gained) at its cascade depth (0: what the action itself did), the element
/// it names (a buff gained, a pickup picked up, a keyword's strike), how many times it happened in a row alike, and the
/// elements it set off — each run of one element's rules together, in the order the engine fired them.
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
        var elements = IndexElements(build.Catalog);
        var events = TraceRenderer.GroupByEvent(resolution.Fired).Select(group => ShapeEvent(build, elements, group, options));
        return new StepOutcomes(CollapseRepeats(events), resolution.Notes);
    }

    private static EventOutcomes ShapeEvent(ValidatedBuild build, ElementIndex elements, ImmutableArray<FiredRule> group, TraceOptions options)
    {
        var first = group[0];
        var sources = GroupRuns(group).Select(rules => ShapeSource(build, elements, rules, options));
        return new EventOutcomes(
            first.Depth,
            build.Catalog.Glossary.DescribeEvent(first.Trigger, build.Build),
            FindEventElement(elements, first.Trigger),
            1,
            [.. sources]);
    }

    /// <summary>Each run of one element's rules, in the engine's order: an element whose rules aren't next to each other (a debuff first, energy last) shows twice.</summary>
    private static IEnumerable<ImmutableArray<FiredRule>> GroupRuns(ImmutableArray<FiredRule> group) =>
        group.Aggregate(ImmutableArray<ImmutableArray<FiredRule>>.Empty, (runs, rule) =>
            !runs.IsEmpty && runs[^1][0].Source == rule.Source
                ? runs.SetItem(runs.Length - 1, runs[^1].Add(rule))
                : runs.Add([rule]));

    private static SourceOutcomes ShapeSource(ValidatedBuild build, ElementIndex elements, ImmutableArray<FiredRule> rules, TraceOptions options)
    {
        var first = rules[0];
        var glossary = build.Catalog.Glossary;
        var copies = TraceRenderer.CountCopies(build, first.Source);
        var lines = rules.SelectMany(rule => ShapeOutcomeLines(glossary, elements, rule, copies));
        var reasons = options.ShowReasons ? rules.SelectMany(rule => ToSequence(rule.Reason)) : [];
        var caveats = options.ShowCaveats
            ? rules.SelectMany(rule => rule.Outcomes.SelectMany(outcome => ToSequence(outcome.Caveat).Select(caveat => $"{glossary.DescribeOutcome(outcome.Outcome)}: {caveat}")))
            : [];
        return new SourceOutcomes(first.Source, first.SourceName, first.SourceKind, first.Affinity, [.. lines], [.. reasons], [.. caveats]);
    }

    private static IEnumerable<OutcomeLine> ShapeOutcomeLines(KeywordGlossary glossary, ElementIndex elements, FiredRule rule, int copies)
    {
        var chance = rule.Likelihood == Likelihood.Chance;
        return rule.NotStackedWith.Match(
            partner => [new OutcomeLine($"doesn't stack with {partner.Value}", Tone.Warning, Optional.None<ElementId>(), chance)],
            _ => rule.Outcomes.IsEmpty
                ? [new OutcomeLine("(no effect)", Tone.Muted, Optional.None<ElementId>(), chance)]
                : glossary.ListOutcomePhrases(rule.Outcomes.Select(outcome => new OutcomeMention(outcome.Outcome, copies)))
                    .Select(phrase => new OutcomeLine(phrase.Text, Tone.Plain, FindOutcomeElement(elements, phrase.Outcome), chance)));
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

    /// <summary>The catalog's elements by their id's text: a status or pickup is the element of the same id, when there is one.</summary>
    private sealed record ElementIndex(ImmutableDictionary<string, ElementId> ById);

    private static ElementIndex IndexElements(RuleCatalog catalog) =>
        new(catalog.Elements.Keys.ToImmutableDictionary(id => id.Value, id => id));

    private static Optional<ElementId> FindOutcomeElement(ElementIndex elements, Outcome outcome) =>
        outcome.Match(
            grantEnergy: _ => Optional.None<ElementId>(),
            convertStacksToEnergy: convert => FindElement(elements, convert.Consumed.Value),
            applyBuff: apply => FindElement(elements, apply.Status.Value),
            removeBuff: remove => FindElement(elements, remove.Status.Value),
            debuffTarget: debuff => FindElement(elements, debuff.Status.Value),
            spawn: spawn => FindElement(elements, spawn.Pickup.Value),
            spawnSummon: summon => FindElement(elements, summon.Summon.Value),
            strikeTarget: strike => FindElement(elements, strike.Via.Value),
            modifyDamage: _ => Optional.None<ElementId>(),
            restoreHealth: _ => Optional.None<ElementId>(),
            resetCooldown: _ => Optional.None<ElementId>());

    private static Optional<ElementId> FindEventElement(ElementIndex elements, GameEvent gameEvent) =>
        gameEvent.Match(
            abilityCast: _ => Optional.None<ElementId>(),
            damaged: damaged => FindOriginElement(elements, damaged.Origin),
            killed: killed => FindOriginElement(elements, killed.Origin),
            targetsHit: hit => FindOriginElement(elements, hit.Origin),
            pickedUp: picked => FindElement(elements, picked.Pickup.Value),
            buffGained: gained => FindElement(elements, gained.Status.Value),
            stacksMaxed: maxed => FindElement(elements, maxed.Status.Value));

    /// <summary>A keyword's or a summon's damage names its element (a Jolt strike: Jolt); a weapon's or an ability's doesn't.</summary>
    private static Optional<ElementId> FindOriginElement(ElementIndex elements, DamageOrigin origin) =>
        origin.Match(
            weapon: _ => Optional.None<ElementId>(),
            ability: _ => Optional.None<ElementId>(),
            keyword: keyword => FindElement(elements, keyword.Status.Value),
            summoned: summoned => FindElement(elements, summoned.Summon.Value));

    private static Optional<ElementId> FindElement(ElementIndex elements, string id) =>
        elements.ById.TryGetValue(id, out var element) ? Optional.Some(element) : Optional.None<ElementId>();

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(found => new[] { found.Value }, _ => []);
}

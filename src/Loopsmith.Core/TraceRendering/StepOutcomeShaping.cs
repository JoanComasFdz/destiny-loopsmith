using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>A keyword in a text: its affinity (it is highlighted in it) and the element it is when the catalog has one (its details).</summary>
public sealed record KeywordPart(Affinity Affinity, Optional<ElementId> Element);

/// <summary>A piece of an event's or an outcome's text: plain, or a keyword ("Bolt Charge", "Jolted", "Ionic Trace").</summary>
public sealed record TextPart(string Text, Optional<KeywordPart> Keyword);

/// <summary>
/// One outcome on a line of its own ("+1 Bolt Charge"), or why its element gave none ("doesn't stack with …"); its text
/// in parts, each keyword apart (Bolt Charge, Jolt, Ionic Trace, Combination Blow: highlighted, with its details when the
/// catalog has it as an element).
/// </summary>
public sealed record OutcomeLine(string Text, Tone Tone, ImmutableArray<TextPart> Parts, bool Chance);

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
/// An event of a step (a hit, a kill, a buff gained) at its cascade depth (0: what the action itself did), its text in
/// parts (the keywords it names apart: "Grenade kill on Jolted target", "Bolt Charge gained"), the weapon slot whose hit
/// or kill it is (so a perk two weapons roll is this one's), the pickup it picked up ("Picked up Orb of Power"), how many
/// times it happened in a row alike, and the elements it set off — each run of one element's rules together, in the
/// order the engine fired them.
/// </summary>
public sealed record EventOutcomes(
    int Depth,
    string Event,
    ImmutableArray<TextPart> Parts,
    Optional<WeaponSlot> Weapon,
    Optional<PickupId> Pickup,
    int Repeats,
    ImmutableArray<SourceOutcomes> Sources);

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
        var catalog = build.Catalog;
        var events = TraceRenderer.GroupByEvent(resolution.Fired).Select(group => ShapeEvent(build, catalog, group, options));
        return new StepOutcomes(CollapseRepeats(events), resolution.Notes);
    }

    private static EventOutcomes ShapeEvent(ValidatedBuild build, RuleCatalog catalog, ImmutableArray<FiredRule> group, TraceOptions options)
    {
        var first = group[0];
        var sources = GroupRuns(group).Select(rules => ShapeSource(build, catalog, rules, options));
        var text = build.Catalog.Glossary.DescribeEvent(first.Trigger, build.Build);
        var pickup = first.Trigger is GameEvent.PickedUp picked ? Optional.Some(picked.Pickup) : Optional.None<PickupId>();
        return new EventOutcomes(first.Depth, text, SplitText(build.Catalog.Glossary, catalog, text), FindEventWeapon(first.Trigger), pickup, 1, [.. sources]);
    }

    /// <summary>Each run of one element's rules, in the engine's order: an element whose rules aren't next to each other (a debuff first, energy last) shows twice.</summary>
    private static IEnumerable<ImmutableArray<FiredRule>> GroupRuns(ImmutableArray<FiredRule> group) =>
        group.Aggregate(ImmutableArray<ImmutableArray<FiredRule>>.Empty, (runs, rule) =>
            !runs.IsEmpty && runs[^1][0].Source == rule.Source
                ? runs.SetItem(runs.Length - 1, runs[^1].Add(rule))
                : runs.Add([rule]));

    private static SourceOutcomes ShapeSource(ValidatedBuild build, RuleCatalog catalog, ImmutableArray<FiredRule> rules, TraceOptions options)
    {
        var first = rules[0];
        var glossary = build.Catalog.Glossary;
        var copies = TraceRenderer.CountCopies(build, first.Source);
        var superType = build.ReadSuperType();
        var lines = rules.SelectMany(rule => ShapeOutcomeLines(glossary, catalog, rule, copies, superType));
        var reasons = options.ShowReasons ? rules.SelectMany(rule => ToSequence(rule.Reason)) : [];
        var caveats = options.ShowCaveats
            ? rules.SelectMany(rule => rule.Outcomes.SelectMany(outcome => ToSequence(outcome.Caveat).Select(caveat => $"{glossary.DescribeOutcome(outcome.Outcome, superType)}: {caveat}")))
            : [];
        return new SourceOutcomes(first.Source, first.SourceName, first.SourceKind, first.Affinity, [.. lines], [.. reasons], [.. caveats]);
    }

    private static IEnumerable<OutcomeLine> ShapeOutcomeLines(
        KeywordGlossary glossary, RuleCatalog catalog, FiredRule rule, int copies, Optional<DamageType> superType)
    {
        var chance = rule.Likelihood == Likelihood.Chance;
        return rule.NotStackedWith.Match(
            partner => [ToLine(glossary, catalog, $"doesn't stack with {partner.Value}", Tone.Warning, chance)],
            _ => rule.Outcomes.IsEmpty
                ? [ToLine(glossary, catalog, "(no effect)", Tone.Muted, chance)]
                : glossary.ListOutcomePhrases(rule.Outcomes.Select(outcome => new OutcomeMention(outcome.Outcome, copies)), superType)
                    .Select(phrase => ToLine(glossary, catalog, phrase.Text, Tone.Plain, chance)));
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

    private static OutcomeLine ToLine(KeywordGlossary glossary, RuleCatalog catalog, string text, Tone tone, bool chance) =>
        new(text, tone, SplitText(glossary, catalog, text), chance);

    /// <summary>A text in parts, each keyword the glossary knows apart, with the element of its id when the catalog has one.</summary>
    private static ImmutableArray<TextPart> SplitText(KeywordGlossary glossary, RuleCatalog catalog, string text) =>
    [
        .. glossary.SplitKeywords(text).Select(part => new TextPart(
            part.Text,
            part.Keyword.Map(keyword => new KeywordPart(keyword.Affinity, catalog.FindKeywordElement(keyword.Keyword))))),
    ];

    private static Optional<WeaponSlot> FindEventWeapon(GameEvent gameEvent) =>
        gameEvent.Match(
            abilityCast: _ => Optional.None<WeaponSlot>(),
            damaged: damaged => FindOriginWeapon(damaged.Origin),
            killed: killed => FindOriginWeapon(killed.Origin),
            targetsHit: hit => FindOriginWeapon(hit.Origin),
            pickedUp: _ => Optional.None<WeaponSlot>(),
            buffGained: _ => Optional.None<WeaponSlot>(),
            stacksMaxed: _ => Optional.None<WeaponSlot>(),
            finisher: _ => Optional.None<WeaponSlot>());

    private static Optional<WeaponSlot> FindOriginWeapon(DamageOrigin origin) =>
        origin.Match(
            weapon: weapon => Optional.Some(weapon.Slot),
            ability: _ => Optional.None<WeaponSlot>(),
            keyword: _ => Optional.None<WeaponSlot>(),
            summoned: _ => Optional.None<WeaponSlot>(),
            finisher: _ => Optional.None<WeaponSlot>());


    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(found => new[] { found.Value }, _ => []);
}

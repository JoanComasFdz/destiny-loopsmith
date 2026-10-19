using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>
/// A keyword in a text: its affinity (it is highlighted in it), the element it is when the catalog has one (its
/// details), and its icon (the game's own keyword icon when the excerpt has it, or the shape the game draws for it).
/// </summary>
public sealed record KeywordPart(Affinity Affinity, Optional<ElementId> Element, Optional<IconRef> Icon);

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
/// or kill it is (so a perk two weapons roll is this one's) and that weapon's icon, the pickup it picked up ("Picked up Orb of Power"), how many
/// times it happened in a row alike, and the elements it set off — each run of one element's rules together, in the
/// order the engine fired them.
/// </summary>
public sealed record EventOutcomes(
    int Depth,
    string Event,
    ImmutableArray<TextPart> Parts,
    Optional<WeaponSlot> Weapon,
    Optional<string> WeaponIcon,
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
        var events = TraceRenderer.GroupByEvent(resolution.Fired)
            .Select(group => new ShapedEvent(ShapeEvent(build, catalog, group, options), TraceRenderer.DescribeEventKind(build, group[0].Trigger)));
        return new StepOutcomes([.. CollapseRepeats([.. events]).Select(shaped => shaped.Outcomes)], resolution.Notes);
    }

    /// <summary>An event's outcomes, and the event without the statuses on its target (what alike events share).</summary>
    private sealed record ShapedEvent(EventOutcomes Outcomes, string Kind);

    private static EventOutcomes ShapeEvent(ValidatedBuild build, RuleCatalog catalog, ImmutableArray<FiredRule> group, TraceOptions options)
    {
        var first = group[0];
        var sources = GroupRuns(group).Select(rules => ShapeSource(build, catalog, rules, options));
        var text = build.Catalog.Glossary.DescribeFiredEvent(group, build.Build);
        var weapon = FindEventWeapon(first.Trigger);
        var pickup = first.Trigger is GameEvent.PickedUp picked ? Optional.Some(picked.Pickup) : Optional.None<PickupId>();
        return new EventOutcomes(first.Depth, text, SplitText(build.Catalog.Glossary, catalog, text), weapon,
            weapon.Bind(slot => IconFinding.FindWeaponIcon(catalog, build.Build, slot)), pickup, 1, [.. sources]);
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
        var copies = build.CountCopies(first.Source);
        var superType = build.ReadSuperType();
        var lines = rules.SelectMany(rule => ShapeOutcomeLines(glossary, catalog, rule, copies, superType));
        var reasons = options.ShowReasons ? rules.SelectMany(rule => ToSequence(rule.Reason)) : [];
        var caveats = options.ShowCaveats
            ? rules.SelectMany(rule => rule.Outcomes.SelectMany(outcome => ToSequence(outcome.Caveat).Select(caveat => $"{glossary.DescribeAppliedOutcome(outcome.Outcome, outcome.Spend, superType)}: {caveat}")))
            : [];
        return new SourceOutcomes(first.Source, first.SourceName, first.SourceKind, first.Affinity, [.. lines], [.. reasons], [.. caveats]);
    }

    private static IEnumerable<OutcomeLine> ShapeOutcomeLines(
        KeywordGlossary glossary, RuleCatalog catalog, FiredRule rule, CopyCount copies, Optional<DamageType> superType)
    {
        var chance = rule.Likelihood == Likelihood.Chance;
        return rule.NotStackedWith.Match(
            partner => [ToLine(glossary, catalog, $"doesn't stack with {partner.Value}", Tone.Warning, chance)],
            _ => rule.Outcomes.IsEmpty
                ? [ToLine(glossary, catalog, "(no effect)", Tone.Muted, chance)]
                : glossary.ListOutcomePhrases(rule.Outcomes.Select(outcome => new OutcomeMention(outcome.Outcome, copies, outcome.Spend)), superType)
                    .Select(phrase => ToLine(glossary, catalog, phrase.Text, Tone.Plain, chance)));
    }

    /// <summary>
    /// Alike events in a row become one, counted — with what the first set off: an event and the deeper events under it are
    /// a block, and a block that sets off nothing the block before it of the same event didn't shows in it, the event
    /// marked ×N. Identical blocks fold (five jolted hits of one grenade, each with its "Bolt Charge gained"; six orbs
    /// picked up), and so does a later one with less (the second kill of one action, after the first consumed Reaper, only
    /// gives Woven Mail again). The statuses on the target don't keep blocks apart when they change nothing they set off
    /// (the kills after the first one's Photonic Flare blinded the pack). A block that sets off something new stays
    /// apart. Blocks collapse at every depth.
    /// </summary>
    private static ImmutableArray<ShapedEvent> CollapseRepeats(ImmutableArray<ShapedEvent> all)
    {
        if (all.IsEmpty)
        {
            return all;
        }

        var depth = all.Min(shaped => shaped.Outcomes.Depth);
        var blocks = SplitBlocks(all, depth)
            .Select(block => (ImmutableArray<ShapedEvent>)[block[0], .. CollapseRepeats(block[1..])]);
        return
        [
            .. blocks
                .Aggregate(ImmutableArray<ImmutableArray<ShapedEvent>>.Empty, (collapsed, next) =>
                    !collapsed.IsEmpty && IsFoldedInto(collapsed[^1], next)
                        ? collapsed.SetItem(collapsed.Length - 1, collapsed[^1].SetItem(0, AddRepeats(collapsed[^1][0], next[0].Outcomes.Repeats)))
                        : collapsed.Add(next))
                .SelectMany(block => block),
        ];
    }

    private static ShapedEvent AddRepeats(ShapedEvent shaped, int repeats) =>
        shaped with { Outcomes = shaped.Outcomes with { Repeats = shaped.Outcomes.Repeats + repeats } };

    /// <summary>The events in blocks: each starts at an event of <paramref name="depth"/> and holds the deeper ones after it.</summary>
    private static ImmutableArray<ImmutableArray<ShapedEvent>> SplitBlocks(ImmutableArray<ShapedEvent> events, int depth) =>
        events.Aggregate(ImmutableArray<ImmutableArray<ShapedEvent>>.Empty, (blocks, shaped) =>
            blocks.IsEmpty || shaped.Outcomes.Depth <= depth
                ? blocks.Add([shaped])
                : blocks.SetItem(blocks.Length - 1, blocks[^1].Add(shaped)));

    /// <summary>A block folds into the one before it when both start with the same event and it sets off nothing that one didn't.</summary>
    private static bool IsFoldedInto(ImmutableArray<ShapedEvent> earlier, ImmutableArray<ShapedEvent> later) =>
        DescribeEventKey(earlier[0]) == DescribeEventKey(later[0])
        && ListBlockFacts(later).IsSubsetOf(ListBlockFacts(earlier));

    /// <summary>What a block shows: each event (a deeper one with its count) and each of its elements' outcomes, reasons and caveats.</summary>
    private static ImmutableHashSet<string> ListBlockFacts(ImmutableArray<ShapedEvent> block) =>
        [.. block.SelectMany((shaped, index) => ListEventFacts(shaped.Outcomes, index == 0 ? DescribeEventKey(shaped) : $"{DescribeEventKey(shaped)}×{shaped.Outcomes.Repeats}"))];

    private static IEnumerable<string> ListEventFacts(EventOutcomes shaped, string key) =>
        shaped.Sources
            .SelectMany(source => source.Outcomes.Select(line => $"{line.Text}{(line.Chance ? "?" : "")}")
                .Concat(source.Reasons.Select(reason => $"why {reason}"))
                .Concat(source.Caveats.Select(caveat => $"· {caveat}"))
                .Select(fact => $"{key}|{source.Source.Value}|{fact}"))
            .Prepend(key);

    private static string DescribeEventKey(ShapedEvent shaped) =>
        $"{shaped.Outcomes.Depth}|{shaped.Kind}";

    private static OutcomeLine ToLine(KeywordGlossary glossary, RuleCatalog catalog, string text, Tone tone, bool chance) =>
        new(text, tone, SplitText(glossary, catalog, text), chance);

    /// <summary>A text in parts, each keyword the glossary knows apart, with the element of its id when the catalog has one.</summary>
    private static ImmutableArray<TextPart> SplitText(KeywordGlossary glossary, RuleCatalog catalog, string text) =>
    [
        .. glossary.SplitKeywords(text).Select(part => new TextPart(
            part.Text,
            part.Keyword.Map(keyword => new KeywordPart(keyword.Affinity, catalog.FindKeywordElement(keyword.Keyword), catalog.FindKeywordIcon(keyword.Keyword))))),
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
            finisher: _ => Optional.None<WeaponSlot>(),
            criticalHealth: _ => Optional.None<WeaponSlot>(),
            shieldBroken: broken => FindOriginWeapon(broken.Origin),
            energyExpended: _ => Optional.None<WeaponSlot>(),
            reloaded: reloaded => FindOriginWeapon(reloaded.Origin),
            hitAtCriticalHealth: _ => Optional.None<WeaponSlot>(),
            slidOverAmmo: slid => FindOriginWeapon(slid.Origin),
            weaponReadied: readied => FindOriginWeapon(readied.Origin));

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

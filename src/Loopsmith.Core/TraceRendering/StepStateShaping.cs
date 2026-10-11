using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>
/// Something present at the end of a step: the keyword it is (a status, a pickup), its name as the state says it
/// ("Bolt Charge (at max)"), its affinity, the element it is when the catalog has one (its details) and the glossary's
/// facts about it ("up to x10 · 15s": shown, never counted down — ADRs D1).
/// </summary>
public sealed record StateEntry(KeywordRef Keyword, string Text, Affinity Affinity, Optional<ElementId> Element, Optional<string> Facts);

/// <summary>A passive whose condition holds right then ("45% damage resistance"), and the element it comes from (Woven Mail).</summary>
public sealed record ActiveEntry(string Text, ElementId Source, string SourceName, Affinity Affinity);

/// <summary>
/// What is present at the end of a step, laid out like the game's HUD: the buffs on you, the buffs on your weapons
/// (Slice, Unraveling Rounds), the debuffs on the pack, the pickups on the ground, and the passives that hold then.
/// </summary>
public sealed record StepState(
    ImmutableArray<StateEntry> Buffs,
    ImmutableArray<StateEntry> Weapons,
    ImmutableArray<StateEntry> Debuffs,
    ImmutableArray<StateEntry> Ground,
    ImmutableArray<ActiveEntry> Active);

/// <summary>Pure: a <see cref="GameState"/> → what the state shows, in the glossary's words (the CLI's state lines, the designer's step state).</summary>
public static class StepStateShaping
{
    public static StepState ShapeStepState(ValidatedBuild build, GameState state, ImmutableArray<ActivePassive> passives)
    {
        var glossary = build.Catalog.Glossary;
        var elements = ElementIndexing.IndexElements(build.Catalog);
        var buffs = state.Buffs.Select(buff => (OnWeapons: IsOnWeapons(glossary, buff.Status), Entry: ShapeBuff(glossary, elements, buff))).ToImmutableArray();
        return new StepState(
            [.. buffs.Where(buff => !buff.OnWeapons).Select(buff => buff.Entry)],
            [.. buffs.Where(buff => buff.OnWeapons).Select(buff => buff.Entry)],
            [.. state.Target.Debuffs.Select(debuff => ShapeDebuff(glossary, elements, debuff))],
            [.. state.Pickups.Select(pickup => ShapePickup(glossary, elements, pickup))],
            [.. ListConditionalPassives(passives).Select(passive => new ActiveEntry(glossary.DescribePassive(passive.Passive.Modifier), passive.Source, passive.SourceName, passive.Affinity))]);
    }

    private static bool IsOnWeapons(KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition) && definition.Kind == KeywordKind.WeaponBuff;

    private static StateEntry ShapeBuff(KeywordGlossary glossary, ElementIndex elements, ActiveBuff buff) =>
        ShapeStatus(glossary, elements, buff.Status, glossary.DescribeActiveBuff(buff));

    private static StateEntry ShapeDebuff(KeywordGlossary glossary, ElementIndex elements, StatusId debuff) =>
        ShapeStatus(glossary, elements, debuff, glossary.DescribeStatus(debuff));

    private static StateEntry ShapeStatus(KeywordGlossary glossary, ElementIndex elements, StatusId status, string text)
    {
        var keyword = new KeywordRef.Status(status);
        var facts = glossary.DescribeStatusFacts(status);
        return new StateEntry(keyword, text, glossary.ReadStatusAffinity(status), elements.FindKeywordElement(keyword), facts.Length > 0 ? Optional.Some(facts) : Optional.None<string>());
    }

    private static StateEntry ShapePickup(KeywordGlossary glossary, ElementIndex elements, PickupId pickup)
    {
        var keyword = new KeywordRef.Pickup(pickup);
        var affinity = glossary.Pickups.TryGetValue(pickup, out var definition) ? definition.Affinity : Affinity.Neutral;
        return new StateEntry(keyword, glossary.DescribePickup(pickup), affinity, elements.FindKeywordElement(keyword), Optional.None<string>());
    }

    /// <summary>Passives whose conditions hold (always-on ones are part of the build, not of the moment).</summary>
    private static IEnumerable<ActivePassive> ListConditionalPassives(ImmutableArray<ActivePassive> passives) =>
        passives.Where(passive => !passive.Passive.When.IsEmpty);
}

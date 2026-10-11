using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>
/// Something present at the end of a step: the keyword it is (a status, a pickup), its name as the state says it
/// ("Bolt Charge (at max)"), its affinity, the element it is when the catalog has one (its details), the glossary's
/// facts about it ("up to x10 · 15s": shown, never counted down — ADRs D1), and what it switches on right then.
/// </summary>
public sealed record StateEntry(
    KeywordRef Keyword,
    string Text,
    Affinity Affinity,
    Optional<ElementId> Element,
    Optional<string> Facts,
    ImmutableArray<SwitchedOnEffect> SwitchesOn);

/// <summary>
/// Another element's passive that a status switches on right then ("+1 Bolt Charge per gain" from Spark of Frequency,
/// while Amplified). A status's own effect (Woven Mail's damage resistance) is in its details, not here.
/// </summary>
public sealed record SwitchedOnEffect(string Text, ElementId Source, string SourceName, Affinity Affinity);

/// <summary>A passive changing the stats of some of the build's weapons right then (by index): shown with those weapons.</summary>
public sealed record WeaponEffect(string Text, ElementId Source, string SourceName, Affinity Affinity, ImmutableArray<int> Weapons);

/// <summary>
/// What is present at the end of a step, laid out like the game's HUD: the buffs on you, the buffs on your weapons
/// (Slice, Unraveling Rounds) and the passives changing those weapons' stats, the debuffs on the pack, the pickups on
/// the ground.
/// </summary>
public sealed record StepState(
    ImmutableArray<StateEntry> Buffs,
    ImmutableArray<StateEntry> Weapons,
    ImmutableArray<WeaponEffect> WeaponEffects,
    ImmutableArray<StateEntry> Debuffs,
    ImmutableArray<StateEntry> Ground);

/// <summary>Pure: a <see cref="GameState"/> → what the state shows, in the glossary's words (the CLI's state lines, the designer's step state).</summary>
public static class StepStateShaping
{
    public static StepState ShapeStepState(ValidatedBuild build, GameState state, ImmutableArray<ActivePassive> passives)
    {
        var glossary = build.Catalog.Glossary;
        var elements = ElementIndexing.IndexElements(build.Catalog);
        var reaching = passives.Select(passive => new SwitchedOnPassive(passive, PassiveReaching.FindReach(build.Build, passive.Passive.Modifier))).ToImmutableArray();
        var buffs = state.Buffs.Select(buff => (OnWeapons: IsOnWeapons(glossary, buff.Status), Entry: ShapeBuff(glossary, elements, reaching, buff))).ToImmutableArray();
        return new StepState(
            [.. buffs.Where(buff => !buff.OnWeapons).Select(buff => buff.Entry)],
            [.. buffs.Where(buff => buff.OnWeapons).Select(buff => buff.Entry)],
            [.. reaching.Where(passive => !passive.Passive.Passive.When.IsEmpty).SelectMany(passive => ToWeaponEffect(glossary, passive))],
            [.. state.Target.Debuffs.Select(debuff => ShapeDebuff(glossary, elements, reaching, debuff))],
            [.. state.Pickups.Select(pickup => ShapePickup(glossary, elements, pickup))]);
    }

    private static bool IsOnWeapons(KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition) && definition.Kind == KeywordKind.WeaponBuff;

    private static StateEntry ShapeBuff(KeywordGlossary glossary, ElementIndex elements, ImmutableArray<SwitchedOnPassive> reaching, ActiveBuff buff) =>
        ShapeStatus(glossary, elements, reaching, buff.Status, glossary.DescribeActiveBuff(buff));

    private static StateEntry ShapeDebuff(KeywordGlossary glossary, ElementIndex elements, ImmutableArray<SwitchedOnPassive> reaching, StatusId debuff) =>
        ShapeStatus(glossary, elements, reaching, debuff, glossary.DescribeStatus(debuff));

    private static StateEntry ShapeStatus(KeywordGlossary glossary, ElementIndex elements, ImmutableArray<SwitchedOnPassive> reaching, StatusId status, string text)
    {
        var keyword = new KeywordRef.Status(status);
        var facts = glossary.DescribeStatusFacts(status);
        return new StateEntry(
            keyword,
            text,
            glossary.ReadStatusAffinity(status),
            elements.FindKeywordElement(keyword),
            facts.Length > 0 ? Optional.Some(facts) : Optional.None<string>(),
            [.. ListSwitchedOn(glossary, reaching, status)]);
    }

    private static StateEntry ShapePickup(KeywordGlossary glossary, ElementIndex elements, PickupId pickup)
    {
        var keyword = new KeywordRef.Pickup(pickup);
        var affinity = glossary.Pickups.TryGetValue(pickup, out var definition) ? definition.Affinity : Affinity.Neutral;
        return new StateEntry(keyword, glossary.DescribePickup(pickup), affinity, elements.FindKeywordElement(keyword), Optional.None<string>(), []);
    }

    /// <summary>What the status switches on that does something in this build (a weapon change none of its weapons takes does nothing).</summary>
    private static IEnumerable<SwitchedOnEffect> ListSwitchedOn(KeywordGlossary glossary, ImmutableArray<SwitchedOnPassive> reaching, StatusId status) =>
        reaching
            .Where(passive => passive.Passive.IsSwitchedOnBy(status) && passive.Reach is not PassiveReach.NoWeapon)
            .Select(passive => new SwitchedOnEffect(glossary.DescribePassive(passive.Passive.Passive.Modifier), passive.Passive.Source, passive.Passive.SourceName, passive.Passive.Affinity));

    private static IEnumerable<WeaponEffect> ToWeaponEffect(KeywordGlossary glossary, SwitchedOnPassive passive) =>
        passive.Reach is PassiveReach.Weapons weapons
            ? [new WeaponEffect(glossary.DescribePassive(passive.Passive.Passive.Modifier), passive.Passive.Source, passive.Passive.SourceName, passive.Passive.Affinity, weapons.Indices)]
            : [];
}

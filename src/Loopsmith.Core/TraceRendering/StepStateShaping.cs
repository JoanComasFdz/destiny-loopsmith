using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>
/// Something present at the end of a step: the keyword it is (a status, a pickup), its name as the state says it
/// ("Bolt Charge (at max)"), its affinity, the element it is when the catalog has one (its details), the glossary's
/// facts about it ("up to x10 · 15s": shown, never counted down — ADRs D1), what it switches on right then, and what its
/// own element does while it is there (Stats for All's stats on its weapon: one chip, not two; Spark of Recharge's faster
/// regeneration).
/// </summary>
public sealed record StateEntry(
    KeywordRef Keyword,
    string Text,
    Affinity Affinity,
    Optional<ElementId> Element,
    Optional<string> Facts,
    ImmutableArray<SwitchedOnEffect> SwitchesOn,
    ImmutableArray<string> OwnEffects);

/// <summary>
/// Another element's passive that a status switches on right then ("+1 Bolt Charge per gain" from Spark of Frequency,
/// while Amplified). A status's own effect is with it (<see cref="StateEntry.OwnEffects"/>), or, for a resistance or a
/// damage bonus (Woven Mail's), in that section of the step.
/// </summary>
public sealed record SwitchedOnEffect(string Text, ElementId Source, string SourceName, Affinity Affinity);

/// <summary>
/// A passive changing the stats of some of the build's weapons right then (by index): shown with those weapons. A status's
/// own (Stats for All's, while Stats for All is there) is shown with the status instead (<see cref="StateEntry.OwnEffects"/>).
/// </summary>
public sealed record WeaponEffect(string Text, ElementId Source, string SourceName, Affinity Affinity, ImmutableArray<int> Weapons);

/// <summary>
/// A counted buff on you (Armor Charge, ADRs D33): its stacks ("x1 · +1 (chance)"), each change since it was last gained
/// from none, with its step ("#6 Orb of Power: +1 → x1"), and how it decays — a fact a loop never applies.
/// </summary>
public sealed record StackCountEntry(StatusId Status, string Name, Affinity Affinity, string Count, ImmutableArray<string> Changes, Optional<string> Decay);

/// <summary>
/// What is present at the end of a step, laid out like the game's HUD: the buffs on you, the buffs on your weapons
/// (Slice, Unraveling Rounds) and the passives changing those weapons' stats, the debuffs on the pack, the pickups on
/// the ground — the stacks of each counted buff and where they came from (<see cref="StackCountEntry"/>), your damage
/// resistance right then, PvE and PvP (<see cref="ResistanceShaping"/>; none without a source), and the damage bonuses
/// holding right then, each source with its own value (<see cref="DamageBonusShaping"/>; none without a source).
/// </summary>
public sealed record StepState(
    ImmutableArray<StateEntry> Buffs,
    ImmutableArray<StateEntry> Weapons,
    ImmutableArray<WeaponEffect> WeaponEffects,
    ImmutableArray<StateEntry> Debuffs,
    ImmutableArray<StateEntry> Ground,
    ImmutableArray<StackCountEntry> Counts,
    Optional<DamageResistance> Resistance,
    ImmutableArray<DamageBonusLine> DamageBonus);

/// <summary>Pure: a <see cref="GameState"/> → what the state shows, in the glossary's words (the trace's state lines, the designer's step state).</summary>
public static class StepStateShaping
{
    public static StepState ShapeStepState(ValidatedBuild build, GameState state, ImmutableArray<ActivePassive> passives)
    {
        var glossary = build.Catalog.Glossary;
        var catalog = build.Catalog;
        var superType = build.ReadSuperType();
        var reaching = passives
            .Select(passive => new ReachingPassive(
                passive,
                PassiveReaching.FindReach(build.Build, passive.Passive.Modifier),
                glossary.DescribePassive(passive.Passive.Modifier, build.CountCopies(passive.Source), superType)))
            .ToImmutableArray();
        var buffs = state.Buffs.Select(buff => (OnWeapons: IsOnWeapons(glossary, buff.Status), Entry: ShapeBuff(build, reaching, buff))).ToImmutableArray();
        var statuses = state.Buffs.Select(buff => buff.Status).Concat(state.Target.Debuffs).ToImmutableArray();
        return new StepState(
            [.. buffs.Where(buff => !buff.OnWeapons).Select(buff => buff.Entry)],
            [.. buffs.Where(buff => buff.OnWeapons).Select(buff => buff.Entry)],
            [
                .. reaching
                    .Where(passive => !passive.Passive.Passive.When.IsEmpty && !statuses.Any(status => passive.Passive.Source.IsStatusElement(status)))
                    .SelectMany(ToWeaponEffect),
            ],
            [.. state.Target.Debuffs.Select(debuff => ShapeDebuff(build, reaching, debuff))],
            [.. state.Pickups.Select(ground => ShapePickup(glossary, catalog, ground))],
            [.. state.Buffs.SelectMany(buff => buff.Tally.Match(tally => new[] { ShapeCount(glossary, buff.Status, tally.Value) }, _ => []))],
            ResistanceShaping.ShapeResistance(build, passives),
            DamageBonusShaping.ShapeDamageBonus(build, passives));
    }

    private static StackCountEntry ShapeCount(KeywordGlossary glossary, StatusId status, StackTally tally) =>
        new(
            status,
            glossary.DescribeStatus(status),
            glossary.ReadStatusAffinity(status),
            DomainPhrasing.DescribeStacks(tally.Sure, tally.Chance),
            [.. tally.Changes.Select(DomainPhrasing.DescribeStackChange)],
            glossary.FindStatus(status).Bind(definition => definition.Decay));

    /// <summary>A passive holding right then, what it reaches in the build, and what it does in the glossary's words.</summary>
    private sealed record ReachingPassive(ActivePassive Passive, PassiveReach Reach, string Text);

    private static bool IsOnWeapons(KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition) && definition.Kind == KeywordKind.WeaponBuff;

    private static StateEntry ShapeBuff(ValidatedBuild build, ImmutableArray<ReachingPassive> reaching, ActiveBuff buff) =>
        ShapeStatus(build, reaching, buff.Status, build.Catalog.Glossary.DescribeActiveBuff(buff));

    private static StateEntry ShapeDebuff(ValidatedBuild build, ImmutableArray<ReachingPassive> reaching, StatusId debuff) =>
        ShapeStatus(build, reaching, debuff, build.Catalog.Glossary.DescribeStatus(debuff));

    private static StateEntry ShapeStatus(ValidatedBuild build, ImmutableArray<ReachingPassive> reaching, StatusId status, string text)
    {
        var glossary = build.Catalog.Glossary;
        var keyword = new KeywordRef.Status(status);
        var facts = build.DescribeStatusFacts(status);
        return new StateEntry(
            keyword,
            text,
            glossary.ReadStatusAffinity(status),
            build.Catalog.FindKeywordElement(keyword),
            facts,
            [.. ListSwitchedOn(reaching, status)],
            [.. ListOwnEffects(reaching, status)]);
    }

    /// <summary>
    /// What the status's own element does while it is there that no other part of the step shows: its changes to the
    /// build's weapons (Stats for All's stats) and its faster regeneration (Spark of Recharge's). Its damage resistance and
    /// damage bonuses are in their own sections (Woven Mail's).
    /// </summary>
    private static IEnumerable<string> ListOwnEffects(ImmutableArray<ReachingPassive> reaching, StatusId status) =>
        reaching
            .Where(passive => passive.Passive.Source.IsStatusElement(status) && IsOwnEffect(passive))
            .Select(passive => passive.Text + (passive.Passive.Likelihood == Likelihood.Chance ? " (chance)" : ""));

    private static bool IsOwnEffect(ReachingPassive passive) =>
        passive.Reach is PassiveReach.Weapons || passive.Passive.Passive.Modifier is Passive.FasterRegeneration;

    /// <summary>A pickup on the ground; one only chance rules dropped says so ("Orb of Power (chance)", ADRs D8).</summary>
    private static StateEntry ShapePickup(KeywordGlossary glossary, RuleCatalog catalog, GroundPickup ground)
    {
        var pickup = ground.Pickup;
        var keyword = new KeywordRef.Pickup(pickup);
        var affinity = glossary.Pickups.TryGetValue(pickup, out var definition) ? definition.Affinity : Affinity.Neutral;
        var text = glossary.DescribePickup(pickup) + (ground.Likelihood == Likelihood.Chance ? " (chance)" : "");
        return new StateEntry(keyword, text, affinity, catalog.FindKeywordElement(keyword), Optional.None<string>(), [], []);
    }

    /// <summary>What the status switches on that does something in this build (a weapon change none of its weapons takes does nothing).</summary>
    private static IEnumerable<SwitchedOnEffect> ListSwitchedOn(ImmutableArray<ReachingPassive> reaching, StatusId status) =>
        reaching
            .Where(passive => passive.Passive.IsSwitchedOnBy(status) && passive.Reach is not PassiveReach.NoWeapon)
            .Select(passive => new SwitchedOnEffect(
                passive.Text + (passive.Passive.Likelihood == Likelihood.Chance ? " (chance)" : ""),
                passive.Passive.Source, passive.Passive.SourceName, passive.Passive.Affinity));

    private static IEnumerable<WeaponEffect> ToWeaponEffect(ReachingPassive passive) =>
        passive.Reach is PassiveReach.Weapons weapons
            ? [new WeaponEffect(passive.Text, passive.Passive.Source, passive.Passive.SourceName, passive.Passive.Affinity, weapons.Indices)]
            : [];
}

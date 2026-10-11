using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.TraceRendering;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.TraceRendering;

/// <summary>
/// A step's outcomes as the web designer draws them: each event, the elements it set off (one each, in the order they
/// fired), each outcome on a line of its own, every keyword it names apart, cascades a level deeper, repeats counted.
/// </summary>
public sealed class StepOutcomeShapingTests
{
    private static readonly PlayerAction GrenadeHit = new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage, TargetCount.One);
    private static readonly TraceOptions Plain = new(false, false, false);
    private static readonly DamageSource Any = new DamageSource.AnySource();

    private static DesignSession Play(ValidatedBuild build, params PlayerAction[] actions) =>
        actions.Aggregate(
            LoopDesigning.CreateSession(build, new LoopDesign(
                "Test", Optional.None<string>(), Optional.None<string>(), Optional.None<CatalogVersion>(), new SourceText("b", ""), [])),
            (session, action) => LoopDesigning.AppendStep(session, action, Optional.None<string>()));

    private static ValidatedBuild ValidateSparkBuild()
    {
        var spark = Element("spark", ElementKind.Fragment, [On(new Trigger.Damage(Any), new Outcome.DebuffTarget(Status("jolt"), Optional.None<Seconds>()))]);
        var vow = Element("vow", ElementKind.Fragment,
            [On(new Trigger.Damage(Any), Buff("bolt-charge"), Energy(AbilityKind.Grenade, new GameValue.Unknown())) with { Reason = Optional.Some("Grenade hits charge you.") }]);
        var boltCharge = Element("bolt-charge", ElementKind.Keyword, [On(new Trigger.BuffGained(Status("bolt-charge")), Energy(AbilityKind.Melee, new GameValue.Known(0.025m)))]);
        var jolt = Element("jolt", ElementKind.Keyword, []);
        return ValidateBuild([spark, vow], keywords: [boltCharge, jolt]);
    }

    [Fact]
    public void Each_element_an_event_set_off_has_its_outcomes_on_lines_of_their_own()
    {
        var session = Play(ValidateSparkBuild(), GrenadeHit);

        var shaped = LoopDesigning.ShapeStepOutcomes(session, session.Resolutions[0], Plain);

        var hit = shaped.Events[0];
        Assert.Equal((0, "Grenade hit"), (hit.Depth, hit.Event));
        Assert.Equal(["spark", "vow"], hit.Sources.Select(source => source.Source.Value));
        var jolted = Assert.Single(hit.Sources[0].Outcomes);
        Assert.StartsWith("Jolt target", jolted.Text);
        Assert.Equal([("Jolt", Affinity.Arc, Optional.Some(ElementId.From("jolt")))], ListKeywords(jolted.Parts));   // the debuff is a keyword: tappable
        Assert.Equal(["+1 Bolt Charge", "+?% grenade energy"], hit.Sources[1].Outcomes.Select(line => line.Text));
        Assert.Equal(["+1 ", "Bolt Charge"], hit.Sources[1].Outcomes[0].Parts.Select(part => part.Text));
        Assert.Equal([("Bolt Charge", Affinity.Arc, Optional.Some(ElementId.From("bolt-charge")))], ListKeywords(hit.Sources[1].Outcomes[0].Parts));
        Assert.Empty(ListKeywords(hit.Sources[1].Outcomes[1].Parts));
        Assert.Empty(hit.Sources[1].Reasons);                                                    // only when asked for
    }

    [Fact]
    public void A_cascade_is_an_event_a_level_deeper_that_names_its_keyword()
    {
        var session = Play(ValidateSparkBuild(), GrenadeHit);

        var shaped = LoopDesigning.ShapeStepOutcomes(session, session.Resolutions[0], Plain with { ShowReasons = true });

        var gained = Assert.Single(shaped.Events, shapedEvent => shapedEvent.Depth == 1);
        Assert.Equal("Bolt Charge gained", gained.Event);
        Assert.Equal([("Bolt Charge", Affinity.Arc, Optional.Some(ElementId.From("bolt-charge")))], ListKeywords(gained.Parts));
        Assert.Equal(["+2.5% melee energy"], Assert.Single(gained.Sources).Outcomes.Select(line => line.Text));
        Assert.Equal(["Grenade hits charge you."], shaped.Events[0].Sources[1].Reasons);
    }

    [Fact]
    public void The_same_event_in_a_row_is_shown_once_and_counted()
    {
        var spawner = Element("spawner", ElementKind.Fragment, [On(new Trigger.KillAny(Any), new Outcome.Spawn(Pickup("orb-of-power"), 1))]);
        var threeKills = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.From(3));
        var session = Play(ValidateBuild([spawner]), threeKills);

        var shaped = LoopDesigning.ShapeStepOutcomes(session, session.Resolutions[0], Plain);

        var killed = Assert.Single(shaped.Events);
        Assert.Equal((3, "spawner"), (killed.Repeats, Assert.Single(killed.Sources).Source.Value));
        Assert.Equal(
            [("Orb of Power", Affinity.Neutral, Optional.None<ElementId>())],                     // a keyword still, with no orb element in this catalog
            ListKeywords(Assert.Single(killed.Sources[0].Outcomes).Parts));
    }

    [Fact]
    public void An_element_that_gives_way_says_so_on_its_line()
    {
        var first = Element("first", ElementKind.Fragment, [On(new Trigger.Damage(Any), Buff("amplified"))]);
        var second = Element("second", ElementKind.Fragment, [On(new Trigger.Damage(Any), Buff("amplified")).NotStackingWith("first")]);
        var session = Play(ValidateBuild([first, second]), GrenadeHit);

        var shaped = LoopDesigning.ShapeStepOutcomes(session, session.Resolutions[0], Plain);

        var line = Assert.Single(shaped.Events[0].Sources, source => source.Source.Value == "second").Outcomes.Single();
        Assert.Equal(("doesn't stack with First", Tone.Warning), (line.Text, line.Tone));
    }

    [Fact]
    public void An_element_whose_rules_fired_apart_shows_at_each_place_in_the_engines_order()
    {
        var first = Element("first", ElementKind.Fragment,
            [On(new Trigger.Damage(Any), new Outcome.DebuffTarget(Status("jolt"), Optional.None<Seconds>())), On(new Trigger.Damage(Any), Energy(AbilityKind.Grenade, new GameValue.Unknown()))]);
        var second = Element("second", ElementKind.Fragment, [On(new Trigger.Damage(Any), Buff("amplified"))]);
        var session = Play(ValidateBuild([first, second]), GrenadeHit);

        var shaped = LoopDesigning.ShapeStepOutcomes(session, session.Resolutions[0], Plain);

        Assert.Equal(["first", "second", "first"], shaped.Events[0].Sources.Select(source => source.Source.Value));   // debuff, empower, refund
    }

    [Fact]
    public void A_keywords_strike_names_its_keyword()
    {
        var spark = Element("spark", ElementKind.Fragment, [On(new Trigger.Damage(Any), new Outcome.StrikeTarget(Status("jolt"), HitOutcome.Damage))]);
        var chained = Element("chained", ElementKind.Fragment, [On(new Trigger.Damage(new DamageSource.OfType(DamageType.Arc)), Buff("amplified"))]);
        var jolt = Element("jolt", ElementKind.Keyword, []);
        var session = Play(ValidateBuild([spark, chained], keywords: [jolt]), GrenadeHit);

        var shaped = LoopDesigning.ShapeStepOutcomes(session, session.Resolutions[0], Plain);

        Assert.Contains(shaped.Events, shapedEvent => shapedEvent.Depth == 1
            && ListKeywords(shapedEvent.Parts).Any(keyword => keyword.Element == Optional.Some(ElementId.From("jolt"))));
    }

    [Fact]
    public void A_weapons_hit_or_kill_names_its_weapon_slot_an_abilitys_none()
    {
        var spawner = Element("spawner", ElementKind.Fragment, [On(new Trigger.KillAny(Any), new Outcome.Spawn(Pickup("orb-of-power"), 1))]);
        var session = Play(ValidateBuild([spawner]), new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One), new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One));

        var shot = LoopDesigning.ShapeStepOutcomes(session, session.Resolutions[0], Plain);
        var thrown = LoopDesigning.ShapeStepOutcomes(session, session.Resolutions[1], Plain);

        Assert.Equal(Optional.Some(WeaponSlot.Energy), Assert.Single(shot.Events).Weapon);
        Assert.Equal(Optional.None<WeaponSlot>(), Assert.Single(thrown.Events).Weapon);
    }

    [Fact]
    public void A_keyword_is_a_whole_word_and_a_debuff_also_by_its_adjective()
    {
        var parts = Glossary.SplitKeywords("Kill on Jolted target near an Ionic Traceless Jolt");

        Assert.Equal(["Kill on ", "Jolted", " target near an Ionic Traceless ", "Jolt"], parts.Select(part => part.Text));
        Assert.Equal([false, true, false, true], parts.Select(part => part.Keyword.IsSome()));
    }

    private static ImmutableArray<(string Text, Affinity Affinity, Optional<ElementId> Element)> ListKeywords(ImmutableArray<TextPart> parts) =>
    [
        .. parts.SelectMany(part => part.Keyword.Match(
            keyword => new[] { (part.Text, keyword.Value.Affinity, keyword.Value.Element) },
            _ => [])),
    ];
}

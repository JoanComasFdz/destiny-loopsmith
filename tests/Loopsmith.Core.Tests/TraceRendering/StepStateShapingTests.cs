using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Core.Tests.TraceRendering;

/// <summary>
/// The state at the end of a step, laid out like the game's HUD: buffs on you, buffs on your weapons (a glossary
/// <c>weaponBuff</c>: Slice, Unraveling Rounds), debuffs on the pack, pickups on the ground, the passives that hold.
/// </summary>
public sealed class StepStateShapingTests
{
    private static readonly string RulesDirectory = RepoFiles.ToPath("rules");                                   // pure

    private static readonly Result<ImmutableArray<SourceText>, string> RuleFiles = FileSourceFetching.ReadRuleFiles(RulesDirectory);   // impure

    private static readonly RuleCatalog Catalog =
        RuleFiles.Bind(CatalogLoading.ParseCatalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    [Fact]
    public void A_weapon_buff_sits_with_the_weapons_and_names_the_weapon_whose_perk_gives_it()
    {
        var played = Play(
            new PlayerAction.UseClassAbility(),
            new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One));

        var state = LoopDesigning.ShapeStepState(played, played.Resolutions[1]);

        Assert.Equal(["Slice"], state.Weapons.Select(entry => entry.Text));
        Assert.DoesNotContain(state.Buffs, entry => entry.Text == "Slice");
        Assert.Contains(state.Debuffs, entry => entry.Text == "Sever" && entry.Affinity == Affinity.Strand);
        Assert.Contains(state.Ground, entry => entry.Keyword == new KeywordRef.Pickup(PickupId.From("orb-of-power")));
        Assert.Equal([0], LoopDesigning.ListWeaponsGiving(played.Build, StatusId.From("slice")));             // Festival Flight rolls Slice
        Assert.Empty(LoopDesigning.ListWeaponsGiving(played.Build, StatusId.From("unraveling-rounds")));     // an artifact perk gives it
    }

    [Fact]
    public void An_entry_carries_its_element_and_the_glossarys_facts_and_a_passive_that_holds_is_active()
    {
        var played = Play(
            new PlayerAction.UseClassAbility(),
            new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One));

        var state = LoopDesigning.ShapeStepState(played, played.Resolutions[1]);

        var slice = Assert.Single(state.Weapons);
        Assert.Equal((Optional.Some(ElementId.From("slice")), Optional.Some("up to x5 · 8s")), (slice.Element, slice.Facts));
        var woven = Assert.Single(state.Buffs, entry => entry.Text == "Woven Mail");
        Assert.Equal(Optional.Some(ElementId.From("woven-mail")), woven.Element);
        Assert.Contains(state.Active, active => active.SourceName == "Woven Mail" && active.Text.EndsWith("damage resistance", StringComparison.Ordinal));
    }

    [Fact]
    public void A_weapon_buff_is_a_buff_to_the_engine_it_can_be_declared_at_max_and_ended()
    {
        var played = Play(
            new PlayerAction.UseClassAbility(),
            new PlayerAction.Declare(new StateDeclaration.ReachMax(StatusId.From("slice"))),
            new PlayerAction.UseClassAbility(),
            new PlayerAction.Declare(new StateDeclaration.EndStatus(StatusId.From("slice"))));

        Assert.All(played.Resolutions, resolution => Assert.False(resolution.Blocked.IsSome()));
        Assert.Contains(played.Resolutions[1].Fired, fired => fired.Source.Value == "slice");                 // its 5 hits done: Slice ends
        Assert.Empty(LoopDesigning.ShapeStepState(played, played.Resolutions[3]).Weapons);
    }

    [Fact]
    public void The_CLIs_state_lines_follow_the_same_layout()
    {
        var played = Play(new PlayerAction.UseClassAbility());

        var lines = TraceRenderer.RenderState(played.Build, played.Resolutions[0].State, played.Resolutions[0].ActivePassives).Select(line => line.ToPlainText().Trim());

        Assert.Equal(["Buffs         Reaper", "Weapons       Slice", "Debuffs       none", "Ground        none"], lines);
    }

    private static DesignSession Play(params PlayerAction[] actions)
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        var file = RepoFiles.ToPath(path);                                                       // pure
        var json = File.ReadAllText(file);                                                       // impure
        var session = LoopDesigning.StartSavedDimDesign(Catalog, new SourceText(path, json))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        return actions.Aggregate(session, (played, action) => LoopDesigning.AppendStep(played, action, Optional.None<string>()));
    }
}

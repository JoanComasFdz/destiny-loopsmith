using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// Editing the subclass in the designer: what each subclass can hold comes from the manifest excerpt, the aspects open the
/// fragment slots, a saved edit rewrites only the embedded build's subclass entries and the steps replay with it.
/// </summary>
public sealed class SubclassEditingTests
{
    private static readonly ItemHash TempestStrike = ItemHash.From(4194622037u);
    private static readonly ItemHash FlowState = ItemHash.From(4194622036u);
    private static readonly ItemHash LethalCurrent = ItemHash.From(4194622038u);
    private static readonly ItemHash Knockout = ItemHash.From(1656549674u);         // a Striker's aspect: the rules don't know it
    private static readonly ItemHash Ascension = ItemHash.From(4194622039u);
    private static readonly ItemHash SparkOfShock = ItemHash.From(1727069364u);
    private static readonly ItemHash SparkOfVolts = ItemHash.From(3277705904u);
    private static readonly ItemHash BlinkJump = ItemHash.From(95544329u);
    private static readonly ItemHash SkipGrenade = ItemHash.From(146194908u);

    private static readonly RuleCatalog Catalog =
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    [Fact]
    public void Arcstrider_lists_everything_it_can_hold_by_slot_from_the_manifest()
    {
        var build = OpenLoop("helicopter-skip-grenades").Build;

        Assert.True(LoopDesigning.CanEditSubclass(build));
        Assert.Equal(3, LoopDesigning.ListSubclassOptions(build, SubclassSlot.Super).Length);
        Assert.Equal(7, LoopDesigning.ListSubclassOptions(build, SubclassSlot.Grenade).Length);
        Assert.Equal(2, LoopDesigning.ListSubclassOptions(build, SubclassSlot.Melee).Length);
        Assert.Equal(2, LoopDesigning.ListSubclassOptions(build, SubclassSlot.ClassAbility).Length);
        Assert.Equal(4, LoopDesigning.ListSubclassOptions(build, SubclassSlot.Jump).Length);
        Assert.Equal([FlowState, TempestStrike, LethalCurrent, Ascension], LoopDesigning.ListSubclassOptions(build, SubclassSlot.Aspect).Order());
        Assert.Equal(16, LoopDesigning.ListSubclassOptions(build, SubclassSlot.Fragment).Length);
    }

    [Fact]
    public void The_builds_plugs_are_read_by_hash_whether_the_rules_know_them_or_not()
    {
        var plugs = LoopDesigning.ReadSubclassPlugs(OpenLoop("helicopter-skip-grenades").Build);

        Assert.Equal(Optional.Some(ItemHash.From(3769507633u)), plugs.Super);                       // Arc Staff
        Assert.Equal(Optional.Some(ItemHash.From(426473317u)), plugs.ClassAbility);                 // Gambler's Dodge: this subclass's copy
        Assert.Equal([TempestStrike, Ascension], plugs.Aspects);
        Assert.Equal(4, plugs.Fragments.Length);
        Assert.Equal(Optional.None<ItemHash>(), plugs.Jump);
    }

    [Fact]
    public void The_aspects_open_the_fragment_slots_and_an_unknown_count_is_not_a_number()
    {
        var build = OpenLoop("helicopter-skip-grenades").Build;

        Assert.Equal(Optional.Some(4), LoopDesigning.CountFragmentSlots(build, [TempestStrike, FlowState]).Slots);
        Assert.Equal(Optional.Some(5), LoopDesigning.CountFragmentSlots(build, [TempestStrike, Ascension]).Slots);
        Assert.Equal(Optional.Some(2), LoopDesigning.CountFragmentSlots(build, [TempestStrike]).Slots);
        Assert.Equal(Optional.Some(0), LoopDesigning.CountFragmentSlots(build, []).Slots);
        Assert.Equal(Optional.Some(4), LoopDesigning.CountFragmentSlots(build, [TempestStrike, LethalCurrent]).Slots);
        Assert.Equal(Optional.None<int>(), LoopDesigning.CountFragmentSlots(build, [TempestStrike, Knockout]).Slots);
    }

    [Fact]
    public void An_aspect_change_that_closes_slots_removes_the_last_fragments_and_names_them()
    {
        var build = OpenLoop("helicopter-skip-grenades").Build;
        var plugs = LoopDesigning.ReadSubclassPlugs(build);

        var trimmed = LoopDesigning.SetSubclassAspects(build, plugs, [TempestStrike]);
        var kept = LoopDesigning.SetSubclassAspects(build, plugs, [TempestStrike, FlowState]);
        var unknown = LoopDesigning.SetSubclassAspects(build, plugs, [TempestStrike, Knockout]);

        Assert.Equal(plugs.Fragments.Take(2), trimmed.Plugs.Fragments);
        Assert.Equal(plugs.Fragments.Skip(2), trimmed.Removed);
        Assert.Empty(kept.Removed);                                                              // 4 slots hold the 4 fragments
        Assert.Empty(unknown.Removed);                                                           // "?" slots limit nothing
        Assert.Equal(plugs.Fragments, unknown.Plugs.Fragments);
    }

    [Fact]
    public void Saving_swaps_the_aspect_keeps_the_steps_and_the_file_and_replays_without_Ascensions_jolt()
    {
        var session = OpenLoop("helicopter-skip-grenades");
        var plugs = LoopDesigning.ReadSubclassPlugs(session.Build);
        var swapped = plugs with { Aspects = [TempestStrike, FlowState] };

        var saved = Edit(session, swapped);

        Assert.Equal(["tempest-strike", "flow-state"], saved.Build.Build.Aspects.Select(id => id.Value));
        Assert.Equal(session.Design.Steps, saved.Design.Steps);
        Assert.Equal(session.Resolutions.Length, saved.Resolutions.Length);
        Assert.DoesNotContain(saved.Build.Equipped, equipped => equipped.Element.Id.Value == "ascension");
        Assert.Contains("aspects: [tempest-strike, flow-state]", saved.Design.Build.Text, StringComparison.Ordinal);
        Assert.Contains("# Ascension replaces Flow State", saved.Design.Build.Text, StringComparison.Ordinal);   // the rest of the file stays as it was
        Assert.NotEqual(
            session.Resolutions.SelectMany(resolution => resolution.Fired.Select(fired => fired.Source.Value)).Count(source => source == "ascension"),
            saved.Resolutions.SelectMany(resolution => resolution.Fired.Select(fired => fired.Source.Value)).Count(source => source == "ascension"));
    }

    [Fact]
    public void Every_Arcstrider_plug_is_the_builds_a_jump_too_and_survives_the_file()
    {
        var session = OpenLoop("helicopter-skip-grenades");
        var plugs = LoopDesigning.ReadSubclassPlugs(session.Build);

        var saved = Edit(session, plugs with { Aspects = [TempestStrike, LethalCurrent], Jump = Optional.Some(BlinkJump), Fragments = [SparkOfShock, SparkOfVolts] });
        var reopened = LoopDesigning.ImportLoop(Catalog, new SourceText("x.loop.yaml", LoopDesigning.ExportLoop(saved)))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Equal(["tempest-strike", "lethal-current"], saved.Build.Build.Aspects.Select(id => id.Value));
        Assert.Equal(["spark-of-shock", "spark-of-volts"], saved.Build.Build.Fragments.Select(id => id.Value));
        Assert.DoesNotContain(saved.Build.Build.LeftOut, item => item.Part == LoadoutPart.SubclassPlug);
        Assert.Equal<AbilityPlug>(new AbilityPlug.Known(ElementId.From("hunter-blink")), saved.Build.Build.Abilities.Jump);
        Assert.Equal(Optional.Some(BlinkJump), LoopDesigning.ReadSubclassPlugs(reopened.Build).Jump);
        var before = LoopDesigning.ReadSubclassPlugs(saved.Build);
        var after = LoopDesigning.ReadSubclassPlugs(reopened.Build);
        Assert.Equal([before.Super, before.Grenade, before.Melee, before.ClassAbility, before.Jump], [after.Super, after.Grenade, after.Melee, after.ClassAbility, after.Jump]);
        Assert.Equal(before.Aspects, after.Aspects);
        Assert.Equal(before.Fragments, after.Fragments);   // the export reopens as the same build
    }

    [Fact]
    public void Picking_a_jump_writes_it_after_the_class_ability_as_the_builds_jump()
    {
        var session = StartBuild(GuardianClass.Hunter, Subclass.Arc);
        var plugs = LoopDesigning.ReadSubclassPlugs(session.Build);

        var saved = Edit(session, plugs with { Jump = Optional.Some(BlinkJump) });

        Assert.Equal(Optional.Some(ElementId.From("hunter-blink")), LoopDesigning.FindSubclassElement(saved.Build, BlinkJump).Map(element => element.Id));
        Assert.Equal<AbilityPlug>(new AbilityPlug.Known(ElementId.From("hunter-blink")), saved.Build.Build.Abilities.Jump);
        Assert.Contains("jump: hunter-blink", saved.Design.Build.Text, StringComparison.Ordinal);
        Assert.Empty(saved.Build.Build.LeftOut);
        Assert.DoesNotContain(saved.Build.Issues, issue => issue.Severity != Severity.Info);
        Assert.All(LoopDesigning.ListSubclassOptions(saved.Build, SubclassSlot.Jump), jump => Assert.True(LoopDesigning.FindSubclassElement(saved.Build, jump).IsSome()));
    }

    [Fact]
    public void A_file_that_left_its_jump_out_still_opens_with_it_and_a_save_makes_it_the_builds_jump()
    {
        var session = StartBuild(GuardianClass.Hunter, Subclass.Arc, "leftOut: { subclassPlugs: [95544329] }");
        var plugs = LoopDesigning.ReadSubclassPlugs(session.Build);

        var saved = Edit(session, plugs);

        Assert.Equal<AbilityPlug>(new AbilityPlug.Empty(), session.Build.Build.Abilities.Jump);
        Assert.Equal(Optional.Some(BlinkJump), plugs.Jump);
        Assert.Empty(LoopDesigning.ListMissing(session.Build));
        Assert.Equal<AbilityPlug>(new AbilityPlug.Known(ElementId.From("hunter-blink")), saved.Build.Build.Abilities.Jump);
        Assert.Empty(saved.Build.Build.LeftOut);
        Assert.DoesNotContain("leftOut", saved.Design.Build.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_jump_of_a_subclass_the_rules_dont_have_stays_left_out()
    {
        var session = StartBuild(GuardianClass.Hunter, Subclass.Solar);
        var jump = LoopDesigning.ListSubclassOptions(session.Build, SubclassSlot.Jump)[0];
        var plugs = LoopDesigning.ReadSubclassPlugs(session.Build);

        var saved = Edit(session, plugs with { Jump = Optional.Some(jump) });

        Assert.False(LoopDesigning.FindSubclassElement(session.Build, jump).IsSome());
        Assert.Equal<AbilityPlug>(new AbilityPlug.Empty(), saved.Build.Build.Abilities.Jump);
        Assert.Equal([new LeftOutItem(LoadoutPart.SubclassPlug, jump)], saved.Build.Build.LeftOut);
        Assert.IsType<LeftOutReason.NoLoopEffect>(LoopDesigning.ClassifyLeftOut(saved.Build, saved.Build.Build.LeftOut[0]));
        Assert.DoesNotMatch("(?m)^jump:", saved.Design.Build.Text);
        Assert.Equal(Optional.Some(jump), LoopDesigning.ReadSubclassPlugs(saved.Build).Jump);
    }

    [Fact]
    public void An_empty_slot_is_written_and_read_back_as_empty()
    {
        var session = OpenLoop("helicopter-skip-grenades");
        var plugs = LoopDesigning.ReadSubclassPlugs(session.Build);

        var saved = Edit(session, plugs with { Aspects = [TempestStrike], Fragments = ImmutableArray<ItemHash>.Empty, Super = Optional.None<ItemHash>() });

        var reopened = LoopDesigning.ImportLoop(Catalog, new SourceText("x.loop.yaml", LoopDesigning.ExportLoop(saved)))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Equal<AbilityPlug>(new AbilityPlug.Empty(), saved.Build.Build.Abilities.Super);
        Assert.DoesNotMatch("(?m)^super:", saved.Design.Build.Text);
        Assert.Contains("fragments: []", saved.Design.Build.Text, StringComparison.Ordinal);
        Assert.Empty(saved.Build.Build.Fragments);
        Assert.Equal<AbilityPlug>(new AbilityPlug.Empty(), reopened.Build.Build.Abilities.Super);
    }

    [Fact]
    public void A_plug_twice_a_plug_the_subclass_lacks_and_too_many_fragments_are_refused()
    {
        var session = OpenLoop("helicopter-skip-grenades");
        var plugs = LoopDesigning.ReadSubclassPlugs(session.Build);

        var twice = LoopDesigning.EditSubclass(session, Optional.None<BungieProfile>(), plugs with { Fragments = [SparkOfShock, SparkOfShock] });
        var foreign = LoopDesigning.EditSubclass(session, Optional.None<BungieProfile>(), plugs with { Grenade = Optional.Some(ItemHash.From(1u)) });
        var crowded = LoopDesigning.EditSubclass(session, Optional.None<BungieProfile>(), plugs with { Aspects = [TempestStrike] });

        Assert.Equal(new Result<DesignSession, string>.Error("A plug can be equipped once."), twice);
        Assert.IsType<Result<DesignSession, string>.Error>(foreign);
        Assert.Equal(new Result<DesignSession, string>.Error("4 fragments don't fit the 2 slots the aspects open."), crowded);
    }

    [Theory]
    [InlineData(GuardianClass.Hunter, Subclass.Arc)]
    [InlineData(GuardianClass.Hunter, Subclass.Solar)]
    [InlineData(GuardianClass.Hunter, Subclass.Void)]
    [InlineData(GuardianClass.Titan, Subclass.Arc)]
    [InlineData(GuardianClass.Titan, Subclass.Solar)]
    [InlineData(GuardianClass.Titan, Subclass.Void)]
    [InlineData(GuardianClass.Warlock, Subclass.Arc)]
    [InlineData(GuardianClass.Warlock, Subclass.Solar)]
    [InlineData(GuardianClass.Warlock, Subclass.Void)]
    public void Every_subclass_Loopsmith_opens_lists_what_it_can_hold_in_each_slot(GuardianClass guardianClass, Subclass subclass)
    {
        var build = StartBuild(guardianClass, subclass).Build;

        Assert.True(LoopDesigning.CanEditSubclass(build));
        Assert.All(
            new[] { SubclassSlot.Super, SubclassSlot.Grenade, SubclassSlot.Melee, SubclassSlot.ClassAbility, SubclassSlot.Jump, SubclassSlot.Aspect, SubclassSlot.Fragment },
            slot => Assert.NotEmpty(LoopDesigning.ListSubclassOptions(build, slot)));
    }

    [Fact]
    public void A_Striker_saves_the_Arc_plugs_the_rules_author_for_the_Hunter_as_left_out_and_reopens_with_them()
    {
        var session = StartBuild(GuardianClass.Titan, Subclass.Arc);
        ItemHash Find(SubclassSlot slot, string name) =>
            LoopDesigning.ListSubclassOptions(session.Build, slot).Single(hash => Catalog.Manifest.FindItem(hash).Map(item => item.Name) == Optional.Some(name));
        var plugs = new SubclassPlugs(
            Optional.Some(Find(SubclassSlot.Super, "Thundercrash")),
            Optional.Some(SkipGrenade),                                                          // the Hunter's Skip Grenade in the rules: the same plug
            Optional.Some(Find(SubclassSlot.Melee, "Thunderclap")),
            Optional.Some(Find(SubclassSlot.ClassAbility, "Thruster")),
            Optional.None<ItemHash>(),
            [Find(SubclassSlot.Aspect, "Knockout"), Find(SubclassSlot.Aspect, "Juggernaut")],
            [SparkOfShock, SparkOfVolts]);

        var saved = Edit(session, plugs);
        var reopened = LoopDesigning.ImportLoop(Catalog, new SourceText("x.loop.yaml", LoopDesigning.ExportLoop(saved)))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Equal<AbilityPlug>(new AbilityPlug.Unknown(), saved.Build.Build.Abilities.Grenade);
        Assert.Empty(saved.Build.Build.Fragments);
        Assert.Contains(saved.Build.Build.LeftOut, item => item == new LeftOutItem(LoadoutPart.SubclassPlug, SkipGrenade));
        Assert.Contains(saved.Build.Build.LeftOut, item => item == new LeftOutItem(LoadoutPart.SubclassPlug, SparkOfShock));
        Assert.Equal(Optional.None<BuildElement>(), LoopDesigning.FindSubclassElement(saved.Build, SparkOfShock));
        var after = LoopDesigning.ReadSubclassPlugs(reopened.Build);
        Assert.Equal([plugs.Super, plugs.Grenade, plugs.Melee, plugs.ClassAbility], [after.Super, after.Grenade, after.Melee, after.ClassAbility]);
        Assert.Equal(plugs.Aspects, after.Aspects);
        Assert.Equal(plugs.Fragments, after.Fragments);
    }

    private static DesignSession StartBuild(GuardianClass guardianClass, Subclass subclass, string extra = "")
    {
        var text = $"""
            name: Test
            class: {guardianClass.ToString().ToLowerInvariant()}
            subclass: {subclass.ToString().ToLowerInvariant()}
            super: "?"
            grenade: "?"
            melee: "?"
            classAbility: "?"
            {extra}
            """;
        return LoopDesigning.StartDesign(Catalog, new SourceText("build.yaml", text), "Test")
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
    }

    private static DesignSession Edit(DesignSession session, SubclassPlugs plugs) =>
        LoopDesigning.EditSubclass(session, Optional.None<BungieProfile>(), plugs)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static DesignSession OpenLoop(string name)
    {
        var path = name == "helicopter-skip-grenades"
            ? "builds/skip-grenade-hunter-ascension/loops/helicopter-skip-grenades.loop.yaml"
            : $"builds/skip-grenade-hunter/loops/{name}.loop.yaml";
        return LoopDesigning.ImportLoop(Catalog, new SourceText(path, File.ReadAllText(RepoFiles.ToPath(path))))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
    }
}

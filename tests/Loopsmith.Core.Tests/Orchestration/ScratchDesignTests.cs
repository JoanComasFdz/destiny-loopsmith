using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// A loop started from scratch: an Arc Hunter with nothing equipped, whose empty slots stay empty (not "?") through the
/// subclass editor, export and import; it has nothing to trigger until the editor or a weapon gives it something.
/// </summary>
public sealed class ScratchDesignTests
{
    private static readonly ItemHash ArcStaff = ItemHash.From(3769507633u);
    private static readonly ItemHash SkipGrenade = ItemHash.From(146194908u);

    private static readonly RuleCatalog Catalog =
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static readonly AbilityLoadout NoAbilities =
        new(new AbilityPlug.Empty(), new AbilityPlug.Empty(), new AbilityPlug.Empty(), new AbilityPlug.Empty(), new AbilityPlug.Empty());

    [Fact]
    public void A_loop_from_scratch_is_an_arc_hunter_with_nothing_equipped()
    {
        var session = StartScratch();
        var build = session.Build.Build;

        Assert.Equal(("Arc Hunter loop", "Arc Hunter", GuardianClass.Hunter, Subclass.Arc), (session.Design.Name, build.Name, build.Class, build.Subclass));
        Assert.Empty(session.Design.Steps);
        Assert.Equal(NoAbilities, build.Abilities);
        Assert.Empty(build.Aspects);
        Assert.Empty(build.Fragments);
        Assert.Equal(Optional.None<ElementId>(), build.ExoticArmor);
        Assert.Empty(build.ArmorSetBonuses);
        Assert.Empty(build.ArmorMods);
        Assert.Empty(build.ArtifactPerks);
        Assert.Empty(build.Weapons);
        Assert.Empty(build.ArmorCopies);
        Assert.Empty(build.LeftOut);
        Assert.True(LoopDesigning.CanEditSubclass(session.Build));
    }

    [Fact]
    public void Its_empty_abilities_are_not_called_unknown()
    {
        var issues = LoopDesigning.ListDesignIssues(StartScratch());

        Assert.DoesNotContain(issues, issue => issue.Message.Contains(DomainPhrasing.Unknown, StringComparison.Ordinal));
        Assert.DoesNotContain(issues, issue => issue.Severity == Severity.Blocking);
    }

    [Fact]
    public void It_has_nothing_to_trigger_until_the_subclass_editor_gives_it_an_ability()
    {
        var session = StartScratch();
        var plugs = LoopDesigning.ReadSubclassPlugs(session.Build);

        var edited = LoopDesigning.EditSubclass(session, Optional.None<BungieProfile>(), plugs with { Super = Optional.Some(ArcStaff), Grenade = Optional.Some(SkipGrenade) })
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.False(LoopDesigning.HasTriggerSource(session.Build));
        Assert.True(LoopDesigning.HasTriggerSource(edited.Build));
        Assert.Equal(
            new AbilityLoadout(
                new AbilityPlug.Known(ElementId.From("arc-staff")),
                new AbilityPlug.Known(ElementId.From("skip-grenade")),
                new AbilityPlug.Empty(),
                new AbilityPlug.Empty(),
                new AbilityPlug.Empty()),
            edited.Build.Build.Abilities);
    }

    [Fact]
    public void A_build_with_a_weapon_or_an_unknown_ability_has_something_to_trigger()
    {
        var weapon = new WeaponLoadout(WeaponSlot.Kinetic, "Festival Flight", DamageType.Strand, Optional.None<string>(), [], Optional.None<ItemHash>(), [], false, Optional.None<ItemInstanceId>());
        var scratch = StartScratch().Build.Build;

        var armed = Validate(scratch with { Weapons = [weapon] });
        var unknown = Validate(scratch with { Abilities = NoAbilities with { Melee = new AbilityPlug.Unknown() } });

        Assert.True(LoopDesigning.HasTriggerSource(armed));
        Assert.True(LoopDesigning.HasTriggerSource(unknown));
    }

    [Fact]
    public void Its_empty_slots_stay_empty_through_export_and_import()
    {
        var session = StartScratch();

        var file = LoopDesigning.ExportLoop(session);
        var reopened = LoopDesigning.ImportLoop(Catalog, new SourceText("arc-hunter.loop.yaml", file))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.DoesNotContain(DomainPhrasing.Unknown, session.Design.Build.Text, StringComparison.Ordinal);
        Assert.Equal(NoAbilities, reopened.Build.Build.Abilities);
        Assert.Equal("Arc Hunter loop", reopened.Design.Name);
        Assert.Empty(reopened.Build.Build.Weapons);
    }

    private static ValidatedBuild Validate(Build build) =>
        BuildValidation.ValidateBuild(build, Catalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static DesignSession StartScratch() =>
        LoopDesigning.StartScratchDesign(Catalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}

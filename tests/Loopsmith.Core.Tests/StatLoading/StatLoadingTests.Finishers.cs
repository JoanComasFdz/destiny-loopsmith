using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Core.Tests.StatLoading;

/// <summary>
/// The build's finisher, as the HUD shows it (docs/loop-format.md, "Next trigger"): the one the player's character of
/// the build's class has equipped, after a Bungie login; else the class's own (Blade Carnival for a Hunter, the
/// glossary's), the one the game plays when none is equipped.
/// </summary>
public sealed partial class StatLoadingTests
{
    private const uint FinishersBucket = 3683254069;
    private static readonly ItemHash BladeCarnival = ItemHash.From(4012203003u);   // the Hunter's own
    private static readonly ItemHash FocusedFist = ItemHash.From(2854370169u);     // the Titan's own, in the excerpt
    private static readonly ItemHash Dropkick = ItemHash.From(2504641454u);        // Golden Age Dropkick: not in the excerpt

    [Fact]
    public void Every_class_has_its_own_finisher_in_the_glossary_and_the_excerpt()
    {
        Assert.Equal(
            [(GuardianClass.Hunter, "Blade Carnival"), (GuardianClass.Titan, "The Focused Fist"), (GuardianClass.Warlock, "Palm of Wen Jie")],
            Catalog.Glossary.Finishers.Select(finisher => (finisher.Class, finisher.Name)));
        Assert.All(Catalog.Glossary.Finishers, finisher => Assert.True(
            Catalog.FindClassFinisher(finisher.Class).Bind(item => item.Icon).IsSome(), $"{finisher.Name} isn't in rules/manifest.yaml with its icon: rerun tools/manifest/"));
    }

    [Fact]
    public void The_profile_gives_the_finisher_each_character_has_equipped_and_no_other()
    {
        var profile = ReadProfile(
            [
                new FakeItem(Dropkick, "f1", "equipped", [], null, Bucket: FinishersBucket),
                new FakeItem(FocusedFist, "f2", "carried", [], null, Bucket: FinishersBucket),
                new FakeItem(BladeCarnival, "f3", "vault", [], null, Bucket: FinishersBucket),
                new FakeItem(Mask, "1", "equipped", [FullMasterwork], 11),
            ]);

        Assert.Equal([new EquippedFinisher(GuardianClass.Hunter, Dropkick)], profile.Finishers);
        Assert.Empty(profile.Weapons);
        Assert.Single(profile.Armor);
    }

    [Fact]
    public void The_finisher_is_the_players_equipped_one_when_the_excerpt_names_it_else_the_classs_own()
    {
        var build = OpenOwnersBuild();
        var none = Optional.None<BungieProfile>();
        var focused = Optional.Some(new BungieProfile([], [], [new EquippedFinisher(GuardianClass.Hunter, FocusedFist)]));
        var titans = Optional.Some(new BungieProfile([], [], [new EquippedFinisher(GuardianClass.Titan, FocusedFist)]));
        var unknown = Optional.Some(new BungieProfile([], [], [new EquippedFinisher(GuardianClass.Hunter, Dropkick)]));

        Assert.Equal(Optional.Some("Blade Carnival"), LoopDesigning.FindFinisher(build, none).Map(item => item.Name));
        Assert.True(LoopDesigning.FindFinisher(build, none).Bind(item => item.Icon).IsSome());
        Assert.Equal(Optional.Some("The Focused Fist"), LoopDesigning.FindFinisher(build, focused).Map(item => item.Name));
        Assert.Equal(Optional.Some("Blade Carnival"), LoopDesigning.FindFinisher(build, titans).Map(item => item.Name));   // a Titan's isn't the Hunter's
        Assert.Equal(Optional.Some("Blade Carnival"), LoopDesigning.FindFinisher(build, unknown).Map(item => item.Name));  // until the full manifest names it
    }

    [Fact]
    public void A_finisher_step_shows_the_finishers_icon_and_the_login_asks_bungie_for_an_unknown_finisher()
    {
        var build = OpenOwnersBuild();
        var finisher = new PlayerAction.PerformFinisher();
        var own = Catalog.Manifest.FindItem(BladeCarnival).Bind(item => item.Icon).Map(IconFinding.ToManifestIcon);
        var focused = Catalog.Manifest.FindItem(FocusedFist).Bind(item => item.Icon).Map(IconFinding.ToManifestIcon);
        var profile = new BungieProfile([], [], [new EquippedFinisher(GuardianClass.Hunter, Dropkick)]);
        var session = OpenOwnersSession(Catalog);

        Assert.True(own.IsSome());
        Assert.Equal(own, IconFinding.FindActionIcon(Catalog, build.Build, finisher));
        Assert.Equal(own, LoopDesigning.FindActionIcon(build, Optional.None<BungieProfile>(), finisher));
        Assert.Equal(focused, LoopDesigning.FindActionIcon(build, Optional.Some(new BungieProfile([], [], [new EquippedFinisher(GuardianClass.Hunter, FocusedFist)])), finisher));
        Assert.Contains(Dropkick, LoopDesigning.ListUnknownProfileHashes(session, profile));
        Assert.DoesNotContain(FocusedFist, LoopDesigning.ListUnknownProfileHashes(session, profile with { Finishers = [new EquippedFinisher(GuardianClass.Hunter, FocusedFist)] }));
    }
}

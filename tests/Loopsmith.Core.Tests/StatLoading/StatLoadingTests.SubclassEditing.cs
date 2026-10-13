using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.StatLoading;

/// <summary>A subclass edit and the build's stats (docs/loop-format.md, "Stats"): added up again from the armor when they were, typed ones stay.</summary>
public sealed partial class StatLoadingTests
{
    private static readonly ItemHash SparkOfShockPlug = ItemHash.From(1727069364u);
    private static readonly ItemHash SparkOfFocusPlug = ItemHash.From(1727069360u);
    private static readonly ItemHash SparkOfFeedbackPlug = ItemHash.From(3277705907u);

    [Fact]
    public void Removing_a_fragment_adds_the_stats_up_again_from_the_players_armor_and_typed_stats_stay()
    {
        var profile = ReadProfile([.. OwnShare(tuningSockets: 3)]);
        var session = LoopDesigning.ApplyBungieLogin(OpenOwnersSession(Catalog), profile).Session;
        var plugs = LoopDesigning.ReadSubclassPlugs(session.Build);
        var without = plugs with { Fragments = [.. plugs.Fragments.Where(fragment => fragment != SparkOfShockPlug)] };

        var removed = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.EditSubclass(session, Optional.Some(profile), without)).Value;
        var typed = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.SetStats(session, session.Build.Build.Stats with { Grenade = ToKnownStat(150) })).Value;
        var typedKept = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.EditSubclass(typed, Optional.Some(profile), without)).Value;

        Assert.Contains(SparkOfShockPlug, plugs.Fragments);
        Assert.Equal(ToKnownStat(190), session.Build.Build.Stats.Grenade);
        Assert.Equal(ToKnownStat(200), removed.Build.Build.Stats.Grenade);                       // Spark of Shock's −10 Grenade is given back
        Assert.Equal(ToKnownStat(150), typedKept.Build.Build.Stats.Grenade);                     // what the player typed stays as typed
    }

    [Fact]
    public void A_new_arc_fragments_stat_counts_when_the_stats_are_added_up_from_the_players_armor()
    {
        var profile = ReadProfile([.. OwnShare(tuningSockets: 3)]);
        var session = LoopDesigning.ApplyBungieLogin(OpenOwnersSession(Catalog), profile).Session;
        var plugs = LoopDesigning.ReadSubclassPlugs(session.Build);
        var swapped = plugs with { Fragments = [.. plugs.Fragments.Where(fragment => fragment != SparkOfShockPlug), SparkOfFocusPlug] };
        var feedback = plugs with { Fragments = [.. plugs.Fragments.Where(fragment => fragment != SparkOfShockPlug), SparkOfFeedbackPlug] };

        var focused = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.EditSubclass(session, Optional.Some(profile), swapped)).Value.Build.Build.Stats;
        var fed = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.EditSubclass(session, Optional.Some(profile), feedback)).Value.Build.Build.Stats;
        var before = session.Build.Build.Stats;

        Assert.Equal(before.Weapons.Map(stat => stat.Value - 10), focused.Weapons.Map(stat => stat.Value));   // Spark of Focus: −10 Weapons on a Hunter
        Assert.Equal(before.Class.Map(stat => stat.Value), focused.Class.Map(stat => stat.Value));             // its −10 Class is a Warlock's
        Assert.Equal(before.Health.Map(stat => stat.Value + 10), fed.Health.Map(stat => stat.Value));          // Spark of Feedback: +10 Health
    }
}

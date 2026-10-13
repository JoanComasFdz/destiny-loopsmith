using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Core.Tests.StatLoading;

/// <summary>
/// A build's weapon against the player's copy it wears, after a Bungie login (docs/loop-format.md, "Weapons from your
/// account"): the perks the copy has in each column — those its sockets can switch to too (Bungie's reusable plugs) —
/// and a warning under Validation for a column holding a perk the copy doesn't have. The perk picker still offers every
/// perk; only a copy that is the player's is checked.
/// </summary>
public sealed partial class StatLoadingTests
{
    private static readonly ItemHash Slice = ItemHash.From(923806249u);
    private static readonly ItemHash EnhancedSlice = ItemHash.From(3422796781u);
    private static readonly ItemHash AttritionOrbs = ItemHash.From(243981275u);
    private static readonly ItemHash Hatchling = ItemHash.From(831391274u);
    private static readonly ItemHash EnhancedElementalHoning = ItemHash.From(496047945u);

    [Fact]
    public void A_copys_sockets_hold_the_perk_in_them_then_those_they_can_switch_to()
    {
        var profile = ReadProfile([FestivalWithTwoPerks()]);

        var copy = Assert.Single(profile.Weapons);

        Assert.Equal([EnhancedSlice, EnhancedElementalHoning, Shader], copy.Plugs);
        Assert.Equal([[EnhancedSlice], [EnhancedElementalHoning, AttritionOrbs], [Shader]], copy.Sockets.Select(socket => socket.ToArray()));
        Assert.Equal([[EnhancedSlice], [EnhancedElementalHoning, AttritionOrbs]], FindColumns(Festival).ReadColumnPerks(copy.Sockets).Select(column => column.ToArray()));
    }

    [Fact]
    public void A_socket_counts_for_one_column_even_when_its_perk_is_in_two()
    {
        ImmutableArray<TraitColumn> columns = [new([ItemHash.From(1u), ItemHash.From(2u)]), new([ItemHash.From(2u), ItemHash.From(3u)])];
        ImmutableArray<ImmutableArray<ItemHash>> sockets = [[ItemHash.From(2u)], [ItemHash.From(3u), ItemHash.From(2u)], [ItemHash.From(9u)]];

        var read = columns.ReadColumnPerks(sockets);

        Assert.Equal([[2u], [3u, 2u]], read.Select(column => column.Select(hash => hash.Value).ToArray()));
        Assert.Equal([[], []], columns.ReadColumnPerks([]).Select(column => column.ToArray()));
    }

    [Fact]
    public void The_weapon_picker_shows_every_perk_a_column_of_the_copy_has()
    {
        var session = OpenOwnersSession(Catalog);
        var festival = FindWeaponIndex(session, Festival);

        var options = LoopDesigning.ListWeaponOptions(session.Build, ReadProfile([FestivalWithTwoPerks()]), new WeaponTarget.InPlaceOf(festival));

        var option = Assert.Single(options.Options);
        Assert.Equal([Optional.Some(EnhancedSlice), Optional.Some(EnhancedElementalHoning)], option.Roll);   // the build holds the one in it
        Assert.Equal([[EnhancedSlice], [EnhancedElementalHoning, AttritionOrbs]], option.Columns.Select(column => column.ToArray()));
    }

    [Fact]
    public void A_perk_the_players_copy_doesnt_have_is_a_warning_and_one_it_can_switch_to_is_not()
    {
        var profile = ReadProfile([.. OwnShare(tuningSockets: 0), .. OwnShareWeapons().Select(item => item.Hash == Festival ? FestivalWithTwoPerks() : item)]);
        var session = LoopDesigning.ApplyBungieLogin(OpenOwnersSession(Catalog), profile).Session;
        var festival = FindWeaponIndex(session, Festival);

        var hatchling = PickPerk(session, festival, 1, Optional.Some(Hatchling));
        var attrition = PickPerk(hatchling, festival, 1, Optional.Some(AttritionOrbs));
        var cleared = PickPerk(hatchling, festival, 1, Optional.None<ItemHash>());

        Assert.Empty(ListCopyWarnings(session, profile));
        Assert.Equal(["Festival Flight: your copy doesn't have Hatchling in column 2 (it has Elemental Honing (enhanced) or Attrition Orbs)."], ListCopyWarnings(hatchling, profile));
        Assert.Contains(LoopDesigning.ListDesignIssues(hatchling, Optional.Some(profile)), issue => issue.Severity == Severity.Warning && issue.Message.StartsWith("Festival Flight: your copy", StringComparison.Ordinal));
        Assert.Empty(ListCopyWarnings(attrition, profile));
        Assert.Empty(ListCopyWarnings(cleared, profile));
        Assert.Equal(LoopDesigning.ListDesignIssues(hatchling), LoopDesigning.ListDesignIssues(hatchling, Optional.None<BungieProfile>()));   // logged out: no warning
        Assert.DoesNotContain("your copy", hatchling.Design.Build.Text, StringComparison.Ordinal);                                          // nothing of it is saved
    }

    [Fact]
    public void A_copys_enhanced_perk_gives_it_the_base_perk_but_a_base_perk_not_the_enhanced_one()
    {
        var enhancedCopy = ReadProfile([.. OwnShare(tuningSockets: 0), .. OwnShareWeapons()]);
        var baseCopy = ReadProfile([.. OwnShare(tuningSockets: 0), .. OwnShareWeapons().Select(item => item.Hash == Festival ? item with { Plugs = item.Plugs.SetItem(0, Slice) } : item)]);
        var enhanced = LoopDesigning.ApplyBungieLogin(OpenOwnersSession(Catalog), enhancedCopy).Session;
        var plain = LoopDesigning.ApplyBungieLogin(OpenOwnersSession(Catalog), baseCopy).Session;
        var festival = FindWeaponIndex(enhanced, Festival);

        var slice = PickPerk(enhanced, festival, 0, Optional.Some(Slice));
        var enhancedSlice = PickPerk(plain, festival, 0, Optional.Some(EnhancedSlice));
        var columns = Assert.IsType<Optional<ImmutableArray<ImmutableArray<ItemHash>>>.Some>(LoopDesigning.ListCopyColumns(plain.Build, baseCopy, plain.Build.Build.Weapons[festival])).Value;

        Assert.Empty(ListCopyWarnings(slice, enhancedCopy));
        Assert.Equal(["Festival Flight: your copy doesn't have Slice (enhanced) in column 1 (it has Slice)."], ListCopyWarnings(enhancedSlice, baseCopy));
        Assert.True(LoopDesigning.HasCopyPerk(plain.Build, columns[0], Slice));
        Assert.False(LoopDesigning.HasCopyPerk(plain.Build, columns[0], EnhancedSlice));
        Assert.True(LoopDesigning.HasCopyPerk(enhanced.Build, [EnhancedSlice], Slice));
    }

    [Fact]
    public void A_weapon_whose_copy_isnt_the_players_is_not_checked()
    {
        var session = OpenOwnersSession(Catalog);
        var festival = FindWeaponIndex(session, Festival);
        var profile = ReadProfile([.. OwnAll(tuningSockets: 0), .. OwnShareWeapons().Select((weapon, index) => weapon with { Instance = $"{5100 + index}" })]);

        var hatchling = PickPerk(LoopDesigning.ApplyBungieLogin(session, profile).Session, festival, 1, Optional.Some(Hatchling));

        Assert.Empty(ListCopyWarnings(hatchling, profile));
        Assert.False(LoopDesigning.ListCopyColumns(hatchling.Build, profile, hatchling.Build.Build.Weapons[festival]).IsSome());
    }

    /// <summary>The owner's Festival Flight, holding enhanced Slice and enhanced Elemental Honing, its second column able to switch to Attrition Orbs too.</summary>
    private static FakeItem FestivalWithTwoPerks() =>
        new(Festival, FestivalCopy, "equipped", [EnhancedSlice, EnhancedElementalHoning, Shader], null, Tier: 4, DamageType: 7,
            Switchable: ImmutableDictionary.CreateRange([KeyValuePair.Create(1, ImmutableArray.Create(EnhancedElementalHoning, AttritionOrbs))]));

    private static int FindWeaponIndex(DesignSession session, ItemHash weapon) =>
        session.Build.Build.Weapons.IndexOf(session.Build.Build.Weapons.Single(found => found.Hash == Optional.Some(weapon)));

    private static DesignSession PickPerk(DesignSession session, int weapon, int column, Optional<ItemHash> perk) =>
        Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickWeaponPerk(session, weapon, column, perk)).Value;

    private static string[] ListCopyWarnings(DesignSession session, BungieProfile profile) =>
        [.. LoopDesigning.ListDesignIssues(session, Optional.Some(profile)).Where(issue => issue.Message.Contains("your copy", StringComparison.Ordinal)).Select(issue => issue.Message)];
}

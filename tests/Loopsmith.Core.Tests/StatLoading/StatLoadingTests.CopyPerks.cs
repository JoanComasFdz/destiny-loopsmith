using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.StatLoading;

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
        var profile = ReadProfile([OwnFestivalWithTwoPerks()]);

        var copy = Assert.Single(profile.Weapons);

        Assert.Equal([EnhancedSlice, EnhancedElementalHoning, Shader], copy.Plugs);
        Assert.Equal([[EnhancedSlice], [EnhancedElementalHoning, AttritionOrbs], [Shader]], copy.Sockets.Select(socket => socket.ToArray()));
        Assert.Equal([[EnhancedSlice], [EnhancedElementalHoning, AttritionOrbs]], CopyFinding.ReadColumnPerks(FindColumns(Festival), copy.Sockets).Select(column => column.ToArray()));
    }

    [Fact]
    public void A_socket_counts_for_one_column_even_when_its_perk_is_in_two()
    {
        ImmutableArray<TraitColumn> columns = [new([ItemHash.From(1u), ItemHash.From(2u)]), new([ItemHash.From(2u), ItemHash.From(3u)])];
        ImmutableArray<ImmutableArray<ItemHash>> sockets = [[ItemHash.From(2u)], [ItemHash.From(3u), ItemHash.From(2u)], [ItemHash.From(9u)]];

        var read = CopyFinding.ReadColumnPerks(columns, sockets);

        Assert.Equal([[2u], [3u, 2u]], read.Select(column => column.Select(hash => hash.Value).ToArray()));
        Assert.Equal([[], []], CopyFinding.ReadColumnPerks(columns, []).Select(column => column.ToArray()));
    }

    [Fact]
    public void The_weapon_picker_shows_every_perk_a_column_of_the_copy_has()
    {
        var session = OpenOwnersSession(Catalog);
        var festival = FindWeaponIndex(session, Festival);

        var options = LoopDesigning.ListWeaponOptions(session.Build, ReadProfile([OwnFestivalWithTwoPerks()]), new WeaponTarget.InPlaceOf(festival));

        var option = Assert.Single(options.Options);
        Assert.Equal([Optional.Some(EnhancedSlice), Optional.Some(EnhancedElementalHoning)], option.Roll);   // the build holds the one in it
        Assert.Equal([[EnhancedSlice], [EnhancedElementalHoning, AttritionOrbs]], option.Columns.Select(column => column.ToArray()));
    }

    [Fact]
    public void A_perk_the_players_copy_doesnt_have_is_a_warning_of_its_own_and_one_it_can_switch_to_is_not()
    {
        var profile = ReadProfile([.. OwnShare(tuningSockets: 0), .. OwnShareWeapons().Select(item => item.Hash == Festival ? OwnFestivalWithTwoPerks() : item)]);
        var session = LoopDesigning.ApplyBungieLogin(OpenOwnersSession(Catalog), profile).Session;
        var festival = FindWeaponIndex(session, Festival);

        var hatchling = PickPerk(session, festival, 1, Optional.Some(Hatchling));
        var attrition = PickPerk(hatchling, festival, 1, Optional.Some(AttritionOrbs));
        var cleared = PickPerk(hatchling, festival, 1, Optional.None<ItemHash>());

        var loggedOut = LoopDesigning.ListDesignIssues(hatchling);
        var loggedIn = LoopDesigning.ListDesignIssues(hatchling, Optional.Some(profile));

        Assert.Empty(ListCopyWarnings(session, profile));
        Assert.DoesNotContain(loggedOut, issue => issue.Message.Contains("Hatchling", StringComparison.Ordinal));   // the rules don't know it: its square's ⊘ says so
        Assert.Equal(
            [.. loggedOut, new BuildIssue(Severity.Warning, "Festival Flight: your copy doesn't have Hatchling in column 2 (it has Elemental Honing (enhanced) or Attrition Orbs).")],
            loggedIn);
        Assert.Contains(new ColumnPerk(1, Hatchling), LoopDesigning.ListPerksNotOnCopy(hatchling.Build, profile, hatchling.Build.Build.Weapons[festival]));   // its square is marked too
        Assert.DoesNotContain(new ColumnPerk(1, AttritionOrbs), LoopDesigning.ListPerksNotOnCopy(attrition.Build, profile, attrition.Build.Build.Weapons[festival]));
        Assert.Empty(ListCopyWarnings(attrition, profile));
        Assert.Empty(ListCopyWarnings(cleared, profile));
        Assert.Equal(LoopDesigning.ListDesignIssues(hatchling), LoopDesigning.ListDesignIssues(hatchling, Optional.None<BungieProfile>()));   // logged out: no warning
        Assert.DoesNotContain("your copy", hatchling.Design.Build.Text, StringComparison.OrdinalIgnoreCase);                                // nothing of it is saved
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
        var plainMissing = LoopDesigning.ListPerksNotOnCopy(plain.Build, baseCopy, plain.Build.Build.Weapons[festival]);
        var enhancedMissing = LoopDesigning.ListPerksNotOnCopy(enhanced.Build, enhancedCopy, enhanced.Build.Build.Weapons[festival]);

        Assert.Empty(ListCopyWarnings(slice, enhancedCopy));
        Assert.Equal(["Festival Flight: your copy doesn't have Slice (enhanced) in column 1 (it has Slice)."], ListCopyWarnings(enhancedSlice, baseCopy));   // the rules know it: a line of its own
        Assert.DoesNotContain(LoopDesigning.ListDesignIssues(enhancedSlice, Optional.Some(baseCopy)), issue =>
            issue.Message.Contains("Elemental Honing", StringComparison.Ordinal));                                                                     // a perk the copy has says nothing
        Assert.Contains(new ColumnPerk(0, EnhancedSlice), plainMissing);        // the picker marks the enhanced Slice on a plain copy
        Assert.DoesNotContain(new ColumnPerk(0, Slice), plainMissing);
        Assert.DoesNotContain(new ColumnPerk(0, Slice), enhancedMissing);       // nor Slice on an enhanced one
        Assert.DoesNotContain(new ColumnPerk(0, EnhancedSlice), enhancedMissing);
        Assert.Contains(new ColumnPerk(1, Hatchling), enhancedMissing);          // column 2: the copy holds Elemental Honing
    }

    [Fact]
    public void A_weapon_whose_copy_isnt_the_players_is_not_checked()
    {
        var session = OpenOwnersSession(Catalog);
        var festival = FindWeaponIndex(session, Festival);
        var profile = ReadProfile([.. OwnAll(tuningSockets: 0), .. OwnShareWeapons().Select((weapon, index) => weapon with { Instance = $"{5100 + index}" })]);

        var hatchling = PickPerk(LoopDesigning.ApplyBungieLogin(session, profile).Session, festival, 1, Optional.Some(Hatchling));

        Assert.Empty(ListCopyWarnings(hatchling, profile));
        Assert.Empty(LoopDesigning.ListPerksNotOnCopy(hatchling.Build, profile, hatchling.Build.Build.Weapons[festival]));
    }

    /// <summary>The owner's Festival Flight, holding enhanced Slice and enhanced Elemental Honing, its second column able to switch to Attrition Orbs too.</summary>
    private static FakeItem OwnFestivalWithTwoPerks() =>
        new(Festival, FestivalCopy, "equipped", [EnhancedSlice, EnhancedElementalHoning, Shader], null, Tier: 4, DamageType: 7,
            Switchable: ImmutableDictionary.CreateRange([KeyValuePair.Create(1, ImmutableArray.Create(EnhancedElementalHoning, AttritionOrbs))]));

    private static int FindWeaponIndex(DesignSession session, ItemHash weapon) =>
        session.Build.Build.Weapons.IndexOf(session.Build.Build.Weapons.Single(found => found.Hash == Optional.Some(weapon)));

    private static DesignSession PickPerk(DesignSession session, int weapon, int column, Optional<ItemHash> perk) =>
        Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickWeaponPerk(session, weapon, column, perk)).Value;

    private static string[] ListCopyWarnings(DesignSession session, BungieProfile profile) =>
        [.. LoopDesigning.ListDesignIssues(session, Optional.Some(profile)).Where(issue => issue.Message.Contains("your copy", StringComparison.OrdinalIgnoreCase)).Select(issue => issue.Message)];
}

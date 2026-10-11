using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Core.Tests.StatLoading;

/// <summary>
/// What the game draws on the player's copies, as DIM shows it (docs/loop-format.md, "Stats"): each copy's gear tier,
/// masterwork and breaker from their Bungie profile, shown on the build's pieces and weapons: the share's copy when it is
/// theirs, else their own, as DIM resolves a loadout's items; a weapon's own breaker (an exotic's) shows without a login.
/// </summary>
public sealed partial class StatLoadingTests
{
    private const string ThunderlordCopy = "6917529810172542011";   // the owner's share's Thunderlord (equipped[].id)
    private static readonly ItemHash Thunderlord = ItemHash.From(3325463374u);
    private static readonly ItemHash Disruption = ItemHash.From(2611060930u);
    private static readonly ItemHash Stagger = ItemHash.From(3178805705u);

    [Fact]
    public void The_profile_gives_each_copys_tier_masterwork_and_breaker_and_the_weapons_apart_from_the_armor()
    {
        var profile = ReadProfile(
            [
                new FakeItem(Mask, "1", "vault", [FullMasterwork], 11, Tier: 4, State: 4),
                new FakeItem(Vow, "2", "equipped", [], 11, Tier: 0, State: 8),                // no tier; crafted, not masterworked
                new FakeItem(Thunderlord, "3", "equipped", [], null, Tier: 5, State: 4 | 8, DamageType: 2, Breaker: Disruption.Value),
                new FakeItem(ItemHash.From(4019651319u), "4", "vault", [], null, DamageType: 7),
            ]);

        Assert.Equal(new GearMarks(Optional.Some(GearTier.From(4)), true, Optional.None<ItemHash>()), FindCopy(profile, "1").Marks);
        Assert.Equal(new GearMarks(Optional.None<GearTier>(), false, Optional.None<ItemHash>()), FindCopy(profile, "2").Marks);
        Assert.Equal(["3", "4"], profile.Weapons.Select(weapon => weapon.InstanceId.Value).Order());
        var thunderlord = profile.Weapons.Single(weapon => weapon.Hash == Thunderlord);
        Assert.Equal((ItemInstanceId.From("3"), (ArmorPlace)new ArmorPlace.Equipped(GuardianClass.Hunter), new GearMarks(Optional.Some(GearTier.From(5)), true, Optional.Some(Disruption))),
            (thunderlord.InstanceId, thunderlord.Place, thunderlord.Marks));
    }

    [Fact]
    public void The_builds_pieces_and_weapons_show_the_marks_of_the_copies_it_names_when_they_are_the_players()
    {
        var build = OpenOwnersBuild();
        var thunderlord = build.Build.Weapons.Single(weapon => weapon.Hash == Optional.Some(Thunderlord));
        var own = ReadProfile([.. OwnShare(tuningSockets: 0).Select(item => item with { Tier = item.Hash == Mask ? 5 : 3, State = 4 }),
            new FakeItem(Mask, "5000", "vault", [FindRollPlug(StatKind.Grenade, 30)], 11, Tier: 1),   // a weaker copy: not the share's
            new FakeItem(Thunderlord, ThunderlordCopy, "equipped", [], null, Tier: 2, DamageType: 2, Breaker: Disruption.Value),
            new FakeItem(Thunderlord, "5001", "vault", [], null, Tier: 5, DamageType: 2)]);

        Assert.Equal(Optional.Some(ItemInstanceId.From(ThunderlordCopy)), thunderlord.Instance);
        Assert.Equal(Optional.Some(new GearMarks(Optional.Some(GearTier.From(5)), true, Optional.None<ItemHash>())), LoopDesigning.FindArmorMarks(build, own, ArmorSlot.Helmet));
        Assert.Equal(Optional.Some(GearTier.From(3)), LoopDesigning.FindArmorMarks(build, own, ArmorSlot.Arms).Bind(marks => marks.Tier));
        Assert.Equal(Optional.Some(new GearMarks(Optional.Some(GearTier.From(2)), false, Optional.Some(Disruption))), LoopDesigning.FindWeaponMarks(own, thunderlord));
    }

    [Fact]
    public void For_someone_elses_share_the_marks_are_of_the_players_own_copy_as_DIM_resolves_a_loadouts_items()
    {
        var build = OpenOwnersBuild();
        var thunderlord = build.Build.Weapons.Single(weapon => weapon.Hash == Optional.Some(Thunderlord));
        var other = ReadProfile([.. OwnAll(tuningSockets: 0).Select(item => item with { Tier = item.Hash == Mask ? 4 : 2 }),
            new FakeItem(Thunderlord, "5001", "vault", [], null, Tier: 3, DamageType: 2),
            new FakeItem(Thunderlord, "5002", "equipped", [], null, Tier: 5, State: 4, DamageType: 2)]);
        var none = ReadProfile([new FakeItem(Vow, "5003", "vault", [], 11, Tier: 2)]);

        Assert.Equal(Optional.Some(GearTier.From(4)), LoopDesigning.FindArmorMarks(build, other, ArmorSlot.Helmet).Bind(marks => marks.Tier));   // the copy the picker ticks
        Assert.Equal(Optional.Some(new GearMarks(Optional.Some(GearTier.From(5)), true, Optional.None<ItemHash>())), LoopDesigning.FindWeaponMarks(other, thunderlord));   // the highest tier
        Assert.False(LoopDesigning.FindArmorMarks(build, none, ArmorSlot.Helmet).IsSome());   // no Mask of theirs
        Assert.False(LoopDesigning.FindWeaponMarks(none, thunderlord).IsSome());
    }

    [Fact]
    public void A_weapons_breaker_is_its_copys_after_the_login_else_its_own_and_a_weapon_without_one_has_none()
    {
        var build = OpenOwnersBuild();
        var thunderlord = build.Build.Weapons.Single(weapon => weapon.Hash == Optional.Some(Thunderlord));
        var festival = build.Build.Weapons.Single(weapon => weapon.Hash == Optional.Some(ItemHash.From(4019651319u)));
        var staggering = Optional.Some(new GearMarks(Optional.None<GearTier>(), false, Optional.Some(Stagger)));

        Assert.Equal(Optional.Some("Disruption"), LoopDesigning.FindWeaponBreaker(build, thunderlord, Optional.None<GearMarks>()).Map(breaker => breaker.Name));
        Assert.Equal(Optional.Some("Stagger"), LoopDesigning.FindWeaponBreaker(build, thunderlord, staggering).Map(breaker => breaker.Name));
        Assert.False(LoopDesigning.FindWeaponBreaker(build, festival, Optional.None<GearMarks>()).IsSome());
        Assert.StartsWith("/common/destiny2_content/icons/DestinyBreakerTypeDefinition_", LoopDesigning.FindWeaponBreaker(build, thunderlord, Optional.None<GearMarks>())
            .Match(breaker => breaker.Value.Icon, _ => ""), StringComparison.Ordinal);
    }

    [Fact]
    public void The_excerpt_has_the_tier_and_masterwork_overlays_and_an_excerpt_without_them_draws_none()
    {
        var overlays = Catalog.Manifest.Overlays;

        Assert.Equal(5, overlays.Tiers.Length);
        Assert.Equal(Optional.Some("/img/destiny_content/items/inventory-item-tier1.png"), overlays.FindTierOverlay(GearTier.From(1)));   // Bungie's blank one
        Assert.Equal(Optional.Some("/img/destiny_content/items/inventory-item-tier4.png"), overlays.FindTierOverlay(GearTier.From(4)));
        Assert.Equal(Optional.Some("/img/destiny_content/items/masterwork-overlay-exotic.png"), overlays.FindMasterworkOverlay(Optional.Some(ItemTier.Exotic)));
        Assert.Equal(Optional.Some("/img/destiny_content/items/masterwork-overlay.png"), overlays.FindMasterworkOverlay(Optional.Some(ItemTier.Legendary)));
        Assert.False(new ItemOverlays([], Optional.None<string>(), Optional.None<string>()).FindTierOverlay(GearTier.From(1)).IsSome());
    }

    private static OwnedArmor FindCopy(BungieProfile profile, string instance) =>
        profile.Armor.Single(armor => armor.InstanceId == ItemInstanceId.From(instance));
}

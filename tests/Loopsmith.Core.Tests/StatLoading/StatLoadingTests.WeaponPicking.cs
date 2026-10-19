using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Core.Tests.StatLoading;

/// <summary>
/// The player's own weapons in the build, after a Bungie login (docs/loop-format.md, "Weapons from your account"): the
/// share's owner gets their copies' perks and catalyst at once; anyone picks their own weapons with ⋯, and a weapon
/// picked swaps into the build with its perks and copy (or fills an empty slot). A build wears one exotic weapon.
/// </summary>
public sealed partial class StatLoadingTests
{
    private const string FestivalCopy = "6917530186321554819";   // the owner's share's weapons (equipped[].id)
    private const string CrisisCopy = "6917529816091863651";
    private static readonly ItemHash Festival = ItemHash.From(4019651319u);
    private static readonly ItemHash Crisis = ItemHash.From(2888266564u);
    private static readonly ItemHash ThunderlordCatalyst = ItemHash.From(1550063955u);
    private static readonly ItemHash Shader = ItemHash.From(4248210736u);

    private static readonly ItemHash ExoticRifle = ItemHash.From(9201u);
    private static readonly ItemHash KineticRifle = ItemHash.From(9202u);

    private static ManifestExcerpt OtherWeapons => new(
        Optional.None<string>(),
        ImmutableDictionary<DamageType, string>.Empty,
        ImmutableDictionary<ItemHash, ArmorSet>.Empty,
        ImmutableDictionary.CreateRange(
        [
            ToWeaponItem(ExoticRifle, "Exotic Rifle", WeaponSlot.Energy, DamageType.Arc, ItemTier.Exotic, [[9211u, 9212u]]),
            ToWeaponItem(KineticRifle, "Kinetic Rifle", WeaponSlot.Kinetic, DamageType.Kinetic, ItemTier.Legendary, [[9221u, 9222u], [9231u, 9232u]]),
        ]),
        ImmutableDictionary<ItemHash, StatPlug>.Empty,
        ImmutableDictionary<ItemHash, BreakerType>.Empty,
        new ItemOverlays([], Optional.None<string>(), Optional.None<string>()),
        ImmutableDictionary<ItemHash, KeywordTrait>.Empty,
        ImmutableDictionary<StatKind, ArmorStatIcon>.Empty);

    [Fact]
    public void Logging_in_with_the_shares_own_account_loads_its_weapons_perks_and_catalyst()
    {
        var session = OpenOwnersSession(Catalog);
        var festival = FindColumns(Festival);

        var login = LoopDesigning.ApplyBungieLogin(session, ReadProfile([.. OwnShare(tuningSockets: 0), .. OwnShareWeapons()]));
        var weapons = login.Session.Build.Build.Weapons;

        Assert.EndsWith("Its weapons are yours too, with their perks.", login.Message, StringComparison.Ordinal);
        Assert.Equal([Optional.Some(festival[0].Options[^1]), Optional.Some(festival[1].Options[^1])], weapons.Single(weapon => weapon.Hash == Optional.Some(Festival)).Roll);
        Assert.True(weapons.Single(weapon => weapon.Hash == Optional.Some(Thunderlord)).Catalyst);
        Assert.Contains("{hash: 3325463374, perks: [], roll: [1419069769, 2779035018], catalyst: true, instance: \"6917529810172542011\"} # Thunderlord", login.Session.Design.Build.Text, StringComparison.Ordinal);
        Assert.Contains(login.Session.Build.Equipped, equipped => equipped.Element.Id.Value == "thunderlord-catalyst");
    }

    [Fact]
    public void An_enhanced_copys_slice_still_arms_slice_and_severs_after_the_login()
    {
        var login = LoopDesigning.ApplyBungieLogin(OpenOwnersSession(Catalog), ReadProfile([.. OwnShare(tuningSockets: 0), .. OwnShareWeapons()]));
        var played = new PlayerAction[] { new PlayerAction.UseClassAbility(), new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Damage, TargetCount.One) }
            .Aggregate(login.Session, (session, action) => LoopDesigning.AppendStep(session, action, Optional.None<string>()));

        Assert.Contains(login.Session.Build.Equipped, equipped => equipped.Element.Id.Value == "slice-enhanced");   // the copy rolls the enhanced trait
        Assert.DoesNotContain(login.Session.Build.Issues, issue => issue.Message.Contains("Slice (Enhanced Trait)", StringComparison.Ordinal));
        Assert.Contains(played.Resolutions[0].Fired, fired => fired.Source.Value == "slice-enhanced");              // the dodge arms it
        Assert.Contains(played.Resolutions[1].Fired, fired => fired.Source.Value == "slice-enhanced"
            && fired.Outcomes.Any(outcome => outcome.Outcome is Outcome.DebuffTarget { Status.Value: "sever" }));    // the Strand hit severs
    }

    [Fact]
    public void Someone_elses_share_keeps_its_weapons_and_the_player_is_told_to_pick_their_own()
    {
        var session = OpenOwnersSession(Catalog);

        var login = LoopDesigning.ApplyBungieLogin(session, ReadProfile([.. OwnAll(tuningSockets: 0), .. OwnShareWeapons().Select((weapon, index) => weapon with { Instance = $"{5100 + index}" })]));

        Assert.EndsWith("Tap ⋯ next to a weapon to pick one of yours.", login.Message, StringComparison.Ordinal);
        Assert.Equal(session.Design.Build.Text, login.Session.Design.Build.Text);
    }

    [Fact]
    public void The_weapon_picker_lists_the_players_weapons_for_the_slot_with_the_builds_copy_first()
    {
        var session = OpenOwnersSession(Catalog with { Manifest = Catalog.Manifest.AddExcerpt(OtherWeapons) });
        var festival = session.Build.Build.Weapons.IndexOf(session.Build.Build.Weapons.Single(weapon => weapon.Hash == Optional.Some(Festival)));
        var crisis = session.Build.Build.Weapons.IndexOf(session.Build.Build.Weapons.Single(weapon => weapon.Hash == Optional.Some(Crisis)));
        var profile = ReadProfile([.. OwnShareWeapons(),
            new FakeItem(KineticRifle, "4001", "vault", ImmutableArray.Create(ItemHash.From(9222u), ItemHash.From(9231u)), null, Tier: 3, DamageType: 1),
            new FakeItem(Festival, "4002", "vault", [], null, Tier: 5, DamageType: 7),
            new FakeItem(ExoticRifle, "4003", "vault", [], null, DamageType: 2),
            new FakeItem(ItemHash.From(12345u), "4004", "vault", [], null, DamageType: 2)]);

        var kinetic = LoopDesigning.ListWeaponOptions(session.Build, profile, new WeaponTarget.InPlaceOf(festival));
        var energy = LoopDesigning.ListWeaponOptions(session.Build, profile, new WeaponTarget.InPlaceOf(crisis));

        Assert.Equal([FestivalCopy, "4002", "4001"], kinetic.Options.Select(option => option.Weapon.InstanceId.Value));
        Assert.Equal([true, false, false], kinetic.Options.Select(option => option.Worn));
        Assert.Equal([Optional.Some(ItemHash.From(9222u)), Optional.Some(ItemHash.From(9231u))], kinetic.Options[2].Roll);   // the perks the rifle holds
        Assert.Equal([CrisisCopy], energy.Options.Select(option => option.Weapon.InstanceId.Value));   // the exotic rifle: the build has Thunderlord
        Assert.Equal(
            ["A build wears one exotic weapon: swap Thunderlord for a legendary weapon to pick an exotic here.",
                "1 of your weapons aren't listed: Loopsmith can't tell their slot yet (Bungie's manifest wasn't read for them)."],
            energy.Notes);
        Assert.Contains(ItemHash.From(12345u), LoopDesigning.ListUnknownProfileHashes(session, profile));
    }

    [Fact]
    public void A_picked_weapon_swaps_into_the_build_with_its_perks_and_its_copy()
    {
        var session = OpenOwnersSession(Catalog with { Manifest = Catalog.Manifest.AddExcerpt(OtherWeapons) });
        var festival = session.Build.Build.Weapons.IndexOf(session.Build.Build.Weapons.Single(weapon => weapon.Hash == Optional.Some(Festival)));
        var profile = ReadProfile([.. OwnShareWeapons(), new FakeItem(KineticRifle, "4001", "vault", ImmutableArray.Create(ItemHash.From(9232u), ItemHash.From(9222u), Shader), null, Tier: 3, DamageType: 1)]);

        var picked = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickWeapon(session, profile, new WeaponTarget.InPlaceOf(festival), ItemInstanceId.From("4001"))).Value;
        var rifle = picked.Build.Build.Weapons[festival];

        Assert.Equal(("Kinetic Rifle", DamageType.Kinetic, Optional.Some(KineticRifle), Optional.Some(ItemInstanceId.From("4001"))), (rifle.Name, rifle.Type, rifle.Hash, rifle.Instance));
        Assert.Equal([Optional.Some(ItemHash.From(9222u)), Optional.Some(ItemHash.From(9232u))], rifle.Roll);
        Assert.Contains("{hash: 9202, perks: [], roll: [9222, 9232], instance: \"4001\"} # Kinetic Rifle", picked.Design.Build.Text, StringComparison.Ordinal);
        Assert.Equal(session.Build.Build.Weapons.Length, picked.Build.Build.Weapons.Length);
        Assert.DoesNotContain("Festival Flight", picked.Design.Build.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_second_exotic_weapon_is_refused_and_an_empty_slot_is_filled()
    {
        var owners = OpenOwnersSession(Catalog with { Manifest = Catalog.Manifest.AddExcerpt(OtherWeapons) });
        var crisis = owners.Build.Build.Weapons.IndexOf(owners.Build.Build.Weapons.Single(weapon => weapon.Hash == Optional.Some(Crisis)));
        var handWritten = LoopDesigning.CreateSession(OpenHandWrittenBuild(), new LoopDesign("Test", Optional.None<string>(), Optional.None<string>(), Optional.None<CatalogVersion>(), ToHandWrittenBuildFile(), []));
        var profile = ReadProfile([.. OwnShareWeapons(), new FakeItem(ExoticRifle, "4003", "vault", [], null, DamageType: 2)]);

        var refused = Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.PickWeapon(owners, profile, new WeaponTarget.InPlaceOf(crisis), ItemInstanceId.From("4003")));
        var added = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickWeapon(handWritten, profile, new WeaponTarget.IntoEmpty(WeaponSlot.Power), ItemInstanceId.From(ThunderlordCopy))).Value;

        Assert.Equal("A build wears one exotic weapon: swap Thunderlord for a legendary weapon first.", refused.Failure);
        Assert.Equal(handWritten.Build.Build.Weapons.Length + 1, added.Build.Build.Weapons.Length);
        Assert.Equal((WeaponSlot.Power, "Thunderlord", true), (added.Build.Build.Weapons[^1].Slot, added.Build.Build.Weapons[^1].Name, added.Build.Build.Weapons[^1].Catalyst));
        Assert.StartsWith(ToHandWrittenBuildFile().Text.Split("weapons:")[0], added.Design.Build.Text, StringComparison.Ordinal);   // the rest of the file as it was
    }

    [Fact]
    public void A_weapon_goes_only_into_its_own_slot_and_an_empty_slot_must_be_empty()
    {
        var session = OpenOwnersSession(Catalog with { Manifest = Catalog.Manifest.AddExcerpt(OtherWeapons) });
        var crisis = session.Build.Build.Weapons.IndexOf(session.Build.Build.Weapons.Single(weapon => weapon.Hash == Optional.Some(Crisis)));
        var profile = ReadProfile([.. OwnShareWeapons(), new FakeItem(KineticRifle, "4001", "vault", [], null, DamageType: 1)]);

        var wrongSlot = Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.PickWeapon(session, profile, new WeaponTarget.InPlaceOf(crisis), ItemInstanceId.From("4001")));
        var taken = Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.PickWeapon(session, profile, new WeaponTarget.IntoEmpty(WeaponSlot.Kinetic), ItemInstanceId.From("4001")));
        var missing = Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.PickWeapon(session, profile, new WeaponTarget.InPlaceOf(9), ItemInstanceId.From("4001")));

        Assert.Equal("Kinetic Rifle is a kinetic weapon: it can't go in the energy slot.", wrongSlot.Failure);
        Assert.Equal("The build already has a kinetic weapon: pick one in its place.", taken.Failure);
        Assert.Equal("The build has no weapon 10.", missing.Failure);
        Assert.Empty(LoopDesigning.ListWeaponOptions(session.Build, profile, new WeaponTarget.InPlaceOf(9)).Options);
    }

    [Fact]
    public void For_someone_elses_share_no_copy_is_ticked_so_the_players_own_can_be_picked_and_loads_its_perks()
    {
        var session = OpenOwnersSession(Catalog);
        var festival = session.Build.Build.Weapons.IndexOf(session.Build.Build.Weapons.Single(weapon => weapon.Hash == Optional.Some(Festival)));
        var mine = OwnShareWeapons().First() with { Instance = "5100" };
        var profile = ReadProfile([mine]);

        var options = LoopDesigning.ListWeaponOptions(session.Build, profile, new WeaponTarget.InPlaceOf(festival));
        var picked = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.PickWeapon(session, profile, new WeaponTarget.InPlaceOf(festival), ItemInstanceId.From("5100"))).Value;

        Assert.Equal([("5100", false)], options.Options.Select(option => (option.Weapon.InstanceId.Value, option.Worn)));
        Assert.Equal(mine.Plugs.Take(2).Select(Optional.Some), picked.Build.Build.Weapons[festival].Roll);
    }

    [Fact]
    public void A_hand_written_exotic_named_by_its_perks_counts_for_the_one_exotic_rule()
    {
        var build = OpenHandWrittenBuild();
        var withExotic = build.Build with
        {
            Weapons = [.. build.Build.Weapons.Where(weapon => weapon.Slot != WeaponSlot.Energy),
                new WeaponLoadout(WeaponSlot.Energy, "Some exotic", DamageType.Arc, Optional.None<AmmoType>(), Optional.None<string>(), [ElementId.From("reign-havoc")], Optional.None<ItemHash>(), [], false, Optional.None<ItemInstanceId>(), Optional.None<ItemHash>())],
        };

        var refused = Assert.IsType<Result<Unit, string>.Error>(Loopsmith.Core.BuildComposition.WeaponSwapping.CheckWeapon(Catalog, withExotic, new WeaponTarget.IntoEmpty(WeaponSlot.Power), Thunderlord));

        Assert.Equal("A build wears one exotic weapon: swap Some exotic for a legendary weapon first.", refused.Failure);
    }

    /// <summary>The share's three weapons as the owner's own copies, each holding the last perk of each of its columns (Thunderlord: its catalyst too).</summary>
    private static IEnumerable<FakeItem> OwnShareWeapons() =>
    [
        new FakeItem(Festival, FestivalCopy, "equipped", [.. FindColumns(Festival).Select(column => column.Options[^1]), Shader], null, Tier: 4, DamageType: 7),
        new FakeItem(Crisis, CrisisCopy, "equipped", [.. FindColumns(Crisis).Select(column => column.Options[^1])], null, Tier: 5, State: 4, DamageType: 2),
        new FakeItem(Thunderlord, ThunderlordCopy, "equipped", [.. FindColumns(Thunderlord).Select(column => column.Options[^1]), ThunderlordCatalyst], null, State: 4, DamageType: 2, Breaker: Disruption.Value),
    ];

    private static ImmutableArray<TraitColumn> FindColumns(ItemHash weapon) =>
        Assert.IsType<ManifestKind.Weapon>(Catalog.Manifest.Items[weapon].Kind).Traits;

    private static KeyValuePair<ItemHash, ManifestItem> ToWeaponItem(ItemHash hash, string name, WeaponSlot slot, DamageType type, ItemTier tier, uint[][] traits) =>
        KeyValuePair.Create(hash, new ManifestItem(hash, name,
            new ManifestKind.Weapon(slot, Optional.Some(type), Optional.None<AmmoType>(), [.. traits.Select(column => new TraitColumn([.. column.Select(ItemHash.From)]))],
                Optional.None<ItemHash>(), Optional.None<ItemHash>(), Optional.None<ItemHash>(), []),
            Optional.Some("Auto Rifle"), Optional.Some(tier), Optional.None<string>(), Optional.None<string>(), Optional.None<string>()));
}

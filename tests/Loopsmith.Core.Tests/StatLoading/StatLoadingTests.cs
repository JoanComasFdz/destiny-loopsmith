using System.Collections.Immutable;
using System.Text.Json;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.StatLoading;

/// <summary>
/// A build's stats from the player's own armor, after a Bungie login: the login's answers, the profile's armor and
/// characters, the build's pieces matched with the player's copies, and the total added up the way DIM adds up a
/// loadout (each copy's roll and masterwork, the build's stat mods, Balanced Tuning, its fragments).
/// </summary>
public sealed partial class StatLoadingTests
{
    private static readonly Result<ImmutableArray<SourceText>, string> RuleFiles = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"));   // impure

    private static readonly RuleCatalog Catalog =
        RuleFiles.Bind(CatalogLoading.ParseCatalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    // The owner's DIM loadout's armor (builds/skip-grenade-hunter/dim-loadout.json) and the plugs every copy here has.
    private static readonly ItemHash Mask = ItemHash.From(593554567u);
    private static readonly ItemHash Vow = ItemHash.From(1611241895u);
    private static readonly ImmutableArray<ItemHash> Legendary = [Mask, ItemHash.From(2883433250u), ItemHash.From(1047792392u), ItemHash.From(2107326067u)];
    private static readonly ItemHash FullMasterwork = ItemHash.From(788990510u);    // Upgrade Armor: +5 to the stats at 0
    private static readonly ItemHash EmptyTuning = ItemHash.From(2121121504u);      // Empty Tuning Mod Socket

    [Fact]
    public void The_login_page_is_Bungies_with_this_apps_id_and_the_state()
    {
        Assert.Equal(
            "https://www.bungie.net/en/oauth/authorize?client_id=55566&response_type=code&state=abc123",
            LoopDesigning.ToBungieLoginUrl("55566", "abc123"));
        Assert.Equal(
            [KeyValuePair.Create("grant_type", "authorization_code"), KeyValuePair.Create("code", "c0de"), KeyValuePair.Create("client_id", "55566")],
            LoopDesigning.ToBungieTokenForm("55566", "c0de"));
    }

    [Fact]
    public void The_login_pages_answer_gives_the_code_only_for_the_state_sent()
    {
        Assert.Equal(Ok("c0de"), LoopDesigning.ReadBungieCallback("""{ "code": "c0de", "state": "s1" }""", "s1"));
        Assert.Equal(Error("Bungie's answer isn't for this login. Try again."), LoopDesigning.ReadBungieCallback("""{ "code": "c0de", "state": "other" }""", "s1"));
        Assert.Equal(
            Error("You didn't allow Loopsmith to read your Destiny 2 information, so it can't load your stats."),
            LoopDesigning.ReadBungieCallback("""{ "error": "access_denied", "state": "s1" }""", "s1"));
        Assert.Equal(
            Error("Your browser didn't open Bungie's login window: allow pop-ups for this site and try again."),
            LoopDesigning.ReadBungieCallback("""{ "blocked": true }""", "s1"));
        Assert.Equal(Error("Login cancelled."), LoopDesigning.ReadBungieCallback("""{ "cancelled": true }""", "s1"));
    }

    [Fact]
    public void The_token_answer_gives_the_access_token_or_what_Bungie_said()
    {
        Assert.Equal(Ok("t0ken"), LoopDesigning.ReadBungieAccessToken("""{ "access_token": "t0ken", "token_type": "Bearer", "expires_in": 3600, "membership_id": "1" }"""));
        Assert.Equal(
            Error("Bungie didn't log you in: The authorization code is invalid."),
            LoopDesigning.ReadBungieAccessToken("""{ "error": "invalid_grant", "error_description": "The authorization code is invalid" }"""));
    }

    [Fact]
    public void The_membership_read_is_the_cross_save_primary_one()
    {
        var memberships = """
            { "ErrorCode": 1, "ErrorStatus": "Success", "Message": "Ok", "Response": {
              "destinyMemberships": [
                { "membershipType": 2, "membershipId": "222", "crossSaveOverride": 3 },
                { "membershipType": 3, "membershipId": "333", "crossSaveOverride": 3 } ],
              "primaryMembershipId": "333" } }
            """;
        var refused = """{ "ErrorCode": 5, "ErrorStatus": "SystemDisabled", "Message": "This system is temporarily disabled for maintenance." }""";

        var membership = LoopDesigning.ReadDestinyMembership(memberships);

        Assert.Equal(new Result<DestinyMembership, string>.Ok(new DestinyMembership(MembershipType.From(3), MembershipId.From("333"))), membership);
        Assert.Equal(
            "https://www.bungie.net/Platform/Destiny2/3/Profile/333/?components=102,200,201,205,300,305,310",
            LoopDesigning.ToBungieProfileUrl(new DestinyMembership(MembershipType.From(3), MembershipId.From("333"))));
        Assert.Equal(
            new Result<DestinyMembership, string>.Error("Bungie said: This system is temporarily disabled for maintenance. (SystemDisabled)."),
            LoopDesigning.ReadDestinyMembership(refused));
    }

    [Fact]
    public void The_profile_gives_the_armor_worn_carried_and_in_the_vault_and_each_characters_stats()
    {
        var profile = ReadProfile(
            [
                new FakeItem(Mask, "1", "vault", [FindRollPlug(StatKind.Grenade, 30), FullMasterwork], 11),
                new FakeItem(Vow, "2", "equipped", [FindRollPlug(StatKind.Grenade, 30)], 11),
                new FakeItem(ItemHash.From(4019651319u), "3", "carried", [], null),   // a weapon: no armor energy
            ]);

        Assert.Equal(["1", "2"], profile.Armor.Select(armor => armor.InstanceId.Value).Order());
        Assert.Equal(new ArmorPlace.Vault(), profile.Armor.Single(armor => armor.InstanceId == ItemInstanceId.From("1")).Place);
        Assert.Equal(new ArmorPlace.Equipped(GuardianClass.Hunter), profile.Armor.Single(armor => armor.InstanceId == ItemInstanceId.From("2")).Place);
        Assert.Equal(Optional.Some(11), profile.Armor.Single(armor => armor.InstanceId == ItemInstanceId.From("1")).EnergyCapacity);
    }

    [Fact]
    public void The_owners_loadout_adds_up_like_DIM_from_the_rolls_the_masterworks_the_mods_the_tuning_and_the_fragments()
    {
        var build = OpenOwnersBuild();
        var profile = ReadProfile([.. OwnAll(tuningSockets: 3)]);
        var choices = LoopDesigning.ListStatChoices(build, profile);

        var totals = Assert.IsType<Result<StatTotals, string>.Ok>(LoopDesigning.AddUpStats(build, choices)).Value;

        // Each piece: Grenade 30, Super 25, Class 20, and the masterwork's +5 to the three at 0 (Weapons, Health, Melee).
        Assert.Equal(5, choices.Pieces.Length);
        Assert.All(["Luminopotent Mask", "Shinobu's Vow", "Luminopotent Cuirass", "Luminopotent Strides", "Luminopotent Cloak"],
            piece => Assert.Equal(new StatPoints(5, 5, 20, 30, 25, 5), ReadPart(totals, piece)));
        Assert.Equal(new StatPoints(0, 0, 0, 50, 0, 0), ReadPart(totals, "Grenade Mod ×5"));
        Assert.Equal(new StatPoints(2, 2, 0, 0, 0, 2), ReadPart(totals, "Balanced Tuning ×2"));   // +1 to each of two pieces' stats at 0
        Assert.Equal(new StatPoints(0, 0, 0, -10, 0, 0), ReadPart(totals, "Spark of Shock"));
        Assert.Equal(new StatPoints(0, 0, 0, 0, 0, 10), ReadPart(totals, "Spark of Resistance"));
        Assert.Equal(new StatPoints(0, 0, 0, 0, 0, -10), ReadPart(totals, "Spark of Discharge"));
        Assert.Equal(new StatLine(ToKnownStat(27), ToKnownStat(27), ToKnownStat(100), ToKnownStat(190), ToKnownStat(125), ToKnownStat(27)), totals.Stats);
        Assert.Empty(totals.Notes);
    }

    [Fact]
    public void With_two_copies_of_a_piece_the_most_points_come_first_and_a_pick_changes_the_total()
    {
        var build = OpenOwnersBuild();
        var weaker = new FakeItem(Mask, "1002", "vault", [FindRollPlug(StatKind.Grenade, 20), FindRollPlug(StatKind.Super, 20), FindRollPlug(StatKind.Class, 20), FullMasterwork, EmptyTuning], 11);
        var profile = ReadProfile([.. OwnAll(tuningSockets: 3), weaker]);
        var choices = LoopDesigning.ListStatChoices(build, profile);

        var helmet = Assert.Single(choices.Pieces, choice => choice.Piece.Slot == ArmorSlot.Helmet);
        var wearing = build with { Build = build.Build with { ArmorCopies = [new ArmorCopy(Mask, ItemInstanceId.From("1002"))] } };
        var best = Assert.IsType<Result<StatTotals, string>.Ok>(LoopDesigning.AddUpStats(build, choices)).Value;
        var worn = Assert.IsType<Result<StatTotals, string>.Ok>(LoopDesigning.AddUpStats(wearing, choices)).Value;

        Assert.Equal(["1001", "1002"], helmet.Copies.Select(copy => copy.Armor.InstanceId.Value));
        Assert.Equal(Optional.Some(new StatPoints(5, 5, 20, 20, 20, 5)), helmet.Copies[1].Points);
        Assert.Equal(new StatPoints(5, 5, 20, 30, 25, 5), ReadPart(best, "Luminopotent Mask"));     // no copy named: the best one
        Assert.Equal(new StatPoints(5, 5, 20, 20, 20, 5), ReadPart(worn, "Luminopotent Mask"));     // the copy the build wears
    }

    [Fact]
    public void Without_a_tuning_socket_Balanced_Tuning_adds_nothing_and_says_so()
    {
        var build = OpenOwnersBuild();
        var choices = LoopDesigning.ListStatChoices(build, ReadProfile([.. OwnAll(tuningSockets: 1)]));

        var totals = Assert.IsType<Result<StatTotals, string>.Ok>(LoopDesigning.AddUpStats(build, choices)).Value;

        Assert.Equal(new StatPoints(1, 1, 0, 0, 0, 1), ReadPart(totals, "Balanced Tuning"));
        Assert.Equal(["Balanced Tuning found no tuning socket on these pieces, so it adds nothing."], totals.Notes);
    }

    [Fact]
    public void A_piece_the_player_doesnt_own_or_whose_roll_Loopsmith_cant_read_stops_the_adding_up()
    {
        var build = OpenOwnersBuild();
        var unowned = LoopDesigning.ListStatChoices(build, ReadProfile([.. OwnAll(tuningSockets: 0).Where(item => item.Hash != Mask)]));
        var unread = LoopDesigning.ListStatChoices(build, ReadProfile([.. OwnAll(tuningSockets: 0).Select(item => item.Hash == Mask ? item with { Plugs = [FullMasterwork] } : item)]));

        Assert.Equal(
            new Result<StatTotals, string>.Error("Your account has no Luminopotent Mask, so Loopsmith can't add up the build's stats from your armor."),
            LoopDesigning.AddUpStats(build, unowned));
        Assert.Equal(
            new Result<StatTotals, string>.Error("Loopsmith can't read the stats of your Luminopotent Mask (Bungie's data may be newer than Loopsmith's). Type the stats instead."),
            LoopDesigning.AddUpStats(build, unread));
    }

    [Fact]
    public void An_Armor_2_piece_at_full_energy_gets_plus_2_to_every_stat()
    {
        var build = OpenOwnersBuild();
        var legacy = new FakeItem(Mask, "2001", "vault", [FindRollPlug(StatKind.Grenade, 20)], 10);
        var choices = LoopDesigning.ListStatChoices(build, ReadProfile([.. OwnAll(tuningSockets: 0).Where(item => item.Hash != Mask), legacy]));

        var helmet = Assert.Single(choices.Pieces, choice => choice.Piece.Slot == ArmorSlot.Helmet);

        Assert.Equal(Optional.Some(new StatPoints(2, 2, 2, 22, 2, 2)), Assert.Single(helmet.Copies).Points);
    }

    [Fact]
    public void A_hand_written_build_names_no_pieces_so_its_stats_wait_for_picked_pieces_or_typing()
    {
        var build = OpenHandWrittenBuild();
        var choices = LoopDesigning.ListStatChoices(build, ReadProfile([.. OwnAll(tuningSockets: 0)]));

        var refused = Assert.IsType<Result<StatTotals, string>.Error>(LoopDesigning.AddUpStats(build, choices));

        Assert.StartsWith("The build names no armor piece for the helmet, the chest, the legs and the class item", refused.Failure, StringComparison.Ordinal);
        Assert.EndsWith("Pick a piece for it with ⋯, or type the stats.", refused.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Setting_the_stats_rewrites_only_the_build_files_stats_and_the_stat_checks_speak()
    {
        var session = LoopDesigning.CreateSession(OpenHandWrittenBuild(), new LoopDesign("Test", Optional.None<string>(), Optional.None<string>(), Optional.None<CatalogVersion>(), ReadBuildFile(), []));
        var stats = new StatLine(ToKnownStat(47), Optional.None<StatValue>(), ToKnownStat(60), ToKnownStat(145), ToKnownStat(27), ToKnownStat(40));

        var changed = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.SetStats(session, stats)).Value;

        Assert.Equal(stats, changed.Build.Build.Stats);
        Assert.Contains("stats: {melee: 40, grenade: 145, super: 27, class: 60, weapons: 47}", changed.Design.Build.Text, StringComparison.Ordinal);
        Assert.Equal(session.Design.Build.Text.Split('\n').Length, changed.Design.Build.Text.Split('\n').Length);   // one line changed, nothing added
        Assert.Contains(changed.Build.Issues, issue => issue.Severity == Severity.Warning && issue.Message.StartsWith("With 40 Melee, a Gambler's Dodge gives back less than a full melee charge", StringComparison.Ordinal));
        Assert.Contains(changed.Build.Issues, issue => issue.Severity == Severity.Warning && issue.Message.StartsWith("With 60 Class, a Combination Blow kill gives back less than a full class ability", StringComparison.Ordinal));
    }

    [Fact]
    public void A_build_file_without_stats_gets_them_added_at_its_end()
    {
        var session = LoopDesigning.CreateSession(OpenOwnersBuild(), new LoopDesign("Test", Optional.None<string>(), Optional.None<string>(), Optional.None<CatalogVersion>(), WriteOwnersBuildFile(), []));

        var changed = Assert.IsType<Result<DesignSession, string>.Ok>(LoopDesigning.SetStats(session, new StatLine(ToKnownStat(10), ToKnownStat(20), ToKnownStat(70), ToKnownStat(150), ToKnownStat(30), ToKnownStat(79)))).Value;

        Assert.EndsWith("stats: {health: 20, melee: 79, grenade: 150, super: 30, class: 70, weapons: 10}\n", changed.Design.Build.Text, StringComparison.Ordinal);
        Assert.Contains(changed.Build.Issues, issue => issue.Severity == Severity.Info && issue.Message.StartsWith("With 79 Melee, a Gambler's Dodge gives back a full melee charge only within 15 m", StringComparison.Ordinal));
        Assert.DoesNotContain(changed.Build.Issues, issue => issue.Message.Contains("Combination Blow kill", StringComparison.Ordinal));   // 70 Class is enough
    }

    [Fact]
    public void A_typed_stat_is_a_whole_number_from_0_to_200_or_empty_for_unknown()
    {
        Assert.Equal(new Result<Optional<StatValue>, string>.Ok(ToKnownStat(79)), LoopDesigning.ReadStatInput(StatKind.Melee, " 79 "));
        Assert.Equal(new Result<Optional<StatValue>, string>.Ok(Optional.None<StatValue>()), LoopDesigning.ReadStatInput(StatKind.Melee, ""));
        Assert.Equal(
            new Result<Optional<StatValue>, string>.Error("Melee: a whole number from 0 to 200, or empty for \"?\"."),
            LoopDesigning.ReadStatInput(StatKind.Melee, "201"));
        Assert.Equal(   // the inputs in the game's order (Health, Melee, Grenade, Super, Class, Weapons); those not given are "?"
            new StatLine(Optional.None<StatValue>(), ToKnownStat(47), Optional.None<StatValue>(), ToKnownStat(104), Optional.None<StatValue>(), Optional.None<StatValue>()),
            Assert.IsType<Result<StatLine, ImmutableArray<string>>.Ok>(LoopDesigning.ReadStatInputs(["47", " ", "104"])).Value);
        Assert.Equal(
            ["Melee: a whole number from 0 to 200, or empty for \"?\".", "Weapons: a whole number from 0 to 200, or empty for \"?\"."],
            Assert.IsType<Result<StatLine, ImmutableArray<string>>.Error>(LoopDesigning.ReadStatInputs(["1", "x", "", "", "", "-3"])).Failure);
    }

    [Fact]
    public void Bungies_answer_is_read_as_JSON_even_with_an_error_status_unless_Bungie_is_down()
    {
        Assert.Equal(Ok("""{ "ErrorCode": 99 }"""), LoopDesigning.ReadBungieAnswer(401, """{ "ErrorCode": 99 }"""));
        Assert.Equal(Error("Bungie didn't answer (HTTP 503). Try again later."), LoopDesigning.ReadBungieAnswer(503, "<html>down</html>"));
        Assert.Equal("Couldn't reach Bungie (Failed to fetch). Check your connection and try again.", LoopDesigning.DescribeBungieUnreachable("Failed to fetch"));
    }

    [Fact]
    public void A_copy_the_build_names_that_the_player_doesnt_have_gives_way_to_their_best()
    {
        var build = OpenOwnersBuild();
        var stale = build with { Build = build.Build with { ArmorCopies = [new ArmorCopy(Mask, ItemInstanceId.From("9999"))] } };
        var choices = LoopDesigning.ListStatChoices(stale, ReadProfile([.. OwnAll(tuningSockets: 0)]));

        var totals = Assert.IsType<Result<StatTotals, string>.Ok>(LoopDesigning.AddUpStats(stale, choices)).Value;

        Assert.Equal(new StatPoints(5, 5, 20, 30, 25, 5), ReadPart(totals, "Luminopotent Mask"));
    }

    /// <summary>
    /// An item of a fake profile: armor has energy; a weapon a damage type (Bungie's: 2 Arc); either a tier, a state (4:
    /// masterworked), a breaker and a power level; any item the bucket it is in (a finisher's: 3683254069). Its plugs are
    /// one per socket; <see cref="Switchable"/> gives a socket (by index) the reusable plugs Bungie lists for it (310).
    /// </summary>
    private sealed record FakeItem(ItemHash Hash, string Instance, string Place, ImmutableArray<ItemHash> Plugs, int? Energy, int? Tier = null, int State = 0, int DamageType = 0, uint Breaker = 0, uint Bucket = 0, int? Power = null, ImmutableDictionary<int, ImmutableArray<ItemHash>>? Switchable = null);

    /// <summary>Every piece of the owner's loadout as a Grenade 30 · Super 25 · Class 20 roll, masterworked; the first ones with a tuning socket.</summary>
    private static IEnumerable<FakeItem> OwnAll(int tuningSockets) =>
        Legendary.Prepend(Vow).Select((hash, index) => new FakeItem(
            hash,
            hash == Mask ? "1001" : $"{1100 + index}",
            index == 0 ? "equipped" : "vault",
            [FindRollPlug(StatKind.Grenade, 30), FindRollPlug(StatKind.Super, 25), FindRollPlug(StatKind.Class, 20), FullMasterwork, .. index < tuningSockets ? [EmptyTuning] : ImmutableArray<ItemHash>.Empty],
            11));

    /// <summary>A Bungie profile answer holding these items (in the vault, or worn or carried by a Hunter) and the Hunter's stats.</summary>
    private static BungieProfile ReadProfile(ImmutableArray<FakeItem> items)
    {
        object ToItem(FakeItem item) => new { itemHash = item.Hash.Value, itemInstanceId = item.Instance, state = item.State, bucketHash = item.Bucket };
        object ToInstance(FakeItem item) => new Dictionary<string, object>(
        [
            KeyValuePair.Create<string, object>("damageType", item.DamageType),
            KeyValuePair.Create<string, object>("breakerTypeHash", item.Breaker),
            .. item.Energy is { } energy ? [KeyValuePair.Create<string, object>("energy", new { energyCapacity = energy })] : Array.Empty<KeyValuePair<string, object>>(),
            .. item.Tier is { } tier ? [KeyValuePair.Create<string, object>("gearTier", tier)] : Array.Empty<KeyValuePair<string, object>>(),
            .. item.Power is { } power ? [KeyValuePair.Create<string, object>("primaryStat", new { statHash = 1935470627u, value = power })] : Array.Empty<KeyValuePair<string, object>>(),
        ]);
        var answer = new
        {
            ErrorCode = 1,
            ErrorStatus = "Success",
            Message = "Ok",
            Response = new
            {
                profileInventory = new { data = new { items = items.Where(item => item.Place == "vault").Select(ToItem).ToArray() } },
                characters = new { data = new Dictionary<string, object> { ["c1"] = new { classType = 1 } } },
                characterEquipment = new { data = new Dictionary<string, object> { ["c1"] = new { items = items.Where(item => item.Place == "equipped").Select(ToItem).ToArray() } } },
                characterInventories = new { data = new Dictionary<string, object> { ["c1"] = new { items = items.Where(item => item.Place == "carried").Select(ToItem).ToArray() } } },
                itemComponents = new
                {
                    instances = new { data = items.ToDictionary(item => item.Instance, ToInstance) },
                    sockets = new { data = items.ToDictionary(item => item.Instance, item => new { sockets = item.Plugs.Select(plug => new { plugHash = plug.Value, isEnabled = true }).ToArray() }) },
                    reusablePlugs = new
                    {
                        data = items.Where(item => item.Switchable is not null).ToDictionary(item => item.Instance, item => new
                        {
                            plugs = item.Switchable!.ToDictionary(
                                socket => socket.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                socket => socket.Value.Select(plug => new { plugItemHash = plug.Value, canInsert = true, enabled = true }).ToArray()),
                        }),
                    },
                },
            },
        };
        var json = JsonSerializer.Serialize(answer);
        return Assert.IsType<Result<BungieProfile, string>.Ok>(LoopDesigning.ReadBungieProfile(json)).Value;
    }

    /// <summary>An armor-stat plug of the excerpt that gives just this: a piece's roll in one stat.</summary>
    private static ItemHash FindRollPlug(StatKind stat, int value) =>
        Catalog.Manifest.StatPlugs
            .Where(entry => entry.Value is StatPlug.Rolled { Stats: [var only] } && only.Stat == stat && only.Value == value)
            .Select(entry => entry.Key)
            .Order()
            .First();

    private static StatPoints ReadPart(StatTotals totals, string source) =>
        Assert.Single(totals.Parts, part => part.Source == source).Points;

    private static Optional<StatValue> ToKnownStat(int value) =>
        Optional.Some(StatValue.From(value));

    private static Result<string, string> Ok(string value) => new Result<string, string>.Ok(value);

    private static Result<string, string> Error(string why) => new Result<string, string>.Error(why);

    private static ValidatedBuild OpenOwnersBuild()
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        var json = File.ReadAllText(RepoFiles.ToPath(path));                                     // impure
        return LoopDesigning.StartSavedDimDesign(Catalog, new SourceText(path, json))
            .Match(ok => ok.Value.Build, error => throw new InvalidOperationException(error.Failure));
    }

    private static SourceText WriteOwnersBuildFile()
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        var json = File.ReadAllText(RepoFiles.ToPath(path));                                     // impure
        return LoopDesigning.StartSavedDimDesign(Catalog, new SourceText(path, json))
            .Match(ok => ok.Value.Design.Build, error => throw new InvalidOperationException(error.Failure));
    }

    private static SourceText ReadBuildFile()
    {
        const string path = "builds/skip-grenade-hunter/build.yaml";
        return new SourceText(path, File.ReadAllText(RepoFiles.ToPath(path)));                  // impure
    }

    private static ValidatedBuild OpenHandWrittenBuild() =>
        BuildFileParsing.ParseBuildFile(ReadBuildFile(), Catalog.Manifest)
            .Bind(build => BuildValidation.ValidateBuild(build, Catalog))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}

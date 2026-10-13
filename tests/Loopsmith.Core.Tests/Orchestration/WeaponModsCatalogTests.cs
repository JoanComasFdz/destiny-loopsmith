using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.LoopFiles;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// Weapon mods, in the real rules: every one the Compendium lists is known (Synergy with rules, the rest inert, each
/// saying why), named as Bungie's manifest names it; a weapon holds one in its mod socket — picked in the designer, read
/// from a DIM link, written in the build file — and Synergy spawns its weapon's elemental pickup, for sure on the kills
/// that fill its counter and as a chance on fewer.
/// </summary>
public sealed class WeaponModsCatalogTests
{
    private const uint Synergy = 3820243349;
    private const uint EnhancedSynergy = 3634656993;
    private const uint IcarusGrip = 941997506;
    private const uint AdeptRange = 299264772;   // a mod Crisis Inverted takes that the Compendium doesn't list
    private const int FestivalFlight = 0;        // kinetic, Strand
    private const int CrisisInverted = 2;        // energy, Arc

    private static readonly Result<ImmutableArray<SourceText>, string> RuleFiles = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"));   // impure

    private static readonly RuleCatalog Catalog =
        RuleFiles.Bind(CatalogLoading.ParseCatalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static ImmutableArray<BuildElement> WeaponMods =>
        [.. Catalog.Elements.Values.Where(element => element.Kind == ElementKind.WeaponMod)];

    [Fact]
    public void Every_weapon_mod_is_inert_and_says_why_but_Synergy_which_has_rules()
    {
        var withRules = WeaponMods.Where(mod => !mod.Rules.IsEmpty).Select(mod => mod.Id.Value).Order(StringComparer.Ordinal);
        var silent = WeaponMods.Where(mod => mod.Rules.IsEmpty && mod.Passives.IsEmpty && !mod.Inert.IsSome()).Select(mod => mod.Id.Value);

        Assert.Equal(["synergy", "synergy-enhanced"], withRules);
        Assert.Empty(silent);
        Assert.Equal(55, WeaponMods.Length);   // the tab's 36 mods and the 19 enhanced versions the manifest has of them
    }

    [Fact]
    public void Every_hash_of_a_weapon_mod_is_in_the_manifest_excerpt_under_its_name_and_an_enhanced_one_only_on_an_enhanced_element()
    {
        var wrong = WeaponMods
            .SelectMany(mod => mod.Hashes.Select(hash => (Mod: mod, Hash: hash, Item: Catalog.Manifest.FindItem(hash))))
            .Where(entry => entry.Item.Match(
                item => item.Value.Kind is not ManifestKind.WeaponMod kind
                    || item.Value.Name != entry.Mod.Name.Replace(" (enhanced)", "", StringComparison.Ordinal)
                    || kind.Enhanced != (entry.Mod.Id.Value.EndsWith("-enhanced", StringComparison.Ordinal) || entry.Mod.Name.StartsWith("Enhanced ", StringComparison.Ordinal)),
                _ => true))
            .Select(entry => $"{entry.Mod.Name} {entry.Hash.Value}");

        Assert.Empty(wrong);
    }

    [Fact]
    public void A_picked_mod_goes_into_the_weapons_entry_and_removing_it_writes_no_mod()
    {
        var session = StartOwnersDimDesign();

        var picked = PickMod(session, FestivalFlight, Optional.Some(ItemHash.From(IcarusGrip)));
        var removed = PickMod(picked, FestivalFlight, Optional.None<ItemHash>());

        Assert.Equal(Optional.Some(ItemHash.From(IcarusGrip)), picked.Build.Build.Weapons[FestivalFlight].Mod);
        Assert.Contains("mod: 941997506}", picked.Design.Build.Text, StringComparison.Ordinal);
        Assert.Contains(picked.Build.Equipped, equipped => equipped.Element.Id.Value == "icarus-grip");
        Assert.DoesNotContain(picked.Build.Issues, issue => issue.Severity == Severity.Warning);
        Assert.Equal(Optional.None<ItemHash>(), removed.Build.Build.Weapons[FestivalFlight].Mod);
        Assert.DoesNotContain("mod:", removed.Design.Build.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(removed.Build.Equipped, equipped => equipped.Element.Id.Value == "icarus-grip");
    }

    [Fact]
    public void A_mod_the_weapons_socket_doesnt_take_cant_be_picked_and_one_the_rules_dont_know_sets_nothing_off()
    {
        var session = StartOwnersDimDesign();

        var refused = Assert.IsType<Result<DesignSession, string>.Error>(LoopDesigning.PickWeaponMod(session, FestivalFlight, Optional.Some(ItemHash.From(AdeptRange))));
        var unknown = PickMod(session, CrisisInverted, Optional.Some(ItemHash.From(AdeptRange)));

        Assert.Equal("Adept Range (Weapon Mod) isn't a mod Festival Flight's mod socket takes in the manifest excerpt.", refused.Failure);
        Assert.Contains(unknown.Build.Issues, issue => issue.Severity == Severity.Info
            && issue.Message == "Crisis Inverted's mod: Adept Range (Weapon Mod) — not in the rule catalog yet, so it sets nothing off.");
    }

    [Fact]
    public void A_build_file_mod_its_socket_doesnt_take_is_a_warning()
    {
        var build = StartOwnersDimDesign().Build.Build;
        var weapons = build.Weapons.SetItem(FestivalFlight, build.Weapons[FestivalFlight] with { Mod = Optional.Some(ItemHash.From(AdeptRange)) });

        var validated = BuildValidation.ValidateBuild(build with { Weapons = weapons }, Catalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Contains(validated.Issues, issue => issue.Severity == Severity.Warning
            && issue.Message == "Festival Flight's mod: Adept Range (Weapon Mod) isn't a mod its mod socket takes in the manifest excerpt.");
    }

    [Fact]
    public void Synergy_spawns_its_weapons_pickup_for_sure_on_twelve_kills_as_a_chance_on_fewer_and_never_for_another_weapon()
    {
        var session = PickMod(StartOwnersDimDesign(), FestivalFlight, Optional.Some(ItemHash.From(Synergy)));

        var played = new PlayerAction[]
            {
                new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.From(12)),
                new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One),
                new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.From(12)),
            }
            .Aggregate(session, (design, action) => LoopDesigning.AppendStep(design, action, Optional.None<string>()));
        var synergy = played.Resolutions.Select(step => step.Fired.Where(fired => fired.Source.Value == "synergy").ToImmutableArray()).ToImmutableArray();

        Assert.Equal([(Likelihood.Always, "tangle")], synergy[0].Select(DescribeSpawn));
        Assert.Equal([(Likelihood.Chance, "tangle")], synergy[1].Select(DescribeSpawn));
        Assert.Empty(synergy[2]);   // Crisis Inverted's kills: Synergy is Festival Flight's
        Assert.Contains(12, LoopDesigning.ListNamedTargetCounts(session.Build).Select(count => count.Value));
    }

    [Fact]
    public void Synergys_rules_read_with_this_weapon_and_the_kills_that_set_each_off()
    {
        var synergy = Catalog.Elements[ElementId.From("synergy")];
        var none = Optional.None<DamageType>();

        Assert.Equal(
            ["Kill 12+ with this weapon (Kinetic)", "Kill 1–11 with this weapon (Kinetic)"],
            synergy.Rules.Take(2).Select(rule => Loopsmith.Core.Phrasing.DomainPhrasing.DescribeTrigger(Catalog.Glossary, rule.On, none)));
    }

    [Fact]
    public void Synergy_on_two_weapons_is_each_weapons_own_with_its_elements_pickup()
    {
        var session = PickMod(PickMod(StartOwnersDimDesign(), FestivalFlight, Optional.Some(ItemHash.From(Synergy))), CrisisInverted, Optional.Some(ItemHash.From(Synergy)));

        var played = LoopDesigning.AppendStep(session, new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.From(12)), Optional.None<string>());

        Assert.Equal(2, session.Build.Equipped.Count(equipped => equipped.Element.Id.Value == "synergy"));
        Assert.Equal(
            [(Likelihood.Always, "ionic-trace")],
            played.Resolutions[^1].Fired.Where(fired => fired.Source.Value == "synergy").Select(DescribeSpawn));
    }

    [Fact]
    public void An_enhanced_mod_fits_only_a_weapon_known_to_be_tier_4_or_5()
    {
        var build = StartOwnersDimDesign().Build;
        var flight = build.Build.Weapons[FestivalFlight];

        bool CanTake(Optional<GearTier> tier, uint mod) =>
            WeaponRolling.ListModOptions(Catalog, flight, tier).Single(option => option.Mod.Hash.Value == mod).CanTake;

        Assert.False(CanTake(Optional.None<GearTier>(), EnhancedSynergy));
        Assert.False(CanTake(Optional.Some(GearTier.From(3)), EnhancedSynergy));
        Assert.True(CanTake(Optional.Some(GearTier.From(4)), EnhancedSynergy));
        Assert.True(CanTake(Optional.None<GearTier>(), Synergy));
        Assert.True(LoopDesigning.ListWeaponMods(build, FestivalFlight, Optional.None<BungieProfile>()).Single(option => option.Mod.Hash.Value == EnhancedSynergy).Enhanced);
    }

    [Fact]
    public void A_dim_link_weapons_mod_is_the_weapons_mod_not_a_left_out_item()
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        var json = File.ReadAllText(RepoFiles.ToPath(path));                                     // impure
        var share = JsonNode.Parse(json)!;                                                       // pure
        share["loadout"]!["equipped"]![0]!["socketOverrides"]!["4"] = Synergy;                   // Festival Flight with Synergy

        var session = LoopDesigning.StartSavedDimDesign(Catalog, new SourceText(path, share.ToJsonString()))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Equal(Optional.Some(ItemHash.From(Synergy)), session.Build.Build.Weapons[FestivalFlight].Mod);
        Assert.DoesNotContain(session.Build.Build.LeftOut, item => item.Hash.Value == Synergy);
        Assert.Contains(session.Build.Equipped, equipped => equipped.Element.Id.Value == "synergy");
    }

    [Fact]
    public void A_build_files_weapon_mod_is_read_back_as_it_is_written()
    {
        var build = PickMod(StartOwnersDimDesign(), FestivalFlight, Optional.Some(ItemHash.From(Synergy))).Build.Build;

        var written = BuildFileWriting.WriteBuildFile(build, []);
        var read = Loopsmith.Core.BuildParsing.BuildFileParsing.ParseBuildFile(new SourceText("written.yaml", written))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Equal(Optional.Some(ItemHash.From(Synergy)), read.Weapons[FestivalFlight].Mod);
        Assert.Equal(Optional.None<ItemHash>(), read.Weapons[CrisisInverted].Mod);
    }

    private static (Likelihood, string) DescribeSpawn(FiredRule fired) =>
        (fired.Likelihood, string.Join(",", fired.Outcomes.Select(outcome => outcome.Outcome).OfType<Outcome.Spawn>().Select(spawn => spawn.Pickup.Value)));

    private static DesignSession StartOwnersDimDesign()
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        var json = File.ReadAllText(RepoFiles.ToPath(path));                                     // impure
        return LoopDesigning.StartSavedDimDesign(Catalog, new SourceText(path, json))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
    }

    private static DesignSession PickMod(DesignSession session, int weapon, Optional<ItemHash> mod) =>
        LoopDesigning.PickWeaponMod(session, weapon, mod).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}

using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.StepPlaying;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// The owner's DIM build, item by item: what has no part in a loop says so (its legendary armor pieces, Triple Jump
/// (an inert element of the rules; a jump they don't have, left out), Feeding Frenzy), the resistance mods show in the
/// step's damage resistance, Powerful Attraction collects the orbs on the ground when you dodge, Ashes to Assets answers a
/// grenade kill, and Charged Up raises the Armor Charge cap the player declares reached.
/// </summary>
public sealed class OwnersBuildModsTests
{
    private static readonly Result<ImmutableArray<SourceText>, string> RuleFiles = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"));   // impure

    private static readonly RuleCatalog Catalog =
        RuleFiles.Bind(CatalogLoading.ParseCatalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static readonly StatusId ArmorCharge = StatusId.From("armor-charge");

    private static readonly PickupId Orb = PickupId.From("orb-of-power");

    [Fact]
    public void Legendary_armor_pieces_Triple_Jump_and_the_stat_mods_have_no_part_in_a_loop_so_nothing_is_missing()
    {
        var build = Play().Build;
        var mask = new LeftOutItem(LoadoutPart.Item, ItemHash.From(593554567u));                // Luminopotent Mask
        var tripleJump = new LeftOutItem(LoadoutPart.SubclassPlug, ItemHash.From(95544328u));       // as a build file from before jumps were elements lists it
        var grenadeMod = new LeftOutItem(LoadoutPart.ArmorMod, ItemHash.From(1435557120u));
        var balancedTuning = new LeftOutItem(LoadoutPart.ArmorMod, ItemHash.From(3122197216u));

        var missing = LoopDesigning.ListMissing(build);
        var leftOut = build.Build.LeftOut;

        Assert.IsType<LeftOutReason.NoLoopEffect>(LoopDesigning.ClassifyLeftOut(build, mask));
        Assert.Equal(new LeftOutReason.NoLoopEffect("A jump: movement has no part in a loop."), LoopDesigning.ClassifyLeftOut(build, tripleJump));
        Assert.Equal(
            new LeftOutReason.NoLoopEffect("A stat mod (+10 Grenade): it changes only stats, and the build's stats count it when they add up from your armor (after the Bungie login). It sets nothing off."),
            LoopDesigning.ClassifyLeftOut(build, grenadeMod));
        Assert.StartsWith("Balanced Tuning (+1 to each stat where its piece has 0, one piece per tuning socket): it changes only stats", Assert.IsType<LeftOutReason.NoLoopEffect>(LoopDesigning.ClassifyLeftOut(build, balancedTuning)).Why, StringComparison.Ordinal);
        Assert.Equal((5, 2), (leftOut.Count(item => item == grenadeMod), leftOut.Count(item => item == balancedTuning)));
        Assert.Empty(missing);
        Assert.Equal<AbilityPlug>(new AbilityPlug.Known(ElementId.From("triple-jump")), build.Build.Abilities.Jump);
        Assert.DoesNotContain(tripleJump, leftOut);
        Assert.DoesNotContain(build.Issues, issue => issue.Message.StartsWith("Left out of the build:", StringComparison.Ordinal));
    }

    [Fact]
    public void A_left_out_fragment_with_stats_is_still_a_gap_not_a_stat_mod()
    {
        var build = Play().Build;
        var fragment = Catalog.Manifest.StatPlugs.First(entry => entry.Value is StatPlug.Fragment).Key;

        Assert.IsType<LeftOutReason.NotInRules>(LoopDesigning.ClassifyLeftOut(build, new LeftOutItem(LoadoutPart.SubclassPlug, fragment)));
    }

    [Fact]
    public void An_inert_element_is_known_so_it_raises_no_missing_rules_warning()
    {
        var build = Play().Build;
        var inert = build.Equipped.Where(equipped => equipped.Element.Inert.IsSome()).Select(equipped => equipped.Element.Name).ToImmutableArray();

        Assert.Equal(["Feeding Frenzy", "Skip Grenade", "Triple Jump"], inert.Order());
        Assert.DoesNotContain(build.Issues, issue => issue.Message.Contains("no authored rules", StringComparison.Ordinal));
    }

    [Fact]
    public void Powerful_Attraction_collects_the_orb_on_the_ground_when_you_dodge_and_the_pickup_sets_off_Recuperation()
    {
        var played = Play(
            new PlayerAction.UseClassAbility(),                                                    // arms Reaper
            new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One),     // Reaper's orb lands
            new PlayerAction.UseClassAbility());                                                   // Powerful Attraction

        var dodge = played.Resolutions[2];

        Assert.Contains(Orb, played.Resolutions[1].State.ListPickups());
        Assert.DoesNotContain(Orb, dodge.State.ListPickups());
        var attraction = Assert.Single(dodge.Fired, fired => fired.Source.Value == "powerful-attraction");
        Assert.Equal(new AppliedOutcome(new Outcome.Collect(Orb), Certainty.Known, Optional.None<string>(), Optional.None<StackSpend>()), Assert.Single(attraction.Outcomes));
        Assert.Contains(dodge.Fired, fired => fired.Source.Value == "recuperation" && fired.Trigger == new GameEvent.PickedUp(Orb));
        Assert.Contains(dodge.State.Buffs, buff => buff.Status == ArmorCharge);                     // the orb's Armor Charge
    }

    [Fact]
    public void The_analysis_says_the_dodge_picks_up_the_orb_the_kill_before_it_dropped()
    {
        var played = Play(
            new PlayerAction.UseClassAbility(),
            new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One),
            new PlayerAction.UseClassAbility());

        var dodge = LoopDesigning.AnalyzeDesign(played).Steps[2];

        var orb = Assert.Single(dodge.Needs, need => need.What == "Orb of Power");
        Assert.Equal(1, Assert.IsType<NeedProvider.EarlierStep>(orb.Provider).StepIndex);
        Assert.Contains(dodge.InOrder, element => element.Source.Value == "powerful-attraction");
    }

    [Fact]
    public void Powerful_Attraction_with_nothing_on_the_ground_collects_nothing()
    {
        var played = Play(new PlayerAction.UseClassAbility());

        var attraction = Assert.Single(played.Resolutions[0].Fired, fired => fired.Source.Value == "powerful-attraction");

        Assert.Equal(Optional.Some("nothing on the ground"), Assert.Single(attraction.Outcomes).Caveat);
        Assert.DoesNotContain(played.Resolutions[0].Fired, fired => fired.Source.Value == "recuperation");
    }

    [Fact]
    public void Ashes_to_Assets_gives_Super_energy_on_a_grenade_kill()
    {
        var played = Play(new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One));

        var ashes = Assert.Single(played.Resolutions[0].Fired, fired => fired.Source.Value == "ashes-to-assets");

        Assert.Equal([new Outcome.GrantEnergy(AbilityKind.Super, new EnergyGrant.Fraction(new GameValue.Unknown()))], ashes.Outcomes.Select(outcome => outcome.Outcome));
    }

    [Fact]
    public void Charged_Up_raises_the_Armor_Charge_the_player_declares_at_max_to_x4_and_with_two_copies_to_x5()
    {
        var played = Play(
            new PlayerAction.UseClassAbility(),
            new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One),
            new PlayerAction.UseClassAbility(),
            new PlayerAction.Declare(new StateDeclaration.ReachMax(ArmorCharge)));
        var twoCopies = Revalidate(played.Build, played.Build.Build with { ArmorMods = played.Build.Build.ArmorMods.Add(ElementId.From("charged-up")) });

        var state = LoopDesigning.ShapeStepState(played, played.Resolutions[3]);

        Assert.Equal(Optional.Some<GameValue>(new GameValue.Known(4)), played.Build.ReadStackCap(ArmorCharge));
        Assert.Equal("Armor Charge at max (x4)", played.Build.DescribeAction(played.Design.Steps[3].Action));
        var charge = Assert.Single(state.Buffs, entry => entry.Keyword == new KeywordRef.Status(ArmorCharge));
        Assert.Equal(("Armor Charge x4 (at max)", Optional.Some("up to x4")), (charge.Text, charge.Facts));
        Assert.Equal(Optional.Some<GameValue>(new GameValue.Known(5)), twoCopies.ReadStackCap(ArmorCharge));            // the Compendium's "4 | 5 | 6"
        Assert.Equal(Optional.Some<GameValue>(new GameValue.Known(10)), played.Build.ReadStackCap(StatusId.From("bolt-charge")));   // nothing raises it
        Assert.Equal(Optional.None<GameValue>(), played.Build.ReadStackCap(StatusId.From("amplified")));                 // doesn't stack
    }

    private static ValidatedBuild Revalidate(ValidatedBuild played, Build build) =>
        BuildValidation.ValidateBuild(build, played.Catalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static DesignSession Play(params PlayerAction[] actions)
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        var json = File.ReadAllText(RepoFiles.ToPath(path));                                     // impure
        var session = LoopDesigning.StartSavedDimDesign(Catalog, new SourceText(path, json))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        return actions.Aggregate(session, (played, action) => LoopDesigning.AppendStep(played, action, Optional.None<string>()));
    }
}

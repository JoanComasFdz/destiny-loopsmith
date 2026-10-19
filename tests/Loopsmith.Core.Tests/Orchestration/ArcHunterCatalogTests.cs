using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// The Arc Hunter in the real rules: every plug the Arcstrider holds is an element (rules, passives or why it changes no
/// loop), named as Bungie's manifest names it, and the new ones played in an Arc Hunter with their subclass
/// swapped in — Disorienting Blow, Flashbang, Lethal Current and the Sparks set off what the Compendium says.
/// </summary>
public sealed class ArcHunterCatalogTests
{
    private static readonly Result<ImmutableArray<SourceText>, string> RuleFiles = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"));   // impure

    private static readonly RuleCatalog Catalog =
        RuleFiles.Bind(CatalogLoading.ParseCatalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    /// <summary>An Arcstrider with its abilities, a Strand Special, an Arc Primary and an Arc Heavy weapon: each test equips its aspects and fragments.</summary>
    private const string BuildText = """
        name: Arc Hunter
        class: hunter
        subclass: arc
        grenade: skip-grenade
        melee: combination-blow
        classAbility: gamblers-dodge
        weapons:
          - {hash: 4019651319} # Festival Flight
          - {hash: 2888266564} # Crisis Inverted
          - {hash: 3325463374} # Thunderlord
        """;

    private static readonly StatusId Blind = StatusId.From("blind");
    private static readonly StatusId Jolt = StatusId.From("jolt");
    private static readonly StatusId Amplified = StatusId.From("amplified");
    private static readonly StatusId BoltCharge = StatusId.From("bolt-charge");
    private static readonly StatusId LethalCurrentArmed = StatusId.From("lethal-current");
    private static readonly PickupId IonicTrace = PickupId.From("ionic-trace");
    private static readonly PickupId OrbOfPower = PickupId.From("orb-of-power");

    private static ImmutableArray<BuildElement> ArcHunterElements =>
        [.. Catalog.Elements.Values.Where(element => element.Class == Optional.Some(GuardianClass.Hunter) && element.Affinity == Affinity.Arc)];

    [Fact]
    public void Every_plug_of_the_Arcstrider_is_an_element_of_the_rules_for_the_Hunter()
    {
        var build = Play(build => build).Build;
        var plugs = LoopDesigning.ListSubclassOptions(build, SubclassSlot.Super)
            .Concat(LoopDesigning.ListSubclassOptions(build, SubclassSlot.Grenade))
            .Concat(LoopDesigning.ListSubclassOptions(build, SubclassSlot.Melee))
            .Concat(LoopDesigning.ListSubclassOptions(build, SubclassSlot.ClassAbility))
            .Concat(LoopDesigning.ListSubclassOptions(build, SubclassSlot.Jump))
            .Concat(LoopDesigning.ListSubclassOptions(build, SubclassSlot.Aspect))
            .Concat(LoopDesigning.ListSubclassOptions(build, SubclassSlot.Fragment))
            .ToImmutableArray();

        var missing = plugs.Where(plug => !LoopDesigning.FindSubclassElement(build, plug).IsSome()).Select(plug => plug.Value);

        Assert.Empty(missing);
        Assert.Equal(38, plugs.Length);   // 3 supers, 7 grenades, 2 melees, 2 class abilities, 4 jumps, 4 aspects, 16 fragments
    }

    [Fact]
    public void Every_Arc_Hunter_element_has_rules_passives_or_says_why_it_changes_no_loop()
    {
        var silent = ArcHunterElements.Where(element => element.Rules.IsEmpty && element.Passives.IsEmpty && !element.Inert.IsSome()).Select(element => element.Id.Value);

        Assert.Empty(silent);
    }

    [Fact]
    public void Every_hash_of_an_Arc_Hunter_element_is_in_the_manifest_excerpt_under_its_name()
    {
        var wrong = ArcHunterElements
            .SelectMany(element => element.Hashes.Select(hash => (element.Name, Hash: hash, Item: Catalog.Manifest.FindItem(hash))))
            .Where(entry => entry.Item.Match(item => item.Value.Name != entry.Name, _ => true))
            .Select(entry => $"{entry.Name} {entry.Hash.Value}");

        Assert.Empty(wrong);
    }

    [Fact]
    public void Lethal_Current_opens_two_fragment_slots_and_with_each_other_aspect_the_manifests_count()
    {
        var build = Play(build => build).Build;
        int? Count(params uint[] aspects) =>
            LoopDesigning.CountFragmentSlots(build, [.. aspects.Select(ItemHash.From)]).Slots.Match<int?>(some => some.Value.Value, _ => null);

        Assert.Equal(4, Count(4194622038u, 4194622036u));   // Flow State
        Assert.Equal(4, Count(4194622038u, 4194622037u));   // Tempest Strike
        Assert.Equal(5, Count(4194622038u, 4194622039u));   // Ascension
    }

    [Fact]
    public void A_Disorienting_Blow_hit_blinds_the_pack_and_gives_Amplified_and_two_Bolt_Charge_but_not_the_sliding_uppercut()
    {
        var played = Play(build => Equip(build, melee: "disorienting-blow", aspects: ["tempest-strike", "flow-state"], fragments: []),
            new PlayerAction.CastAbility(OffensiveAbility.Melee, HitOutcome.Damage, TargetCount.One),
            new PlayerAction.CastAbility(OffensiveAbility.Melee, HitOutcome.Damage, TargetCount.One, AbilityInput.Slide));

        var blow = Assert.Single(played.Resolutions[0].Fired, fired => fired.Source.Value == "disorienting-blow");
        Assert.Contains(blow.Outcomes, outcome => outcome.Outcome is Outcome.DebuffTarget { Status: var status } && status == Blind);
        Assert.Contains(blow.Outcomes, outcome => outcome.Outcome is Outcome.ApplyBuff { Status: var status } && status == Amplified);
        Assert.Contains(blow.Outcomes, outcome => outcome.Outcome is Outcome.ApplyBuff { Status: var status, Stacks: StackGrant.Fixed { Count.Value: 2 } } && status == BoltCharge);
        Assert.Contains(Blind, played.Resolutions[0].State.Target.Debuffs);
        Assert.DoesNotContain(played.Resolutions[1].Fired, fired => fired.Source.Value == "disorienting-blow");
    }

    [Fact]
    public void A_Flashbang_hit_blinds_the_pack()
    {
        var played = Play(build => Equip(build, grenade: "flashbang-grenade", aspects: ["tempest-strike", "flow-state"], fragments: []),
            new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage, TargetCount.One));

        var flashbang = Assert.Single(played.Resolutions[0].Fired, fired => fired.Source.Value == "flashbang-grenade");
        Assert.Contains(flashbang.Outcomes, outcome => outcome.Outcome is Outcome.DebuffTarget { Status: var status } && status == Blind);
        Assert.Contains(Blind, played.Resolutions[0].State.Target.Debuffs);
    }

    [Fact]
    public void Lethal_Current_arms_on_a_dodge_jolts_with_the_next_melee_hit_and_a_punch_blinds_a_jolted_enemy()
    {
        var played = Play(build => Equip(build, aspects: ["lethal-current", "flow-state"], fragments: []),
            new PlayerAction.UseClassAbility(),
            new PlayerAction.CastAbility(OffensiveAbility.Melee, HitOutcome.Damage, TargetCount.One),
            new PlayerAction.CastAbility(OffensiveAbility.Melee, HitOutcome.Damage, TargetCount.One));

        Assert.Contains(played.Resolutions[0].State.Buffs, buff => buff.Status == LethalCurrentArmed);
        var armed = played.Resolutions[1].Fired.Where(fired => fired.Source.Value == "lethal-current").SelectMany(fired => fired.Outcomes).Select(outcome => outcome.Outcome).ToImmutableArray();
        Assert.Contains(armed, outcome => outcome is Outcome.RemoveBuff { Status: var status } && status == LethalCurrentArmed);
        Assert.Contains(armed, outcome => outcome is Outcome.DebuffTarget { Status: var status } && status == Jolt);
        Assert.Contains(armed, outcome => outcome is Outcome.StrikeTarget);
        Assert.DoesNotContain(played.Resolutions[1].State.Buffs, buff => buff.Status == LethalCurrentArmed);
        var punch = Assert.Single(played.Resolutions[2].Fired, fired => fired.Source.Value == "lethal-current");
        Assert.Contains(punch.Outcomes, outcome => outcome.Outcome is Outcome.DebuffTarget { Status: var status } && status == Blind);
    }

    [Fact]
    public void Spark_of_Ions_creates_an_Ionic_Trace_on_a_jolted_kill_and_a_Bolt_Charge_kill()
    {
        var played = Play(build => Equip(build, melee: "disorienting-blow", aspects: ["tempest-strike", "flow-state"], fragments: ["spark-of-ions"]),
            new PlayerAction.CastAbility(OffensiveAbility.Melee, HitOutcome.Damage, TargetCount.One, AbilityInput.Slide),   // Tempest Strike jolts
            new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One),
            new PlayerAction.CastAbility(OffensiveAbility.Melee, HitOutcome.Damage, TargetCount.One),                      // Bolt Charge
            new PlayerAction.Declare(new StateDeclaration.ReachMax(BoltCharge)),
            new PlayerAction.Declare(new StateDeclaration.NewPack()),
            new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage, TargetCount.One));                   // the strike kills

        Assert.Contains(played.Resolutions[1].Fired, fired => fired.Source.Value == "spark-of-ions" && fired.Outcomes.Any(outcome => outcome.Outcome is Outcome.Spawn { Pickup: var pickup } && pickup == IonicTrace));
        Assert.Contains(played.Resolutions[5].Fired, fired => fired.Source.Value == "spark-of-ions" && fired.Outcomes.Any(outcome => outcome.Outcome is Outcome.Spawn { Pickup: var pickup } && pickup == IonicTrace));
    }

    [Fact]
    public void Spark_of_Volts_makes_a_finisher_give_Amplified_and_Bolt_Charge()
    {
        var played = Play(build => Equip(build, aspects: ["tempest-strike", "flow-state"], fragments: ["spark-of-volts"]),
            new PlayerAction.PerformFinisher());

        var volts = Assert.Single(played.Resolutions[0].Fired, fired => fired.Source.Value == "spark-of-volts");
        Assert.Contains(volts.Outcomes, outcome => outcome.Outcome is Outcome.ApplyBuff { Status: var status } && status == Amplified);
        Assert.Contains(volts.Outcomes, outcome => outcome.Outcome is Outcome.ApplyBuff { Status: var status } && status == BoltCharge);
    }

    [Fact]
    public void While_Amplified_kills_may_drop_an_orb_and_a_Heavy_Arc_kill_blinds_and_a_precision_kill_on_a_blinded_enemy_blinds_again()
    {
        var played = Play(build => Equip(build, aspects: ["lethal-current", "flow-state"], fragments: ["spark-of-volts", "spark-of-amplitude", "spark-of-beacons", "spark-of-brilliance"]),
            new PlayerAction.PerformFinisher(),                                                  // Spark of Volts: Amplified
            new PlayerAction.FireWeapon(WeaponSlot.Power, HitOutcome.Kill, TargetCount.One),     // Thunderlord: Heavy, Arc
            new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One, Zone: HitZone.Precision));

        var kill = played.Resolutions[1].Fired;
        var amplitude = Assert.Single(kill, fired => fired.Source.Value == "spark-of-amplitude");
        var beacons = Assert.Single(kill, fired => fired.Source.Value == "spark-of-beacons");
        var brilliance = Assert.Single(played.Resolutions[2].Fired, fired => fired.Source.Value == "spark-of-brilliance");
        Assert.Contains(amplitude.Outcomes, outcome => outcome.Outcome is Outcome.Spawn { Pickup: var pickup } && pickup == OrbOfPower);
        Assert.Contains(beacons.Outcomes, outcome => outcome.Outcome is Outcome.DebuffTarget { Status: var status } && status == Blind);
        Assert.Contains(brilliance.Outcomes, outcome => outcome.Outcome is Outcome.DebuffTarget { Status: var status } && status == Blind);
        Assert.Equal(Likelihood.Chance, amplitude.Likelihood);
        Assert.All([beacons, brilliance], fired => Assert.Equal(Likelihood.Always, fired.Likelihood));
    }

    [Fact]
    public void A_body_kill_on_a_blinded_enemy_sets_off_no_Spark_of_Brilliance()
    {
        var played = Play(build => Equip(build, aspects: ["lethal-current", "flow-state"], fragments: ["spark-of-volts", "spark-of-beacons", "spark-of-brilliance"]),
            new PlayerAction.PerformFinisher(),                                                  // Spark of Volts: Amplified
            new PlayerAction.FireWeapon(WeaponSlot.Power, HitOutcome.Kill, TargetCount.One),     // Spark of Beacons blinds: Thunderlord is Heavy
            new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One));

        Assert.DoesNotContain(played.Resolutions[2].Fired, fired => fired.Source.Value == "spark-of-brilliance");
    }

    [Fact]
    public void A_Primary_Arc_weapon_kill_while_Amplified_sets_off_no_Spark_of_Beacons()
    {
        var played = Play(build => Equip(build, aspects: ["lethal-current", "flow-state"], fragments: ["spark-of-volts", "spark-of-beacons"]),
            new PlayerAction.PerformFinisher(),                                                  // Spark of Volts: Amplified
            new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One));   // Crisis Inverted: Primary, Arc

        Assert.DoesNotContain(played.Resolutions[1].Fired, fired => fired.Source.Value == "spark-of-beacons");
    }

    private static Build Equip(Build build, ImmutableArray<string> aspects, ImmutableArray<string> fragments, string? grenade = null, string? melee = null) =>
        build with
        {
            Abilities = build.Abilities with
            {
                Grenade = grenade is null ? build.Abilities.Grenade : new AbilityPlug.Known(ElementId.From(grenade)),
                Melee = melee is null ? build.Abilities.Melee : new AbilityPlug.Known(ElementId.From(melee)),
            },
            Aspects = [.. aspects.Select(ElementId.From)],
            Fragments = [.. fragments.Select(ElementId.From)],
        };

    /// <summary>The Arc Hunter of <see cref="BuildText"/> with its subclass changed by <paramref name="equip"/>, played through <paramref name="actions"/>.</summary>
    private static DesignSession Play(Func<Build, Build> equip, params PlayerAction[] actions)
    {
        var session = LoopDesigning.StartDesign(Catalog, new SourceText("arc-hunter.build.yaml", BuildText), "Arc Hunter loop")
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        var build = equip(session.Build.Build);
        var wearing = BuildValidation.ValidateBuild(build, Catalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        var start = LoopDesigning.CreateSession(wearing, session.Design);
        return actions.Aggregate(start, (played, action) => LoopDesigning.AppendStep(played, action, Optional.None<string>()));
    }
}

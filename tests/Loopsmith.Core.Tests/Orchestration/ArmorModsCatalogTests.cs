using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.StepPlaying;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// The rest of the armor mods, in the real rules: every one is known (rules, passives or why it changes no loop), named
/// as Bungie's manifest names it under each of its hashes, and a few of them played in an Arc Hunter that wears them —
/// Firepower and Heavy Handed drop orbs, a Siphon answers its element's weapon, Grenade Font shows while Armor Charge
/// holds, Utility Finisher spends Armor Charge on a finisher.
/// </summary>
public sealed class ArmorModsCatalogTests
{
    private static readonly Result<ImmutableArray<SourceText>, string> RuleFiles = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"));   // impure

    private static readonly RuleCatalog Catalog =
        RuleFiles.Bind(CatalogLoading.ParseCatalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static readonly StatusId ArmorCharge = StatusId.From("armor-charge");

    private static ImmutableArray<BuildElement> ArmorMods =>
        [.. Catalog.Elements.Values.Where(element => element.Kind == ElementKind.ArmorMod)];

    [Fact]
    public void Every_armor_mod_has_rules_passives_or_says_why_it_changes_no_loop()
    {
        var silent = ArmorMods.Where(mod => mod.Rules.IsEmpty && mod.Passives.IsEmpty && !mod.Inert.IsSome()).Select(mod => mod.Id.Value);

        Assert.Empty(silent);
        Assert.True(ArmorMods.Length > 130, $"{ArmorMods.Length} armor mods");
    }

    [Fact]
    public void Every_hash_of_an_armor_mod_is_in_the_manifest_excerpt_under_its_name_as_an_armor_mod()
    {
        var wrong = ArmorMods
            .SelectMany(mod => mod.Hashes.Select(hash => (mod.Name, Hash: hash, Item: Catalog.Manifest.FindItem(hash))))
            .Where(entry => entry.Item.Match(item => item.Value.Kind is not ManifestKind.ArmorMod || item.Value.Name != entry.Name, _ => true))
            .Select(entry => $"{entry.Name} {entry.Hash.Value}");

        Assert.Empty(wrong);
    }

    [Fact]
    public void Firepower_and_Heavy_Handed_drop_an_Orb_of_Power_on_a_grenade_and_a_powered_melee_kill()
    {
        var played = Play(["firepower", "heavy-handed"],
            new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One),
            new PlayerAction.CastAbility(OffensiveAbility.Melee, HitOutcome.Kill, TargetCount.One));

        Assert.Contains(played.Resolutions[0].Fired, fired => fired.Source.Value == "firepower" && fired.Outcomes.Any(outcome => outcome.Outcome is Outcome.Spawn));
        Assert.Contains(played.Resolutions[1].Fired, fired => fired.Source.Value == "heavy-handed" && fired.Outcomes.Any(outcome => outcome.Outcome is Outcome.Spawn));
        Assert.DoesNotContain(played.Resolutions[0].Fired, fired => fired.Source.Value == "heavy-handed");
    }

    [Fact]
    public void Arc_Siphon_answers_an_Arc_weapon_kill_as_a_chance_and_not_a_Strand_one()
    {
        var played = Play(["arc-siphon"],
            new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One),      // Crisis Inverted: Arc
            new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One));    // Festival Flight: Strand

        var siphon = Assert.Single(played.Resolutions[0].Fired, fired => fired.Source.Value == "arc-siphon");
        Assert.Equal(Likelihood.Chance, siphon.Likelihood);
        Assert.DoesNotContain(played.Resolutions[1].Fired, fired => fired.Source.Value == "arc-siphon");
    }

    [Fact]
    public void Grenade_Font_shows_with_Armor_Charge_while_it_holds()
    {
        var played = Play(["reaper", "powerful-attraction", "grenade-font"],
            new PlayerAction.UseClassAbility(),                                                    // arms Reaper
            new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One),     // Reaper's orb
            new PlayerAction.UseClassAbility());                                                   // Powerful Attraction: the orb's Armor Charge

        var before = played.Resolutions[0];
        var charged = played.Resolutions[2];
        var state = LoopDesigning.ShapeStepState(played, charged);

        Assert.DoesNotContain(before.ActivePassives, passive => passive.Source.Value == "grenade-font");
        Assert.Contains(charged.ActivePassives, passive => passive.Source.Value == "grenade-font");
        var armorCharge = Assert.Single(state.Buffs, entry => entry.Keyword == new KeywordRef.Status(ArmorCharge));
        Assert.Contains("+20 Grenade", armorCharge.SwitchesOn.Select(effect => effect.Text));
    }

    [Fact]
    public void Utility_Finisher_is_offered_and_spends_Armor_Charge_on_a_finisher()
    {
        var charged = Play(["reaper", "charged-up", "utility-finisher"],
            new PlayerAction.UseClassAbility(),                                                    // arms Reaper
            new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One),     // Reaper's orb
            new PlayerAction.CollectPickups(PickupId.From("orb-of-power")),
            new PlayerAction.Declare(new StateDeclaration.ReachMax(ArmorCharge)));

        var finished = LoopDesigning.AppendStep(charged, new PlayerAction.PerformFinisher(), Optional.None<string>());
        var finisher = finished.Resolutions[^1];

        Assert.Contains(LoopDesigning.ListTriggerOptions(charged), option => option.Token == "finisher");
        var utility = Assert.Single(finisher.Fired, fired => fired.Source.Value == "utility-finisher");
        var spent = Assert.Single(utility.Outcomes, outcome => outcome.Outcome is Outcome.ConvertStacksToEnergy { To: AbilityKind.ClassAbility });
        Assert.Equal(Likelihood.Always, utility.Likelihood);
        Assert.Equal(Optional.Some<GameValue>(new GameValue.Known(0.8m)), spent.Spend.Bind(spend => spend.AtSure));   // x4 spent (Charged Up's cap): 80%
        Assert.DoesNotContain(finisher.State.Buffs, buff => buff.Status == ArmorCharge);
    }

    [Fact]
    public void Utility_Finisher_short_of_three_Armor_Charge_doesnt_fire_and_says_so()
    {
        var charged = Play(["reaper", "utility-finisher"],
            new PlayerAction.UseClassAbility(),
            new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One),
            new PlayerAction.CollectPickups(PickupId.From("orb-of-power")));

        var finisher = LoopDesigning.AppendStep(charged, new PlayerAction.PerformFinisher(), Optional.None<string>()).Resolutions[^1];

        Assert.DoesNotContain(finisher.Fired, fired => fired.Source.Value == "utility-finisher");
        Assert.Contains(finisher.Notes, note => note.StartsWith("Utility Finisher needs x3 Armor Charge, holds x1", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_a_finisher_mod_a_finisher_is_still_offered()
    {
        Assert.Contains(LoopDesigning.ListTriggerOptions(Play([])), option => option.Token == "finisher");
    }

    [Fact]
    public void Shield_Break_Charge_gives_Armor_Charge_by_copies_on_a_weapon_step_that_breaks_a_shield()
    {
        var breaking = new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One, ShieldBreak.Breaks);

        var one = Play(["shield-break-charge"], breaking);
        var two = Play(["shield-break-charge", "shield-break-charge"], breaking);
        var plain = Play(["shield-break-charge"], new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.One));

        Assert.False(Catalog.Elements[ElementId.From("shield-break-charge")].Inert.IsSome());
        Assert.Contains(one.Resolutions[0].Fired, fired => fired.Source.Value == "shield-break-charge");
        Assert.Equal(1, ReadSure(one.Resolutions[0].State));
        Assert.Equal(2, ReadSure(two.Resolutions[0].State));
        Assert.DoesNotContain(plain.Resolutions[0].Fired, fired => fired.Source.Value == "shield-break-charge");
    }

    [Fact]
    public void Grenade_Kickstart_with_Shinobus_Vows_two_grenades_spends_Armor_Charge_only_on_the_last_one()
    {
        var orb = new PlayerAction.CollectPickups(PickupId.From("orb-of-power"));
        PlayerAction[] reaped = [new PlayerAction.UseClassAbility(), new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One), orb];

        var first = Play(["reaper", "grenade-kickstart", "grenade-kickstart"], [.. reaped.Append(new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage, TargetCount.One))]);
        var last = Play(["reaper", "grenade-kickstart", "grenade-kickstart"], [.. reaped.Append(new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage, TargetCount.One, AbilityInput.Press, ChargeUse.Last))]);

        Assert.Equal(2, first.Build.CountCharges(AbilityKind.Grenade).Value);
        Assert.DoesNotContain(first.Resolutions[^1].Fired, fired => fired.Source.Value == "grenade-kickstart");
        Assert.True(ReadSure(first.Resolutions[^1].State) > 0);
        Assert.Contains(last.Resolutions[^1].Fired, fired => fired.Source.Value == "grenade-kickstart");
        Assert.Equal(0, ReadSure(last.Resolutions[^1].State));
    }

    private static int ReadSure(GameState state) =>
        state.ReadTally(ArmorCharge).Match(tally => tally.Value.Sure.Value, _ => 0);

    /// <summary>
    /// An Arc Hunter wearing Shinobu's Vow (two grenade charges) and <paramref name="mods"/>, with a Strand Special (Festival
    /// Flight) and an Arc Primary (Crisis Inverted), played through <paramref name="actions"/>.
    /// </summary>
    private static DesignSession Play(ImmutableArray<string> mods, params PlayerAction[] actions)
    {
        var text = WriteBuildText(mods);
        var start = LoopDesigning.StartDesign(Catalog, new SourceText("armor-mods.build.yaml", text), "Armor mods loop")
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        return actions.Aggregate(start, (played, action) => LoopDesigning.AppendStep(played, action, Optional.None<string>()));
    }

    private static string WriteBuildText(ImmutableArray<string> mods) =>
        $$"""
        name: Armor Mods Hunter
        class: hunter
        subclass: arc
        super: gathering-storm
        grenade: skip-grenade
        melee: combination-blow
        classAbility: gamblers-dodge
        exoticArmor: shinobus-vow
        armorMods: [{{string.Join(", ", mods)}}]
        weapons:
          - {hash: 4019651319} # Festival Flight
          - {hash: 2888266564} # Crisis Inverted
        """;
}

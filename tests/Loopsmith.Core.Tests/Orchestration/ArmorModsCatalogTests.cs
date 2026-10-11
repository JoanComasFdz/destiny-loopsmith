using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// The rest of the armor mods, in the real rules: every one is known (rules, passives or why it changes no loop), named
/// as Bungie's manifest names it under each of its hashes, and a few of them played in the owner's DIM build —
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
        var played = Play(["grenade-font"],
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
        var charged = Play(["utility-finisher"],
            new PlayerAction.UseClassAbility(),
            new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One),
            new PlayerAction.UseClassAbility());

        var finished = LoopDesigning.AppendStep(charged, new PlayerAction.PerformFinisher(), Optional.None<string>());
        var finisher = finished.Resolutions[^1];

        Assert.Contains(LoopDesigning.ListTriggerOptions(charged), option => option.Token == "finisher");
        var utility = Assert.Single(finisher.Fired, fired => fired.Source.Value == "utility-finisher");
        Assert.Contains(utility.Outcomes, outcome => outcome.Outcome is Outcome.ConvertStacksToEnergy { To: AbilityKind.ClassAbility });
        Assert.DoesNotContain(finisher.State.Buffs, buff => buff.Status == ArmorCharge);
    }

    [Fact]
    public void Without_a_finisher_mod_no_finisher_is_offered()
    {
        Assert.DoesNotContain(LoopDesigning.ListTriggerOptions(Play([])), option => option.Token == "finisher");
    }

    /// <summary>The owner's DIM build wearing <paramref name="mods"/> too, played through <paramref name="actions"/>.</summary>
    private static DesignSession Play(ImmutableArray<string> mods, params PlayerAction[] actions)
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        var json = File.ReadAllText(RepoFiles.ToPath(path));                                     // impure
        var session = LoopDesigning.StartSavedDimDesign(Catalog, new SourceText(path, json))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        var build = session.Build.Build with { ArmorMods = session.Build.Build.ArmorMods.AddRange(mods.Select(ElementId.From)) };
        var wearing = BuildValidation.ValidateBuild(build, Catalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        var start = LoopDesigning.CreateSession(wearing, session.Design);
        return actions.Aggregate(start, (played, action) => LoopDesigning.AppendStep(played, action, Optional.None<string>()));
    }
}

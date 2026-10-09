using System.Collections.Immutable;
using Loopsmith.Core.BuildExplanation;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.LoopGraphing;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.Simulation;
using Loopsmith.Core.Tests.Support;
using Loopsmith.Core.TraceRendering;
using static VerifyXunit.Verifier;

namespace Loopsmith.Core.Tests.Golden;

/// <summary>
/// Golden tests from the user's own build note (builds/skip-grenade-hunter/note.txt), one test per
/// scenario of note-map.md: the engine must fire exactly what the note explains (minus the documented
/// "not reproduced" parts, see discrepancies.md).
/// </summary>
public class SkipGrenadeHunterGoldenTests
{
    private const string BuildPath = "builds/skip-grenade-hunter/build.yaml";

    private static readonly ValidatedBuild Build = RepoFiles.LoadBuild(BuildPath);

    private static GameState Fresh => ActionResolution.CreateInitialState(Build);

    // ── note line 1: Class ability ──────────────────────────────────────────────
    [Fact]
    public void Line1_class_ability() =>
        AssertFires(Resolve(Fresh, new PlayerAction.UseClassAbility()),
            must: ["gamblers-dodge", "slice", "reaper", "bomber"]);

    // ── note line 2: Kill with strand weapon ────────────────────────────────────
    [Fact]
    public void Line2a_first_strand_kill_after_the_dodge()
    {
        var afterDodge = Resolve(Fresh, new PlayerAction.UseClassAbility()).State;

        var resolution = Resolve(afterDodge, new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill));

        AssertFires(resolution, must: ["slice", "attrition-orbs", "reaper", "strand-siphon", "to-shreds"], mustNot: ["horde-shuttle"]);
        Assert.DoesNotContain(resolution.Fired, f => f.Source.Value == "to-shreds" && f.Trigger is GameEvent.Damaged);
    }

    [Fact]
    public void Line2b_strand_kill_on_a_severed_and_unraveled_pack()
    {
        var start = WithTarget(WithBuffs(Fresh, ("slice", 1), ("reaper", 1)), "sever", "unravel");

        var resolution = Resolve(start, new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill));

        AssertFires(resolution, must: ["to-shreds", "horde-shuttle", "slice", "attrition-orbs", "reaper", "strand-siphon"]);
    }

    // ── note line 3: Arc grenade ────────────────────────────────────────────────
    [Fact]
    public void Line3_arc_grenade_hit() =>
        AssertFires(Resolve(Fresh, new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage)),
            must: ["spark-of-shock", "shinobus-vow"], mustNot: ["jolt"]);

    // ── note line 4: Slide + melee ──────────────────────────────────────────────
    [Fact]
    public void Line4_melee_hit() =>
        AssertFires(Resolve(Fresh, new PlayerAction.CastAbility(OffensiveAbility.Melee, HitOutcome.Damage)),
            must: ["tempest-strike", "impact-induction"]);

    // ── note line 5: Kill jolted target ─────────────────────────────────────────
    [Fact]
    public void Line5a_arc_weapon_kill_on_a_jolted_severed_pack()
    {
        var resolution = Resolve(WithTarget(Fresh, "jolt", "sever"), new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill));

        AssertFires(resolution, must:
        [
            "flow-state", "tempest-strike", "harmonic-siphon", "dielectric", "photonic-flare", "luminopotent-4pc",
            "spark-of-discharge", "to-shreds", "jolt", "ionic-trace", "elemental-charge", "shinobus-vow",
        ]);
        Assert.Contains(resolution.ActivePassives, p => p.Source.Value == "spark-of-frequency");
    }

    [Fact]
    public void Line5b_grenade_kill_is_a_jolted_kill() =>
        AssertFires(Resolve(Fresh, new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill)),
            must: ["flow-state", "tempest-strike", "luminopotent-4pc", "dielectric", "ionic-trace", "spark-of-discharge", "elemental-charge", "shinobus-vow"],
            mustNot: ["harmonic-siphon", "photonic-flare"]);

    // ── note line 6: Amplified ──────────────────────────────────────────────────
    [Fact]
    public void Line6_while_amplified_bolt_charge_gains_an_extra_stack()
    {
        var resolution = Resolve(WithBuffs(Fresh, ("amplified", 1)), new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage));

        AssertFires(resolution, must: ["spark-of-shock", "shinobus-vow"]);
        Assert.Equal(2, ReadStacks(resolution.State, "bolt-charge"));
        Assert.Contains(resolution.ActivePassives, p => p.Source.Value == "spark-of-frequency");
        Assert.Contains(resolution.ActivePassives, p => p.Source.Value == "luminopotent-2pc");
    }

    // ── note line 7: Orb of power ───────────────────────────────────────────────
    [Fact]
    public void Line7a_orb_pickup() =>
        AssertFires(Resolve(WithGround(Fresh, ("orb-of-power", 1)), new PlayerAction.CollectPickups(PickupId.From("orb-of-power"))),
            must: ["orb-of-power", "unraveling-orbs"]);

    [Fact]
    public void Line7b_grenade_kickstart_spends_armor_charge_on_the_next_grenade()
    {
        var resolution = Resolve(WithBuffs(Fresh, ("armor-charge", 2)), new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Damage));

        AssertFires(resolution, must: ["grenade-kickstart", "spark-of-shock", "shinobus-vow"]);
        Assert.Equal(0, ReadStacks(resolution.State, "armor-charge"));
    }

    // ── note line 8: Weapon kill ────────────────────────────────────────────────
    [Fact]
    public void Line8a_arc_weapon_kill_makes_an_ionic_trace() =>
        AssertFires(Resolve(Fresh, new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill)),
            must: ["spark-of-discharge", "harmonic-siphon", "ionic-trace", "elemental-charge", "shinobus-vow"]);

    [Fact]
    public void Line8b_strand_weapon_kill_does_not()
    {
        var resolution = Resolve(Fresh, new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill));

        Assert.Equal(["attrition-orbs", "strand-siphon"], ListSources(resolution).Order());
    }

    // ── note line 9: Ionic trace ────────────────────────────────────────────────
    [Fact]
    public void Line9_ionic_trace_pickup() =>
        AssertFires(Resolve(WithGround(Fresh, ("ionic-trace", 1)), new PlayerAction.CollectPickups(PickupId.From("ionic-trace"))),
            must: ["ionic-trace", "spark-of-discharge", "elemental-charge", "shinobus-vow"]);

    // ── note line 10: Max bolt charge ───────────────────────────────────────────
    [Fact]
    public void Line10_max_bolt_charge_closes_the_loop()
    {
        var start = WithGround(WithBuffs(Fresh, ("bolt-charge", 9)), ("ionic-trace", 1));

        var resolution = Resolve(start, new PlayerAction.CollectPickups(PickupId.From("ionic-trace")));

        AssertFires(resolution, must:
        [
            "bolt-charge", "flashover", "shinobus-vow", "defibrillating-blast", "tempest-strike", "flow-state",
            "luminopotent-4pc", "dielectric", "ionic-trace", "spark-of-discharge", "elemental-charge",
        ]);
        Assert.True(resolution.State.Buffs.Any(b => b.Status.Value == "new-tricks"));
        Assert.True(resolution.State.Buffs.Any(b => b.Status.Value == "amplified"));
    }

    // ── loop discovery: the build's headline loop must be found ─────────────────
    [Fact]
    public void Loop_discovery_finds_the_infinite_skip_grenade_loop()
    {
        var graph = LoopGraphBuilding.BuildLoopGraph(Build);
        var loops = LoopFinding.FindLoops(graph);

        Assert.Contains(loops, loop => loop.RefundsEnergy
            && loop.NodeKeys.Contains("a:Grenade")
            && loop.NodeKeys.Contains("e:Grenade")
            && loop.Edges.Any(e => e.Sources.Contains("Shinobu's Vow")));
    }

    // ── snapshots of the full outputs (review the diff when rules change) ───────
    [Fact]
    public Task Explain_note_style()
    {
        var groups = BuildExplaining.ExplainBuild(Build);
        var text = BuildExplaining.RenderExplanation(groups, ExplanationStyle.Note).ToPlainText();
        return Verify(text);
    }

    [Fact]
    public Task Scenario_trace()
    {
        var scenario = File.ReadAllText(RepoFiles.ToPath("builds/skip-grenade-hunter/scenario.txt"));
        var actions = CommandShells.ParseActions([], Optional.Some(scenario)).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        var resolutions = ActionResolution.ResolveSequence(Build, Fresh, actions);
        var text = TraceRenderer.RenderSequence(Build, Fresh, resolutions, new TraceOptions(true, false, false)).ToPlainText();
        return Verify(text);
    }

    [Fact]
    public Task Loops()
    {
        var graph = LoopGraphBuilding.BuildLoopGraph(Build);
        var loops = LoopFinding.FindLoops(graph);
        var text = LoopRendering.RenderLoops(graph, loops, 10).ToPlainText();
        return Verify(text);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private static Resolution Resolve(GameState state, PlayerAction action) =>
        ActionResolution.ResolveAction(Build, state, action);

    private static ImmutableArray<string> ListSources(Resolution resolution) =>
        resolution.Fired.Select(f => f.Source.Value).Distinct().ToImmutableArray();

    private static void AssertFires(Resolution resolution, string[] must, string[]? mustNot = null)
    {
        var fired = ListSources(resolution);
        var missing = must.Except(fired).ToArray();
        var unexpected = (mustNot ?? []).Intersect(fired).ToArray();
        Assert.True(missing.Length == 0 && unexpected.Length == 0,
            $"missing: [{string.Join(", ", missing)}] unexpected: [{string.Join(", ", unexpected)}] fired: [{string.Join(", ", fired)}]");
    }

    private static int ReadStacks(GameState state, string status) =>
        state.Buffs.FirstOrDefault(b => b.Status.Value == status)?.Stacks.Value ?? 0;

    private static GameState WithBuffs(GameState state, params (string Status, int Stacks)[] buffs) =>
        state with
        {
            Buffs = state.Buffs.AddRange(buffs.Select(b => new ActiveStatus(StatusId.From(b.Status), StackCount.From(b.Stacks), Optional.None<Seconds>()))),
        };

    private static GameState WithTarget(GameState state, params string[] debuffs) =>
        state with
        {
            Target = state.Target with
            {
                Debuffs = debuffs.Select(d => new ActiveStatus(StatusId.From(d), StackCount.From(1), Optional.None<Seconds>())).ToImmutableArray(),
            },
        };

    private static GameState WithGround(GameState state, params (string Pickup, int Count)[] pickups) =>
        state with { Pickups = pickups.Select(p => new GroundPickup(PickupId.From(p.Pickup), p.Count)).ToImmutableArray() };
}

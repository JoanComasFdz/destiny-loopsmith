using Loopsmith.Core.Domain;
using Loopsmith.Core.LoopGraphing;
using Loopsmith.Core.Phrasing;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.LoopGraphing;

public class LoopFindingTests
{
    [Fact]
    public void Finds_the_grenade_energy_loop_through_a_buff()
    {
        // Grenade damage → +Bolt Charge; gaining Bolt Charge → grenade energy; energy → throw again.
        var vow = Element("vow", ElementKind.Fragment,
        [
            On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), Buff("bolt-charge")),
            On(new Trigger.BuffGained(Status("bolt-charge")), Energy(AbilityKind.Grenade, new GameValue.Unknown())),
        ]);
        var build = ValidateBuild([vow]);

        var graph = LoopGraphBuilding.BuildLoopGraph(build);
        var loops = LoopFinding.FindLoops(graph);

        var loop = Assert.Single(loops);
        Assert.True(loop.RefundsEnergy);
        Assert.Equal(["a:Grenade", "t:Grenade damage", "t:Gain Bolt Charge", "e:Grenade"], loop.NodeKeys);
    }

    [Fact]
    public void Rules_that_do_not_stack_meet_in_a_filter_node_and_only_one_arrow_leaves_it()
    {
        // Two elements give Bolt Charge on grenade damage, but "yielder" doesn't stack with "giver"; a third stacks.
        var giver = Element("giver", ElementKind.Fragment, [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), Buff("bolt-charge"))]);
        var yielder = Element("yielder", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), Buff("bolt-charge")).NotStackingWith("giver")]);
        var stacker = Element("stacker", ElementKind.Fragment, [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), Buff("bolt-charge"))]);
        var vow = Element("vow", ElementKind.Fragment, [On(new Trigger.BuffGained(Status("bolt-charge")), Energy(AbilityKind.Grenade, new GameValue.Unknown()))]);
        var build = ValidateBuild([giver, yielder, stacker, vow]);

        var graph = LoopGraphBuilding.BuildLoopGraph(build);
        var loops = LoopFinding.FindLoops(graph);

        var filter = Assert.Single(graph.Nodes, node => node.Kind == NodeKind.Filter);
        Assert.Equal("Doesn't stack", filter.Label);
        var into = Assert.Single(graph.Edges, edge => edge.To == filter.Key);
        Assert.Equal(("t:Grenade damage", "Giver, Yielder"), (into.From, string.Join(", ", into.Sources.Order())));
        var outOf = Assert.Single(graph.Edges, edge => edge.From == filter.Key);
        Assert.Equal(("t:Gain Bolt Charge", "Giver"), (outOf.To, string.Join(", ", outOf.Sources)));
        Assert.Contains(graph.Edges, edge => edge.From == "t:Grenade damage" && edge.To == "t:Gain Bolt Charge" && edge.Sources.SequenceEqual(["Stacker"]));
        Assert.Contains(loops, loop => loop.NodeKeys.Contains(filter.Key));
        Assert.Contains("Doesn't stack →[Giver] Gain Bolt Charge", LoopRendering.RenderLoops(graph, loops, 10).ToPlainText());
    }

    [Fact]
    public void Multi_target_triggers_are_reached_by_the_player_actions_that_can_hit_many()
    {
        // One For All on the rifle; a grenade that refunds itself when it kills two (both inline examples).
        var oneForAll = Element("one-for-all", ElementKind.Fragment,
            [On(new Trigger.DamageMultiple(new DamageSource.AnyWeapon(), TargetCount.From(3)), Buff("amplified"))]);
        var multikill = Element("multikill", ElementKind.Fragment,
            [On(new Trigger.KillMultiple(new DamageSource.AbilityOf(AbilityKind.Grenade), TargetCount.From(2)), Energy(AbilityKind.Grenade, new GameValue.Known(0.2m)))]);
        var strike = Element("strike", ElementKind.Fragment,
            [On(new Trigger.BuffGained(Status("amplified")), new Outcome.StrikeTarget(Status("jolt"), HitOutcome.Kill))]);
        var build = ValidateBuild([oneForAll, multikill, strike]);

        var graph = LoopGraphBuilding.BuildLoopGraph(build);
        var loops = LoopFinding.FindLoops(graph);

        Assert.Contains(graph.Edges, e => e.From == "w:Energy" && e.To == "t:Hit 3+ enemies with weapon" && e.Kind == EdgeKind.Player);
        Assert.DoesNotContain(graph.Edges, e => e.From == "a:Grenade" && e.To == "t:Hit 3+ enemies with weapon");
        Assert.DoesNotContain(graph.Edges, e => e.From == "t:Gain Amplified" && e.To == "t:Kill 2+ with grenade");   // strikes hit one enemy
        Assert.Contains(loops, loop => loop.NodeKeys.SequenceEqual(["a:Grenade", "t:Kill 2+ with grenade", "e:Grenade"]));
    }

    [Fact]
    public void Debuff_prerequisites_are_edges_but_not_loop_steps()
    {
        var shock = Element("shock", ElementKind.Fragment,
            [On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), new Outcome.DebuffTarget(Status("jolt"), Loopsmith.Core.Functional.Optional.None<Seconds>()))]);
        var flow = Element("flow", ElementKind.Fragment,
            [On(new Trigger.KillDebuffed(new DamageSource.AnySource(), [Status("jolt")]), Buff("amplified"))]);
        var build = ValidateBuild([shock, flow]);

        var graph = LoopGraphBuilding.BuildLoopGraph(build);

        Assert.Contains(graph.Edges, e => e.Kind == EdgeKind.Enables && e.To == "t:Kill Jolted target");
        Assert.Empty(LoopFinding.FindLoops(graph));
    }

    [Fact]
    public void Mermaid_output_is_a_flowchart_with_thick_loop_edges()
    {
        var vow = Element("vow", ElementKind.Fragment,
        [
            On(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), Buff("bolt-charge")),
            On(new Trigger.BuffGained(Status("bolt-charge")), Energy(AbilityKind.Grenade, new GameValue.Unknown())),
        ]);
        var build = ValidateBuild([vow]);
        var graph = LoopGraphBuilding.BuildLoopGraph(build);
        var loops = LoopFinding.FindLoops(graph);

        var mermaid = LoopRendering.RenderMermaid(graph, loops, loopsOnly: true);

        Assert.StartsWith("flowchart LR", mermaid);
        Assert.Contains("==>|\"Vow\"|", mermaid);
        Assert.Contains("linkStyle", mermaid);
    }
}

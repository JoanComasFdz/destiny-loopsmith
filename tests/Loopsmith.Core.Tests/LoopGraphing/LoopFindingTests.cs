using Loopsmith.Core.Domain;
using Loopsmith.Core.LoopGraphing;
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

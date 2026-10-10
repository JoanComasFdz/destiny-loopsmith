using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>A step that could not happen (nothing to pick up, no weapon in that slot, a declaration that doesn't hold).</summary>
public sealed record BlockedStep(int StepIndex, string Label, string Reason);

/// <summary>An element named in the analysis, marked chance when every rule of it that fired there is a chance rule (ADRs D8).</summary>
public sealed record ElementMention(ElementId Source, string Name, Affinity Affinity, Likelihood Likelihood);

/// <summary>Which step provided what a step needs.</summary>
[Union]
public partial record NeedProvider
{
    /// <summary>An earlier step of the same pass, and the elements whose outcomes provided it (none for a declared state).</summary>
    partial record EarlierStep(int StepIndex, ImmutableArray<ElementMention> Elements);

    /// <summary>A step of the pass before, the need carrying over into this pass.</summary>
    partial record PreviousPass(int StepIndex, ImmutableArray<ElementMention> Elements);
}

/// <summary>
/// What a step uses that an earlier step provided: a pickup it picks up, a buff a rule consumes or a guard reads, a debuff
/// a trigger needs on the pack, a declared state ("Bolt Charge at max").
/// </summary>
public sealed record StepNeed(string What, Affinity Affinity, NeedProvider Provider);

/// <summary>A rule that gave way there, because it doesn't stack with <see cref="PartnerName"/>'s (ADRs D6).</summary>
public sealed record WastedMention(ElementMention Source, string PartnerName);

/// <summary>
/// One step of a pass, in the order of the loop: what it needs and from where, what it sets off, what is wasted.
/// <see cref="InOrder"/> are the elements of <see cref="SetsOff"/> that fire there because an earlier step provided
/// what their rule needed (or what the step itself needs: the orb it picks up, the buff it declares at max); the
/// others fire wherever the step goes.
/// </summary>
public sealed record StepAnalysis(
    int StepIndex,
    string Token,
    string Label,
    ImmutableArray<StepNeed> Needs,
    ImmutableArray<ElementMention> SetsOff,
    ImmutableArray<ElementMention> InOrder,
    ImmutableArray<WastedMention> Wasted,
    Optional<string> Blocked)
{
    /// <summary>The elements of <see cref="SetsOff"/> that fire wherever the step goes (not in <see cref="InOrder"/>).</summary>
    public ImmutableArray<ElementMention> OnItsOwn =>
        [.. SetsOff.Where(element => !InOrder.Any(other => other.Source == element.Source))];
}

/// <summary>
/// An arrow of the step chain: step <see cref="From"/> provided what step <see cref="To"/> needs — in the pass before
/// when <see cref="FromPreviousPass"/> (it carries over, around the loop). <see cref="Likelihood"/> is chance when every
/// need on it came only from chance rules.
/// </summary>
public sealed record StepLink(int From, int To, bool FromPreviousPass, ImmutableArray<StepNeed> Needs, Likelihood Likelihood);

/// <summary>A step that does something else on the first pass from a fresh spawn than in the repeating pass.</summary>
public sealed record PassDifference(
    int StepIndex,
    string Label,
    Optional<string> BlockedOnFirstPass,
    ImmutableArray<ElementMention> OnlyInRepeatingPass,
    ImmutableArray<ElementMention> OnlyOnFirstPass);

/// <summary>Whether the loop's order can be played again and again (docs/loop-format.md, "Analysis").</summary>
[Union]
public partial record LoopVerdict
{
    partial record NoSteps();

    /// <summary>The repeating pass has no blocked step; <see cref="FirstPassBlocked"/> names one the first pass has.</summary>
    partial record Repeats(Optional<BlockedStep> FirstPassBlocked);

    /// <summary>The repeating pass has a blocked step (its first one).</summary>
    partial record Breaks(BlockedStep Blocked);
}

/// <summary>One pass through the loop's steps, from <see cref="Start"/>.</summary>
public sealed record LoopPass(GameState Start, ImmutableArray<Resolution> Resolutions);

/// <summary>
/// A designed loop analysed by its order of triggers (ADRs D2): played from a fresh spawn (<see cref="FirstPass"/>), then
/// again from where it ended until a pass starts the way an earlier one did (<see cref="RepeatingPass"/>; the same pass
/// as the first when <see cref="FirstPassRepeats"/>). For each step of the repeating pass: what it needs and which step
/// provided it, what it sets off, what is wasted there. No totals and no counts (ADRs D1).
/// </summary>
public sealed record LoopReport(
    string LoopName,
    string BuildName,
    ImmutableArray<string> StepLabels,
    LoopVerdict Verdict,
    LoopPass FirstPass,
    LoopPass RepeatingPass,
    bool FirstPassRepeats,
    ImmutableArray<StepAnalysis> Steps,
    ImmutableArray<StepLink> Links,
    ImmutableArray<PassDifference> FirstPassDifferences);

/// <summary>A trigger's step in one loop, and whether that loop's first pass does something else there.</summary>
public sealed record PlacedStep(StepAnalysis Step, bool DiffersOnFirstPass);

/// <summary>Where a trigger sits in the two loops of a comparison.</summary>
[Union]
public partial record TriggerPlacement
{
    /// <summary>Both loops have it; <c>OnlyLeft</c> and <c>OnlyRight</c> are the elements it sets off in only one of them.</summary>
    partial record InBoth(PlacedStep Left, PlacedStep Right, ImmutableArray<ElementMention> OnlyLeft, ImmutableArray<ElementMention> OnlyRight);

    partial record OnlyInLeft(PlacedStep Step);

    partial record OnlyInRight(PlacedStep Step);
}

/// <summary>
/// One trigger of two loops, matched by occurrence (A's first grenade kill with B's first grenade kill), and where it
/// sits in each.
/// </summary>
public sealed record TriggerComparison(string Label, TriggerPlacement Placement);

/// <summary>
/// A link of one loop's chain, and whether the other loop lacks it: no link between the same two triggers, the same
/// way round the loop (within a pass, or from the pass before).
/// </summary>
public sealed record ComparedLink(StepLink Link, bool OnlyHere);

/// <summary>Two loops' orders side by side (they may use different builds), trigger by trigger and as chains; nothing is scored.</summary>
public sealed record LoopComparison(
    LoopReport Left,
    LoopReport Right,
    ImmutableArray<TriggerComparison> Triggers,
    ImmutableArray<ComparedLink> LeftLinks,
    ImmutableArray<ComparedLink> RightLinks);

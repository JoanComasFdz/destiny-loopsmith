using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.BuildExplanation;

/// <summary>
/// One effect a status switches on in this build, as its details list it: the element it is (its name, kind and
/// affinity), what it does ("+1 Bolt Charge per gain"), and where it reaches when that is weapons — on which of the
/// build's weapons, or that none of them is one, so it does nothing here.
/// </summary>
public sealed record SwitchedOnLine(ElementId Source, string SourceName, ElementKind SourceKind, Affinity Affinity, string Text, Optional<string> Note);

/// <summary>Pure: what a status switches on in a build (Amplified: Spark of Frequency's extra Bolt Charge, Ionic Overclock).</summary>
public static class SwitchedOnExplaining
{
    public static ImmutableArray<SwitchedOnLine> ExplainSwitchedOn(ValidatedBuild build, StatusId status)
    {
        var glossary = build.Catalog.Glossary;
        return
        [
            .. build.ListSwitchedOn(status).Select(switched => new SwitchedOnLine(
                switched.Passive.Source,
                switched.Passive.SourceName,
                switched.SourceKind,
                switched.Passive.Affinity,
                glossary.DescribePassive(switched.Passive.Passive.Modifier),
                DescribeReach(build.Build, switched.Reach))),
        ];
    }

    private static Optional<string> DescribeReach(Build build, PassiveReach reach) =>
        reach.Match(
            _ => Optional.None<string>(),
            weapons => Optional.Some($"On {string.Join(", ", weapons.Indices.Select(index => build.Weapons[index].Name))}."),
            _ => Optional.Some(build.HasKnownArchetypes()
                ? "No weapon in this build is one of them, so it does nothing here."
                : "No weapon in this build is known to be one of them."));
}

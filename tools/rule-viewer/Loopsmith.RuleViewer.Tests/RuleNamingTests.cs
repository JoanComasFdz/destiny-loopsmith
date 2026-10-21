using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.RuleViewer.Core;

namespace Loopsmith.RuleViewer.Tests;

/// <summary>A rule's full name (ADRs D40): its file's path under <c>rules/</c>, without the extension, dashes for slashes, then its id.</summary>
public sealed class RuleNamingTests
{
    [Theory]
    [InlineData("hunter/arc/melees.yaml", "combination-blow-2", "hunter-arc-melees-combination-blow-2")]
    [InlineData("all-classes/arc/fragments.yaml", "spark-of-ions-1", "all-classes-arc-fragments-spark-of-ions-1")]
    [InlineData("mods/arms.yaml", "heavy-handed-1", "mods-arms-heavy-handed-1")]
    [InlineData("", "combination-blow-2", "combination-blow-2")]
    public void A_rules_full_name_is_its_files_path_with_dashes_then_its_id(string file, string id, string name)
    {
        var element = ToElement(file);

        Assert.Equal(name, element.ToRuleName(RuleId.From(id)));
    }

    private static BuildElement ToElement(string file) =>
        new(
            ElementId.From("probe"), "Probe", ElementKind.Melee, Affinity.Arc, Optional.Some(GuardianClass.Hunter),
            [], Optional.None<string>(), [], [], Optional.None<AbilityProfile>(), Optional.None<FragmentSlots>(),
            new Provenance.Authored("probe.yaml", 1), Optional.None<string>(), [], file);
}

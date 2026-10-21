using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.Phrasing;

/// <summary>
/// A heal as a fact (ADRs D1, D5): its amount as a share of your health or in HP, and one whose amount isn't known reads
/// "?", never nothing (CLAUDE.md, unknown is data).
/// </summary>
public sealed class HealPhrasingTests
{
    private static string DescribeHeal(HealAmount amount, bool includesAllies) =>
        Glossary.DescribeOutcome(new Outcome.RestoreHealth(amount, includesAllies), Optional.None<DamageType>());

    [Fact]
    public void A_heal_of_unknown_amount_reads_a_question_mark()
    {
        Assert.Equal("heals you ?", DescribeHeal(new HealAmount.Share(new GameValue.Unknown()), false));
        Assert.Equal("heals you and allies ?", DescribeHeal(new HealAmount.Share(new GameValue.Unknown()), true));
    }

    [Fact]
    public void A_heal_in_hp_reads_its_hp()
    {
        Assert.Equal("heals you ~55 HP", DescribeHeal(new HealAmount.Points(new GameValue.Approximate(55m)), false));
        Assert.Equal("heals you 16|21|21 HP", DescribeHeal(new HealAmount.Points(new GameValue.PerModCount(ImmutableArray.Create(16m, 21m, 21m))), false));
    }

    [Fact]
    public void A_heal_as_a_share_reads_its_percent() =>
        Assert.Equal("heals you 100%", DescribeHeal(new HealAmount.Share(new GameValue.Known(1m)), false));
}

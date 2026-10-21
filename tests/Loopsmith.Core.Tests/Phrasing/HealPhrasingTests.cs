using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.Phrasing;

/// <summary>
/// A heal as a fact (ADRs D1, D5): its amount as a share of your health or in HP, one whose amount isn't known reads
/// "?", never nothing (CLAUDE.md, unknown is data), and one that comes in pulses reads their count, never added up.
/// </summary>
public sealed class HealPhrasingTests
{
    private static string DescribeHeal(HealAmount amount, bool includesAllies, int pulses = 1) =>
        Glossary.DescribeOutcome(new Outcome.RestoreHealth(amount, includesAllies, pulses), Optional.None<DamageType>());

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

    [Fact]
    public void A_heal_in_pulses_reads_their_count_times_each_ones_amount()
    {
        Assert.Equal("heals you and allies 3 × 15 HP", DescribeHeal(new HealAmount.Points(new GameValue.Known(15m)), true, pulses: 3));
        Assert.Equal("heals you 2 × ?", DescribeHeal(new HealAmount.Share(new GameValue.Unknown()), false, pulses: 2));
    }
}

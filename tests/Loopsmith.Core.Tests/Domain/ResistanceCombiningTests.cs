using Loopsmith.Core.Domain;

namespace Loopsmith.Core.Tests.Domain;

/// <summary>Damage resistances multiply what the others let through (compendium Armor Mods#7); "?" is never a number.</summary>
public sealed class ResistanceCombiningTests
{
    [Fact]
    public void Resistances_multiply_rather_than_add_up()
    {
        GameValue[] resistances = [new GameValue.Known(0.25m), new GameValue.Known(0.45m)];

        Assert.Equal(new GameValue.Known(0.5875m), resistances.CombineResistances());           // not 70%
    }

    [Fact]
    public void No_resistance_is_0_and_one_is_itself()
    {
        Assert.Equal(new GameValue.Known(0m), Array.Empty<GameValue>().CombineResistances());
        Assert.Equal(new GameValue.Known(0.345m), new GameValue[] { new GameValue.Known(0.345m) }.CombineResistances());
    }

    [Fact]
    public void An_unknown_resistance_makes_the_total_unknown_and_an_approximate_one_approximate()
    {
        GameValue[] unknown = [new GameValue.Known(0.25m), new GameValue.Unknown()];
        GameValue[] approximate = [new GameValue.Known(0.5m), new GameValue.Approximate(0.5m)];

        Assert.Equal(new GameValue.Unknown(), unknown.CombineResistances());
        Assert.Equal(new GameValue.Approximate(0.75m), approximate.CombineResistances());
    }
}

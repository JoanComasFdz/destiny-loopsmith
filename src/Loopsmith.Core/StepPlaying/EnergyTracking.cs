using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;

namespace Loopsmith.Core.StepPlaying;

/// <summary>
/// Energy an outcome gives an ability (ADRs D48), as a share of one charge: the part that is sure (<see cref="Floor"/>),
/// and how sure the whole is — exact, approximate (a "~" value), or at least the floor (a "?" part: an unknown value,
/// chance scalar or stat, or a chance rule's).
/// </summary>
public sealed record EnergyGain(decimal Floor, EnergyCertainty Certainty);

/// <summary>
/// The grenade's, melee's and class ability's energy through a loop (ADRs D48): a use spends a charge, a refund fills
/// the energy (scaled the way the game scales it), the player declares it recharged, and nothing regenerates by itself.
/// The Super's energy isn't kept: its grants stay facts.
/// </summary>
public static class EnergyTracking
{
    /// <summary>The ability's energy right then: the state's, its build's charges all full when the state has none.</summary>
    public static AbilityEnergy ReadEnergy(this ValidatedBuild build, GameState state, AbilityKind ability) =>
        state.ReadEnergy(ability, build.CountCharges(ability));

    /// <summary>
    /// A use of the ability spends one charge; one played without a charge (not ready: still played, ADRs D48) leaves it
    /// empty. Either way, what is left of a one-charge ability is known: none.
    /// </summary>
    public static GameState SpendCharge(ValidatedBuild build, GameState state, AbilityKind ability)
    {
        var energy = build.ReadEnergy(state, ability);
        var spent = energy.Charges >= 1
            ? energy with { Charges = energy.Charges - 1 }
            : energy with { Progress = 0m };
        var known = build.CountCharges(ability) == ChargeCount.One ? spent with { Progress = 0m, Certainty = EnergyCertainty.Exact } : spent;
        return PutEnergy(build, state, known);
    }

    /// <summary>"Grenade recharged" (ADRs D48): every charge full again, exactly.</summary>
    public static GameState RechargeFully(GameState state, AbilityKind ability) =>
        state with { Energy = state.Energy.RemoveAll(energy => energy.Ability == ability) };

    /// <summary>
    /// The ability gains energy: its charges and progress go up by the gain's floor, never past its build's charges (the
    /// rest is wasted), and an unknown or approximate part makes it at least or about that much. A full ability stays full.
    /// </summary>
    public static GameState GainEnergy(ValidatedBuild build, GameState state, AbilityKind ability, EnergyGain gain)
    {
        var energy = build.ReadEnergy(state, ability);
        var total = build.CountCharges(ability).Value;
        if (energy.Charges >= total)
        {
            return state;
        }

        var value = Math.Min(energy.Charges + energy.Progress + gain.Floor, total);
        var charges = (int)Math.Floor(value);
        var gained = new AbilityEnergy(ability, charges, value - charges, CombineCertainties(energy.Certainty, gain.Certainty));
        return PutEnergy(build, state, gained);
    }

    /// <summary>The less sure of two: at least over approximate over exact.</summary>
    public static EnergyCertainty CombineCertainties(EnergyCertainty first, EnergyCertainty second) =>
        (EnergyCertainty)Math.Max((int)first, (int)second);

    /// <summary>
    /// A grant's energy as the receiving ability gets it (ADRs D48): its value, narrowed to the element's copies, times the
    /// ability's chunk scalar (compendium Game Mechanics#12) at the stat that matches the listed value (70), unless the
    /// grant is unscaled (Gambler's Dodge). An unknown part — the value, a stat above 70, a chance rule — keeps the known
    /// part as a floor: at least that much, never 0 in its place; an unknown chunk scalar, or a stat below 70 or not
    /// given, leaves no floor ("?").
    /// </summary>
    public static EnergyGain ScaleGrant(ValidatedBuild build, AbilityKind ability, EnergyGrant grant, EnergyScaling scaling, CopyCount copies, Likelihood likelihood)
    {
        var value = ReadGrantValue(build, grant, copies);
        var scaled = grant is EnergyGrant.Full || scaling == EnergyScaling.Unscaled
            ? value
            : ScaleGain(build, ability, value);
        return likelihood == Likelihood.Chance ? new EnergyGain(0m, EnergyCertainty.AtLeast) : scaled;
    }

    /// <summary>A value the receiving ability gets scaled (a Kickstart's table value): its chunk scalar and its stat's increase.</summary>
    public static EnergyGain ScaleValue(ValidatedBuild build, AbilityKind ability, GameValue value, Likelihood likelihood) =>
        likelihood == Likelihood.Chance
            ? new EnergyGain(0m, EnergyCertainty.AtLeast)
            : ScaleGain(build, ability, ToGain(value));

    /// <summary>A grant's own value, as a share of one charge: a full charge is 1; a per-stat grant needs the build's stat.</summary>
    private static EnergyGain ReadGrantValue(ValidatedBuild build, EnergyGrant grant, CopyCount copies) =>
        grant.Match(
            fraction => ToGain(fraction.Amount.NarrowToCopies(copies)),
            _ => new EnergyGain(1m, EnergyCertainty.Exact),
            byArchetype => ReadEitherAmount(byArchetype.Otherwise.NarrowToCopies(copies), byArchetype.OnArchetypes.NarrowToCopies(copies)),
            perStat => ReadStatAmount(build, perStat.Stat, perStat.PerPoint.NarrowToCopies(copies)));

    /// <summary>A grant by archetype the build check couldn't bind to a weapon: at least the smaller of its two amounts.</summary>
    private static EnergyGain ReadEitherAmount(GameValue otherwise, GameValue onArchetypes)
    {
        var first = ToGain(otherwise);
        var second = ToGain(onArchetypes);
        return new EnergyGain(Math.Min(first.Floor, second.Floor), EnergyCertainty.AtLeast);
    }

    /// <summary>A share per point of the build's stat, times its points; unknown while the build doesn't give the stat.</summary>
    private static EnergyGain ReadStatAmount(ValidatedBuild build, StatKind stat, GameValue perPoint)
    {
        var gain = ToGain(perPoint);
        return build.Build.Stats.ReadStat(stat).Match(
            points => gain with { Floor = gain.Floor * points.Value.Value },
            _ => new EnergyGain(0m, EnergyCertainty.AtLeast));
    }

    private static EnergyGain ToGain(GameValue value) =>
        value.Match(
            known => new EnergyGain(known.Value, EnergyCertainty.Exact),
            perModCount => new EnergyGain(perModCount.Values.Min(), EnergyCertainty.AtLeast),   // not narrowed to its copies: the least
            approximate => new EnergyGain(approximate.Value, EnergyCertainty.Approximate),
            _ => new EnergyGain(0m, EnergyCertainty.AtLeast));

    /// <summary>The gain times the receiving ability's chunk scalar and what its stat does: no floor when either is unknown.</summary>
    private static EnergyGain ScaleGain(ValidatedBuild build, AbilityKind ability, EnergyGain gain)
    {
        var chunk = ReadChunkScalar(build, ability);
        var stat = ReadStatIncrease(build, ability);
        var floor = gain.Floor * chunk.Floor * stat.Floor;
        var certainty = CombineCertainties(gain.Certainty, CombineCertainties(chunk.Certainty, stat.Certainty));
        return new EnergyGain(floor, certainty);
    }

    /// <summary>The chunk scalar of the ability in its slot (Skip Grenade's 0.75); unknown ("?", or no ability there): a factor with no floor.</summary>
    private static EnergyGain ReadChunkScalar(ValidatedBuild build, AbilityKind ability) =>
        build.Build.Abilities.FindPlug(ability).ToElement()
            .Bind(id => build.Catalog.Elements.TryGetValue(id, out var element) ? element.Ability : Optional.None<AbilityProfile>())
            .Match(
                profile => profile.Value.ChunkScalar is GameValue.Unknown ? new EnergyGain(0m, EnergyCertainty.AtLeast) : ToGain(profile.Value.ChunkScalar),
                _ => new EnergyGain(0m, EnergyCertainty.AtLeast));

    /// <summary>
    /// What the ability's stat does to its gains (the glossary's <c>statGains</c>, ADRs D48): nothing at the stat that
    /// matches the listed value (70), at least nothing above it, and an unknown share below it or while the build has no
    /// stat (no floor: the gain reads "?").
    /// </summary>
    private static EnergyGain ReadStatIncrease(ValidatedBuild build, AbilityKind ability)
    {
        var stat = ToStat(ability);
        var matchesAt = build.Catalog.Glossary.StatGains
            .Where(gain => gain.Stat == stat)
            .Select(gain => Optional.Some(gain.MatchesAt))
            .FindFirstSome();
        return (build.Build.Stats.ReadStat(stat), matchesAt) switch
        {
            (Optional<StatValue>.Some points, Optional<StatValue>.Some benchmark) when points.Value.Value == benchmark.Value.Value => new EnergyGain(1m, EnergyCertainty.Exact),
            (Optional<StatValue>.Some points, Optional<StatValue>.Some benchmark) when points.Value.Value > benchmark.Value.Value => new EnergyGain(1m, EnergyCertainty.AtLeast),
            _ => new EnergyGain(0m, EnergyCertainty.AtLeast),
        };
    }

    /// <summary>The stat that increases an ability's gains: Grenade a grenade's, Melee a melee's, Class a class ability's.</summary>
    private static StatKind ToStat(AbilityKind ability) =>
        ability switch
        {
            AbilityKind.Grenade => StatKind.Grenade,
            AbilityKind.Melee => StatKind.Melee,
            AbilityKind.ClassAbility => StatKind.Class,
            _ => StatKind.Super,
        };

    /// <summary>
    /// The ability a least-charged grant goes to (ADRs D48), when that is sure: the one of the three with the least energy
    /// (a share of its charges), exactly or about known, every other one surely more — or the only one not full. None when every one is full (the
    /// Super, which isn't kept, may then take it), or when an unknown part or a tie leaves it open.
    /// </summary>
    public static LeastCharged FindLeastCharged(ValidatedBuild build, GameState state)
    {
        var candidates = GameStateReading.EnergyAbilities
            .Select(ability => (Energy: build.ReadEnergy(state, ability), Share: ReadShare(build, state, ability)))
            .Where(candidate => candidate.Energy.Charges < build.CountCharges(candidate.Energy.Ability).Value)
            .ToImmutableArray();
        if (candidates.IsEmpty)
        {
            return new LeastCharged.AllFull();
        }

        var least = candidates.MinBy(candidate => candidate.Share);
        var isSure = candidates.Length == 1
            || least.Energy.Certainty != EnergyCertainty.AtLeast && candidates.All(candidate => candidate.Energy.Ability == least.Energy.Ability || candidate.Share > least.Share);
        return isSure
            ? new LeastCharged.Sure(least.Energy.Ability)
            : new LeastCharged.Open([.. candidates.Select(candidate => candidate.Energy.Ability)]);
    }

    private static decimal ReadShare(ValidatedBuild build, GameState state, AbilityKind ability)
    {
        var energy = build.ReadEnergy(state, ability);
        return (energy.Charges + energy.Progress) / build.CountCharges(ability).Value;
    }

    /// <summary>
    /// The ability may have gained something unknown: the least-charged grant that may have gone to it. A full one is
    /// full whatever it gained.
    /// </summary>
    public static GameState MarkMaybeGained(ValidatedBuild build, GameState state, AbilityKind ability) =>
        GainEnergy(build, state, ability, new EnergyGain(0m, EnergyCertainty.AtLeast));

    /// <summary>Puts the ability's energy in the state; at its build's full charges, it has no entry (full, exactly).</summary>
    private static GameState PutEnergy(ValidatedBuild build, GameState state, AbilityEnergy energy)
    {
        var others = state.Energy.RemoveAll(entry => entry.Ability == energy.Ability);
        var isFull = energy.Charges >= build.CountCharges(energy.Ability).Value;
        return state with { Energy = isFull ? others : others.Add(energy) };
    }
}

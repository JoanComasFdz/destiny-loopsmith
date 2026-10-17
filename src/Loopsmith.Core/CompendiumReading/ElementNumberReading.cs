using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.CompendiumReading;

/// <summary>
/// A number an element states as a game value (ADRs D13): where it is (<see cref="Label"/>, "ability.baseCooldown",
/// "modifyDamage") and its value as the rules hold it (a percent as its fraction). <see cref="IsPvp"/>: the PvP part of a
/// "25% [10%]", found among a row's bracketed values.
/// </summary>
public sealed record StatedNumber(string Label, decimal Value, bool IsPvp);

/// <summary>
/// The numbers an element takes from its source: its ability's cooldown and chunk scalar, and every game value, duration,
/// stack table and window of its rules and passives. Counts the player declares or a rule's own modelling chose (stack
/// grants, target counts, Armor Charge thresholds, spawned pickups) are not the source's numbers and are left out.
/// </summary>
public static class ElementNumberReading
{
    public static ImmutableArray<StatedNumber> ReadNumbers(BuildElement element) =>
        [.. element.Ability.Match(some => ReadAbility(some.Value), _ => []),
            .. element.Rules.SelectMany(ReadRule),
            .. element.Passives.SelectMany(passive => ReadPassive(passive.Modifier))];

    private static IEnumerable<StatedNumber> ReadAbility(AbilityProfile ability) =>
        [.. ReadValue("ability.chunkScalar", ability.ChunkScalar), .. ReadValue("ability.baseCooldown", ability.BaseCooldownSeconds)];

    private static IEnumerable<StatedNumber> ReadRule(Rule rule) =>
        [.. ReadTrigger(rule.On), .. rule.Then.SelectMany(ReadOutcome)];

    private static IEnumerable<StatedNumber> ReadTrigger(Trigger trigger) =>
        trigger switch
        {
            Trigger.KillMultiple kill => ReadSeconds("kill.within", kill.Within),
            Trigger.DamageMultiple damage => ReadSeconds("damage.within", damage.Within),
            _ => [],
        };

    private static IEnumerable<StatedNumber> ReadOutcome(Outcome outcome) =>
        outcome.Match<IEnumerable<StatedNumber>>(
            grant => ReadEnergy("grantEnergy", grant.Amount),
            convert => ReadStackEnergy(convert.Energy),
            apply => ReadBuffDuration(apply.Duration),
            _ => [],
            debuff => ReadSeconds("debuffTarget.duration", debuff.Duration),
            _ => [],
            _ => [],
            _ => [],
            modify => ReadValue("modifyDamage", modify.Change),
            restore => restore.Amount.Match(share => ReadValue("restoreHealth", share.Value), points => ReadValue("restoreHealth", points.Value)),
            _ => [],
            _ => [],
            least => ReadEnergy("grantLeastChargedEnergy", least.Amount),
            _ => [],
            _ => [],
            _ => []);

    private static IEnumerable<StatedNumber> ReadPassive(Passive passive) =>
        passive.Match<IEnumerable<StatedNumber>>(
            _ => [],
            _ => [],
            modify => ReadValue("modifyDamage", modify.Change),
            resist => [.. ReadValue("resistDamage", resist.Amount.Pve),
                .. resist.Amount.Pvp.Match(pvp => ReadValue("resistDamage (PvP)", pvp.Value).Select(number => number with { IsPvp = true }), _ => [])],
            weapons => weapons.Changes.SelectMany(change => ReadValue($"weapon stat {change.Stat}", change.Change)),
            raise => ReadValue("raiseMaxStacks", raise.Max),
            stat => ReadValue("modifyStat", stat.Change),
            resist => [.. ReadValue("resistCombatantDamage", resist.Amount), .. ReadRange(resist.Against)]);

    private static IEnumerable<StatedNumber> ReadRange(CombatantDamage against) =>
        against switch
        {
            CombatantDamage.WithinRange within => [new StatedNumber("resistCombatantDamage.within", within.Meters, false)],
            CombatantDamage.BeyondRange beyond => [new StatedNumber("resistCombatantDamage.beyond", beyond.Meters, false)],
            _ => [],
        };

    private static IEnumerable<StatedNumber> ReadEnergy(string label, EnergyGrant grant) =>
        grant.Match<IEnumerable<StatedNumber>>(
            fraction => ReadValue(label, fraction.Amount),
            _ => [],
            archetype => [.. ReadValue(label, archetype.Otherwise), .. ReadValue(label, archetype.OnArchetypes)]);

    private static IEnumerable<StatedNumber> ReadStackEnergy(StackEnergy energy) =>
        energy.Match<IEnumerable<StatedNumber>>(
            perStack => ReadValue("convertStacksToEnergy.perStack", perStack.Amount),
            spent => ReadTable("convertStacksToEnergy", spent.Table),
            charges => ReadTable("convertStacksToEnergy", charges.Table));

    private static IEnumerable<StatedNumber> ReadBuffDuration(BuffDuration duration) =>
        duration.Match<IEnumerable<StatedNumber>>(
            _ => [],
            fixedTime => [new StatedNumber("applyBuff.duration", fixedTime.Time.Value, false)],
            spent => ReadTable("applyBuff.duration", spent.Table));

    private static IEnumerable<StatedNumber> ReadTable(string label, StackTable table) =>
        table.Values.Select(value => new StatedNumber(label, value, false));

    private static IEnumerable<StatedNumber> ReadSeconds(string label, Optional<Seconds> seconds) =>
        seconds.Match<IEnumerable<StatedNumber>>(some => [new StatedNumber(label, some.Value.Value, false)], _ => []);

    private static IEnumerable<StatedNumber> ReadValue(string label, GameValue value) =>
        value.Match<IEnumerable<StatedNumber>>(
            known => [new StatedNumber(label, known.Value, false)],
            perModCount => perModCount.Values.Select(each => new StatedNumber(label, each, false)),
            approximate => [new StatedNumber(label, approximate.Value, false)],
            _ => []);
}

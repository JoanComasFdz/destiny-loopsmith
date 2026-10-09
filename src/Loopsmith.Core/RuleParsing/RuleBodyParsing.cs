using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using YamlDotNet.RepresentationModel;
using static Loopsmith.Core.RuleParsing.ResultAccumulation;
using static Loopsmith.Core.RuleParsing.ValueReading;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary>An element's <c>rules</c> (on → when → then) and <c>passives</c>, with every id checked against the glossary.</summary>
internal static class RuleBodyParsing
{
    private static readonly ImmutableArray<string> RuleKeys = ["on", "when", "then", "chance", "reason"];
    private static readonly ImmutableArray<string> PassiveRuleKeys = ["effect", "when", "reason"];
    private static readonly ImmutableArray<string> KillKeys = ["via", "tier", "targetHas"];
    private static readonly ImmutableArray<string> DamageKeys = ["via", "targetHas"];

    private static readonly ImmutableArray<string> TriggerNames =
        ["abilityCast", "kill", "damage", "pickUp", "buffGained", "stacksMaxed"];

    private static readonly ImmutableArray<string> ConditionNames = ["has", "lacks", "targetHas"];

    private static readonly ImmutableArray<string> OutcomeNames =
    [
        "grantEnergy", "convertStacksToEnergy", "applyBuff", "removeBuff", "debuffTarget", "spawn",
        "summon", "strikeTarget", "modifyDamage", "restoreHealth", "resetCooldown",
    ];

    private static readonly ImmutableArray<string> PassiveNames =
        ["extraStacks", "extraCharges", "modifyDamage", "resistDamage", "modifyWeaponStats"];

    internal static Result<Rule, Errors> ReadRule(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(RuleKeys),
            map.ReadRequired("on", on => ReadTrigger(scope, on)),
            map.ReadOrDefault("when", when => ReadConditions(scope, when), []),
            map.ReadRequired("then", then => ReadOutcomes(scope, then)),
            map.ReadOrDefault("chance", ReadBoolean, false),
            map.ReadOptional("reason", YamlReading.ToText),
            (_, on, when, then, chance, reason) =>
                new Rule(on, when, then, reason, chance ? Likelihood.Chance : Likelihood.Always)));

    internal static Result<PassiveRule, Errors> ReadPassiveRule(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(PassiveRuleKeys),
            map.ReadRequired("effect", effect => ReadPassive(scope, effect)),
            map.ReadOrDefault("when", when => ReadConditions(scope, when), []),
            map.ReadOptional("reason", YamlReading.ToText),
            (_, effect, when, reason) => new PassiveRule(effect, when, reason)));

    // ── Triggers (on) ────────────────────────────────────────────────────────────────────

    private static Result<Trigger, Errors> ReadTrigger(ReferenceScope scope, YamlValue value) =>
        value.ToSingleEntry().Bind(entry => entry.Key switch
        {
            "abilityCast" => ReadVocabularyWord<AbilityKind>(entry.Value).Map(Trigger (kind) => new Trigger.AbilityCast(kind)),
            "kill" => ReadKillTrigger(scope, entry.Value),
            "damage" => ReadDamageTrigger(scope, entry.Value),
            "pickUp" => scope.ReadPickup(entry.Value).Map(Trigger (pickup) => new Trigger.PickUp(pickup)),
            "buffGained" => scope.ReadBuff(entry.Value).Map(Trigger (status) => new Trigger.BuffGained(status)),
            "stacksMaxed" => scope.ReadBuff(entry.Value).Map(Trigger (status) => new Trigger.StacksMaxed(status)),
            _ => entry.Value.FailAt<Trigger>(DescribeUnknown("trigger", entry.Key, TriggerNames)),
        });

    private static Result<Trigger, Errors> ReadKillTrigger(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
                map.CheckKeys(KillKeys),
                map.ReadOrDefault("via", scope.ReadDamageSource, new DamageSource.AnySource()),
                map.ReadOptional("tier", ReadVocabularyWord<EnemyTier>),
                map.ReadOptional("targetHas", targetHas => ReadDebuffList(scope, targetHas)),
                (_, via, tier, targetHas) => (via, tier, targetHas))
            .Bind(kill => ToKillTrigger(map, kill.via, kill.tier, kill.targetHas)));

    /// <summary>A kill is "of a tier" or "of a debuffed target", never both (not representable).</summary>
    private static Result<Trigger, Errors> ToKillTrigger(
        YamlMap map,
        DamageSource via,
        Optional<EnemyTier> tier,
        Optional<ImmutableArray<StatusId>> targetHas) =>
        tier.Match(
            someTier => targetHas.IsSome()
                ? map.FailAtKey<Trigger>("tier", $"{map.Label} cannot combine 'tier' and 'targetHas'")
                : Succeed<Trigger>(new Trigger.KillOfTier(via, someTier.Value)),
            _ => targetHas.Match(
                someStatuses => Succeed<Trigger>(new Trigger.KillDebuffed(via, someStatuses.Value)),
                _ => Succeed<Trigger>(new Trigger.KillAny(via))));

    private static Result<Trigger, Errors> ReadDamageTrigger(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(DamageKeys),
            map.ReadOrDefault("via", scope.ReadDamageSource, new DamageSource.AnySource()),
            map.ReadOptional("targetHas", targetHas => ReadDebuffList(scope, targetHas)),
            (_, via, targetHas) => targetHas.Match<Trigger>(
                some => new Trigger.DamageDebuffed(via, some.Value),
                _ => new Trigger.Damage(via))));

    private static Result<ImmutableArray<StatusId>, Errors> ReadDebuffList(ReferenceScope scope, YamlValue value) =>
        value.ReadEach(value.Label, scope.ReadDebuff).Bind(statuses => statuses.IsEmpty
            ? value.FailAt<ImmutableArray<StatusId>>($"{value.Label} must list at least one debuff")
            : Succeed(statuses));

    // ── Conditions (when) ────────────────────────────────────────────────────────────────

    private static Result<ImmutableArray<Condition>, Errors> ReadConditions(ReferenceScope scope, YamlValue value) =>
        value.ReadEach("condition", condition => ReadCondition(scope, condition));

    private static Result<Condition, Errors> ReadCondition(ReferenceScope scope, YamlValue value) =>
        value.ToSingleEntry().Bind(entry => entry.Key switch
        {
            "has" => scope.ReadBuff(entry.Value).Map(Condition (status) => new Condition.HasBuff(status)),
            "lacks" => scope.ReadBuff(entry.Value).Map(Condition (status) => new Condition.LacksBuff(status)),
            "targetHas" => scope.ReadDebuff(entry.Value).Map(Condition (status) => new Condition.TargetHas(status)),
            _ => entry.Value.FailAt<Condition>(DescribeUnknown("condition", entry.Key, ConditionNames)),
        });

    // ── Outcomes (then) ──────────────────────────────────────────────────────────────────

    private static Result<ImmutableArray<Outcome>, Errors> ReadOutcomes(ReferenceScope scope, YamlValue value) =>
        value.ReadEach("outcome", outcome => ReadOutcome(scope, outcome)).Bind(outcomes => outcomes.IsEmpty
            ? value.FailAt<ImmutableArray<Outcome>>($"{value.Label} must list at least one outcome")
            : Succeed(outcomes));

    private static Result<Outcome, Errors> ReadOutcome(ReferenceScope scope, YamlValue value) =>
        value.ToSingleEntry().Bind(entry => entry.Key switch
        {
            "grantEnergy" => ReadGrantEnergy(entry.Value),
            "convertStacksToEnergy" => ReadConvertStacksToEnergy(scope, entry.Value),
            "applyBuff" => ReadApplyBuff(scope, entry.Value),
            "removeBuff" => scope.ReadBuff(entry.Value).Map(Outcome (status) => new Outcome.RemoveBuff(status)),
            "debuffTarget" => ReadDebuffTarget(scope, entry.Value),
            "spawn" => ReadSpawn(scope, entry.Value),
            "summon" => ReadSpawnSummon(scope, entry.Value),
            "strikeTarget" => ReadStrikeTarget(scope, entry.Value),
            "modifyDamage" => ReadDamageChange(scope, entry.Value)
                .Map(Outcome (change) => new Outcome.ModifyDamage(change.Against, change.Change)),
            "restoreHealth" => ReadRestoreHealth(entry.Value),
            "resetCooldown" => ReadVocabularyWord<AbilityKind>(entry.Value).Map(Outcome (kind) => new Outcome.ResetCooldown(kind)),
            _ => entry.Value.FailAt<Outcome>(DescribeUnknown("outcome", entry.Key, OutcomeNames)),
        });

    private static Result<Outcome, Errors> ReadGrantEnergy(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["to", "amount"]),
            map.ReadRequired("to", ReadVocabularyWord<AbilityKind>),
            map.ReadRequired("amount", ReadEnergyGrant),
            Outcome (_, to, amount) => new Outcome.GrantEnergy(to, amount)));

    /// <summary><c>full</c> → <see cref="EnergyGrant.Full"/>; any game value → a fraction of one charge.</summary>
    private static Result<EnergyGrant, Errors> ReadEnergyGrant(YamlValue value) =>
        value.ParseWith(text => text.Trim() == "full"
            ? new Result<EnergyGrant, string>.Ok(new EnergyGrant.Full())
            : GameNotationParsing.ParseGameValue(text)
                .Map(EnergyGrant (amount) => new EnergyGrant.Fraction(amount))
                .MapError(message => $"{message}, or full"));

    private static Result<Outcome, Errors> ReadConvertStacksToEnergy(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["consumed", "to", "perStack"]),
            map.ReadRequired("consumed", scope.ReadBuff),
            map.ReadRequired("to", ReadVocabularyWord<AbilityKind>),
            map.ReadRequired("perStack", ReadGameValue),
            Outcome (_, consumed, to, perStack) => new Outcome.ConvertStacksToEnergy(consumed, to, perStack)));

    /// <summary><c>{ applyBuff: amplified }</c> or <c>{ applyBuff: { status, stacks = 1, duration } }</c>.</summary>
    private static Result<Outcome, Errors> ReadApplyBuff(ReferenceScope scope, YamlValue value) =>
        value.Node is YamlScalarNode
            ? scope.ReadBuff(value).Map(Outcome (status) => new Outcome.ApplyBuff(status, Optional.None<Seconds>(), OneStack))
            : value.ToMap().Bind(map => Combine(
                map.CheckKeys(["status", "stacks", "duration"]),
                map.ReadRequired("status", scope.ReadBuff),
                map.ReadOrDefault("duration", ReadDuration, Optional.None<Seconds>()),
                map.ReadOrDefault("stacks", ReadStackCount, OneStack),
                Outcome (_, status, duration, stacks) => new Outcome.ApplyBuff(status, duration, stacks)));

    /// <summary><c>{ debuffTarget: jolt }</c> or <c>{ debuffTarget: { status, duration } }</c>.</summary>
    private static Result<Outcome, Errors> ReadDebuffTarget(ReferenceScope scope, YamlValue value) =>
        value.Node is YamlScalarNode
            ? scope.ReadDebuff(value).Map(Outcome (status) => new Outcome.DebuffTarget(status, Optional.None<Seconds>()))
            : value.ToMap().Bind(map => Combine(
                map.CheckKeys(["status", "duration"]),
                map.ReadRequired("status", scope.ReadDebuff),
                map.ReadOrDefault("duration", ReadDuration, Optional.None<Seconds>()),
                Outcome (_, status, duration) => new Outcome.DebuffTarget(status, duration)));

    /// <summary><c>{ spawn: orb-of-power }</c> or <c>{ spawn: { pickup, count = 1 } }</c>.</summary>
    private static Result<Outcome, Errors> ReadSpawn(ReferenceScope scope, YamlValue value) =>
        value.Node is YamlScalarNode
            ? scope.ReadPickup(value).Map(Outcome (pickup) => new Outcome.Spawn(pickup, 1))
            : value.ToMap().Bind(map => Combine(
                map.CheckKeys(["pickup", "count"]),
                map.ReadRequired("pickup", scope.ReadPickup),
                map.ReadOrDefault("count", ReadPositiveInteger, 1),
                Outcome (_, pickup, count) => new Outcome.Spawn(pickup, count)));

    /// <summary><c>{ summon: threadling }</c> or <c>{ summon: { summon, count = 1 } }</c>.</summary>
    private static Result<Outcome, Errors> ReadSpawnSummon(ReferenceScope scope, YamlValue value) =>
        value.Node is YamlScalarNode
            ? scope.ReadSummon(value).Map(Outcome (summon) => new Outcome.SpawnSummon(summon, 1))
            : value.ToMap().Bind(map => Combine(
                map.CheckKeys(["summon", "count"]),
                map.ReadRequired("summon", scope.ReadSummon),
                map.ReadOrDefault("count", ReadPositiveInteger, 1),
                Outcome (_, summon, count) => new Outcome.SpawnSummon(summon, count)));

    private static Result<Outcome, Errors> ReadStrikeTarget(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["via", "hit"]),
            map.ReadRequired("via", scope.ReadStatus),
            map.ReadRequired("hit", ReadVocabularyWord<HitOutcome>),
            Outcome (_, via, hit) => new Outcome.StrikeTarget(via, hit)));

    private static Result<Outcome, Errors> ReadRestoreHealth(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["amount", "allies"]),
            map.ReadRequired("amount", ReadGameValue),
            map.ReadOrDefault("allies", ReadBoolean, false),
            Outcome (_, amount, allies) => new Outcome.RestoreHealth(amount, allies)));

    /// <summary><c>{ against: &lt;source&gt;, change: "+50%" }</c> — shared by the outcome and the passive.</summary>
    private static Result<(DamageSource Against, GameValue Change), Errors> ReadDamageChange(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["against", "change"]),
            map.ReadRequired("against", scope.ReadDamageSource),
            map.ReadRequired("change", ReadGameValue),
            (_, against, change) => (against, change)));

    // ── Passives (effect) ────────────────────────────────────────────────────────────────

    private static Result<Passive, Errors> ReadPassive(ReferenceScope scope, YamlValue value) =>
        value.ToSingleEntry().Bind(entry => entry.Key switch
        {
            "extraStacks" => ReadExtraStacks(scope, entry.Value),
            "extraCharges" => ReadExtraCharges(entry.Value),
            "modifyDamage" => ReadDamageChange(scope, entry.Value)
                .Map(Passive (change) => new Passive.ModifyDamage(change.Against, change.Change)),
            "resistDamage" => ReadGameValue(entry.Value).Map(Passive (amount) => new Passive.ResistDamage(amount)),
            "modifyWeaponStats" => ReadModifyWeaponStats(entry.Value),
            _ => entry.Value.FailAt<Passive>(DescribeUnknown("passive effect", entry.Key, PassiveNames)),
        });

    private static Result<Passive, Errors> ReadExtraStacks(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["status", "extra"]),
            map.ReadRequired("status", scope.ReadBuff),
            map.ReadRequired("extra", ReadStackCount),
            Passive (_, status, extra) => new Passive.ExtraStacks(status, extra)));

    private static Result<Passive, Errors> ReadExtraCharges(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["ability", "extra"]),
            map.ReadRequired("ability", ReadVocabularyWord<AbilityKind>),
            map.ReadRequired("extra", ReadPositiveInteger),
            Passive (_, ability, extra) => new Passive.ExtraCharges(ability, extra)));

    private static Result<Passive, Errors> ReadModifyWeaponStats(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["archetypes", "changes"]),
            map.ReadRequired("archetypes", archetypes => ReadNonEmpty(archetypes, archetypes.ReadEach("archetype", ReadSlug))),
            map.ReadRequired("changes", changes => ReadNonEmpty(changes, changes.ReadEach("change", ReadWeaponStatChange))),
            Passive (_, archetypes, changes) => new Passive.ModifyWeaponStats(archetypes, changes)));

    private static Result<WeaponStatChange, Errors> ReadWeaponStatChange(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["stat", "change"]),
            map.ReadRequired("stat", ReadSlug),
            map.ReadRequired("change", ReadGameValue),
            (_, stat, change) => new WeaponStatChange(stat, change)));

    private static Result<ImmutableArray<T>, Errors> ReadNonEmpty<T>(YamlValue value, Result<ImmutableArray<T>, Errors> items) =>
        items.Bind(list => list.IsEmpty
            ? value.FailAt<ImmutableArray<T>>($"{value.Label} must not be empty")
            : Succeed(list));

    private static string DescribeUnknown(string what, string key, ImmutableArray<string> known) =>
        $"unknown {what} '{key}' (expected one of: {string.Join(", ", known)})";
}

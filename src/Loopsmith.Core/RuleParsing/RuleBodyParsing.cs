using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using YamlDotNet.RepresentationModel;
using static Loopsmith.Core.RuleParsing.ResultAccumulation;
using static Loopsmith.Core.RuleParsing.ValueReading;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary>Who a <c>grantEnergy</c> gives to: an ability, or the least-charged one (the game picks it: <c>to: leastCharged</c>).</summary>
[Union]
internal partial record EnergyRecipient
{
    partial record Ability(AbilityKind Kind);
    partial record LeastCharged();
}

/// <summary>An element's <c>rules</c> (on → when → then) and <c>passives</c>, with every id checked against the glossary.</summary>
internal static class RuleBodyParsing
{
    private static readonly ImmutableArray<string> RuleKeys = ["on", "when", "then", "chance", "reason", "doesNotStackWith"];
    private static readonly ImmutableArray<string> PassiveRuleKeys = ["effect", "when", "reason"];
    private static readonly ImmutableArray<string> KillKeys = ["via", "tier", "targetHas", "atLeast", "fewerThan"];
    private static readonly ImmutableArray<string> DamageKeys = ["via", "targetHas", "atLeast"];

    private static readonly ImmutableArray<string> TriggerNames =
        ["abilityCast", "kill", "damage", "pickUp", "buffGained", "stacksMaxed", "finisher"];

    private static readonly ImmutableArray<string> ConditionNames = ["has", "lacks", "targetHas", "atMax", "during"];

    private static readonly ImmutableArray<string> OutcomeNames =
    [
        "grantEnergy", "convertStacksToEnergy", "applyBuff", "removeBuff", "debuffTarget", "spawn",
        "summon", "strikeTarget", "modifyDamage", "restoreHealth", "resetCooldown", "collect",
    ];

    private static readonly ImmutableArray<string> PassiveNames =
        ["extraStacks", "extraCharges", "modifyDamage", "resistDamage", "modifyWeaponStats", "raiseMaxStacks", "modifyStat", "resistCombatantDamage"];

    internal static Result<Rule, Errors> ReadRule(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(RuleKeys),
            map.ReadRequired("on", on => ReadTrigger(scope, on)),
            map.ReadOrDefault("when", when => ReadConditions(scope, when), []),
            map.ReadRequired("then", then => ReadOutcomes(scope, then)),
            map.ReadOrDefault("chance", ReadBoolean, false),
            map.ReadOptional("reason", YamlReading.ToText),
            map.ReadOrDefault("doesNotStackWith", ReadElementList, []),
            (_, on, when, then, chance, reason, doesNotStackWith) =>
                new Rule(on, when, then, reason, chance ? Likelihood.Chance : Likelihood.Always, doesNotStackWith)));

    internal static Result<PassiveRule, Errors> ReadPassiveRule(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(PassiveRuleKeys),
            map.ReadRequired("effect", effect => ReadPassive(scope, effect)),
            map.ReadOrDefault("when", when => ReadConditions(scope, when), []),
            map.ReadOptional("reason", YamlReading.ToText),
            (_, effect, when, reason) => new PassiveRule(effect, when, reason))
            .Bind(passive => CheckAlwaysRaised(map, passive)));

    /// <summary>A raised cap is the build's, a fact that always holds (ADRs D3): a <c>raiseMaxStacks</c> takes no <c>when</c>.</summary>
    private static Result<PassiveRule, Errors> CheckAlwaysRaised(YamlMap map, PassiveRule passive) =>
        passive.Modifier is Passive.RaiseMaxStacks && !passive.When.IsEmpty
            ? map.FailAtKey<PassiveRule>("when", "raiseMaxStacks raises the build's cap, which always holds, so it takes no when")
            : Succeed(passive);

    // ── Triggers (on) ────────────────────────────────────────────────────────────────────

    private static Result<Trigger, Errors> ReadTrigger(ReferenceScope scope, YamlValue value) =>
        value.ToSingleEntry().Bind(entry => entry.Key switch
        {
            "abilityCast" => ReadAbilityCastTrigger(entry.Value),
            "kill" => ReadKillTrigger(scope, entry.Value),
            "damage" => ReadDamageTrigger(scope, entry.Value),
            "pickUp" => scope.ReadPickup(entry.Value).Map(Trigger (pickup) => new Trigger.PickUp(pickup)),
            "buffGained" => scope.ReadBuff(entry.Value).Map(Trigger (status) => new Trigger.BuffGained(status)),
            "stacksMaxed" => scope.ReadStackingBuff(entry.Value).Map(Trigger (status) => new Trigger.StacksMaxed(status)),
            "finisher" => ReadFinisherTrigger(entry.Value),
            _ => entry.Value.FailAt<Trigger>(DescribeUnknown("trigger", entry.Key, TriggerNames)),
        });

    /// <summary><c>{ finisher: {} }</c>: the player does a finisher on an enemy of the pack (it kills it). It takes no keys.</summary>
    private static Result<Trigger, Errors> ReadFinisherTrigger(YamlValue value) =>
        value.ToMap().Bind(map => map.Node.Children.Count == 0
            ? Succeed<Trigger>(new Trigger.Finisher())
            : map.FailAt<Trigger>($"{map.Label} takes no keys: write {{ finisher: {{}} }}"));

    /// <summary>
    /// <c>{ abilityCast: grenade }</c> (any use of it), or <c>{ abilityCast: { ability: melee, input: slide } }</c> for one use
    /// only: a secondary ability — the melee while sliding (Tempest Strike's uppercut) or in the air, the class ability's
    /// air move (Ascension), the grenade held. A plain cast trigger matches every use.
    /// </summary>
    private static Result<Trigger, Errors> ReadAbilityCastTrigger(YamlValue value) =>
        value.Node is YamlScalarNode
            ? ReadVocabularyWord<AbilityKind>(value).Map(Trigger (kind) => new Trigger.AbilityCast(kind))
            : value.ToMap().Bind(map => Combine(
                    map.CheckKeys(["ability", "input"]),
                    map.ReadRequired("ability", ReadVocabularyWord<AbilityKind>),
                    map.ReadOptional("input", ReadName),
                    (_, kind, word) => (kind, word))
                .Bind(cast => cast.word.Match(
                    word => GameNotationParsing.ParseAbilityInput(cast.kind, word.Value).Match(
                        input => Succeed<Trigger>(new Trigger.AbilityCastAs(cast.kind, input.Value)),
                        why => map.FailAtKey<Trigger>("input", $"{map.Label}: {why.Failure}")),
                    _ => Succeed<Trigger>(new Trigger.AbilityCast(cast.kind)))));

    private static Result<Trigger, Errors> ReadKillTrigger(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
                map.CheckKeys(KillKeys),
                map.ReadOrDefault("via", scope.ReadDamageSource, new DamageSource.AnySource()),
                map.ReadOptional("tier", ReadVocabularyWord<EnemyTier>),
                map.ReadOptional("targetHas", targetHas => ReadDebuffList(scope, targetHas)),
                map.ReadOptional("atLeast", ReadTargetCount),
                map.ReadOptional("fewerThan", ReadFewerThan),
                (_, via, tier, targetHas, atLeast, fewerThan) => (via, tier, targetHas, atLeast, fewerThan))
            .Bind(kill => Combine(
                CheckAtLeastStandsAlone(map, kill.atLeast, ("tier", kill.tier.IsSome()), ("targetHas", kill.targetHas.IsSome()), ("fewerThan", kill.fewerThan.IsSome())),
                CheckFewerThanStandsAlone(map, kill.fewerThan, ("tier", kill.tier.IsSome()), ("targetHas", kill.targetHas.IsSome())),
                ToKillTrigger(map, kill.via, kill.tier, kill.targetHas, kill.atLeast, kill.fewerThan),
                (_, _, trigger) => trigger)));

    /// <summary>
    /// A kill is "of a tier", "of a debuffed target", "of at least N in one action" or "of fewer than N in one action" —
    /// never two (not representable).
    /// </summary>
    private static Result<Trigger, Errors> ToKillTrigger(
        YamlMap map,
        DamageSource via,
        Optional<EnemyTier> tier,
        Optional<ImmutableArray<StatusId>> targetHas,
        Optional<TargetCount> atLeast,
        Optional<TargetCount> fewerThan) =>
        atLeast.Match(
            someCount => Succeed<Trigger>(new Trigger.KillMultiple(via, someCount.Value)),
            _ => fewerThan.Match(
                someLimit => Succeed<Trigger>(new Trigger.KillFewer(via, someLimit.Value)),
                _ => tier.Match(
                    someTier => targetHas.IsSome()
                        ? map.FailAtKey<Trigger>("tier", $"{map.Label} cannot combine 'tier' and 'targetHas'")
                        : Succeed<Trigger>(new Trigger.KillOfTier(via, someTier.Value)),
                    _ => targetHas.Match(
                        someStatuses => Succeed<Trigger>(new Trigger.KillDebuffed(via, someStatuses.Value)),
                        _ => Succeed<Trigger>(new Trigger.KillAny(via))))));

    /// <summary><c>fewerThan</c>: kills in one action below this many, at least one; 2..20 (fewer than 1 would never happen).</summary>
    private static Result<TargetCount, Errors> ReadFewerThan(YamlValue value) =>
        ReadTargetCount(value).Bind(count => count.Value >= 2
            ? Succeed(count)
            : value.FailAt<TargetCount>($"{value.Label}: fewer than {count.Value} kills never happens (use 2..20)"));

    /// <summary><c>fewerThan</c> ("fewer than N enemies in one action") is a trigger of its own, like <c>atLeast</c>.</summary>
    private static Result<Unit, Errors> CheckFewerThanStandsAlone(YamlMap map, Optional<TargetCount> fewerThan, params (string Key, bool IsPresent)[] others) =>
        fewerThan.IsSome()
            ? others
                .Where(other => other.IsPresent)
                .Select(other => map.FailAtKey<Unit>(other.Key, $"{map.Label} cannot combine 'fewerThan' and '{other.Key}'"))
                .CollectAll()
                .Map(Unit (_) => new Unit.Value())
            : Succeed<Unit>(new Unit.Value());

    private static Result<Trigger, Errors> ReadDamageTrigger(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
                map.CheckKeys(DamageKeys),
                map.ReadOrDefault("via", scope.ReadDamageSource, new DamageSource.AnySource()),
                map.ReadOptional("targetHas", targetHas => ReadDebuffList(scope, targetHas)),
                map.ReadOptional("atLeast", ReadTargetCount),
                (_, via, targetHas, atLeast) => (via, targetHas, atLeast))
            .Bind(damage => Combine(
                CheckAtLeastStandsAlone(map, damage.atLeast, ("targetHas", damage.targetHas.IsSome())),
                Succeed(damage.atLeast.Match<Trigger>(
                    someCount => new Trigger.DamageMultiple(damage.via, someCount.Value),
                    _ => damage.targetHas.Match<Trigger>(
                        someStatuses => new Trigger.DamageDebuffed(damage.via, someStatuses.Value),
                        _ => new Trigger.Damage(damage.via)))),
                (_, trigger) => trigger)));

    /// <summary>
    /// <c>atLeast</c> ("N enemies in one action", ADRs D4) is a trigger of its own: it can't be combined with a tier or a
    /// target debuff (one error per combined key, at that key).
    /// </summary>
    private static Result<Unit, Errors> CheckAtLeastStandsAlone(YamlMap map, Optional<TargetCount> atLeast, params (string Key, bool IsPresent)[] others) =>
        atLeast.IsSome()
            ? others
                .Where(other => other.IsPresent)
                .Select(other => map.FailAtKey<Unit>(other.Key, $"{map.Label} cannot combine 'atLeast' and '{other.Key}'"))
                .CollectAll()
                .Map(Unit (_) => new Unit.Value())
            : Succeed<Unit>(new Unit.Value());

    private static Result<ImmutableArray<StatusId>, Errors> ReadDebuffList(ReferenceScope scope, YamlValue value) =>
        value.ReadEach(value.Label, scope.ReadDebuff).Bind(statuses => statuses.IsEmpty
            ? value.FailAt<ImmutableArray<StatusId>>($"{value.Label} must list at least one debuff")
            : Succeed(statuses));

    /// <summary>Element ids (<c>doesNotStackWith</c>); whether they exist is checked once every file is parsed.</summary>
    private static Result<ImmutableArray<ElementId>, Errors> ReadElementList(YamlValue value) =>
        value.ReadEach(value.Label, ReadElementId).Bind(ids => ids.IsEmpty
            ? value.FailAt<ImmutableArray<ElementId>>($"{value.Label} must list at least one element")
            : Succeed(ids));

    // ── Conditions (when) ────────────────────────────────────────────────────────────────

    private static Result<ImmutableArray<Condition>, Errors> ReadConditions(ReferenceScope scope, YamlValue value) =>
        value.ReadEach("condition", condition => ReadCondition(scope, condition));

    private static Result<Condition, Errors> ReadCondition(ReferenceScope scope, YamlValue value) =>
        value.ToSingleEntry().Bind(entry => entry.Key switch
        {
            "has" => scope.ReadBuff(entry.Value).Map(Condition (status) => new Condition.HasBuff(status)),
            "lacks" => scope.ReadBuff(entry.Value).Map(Condition (status) => new Condition.LacksBuff(status)),
            "targetHas" => scope.ReadDebuff(entry.Value).Map(Condition (status) => new Condition.TargetHas(status)),
            "atMax" => scope.ReadStackingBuff(entry.Value).Map(Condition (status) => new Condition.AtMax(status)),
            "during" => ReadVocabularyWord<AbilityKind>(entry.Value).Map(Condition (ability) => new Condition.During(ability)),
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
            "collect" => scope.ReadPickup(entry.Value).Map(Outcome (pickup) => new Outcome.Collect(pickup)),
            _ => entry.Value.FailAt<Outcome>(DescribeUnknown("outcome", entry.Key, OutcomeNames)),
        });

    /// <summary><c>{ to: grenade, amount: "15%" }</c>, or <c>to: leastCharged</c> for the ability with the least energy (the game picks it).</summary>
    private static Result<Outcome, Errors> ReadGrantEnergy(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["to", "amount"]),
            map.ReadRequired("to", ReadEnergyRecipient),
            map.ReadRequired("amount", ReadEnergyGrant),
            (_, to, amount) => to.Match<Outcome>(
                ability => new Outcome.GrantEnergy(ability.Kind, amount),
                _ => new Outcome.GrantLeastChargedEnergy(amount))));

    private static Result<EnergyRecipient, Errors> ReadEnergyRecipient(YamlValue value) =>
        value.ParseWith(text => text == "leastCharged"
            ? new Result<EnergyRecipient, string>.Ok(new EnergyRecipient.LeastCharged())
            : GameNotationParsing.ParseVocabularyWord<AbilityKind>(text)
                .Map(EnergyRecipient (kind) => new EnergyRecipient.Ability(kind))
                .MapError(message => $"{message}, or leastCharged"));

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
            "resistDamage" => ReadPveAndPvp(entry.Value).Map(Passive (amount) => new Passive.ResistDamage(amount)),
            "resistCombatantDamage" => ReadResistCombatantDamage(entry.Value),
            "modifyWeaponStats" => ReadModifyWeaponStats(entry.Value),
            "raiseMaxStacks" => ReadRaiseMaxStacks(scope, entry.Value),
            "modifyStat" => ReadModifyStat(entry.Value),
            _ => entry.Value.FailAt<Passive>(DescribeUnknown("passive effect", entry.Key, PassiveNames)),
        });

    /// <summary>
    /// <c>modifyStat: { stat: grenade, change: "+20 | +40 | +50" }</c> (the Font mods, while Armor Charge): stat points, by
    /// copies — a fact shown, never added to the build's stats (ADRs D1).
    /// </summary>
    private static Result<Passive, Errors> ReadModifyStat(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["stat", "change"]),
            map.ReadRequired("stat", ReadVocabularyWord<StatKind>),
            map.ReadRequired("change", ReadStatPoints),
            Passive (_, stat, change) => new Passive.ModifyStat(stat, change)));

    /// <summary>A number of stat points ("+20", "+20 | +40 | +50", "?"); a percent is refused (it would read as a fraction).</summary>
    private static Result<GameValue, Errors> ReadStatPoints(YamlValue value) =>
        value.ParseWith(text => text.Contains('%')
            ? new Result<GameValue, string>.Error($"'{text}' is a percent; a stat change is a number of points (e.g. +20, or \"+20 | +40 | +50\" by copies)")
            : GameNotationParsing.ParseGameValue(text));

    /// <summary>
    /// <c>resistCombatantDamage: { against: arc, amount: "15% | 25% | 30%" }</c> (by copies): a resistance against
    /// combatants only does nothing in PvP, so its amount takes no PvP value.
    /// </summary>
    private static Result<Passive, Errors> ReadResistCombatantDamage(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
                map.CheckKeys(["against", "amount"]),
                map.ReadRequired("against", ReadCombatantDamage),
                map.ReadRequired("amount", ReadPveAndPvp),
                (_, against, amount) => (Against: against, Amount: amount))
            .Bind(read => read.Amount.Pvp.IsSome()
                ? map.FailAtKey<Passive>("amount", "a resistance against combatants does nothing in PvP, so it has no PvP value: drop the [ ]")
                : Succeed<Passive>(new Passive.ResistCombatantDamage(read.Against, read.Amount.Pve))));

    private static Result<Passive, Errors> ReadExtraStacks(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["status", "extra"]),
            map.ReadRequired("status", scope.ReadBuff),
            map.ReadRequired("extra", ReadStackCount),
            Passive (_, status, extra) => new Passive.ExtraStacks(status, extra)));

    /// <summary><c>raiseMaxStacks: { status: armor-charge, max: "4 | 5 | 6" }</c>: only a status that stacks has a cap to raise.</summary>
    private static Result<Passive, Errors> ReadRaiseMaxStacks(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["status", "max"]),
            map.ReadRequired("status", scope.ReadStackingBuff),
            map.ReadRequired("max", ReadGameValue),
            Passive (_, status, max) => new Passive.RaiseMaxStacks(status, max)));

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

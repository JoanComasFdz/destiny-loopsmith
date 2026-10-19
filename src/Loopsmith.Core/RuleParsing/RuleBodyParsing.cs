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
    private static readonly ImmutableArray<string> RuleKeys = ["id", "on", "when", "then", "chance", "reason", "doesNotStackWith"];
    private static readonly ImmutableArray<string> PassiveRuleKeys = ["id", "effect", "when", "reason"];
    private static readonly ImmutableArray<string> KillKeys = ["via", "tier", "targetHas", "atLeast", "fewerThan", "within", "precision"];
    private static readonly ImmutableArray<string> DamageKeys = ["via", "targetHas", "atLeast", "fewerThan", "within", "sustained"];

    private static readonly ImmutableArray<string> TriggerNames =
        ["abilityCast", "kill", "damage", "pickUp", "buffGained", "stacksMaxed", "finisher", "criticalHealth", "shieldBroken", "energyExpended", "reload"];

    private static readonly ImmutableArray<string> ConditionNames = ["has", "lacks", "targetHas", "atMax", "belowMax", "during", "holds"];

    private static readonly ImmutableArray<string> OutcomeNames =
    [
        "grantEnergy", "convertStacksToEnergy", "applyBuff", "removeBuff", "debuffTarget", "spawn",
        "summon", "strikeTarget", "modifyDamage", "restoreHealth", "resetCooldown", "collect", "spendStacks",
        "reloadWeapon",
    ];

    private static readonly ImmutableArray<string> PassiveNames =
        ["extraStacks", "extraCharges", "modifyDamage", "resistDamage", "modifyWeaponStats", "raiseMaxStacks", "modifyStat", "resistCombatantDamage"];

    internal static Result<Rule, Errors> ReadRule(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(RuleKeys),
            map.ReadRequired("id", ReadRuleId),
            map.ReadRequired("on", on => ReadTrigger(scope, on)),
            map.ReadOrDefault("when", when => ReadConditions(scope, when), []),
            map.ReadRequired("then", then => ReadOutcomes(scope, then)),
            map.ReadOrDefault("chance", ReadBoolean, false),
            map.ReadOptional("reason", YamlReading.ToText),
            map.ReadOrDefault("doesNotStackWith", ReadElementList, []),
            (_, id, on, when, then, chance, reason, doesNotStackWith) =>
                new Rule(id, on, when, then, reason, chance ? Likelihood.Chance : Likelihood.Always, doesNotStackWith)));

    internal static Result<PassiveRule, Errors> ReadPassiveRule(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(PassiveRuleKeys),
            map.ReadRequired("id", ReadRuleId),
            map.ReadRequired("effect", effect => ReadPassive(scope, effect)),
            map.ReadOrDefault("when", when => ReadConditions(scope, when), []),
            map.ReadOptional("reason", YamlReading.ToText),
            (_, id, effect, when, reason) => new PassiveRule(id, effect, when, reason))
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
            "criticalHealth" => ReadCriticalHealthTrigger(entry.Value),
            "shieldBroken" => ReadShieldBrokenTrigger(scope, entry.Value),
            "energyExpended" => ReadEnergyExpendedTrigger(entry.Value),
            "reload" => ReadReloadTrigger(scope, entry.Value),
            _ => entry.Value.FailAt<Trigger>(DescribeUnknown("trigger", entry.Key, TriggerNames)),
        });

    /// <summary><c>{ finisher: {} }</c>: the player does a finisher on an enemy of the pack (it kills it). It takes no keys.</summary>
    private static Result<Trigger, Errors> ReadFinisherTrigger(YamlValue value) =>
        value.ToMap().Bind(map => map.Node.Children.Count == 0
            ? Succeed<Trigger>(new Trigger.Finisher())
            : map.FailAt<Trigger>($"{map.Label} takes no keys: write {{ finisher: {{}} }}"));

    /// <summary><c>{ criticalHealth: {} }</c>: the player declares they reached critical health (<c>health:critical</c>). It takes no keys.</summary>
    private static Result<Trigger, Errors> ReadCriticalHealthTrigger(YamlValue value) =>
        value.ToMap().Bind(map => map.Node.Children.Count == 0
            ? Succeed<Trigger>(new Trigger.CriticalHealth())
            : map.FailAt<Trigger>($"{map.Label} takes no keys: write {{ criticalHealth: {{}} }}"));

    /// <summary>
    /// <c>{ shieldBroken: {} }</c> or <c>{ shieldBroken: { via: weapon } }</c>: a combatant's shield broken by that source
    /// (any, by default). The player says which weapon step breaks one (<c>kinetic:kill:shield</c>, ADRs D36).
    /// </summary>
    private static Result<Trigger, Errors> ReadShieldBrokenTrigger(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["via"]),
            map.ReadOrDefault("via", scope.ReadDamageSource, new DamageSource.AnySource()),
            (_, via) => (Trigger)new Trigger.ShieldBroken(via)));

    /// <summary>
    /// <c>{ reload: {} }</c> or <c>{ reload: { via: weapon:this } }</c>: a weapon reloaded, by the player's Reload step
    /// (<c>kinetic:reload</c>) or by an outcome that reloads it (ADRs D37). <c>via</c> names a weapon (any, by default).
    /// </summary>
    private static Result<Trigger, Errors> ReadReloadTrigger(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
                map.CheckKeys(["via"]),
                map.ReadOrDefault("via", scope.ReadDamageSource, new DamageSource.AnySource()),
                (_, via) => via)
            .Bind(via => IsWeaponSource(via) || via is DamageSource.AnySource
                ? Succeed<Trigger>(new Trigger.Reload(via))
                : map.FailAtKey<Trigger>("via", $"reload's via names a weapon: {WeaponSources}")));

    /// <summary>The weapon sources a reload names: only a weapon reloads.</summary>
    private const string WeaponSources = "any, weapon, weapon:<type>, weapon:harmonic, slot:<slot>, weapon:this or weapon:this:<type>";

    private static bool IsWeaponSource(DamageSource source) =>
        source is DamageSource.AnyWeapon or DamageSource.WeaponOfType or DamageSource.WeaponOfSuperType or DamageSource.WeaponInSlot
            or DamageSource.ThisWeapon or DamageSource.ThisWeaponOfType;

    /// <summary>
    /// <c>{ reloadWeapon: weapon:this }</c> or <c>{ reloadWeapon: slot:power }</c>: reloads that one weapon — the one holding
    /// the element, or the build's in that slot (ADRs D37). Its reload rules fire as on a Reload step; no ammo is counted.
    /// </summary>
    private static Result<Outcome, Errors> ReadReloadWeapon(ReferenceScope scope, YamlValue value) =>
        scope.ReadDamageSource(value).Bind(weapon => weapon is DamageSource.ThisWeapon or DamageSource.ThisWeaponOfType or DamageSource.WeaponInSlot
            ? Succeed<Outcome>(new Outcome.ReloadWeapon(weapon))
            : value.FailAt<Outcome>($"{value.Label} reloads one weapon: weapon:this, weapon:this:<type> or slot:<slot>"));

    /// <summary>
    /// <c>{ energyExpended: grenade }</c> (<c>grenade|melee|classAbility</c>): the ability's last charge used, its energy
    /// fully expended (the Kickstarts, ADRs D36). A Super has one charge and no such trigger.
    /// </summary>
    private static Result<Trigger, Errors> ReadEnergyExpendedTrigger(YamlValue value) =>
        ReadVocabularyWord<AbilityKind>(value).Bind(kind => kind == AbilityKind.Super
            ? value.FailAt<Trigger>($"{value.Label}: a Super has no energyExpended (use grenade, melee or classAbility)")
            : Succeed<Trigger>(new Trigger.EnergyExpended(kind)));

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
                map.ReadOrDefault("within", ReadDuration, Optional.None<Seconds>()),
                map.ReadOrDefault("precision", ReadBoolean, false),
                (_, via, tier, targetHas, atLeast, fewerThan, within, precision) =>
                    (via, tier, targetHas, atLeast, fewerThan, within, needs: precision ? HitZone.Precision : HitZone.Body))
            .Bind(kill => Combine(
                CheckAtLeastStandsAlone(map, kill.atLeast, ("tier", kill.tier.IsSome()), ("targetHas", kill.targetHas.IsSome())),
                CheckFewerThanStandsAlone(map, kill.fewerThan, ("tier", kill.tier.IsSome()), ("targetHas", kill.targetHas.IsSome()), ("within", kill.within.IsSome() && kill.atLeast.IsSome())),
                CheckWithinNeedsAtLeast(map, kill.within, kill.atLeast, "kill"),
                CheckFewerThanAboveAtLeast(map, kill.atLeast, kill.fewerThan),
                ToKillTrigger(map, kill.via, kill.tier, kill.targetHas, kill.atLeast, kill.fewerThan, kill.within, kill.needs),
                (_, _, _, _, trigger) => trigger)));

    /// <summary><c>within</c> is the window a source gives its kills or hits ("2 kills within 3 s"): only a count has one.</summary>
    private static Result<Unit, Errors> CheckWithinNeedsAtLeast(YamlMap map, Optional<Seconds> within, Optional<TargetCount> atLeast, string what) =>
        within.IsSome() && !atLeast.IsSome()
            ? map.FailAtKey<Unit>("within", $"{map.Label}: 'within' is the window of a {what} count, so it needs 'atLeast'")
            : Succeed<Unit>(new Unit.Value());

    /// <summary>
    /// <c>atLeast</c> with <c>fewerThan</c> is a kill count from one to the other ("exactly 2": Reaper's Tithe's x2), so the upper
    /// bound must be above the lower one.
    /// </summary>
    private static Result<Unit, Errors> CheckFewerThanAboveAtLeast(YamlMap map, Optional<TargetCount> atLeast, Optional<TargetCount> fewerThan) =>
        atLeast.Match(
            least => fewerThan.Match(
                limit => limit.Value.Value > least.Value.Value
                    ? Succeed<Unit>(new Unit.Value())
                    : map.FailAtKey<Unit>("fewerThan", $"{map.Label}: fewer than {limit.Value.Value} and at least {least.Value.Value} never happens (fewerThan must be above atLeast)"),
                _ => Succeed<Unit>(new Unit.Value())),
            _ => Succeed<Unit>(new Unit.Value()));

    /// <summary>
    /// A kill is "of a tier", "of a debuffed target", "of at least N in one action" (and fewer than M) or "of fewer than N in
    /// one action" — never two others (not representable). Any of them can need a precision kill (<c>precision: true</c>, ADRs D36).
    /// </summary>
    private static Result<Trigger, Errors> ToKillTrigger(
        YamlMap map,
        DamageSource via,
        Optional<EnemyTier> tier,
        Optional<ImmutableArray<StatusId>> targetHas,
        Optional<TargetCount> atLeast,
        Optional<TargetCount> fewerThan,
        Optional<Seconds> within,
        HitZone needs) =>
        atLeast.Match(
            someCount => Succeed(fewerThan.Match<Trigger>(
                someLimit => new Trigger.KillBetween(via, someCount.Value, someLimit.Value, needs),
                _ => new Trigger.KillMultiple(via, someCount.Value, within, needs))),
            _ => fewerThan.Match(
                someLimit => Succeed<Trigger>(new Trigger.KillFewer(via, someLimit.Value, needs)),
                _ => tier.Match(
                    someTier => targetHas.IsSome()
                        ? map.FailAtKey<Trigger>("tier", $"{map.Label} cannot combine 'tier' and 'targetHas'")
                        : Succeed<Trigger>(new Trigger.KillOfTier(via, someTier.Value, needs)),
                    _ => targetHas.Match(
                        someStatuses => Succeed<Trigger>(new Trigger.KillDebuffed(via, someStatuses.Value, needs)),
                        _ => Succeed<Trigger>(new Trigger.KillAny(via, needs))))));

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
                map.ReadOptional("fewerThan", ReadFewerThanHits),
                map.ReadOrDefault("within", ReadDuration, Optional.None<Seconds>()),
                map.ReadOrDefault("sustained", ReadBoolean, false),
                (_, via, targetHas, atLeast, fewerThan, within, sustained) =>
                    (via, targetHas, atLeast, fewerThan, within, needs: sustained ? DamageSpan.Sustained : DamageSpan.Blow))
            .Bind(damage => Combine(
                CheckAtLeastStandsAlone(map, damage.atLeast, ("targetHas", damage.targetHas.IsSome()), ("sustained", damage.needs == DamageSpan.Sustained), ("fewerThan", damage.fewerThan.IsSome())),
                CheckFewerThanStandsAlone(map, damage.fewerThan, ("targetHas", damage.targetHas.IsSome()), ("sustained", damage.needs == DamageSpan.Sustained)),
                CheckWithinNeedsAtLeast(map, damage.within, damage.atLeast, "hit"),
                Succeed(ToDamageTrigger(damage.via, damage.targetHas, damage.atLeast, damage.fewerThan, damage.within, damage.needs)),
                (_, _, _, trigger) => trigger)));

    /// <summary>A hit is "of at least N in one action", "of fewer than N in one action", "of a debuffed target" or any.</summary>
    private static Trigger ToDamageTrigger(
        DamageSource via,
        Optional<ImmutableArray<StatusId>> targetHas,
        Optional<TargetCount> atLeast,
        Optional<TargetCount> fewerThan,
        Optional<Seconds> within,
        DamageSpan needs) =>
        atLeast.Match<Trigger>(
            someCount => new Trigger.DamageMultiple(via, someCount.Value, within),
            _ => fewerThan.Match<Trigger>(
                someLimit => new Trigger.DamageFewer(via, someLimit.Value),
                _ => targetHas.Match<Trigger>(
                    someStatuses => new Trigger.DamageDebuffed(via, someStatuses.Value, needs),
                    _ => new Trigger.Damage(via, needs))));

    /// <summary><c>fewerThan</c> on a hit: enemies hit in one action below this many, at least one; 2..20.</summary>
    private static Result<TargetCount, Errors> ReadFewerThanHits(YamlValue value) =>
        ReadTargetCount(value).Bind(count => count.Value >= 2
            ? Succeed(count)
            : value.FailAt<TargetCount>($"{value.Label}: fewer than {count.Value} enemies hit never happens (use 2..20)"));

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
            "belowMax" => scope.ReadStackingBuff(entry.Value).Map(Condition (status) => new Condition.BelowMax(status)),
            "during" => ReadVocabularyWord<AbilityKind>(entry.Value).Map(Condition (ability) => new Condition.During(ability)),
            "holds" => ReadHolds(scope, entry.Value),
            _ => entry.Value.FailAt<Condition>(DescribeUnknown("condition", entry.Key, ConditionNames)),
        });

    /// <summary><c>{ holds: { status: armor-charge, atLeast: 3 } }</c>: a counted buff holds at least this many stacks (ADRs D33).</summary>
    private static Result<Condition, Errors> ReadHolds(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["status", "atLeast"]),
            map.ReadRequired("status", scope.ReadCountedBuff),
            map.ReadRequired("atLeast", ReadStackCount),
            Condition (_, status, atLeast) => new Condition.Holds(status, atLeast)));

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
            "spendStacks" => ReadSpendStacks(scope, entry.Value),
            "reloadWeapon" => ReadReloadWeapon(scope, entry.Value),
            _ => entry.Value.FailAt<Outcome>(DescribeUnknown("outcome", entry.Key, OutcomeNames)),
        });

    /// <summary>
    /// <c>{ to: grenade, amount: "15%" }</c>, or <c>to: leastCharged</c> for the ability with the least energy (the game picks
    /// it). <c>byArchetype: { archetypes: [shotgun, glaive], amount: "20%" }</c> gives another amount on a weapon of those
    /// archetypes (Demolitionist): the weapon holding the element, its amount picked when the build is composed.
    /// </summary>
    private static Result<Outcome, Errors> ReadGrantEnergy(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
                map.CheckKeys(["to", "amount", "byArchetype"]),
                map.ReadRequired("to", ReadEnergyRecipient),
                map.ReadRequired("amount", ReadEnergyGrant),
                map.ReadOptional("byArchetype", ReadArchetypeAmount),
                (_, to, amount, byArchetype) => (to, amount, byArchetype))
            .Bind(read => ToArchetypeGrant(map, read.amount, read.byArchetype).Map(amount => read.to.Match<Outcome>(
                ability => new Outcome.GrantEnergy(ability.Kind, amount),
                _ => new Outcome.GrantLeastChargedEnergy(amount)))));

    /// <summary><c>{ archetypes: [fusion-rifle, shotgun], amount: "20%" }</c>: the amount on a weapon of one of those archetypes.</summary>
    private static Result<(ImmutableArray<string> Archetypes, GameValue Amount), Errors> ReadArchetypeAmount(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["archetypes", "amount"]),
            map.ReadRequired("archetypes", archetypes => ReadNonEmpty(archetypes, archetypes.ReadEach("archetype", ReadSlug))),
            map.ReadRequired("amount", ReadGameValue),
            (_, archetypes, amount) => (archetypes, amount)));

    /// <summary>The grant, by archetype when <c>byArchetype</c> is there: both amounts are a fraction of one charge.</summary>
    private static Result<EnergyGrant, Errors> ToArchetypeGrant(
        YamlMap map, EnergyGrant amount, Optional<(ImmutableArray<string> Archetypes, GameValue Amount)> byArchetype) =>
        (amount, byArchetype) switch
        {
            (_, Optional<(ImmutableArray<string>, GameValue)>.None) => Succeed(amount),
            (EnergyGrant.Fraction fraction, Optional<(ImmutableArray<string> Archetypes, GameValue Amount)>.Some some) =>
                Succeed<EnergyGrant>(new EnergyGrant.ByArchetype(fraction.Amount, some.Value.Archetypes, some.Value.Amount)),
            _ => map.FailAtKey<EnergyGrant>("byArchetype", "byArchetype needs an amount that is a fraction of one charge, not full"),
        };

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

    /// <summary>
    /// <c>{ consumed, to, perStack: "?" }</c>, or — for a counted buff (ADRs D33) — a table by the stacks spent
    /// (<c>bySpent: { from: 3, values: "70% | 80% | 90% | 100%" }</c>) or by charges, each copy and each stack spent counting
    /// as one (<c>byCharges: { from: 2, values: "16.2% | 21.7% | …" }</c>). Exactly one of the three.
    /// </summary>
    private static Result<Outcome, Errors> ReadConvertStacksToEnergy(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
                map.CheckKeys(["consumed", "to", "perStack", "bySpent", "byCharges"]),
                map.ReadRequired("consumed", scope.ReadBuff),
                map.ReadRequired("to", ReadVocabularyWord<AbilityKind>),
                map.ReadOptional("perStack", ReadGameValue),
                map.ReadOptional("bySpent", ReadStackTable),
                map.ReadOptional("byCharges", ReadStackTable),
                (_, consumed, to, perStack, bySpent, byCharges) => (consumed, to, perStack, bySpent, byCharges))
            .Bind(read => ToStackEnergy(map, read.perStack, read.bySpent, read.byCharges)
                .Bind(energy => CheckTableOnCounted(scope, map, energy))
                .Map(Outcome (energy) => new Outcome.ConvertStacksToEnergy(read.consumed, read.to, energy))));

    private static Result<StackEnergy, Errors> ToStackEnergy(
        YamlMap map, Optional<GameValue> perStack, Optional<StackTable> bySpent, Optional<StackTable> byCharges) =>
        (perStack, bySpent, byCharges) switch
        {
            (Optional<GameValue>.Some amount, Optional<StackTable>.None, Optional<StackTable>.None) => Succeed<StackEnergy>(new StackEnergy.PerStack(amount.Value)),
            (Optional<GameValue>.None, Optional<StackTable>.Some table, Optional<StackTable>.None) => Succeed<StackEnergy>(new StackEnergy.BySpent(table.Value)),
            (Optional<GameValue>.None, Optional<StackTable>.None, Optional<StackTable>.Some table) => Succeed<StackEnergy>(new StackEnergy.ByCharges(table.Value)),
            _ => map.FailAt<StackEnergy>($"{map.Label} needs exactly one of perStack, bySpent or byCharges"),
        };

    /// <summary>A table by stacks reads a count: only a counted buff has one (ADRs D33).</summary>
    private static Result<StackEnergy, Errors> CheckTableOnCounted(ReferenceScope scope, YamlMap map, StackEnergy energy) =>
        energy is StackEnergy.PerStack
            ? Succeed(energy)
            : map.ReadRequired("consumed", scope.ReadCountedBuff).Map(_ => energy);

    /// <summary>
    /// <c>{ from: 3, values: "70% | 80% | 90% | 100%" }</c>: a value for each count from <c>from</c> on (ADRs D33). The values
    /// are numbers as a source gives them (a percent, or seconds without the s), every one known.
    /// </summary>
    private static Result<StackTable, Errors> ReadStackTable(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
                map.CheckKeys(["from", "values"]),
                map.ReadRequired("from", ReadStackCount),
                map.ReadRequired("values", ReadGameValue),
                (_, from, values) => (from, values))
            .Bind(read => read.values switch
            {
                GameValue.PerModCount list => Succeed(new StackTable(read.from, list.Values)),
                GameValue.Known one => Succeed(new StackTable(read.from, [one.Value])),
                _ => map.FailAtKey<StackTable>("values", $"{map.Label}: the values must be known numbers, one per count (e.g. \"70% | 80% | 90%\")"),
            }));

    /// <summary><c>{ spendStacks: { status: armor-charge, count: 1 } }</c>: a fixed number of a counted buff's stacks (ADRs D33).</summary>
    private static Result<Outcome, Errors> ReadSpendStacks(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["status", "count"]),
            map.ReadRequired("status", scope.ReadCountedBuff),
            map.ReadRequired("count", ReadStackCount),
            Outcome (_, status, count) => new Outcome.SpendStacks(status, count)));

    /// <summary>
    /// <c>{ applyBuff: amplified }</c> or <c>{ applyBuff: { status, stacks = 1, duration } }</c>; <c>durationBySpent</c> in
    /// place of <c>duration</c>: seconds by the stacks the same rule spent before it (Bulwark Finisher, ADRs D33).
    /// </summary>
    private static Result<Outcome, Errors> ReadApplyBuff(ReferenceScope scope, YamlValue value) =>
        value.Node is YamlScalarNode
            ? scope.ReadBuff(value).Map(Outcome (status) => new Outcome.ApplyBuff(status, new BuffDuration.Unstated(), new StackGrant.Fixed(OneStack)))
            : value.ToMap().Bind(map => Combine(
                    map.CheckKeys(["status", "stacks", "duration", "durationBySpent"]),
                    map.ReadRequired("status", scope.ReadBuff),
                    map.ReadOrDefault("duration", ReadDuration, Optional.None<Seconds>()),
                    map.ReadOrDefault("stacks", ReadStackGrant, new StackGrant.Fixed(OneStack)),
                    map.ReadOptional("durationBySpent", ReadStackTable),
                    (_, status, duration, stacks, bySpent) => (status, duration, stacks, bySpent))
                .Bind(read => (read.duration, read.bySpent) switch
                {
                    (Optional<Seconds>.Some, Optional<StackTable>.Some) => map.FailAtKey<Outcome>("durationBySpent", $"{map.Label} takes duration or durationBySpent, not both"),
                    (Optional<Seconds>.Some time, _) => Succeed<Outcome>(new Outcome.ApplyBuff(read.status, new BuffDuration.Fixed(time.Value), read.stacks)),
                    (_, Optional<StackTable>.Some table) => Succeed<Outcome>(new Outcome.ApplyBuff(read.status, new BuffDuration.BySpent(table.Value), read.stacks)),
                    _ => Succeed<Outcome>(new Outcome.ApplyBuff(read.status, new BuffDuration.Unstated(), read.stacks)),
                }));

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

    /// <summary>
    /// <c>{ via: bolt-charge, hit: kill }</c> — a keyword's strike; or <c>{ via: "weapon:this", type: kinetic, hit: damage }</c> —
    /// this weapon's damage dealt by an effect (Veteran's Wisdom's shockwave), of <c>type</c> or else the weapon's own.
    /// </summary>
    private static Result<Outcome, Errors> ReadStrikeTarget(ReferenceScope scope, YamlValue value) =>
        value.ToMap().Bind(map => map.ReadRequired("via", YamlReading.ToText).Bind(via => via.StartsWith("weapon:", StringComparison.Ordinal)
            ? ReadWeaponStrike(scope, map)
            : Combine(
                map.CheckKeys(["via", "hit"]),
                map.ReadRequired("via", scope.ReadStatus),
                map.ReadRequired("hit", ReadVocabularyWord<HitOutcome>),
                Outcome (_, status, hit) => new Outcome.StrikeTarget(status, hit))));

    private static Result<Outcome, Errors> ReadWeaponStrike(ReferenceScope scope, YamlMap map) =>
        Combine(
                map.CheckKeys(["via", "type", "hit"]),
                map.ReadRequired("via", scope.ReadDamageSource),
                map.ReadOrDefault("type", type => ReadVocabularyWord<DamageType>(type).Map(Optional.Some), Optional.None<DamageType>()),
                map.ReadRequired("hit", ReadVocabularyWord<HitOutcome>),
                (_, weapon, type, hit) => (weapon, type, hit))
            .Bind(strike => strike.weapon is DamageSource.ThisWeapon
                ? Succeed<Outcome>(new Outcome.WeaponStrike(strike.weapon, strike.type, strike.hit))
                : map.FailAtKey<Outcome>("via", $"{map.Label}.via: only this weapon strikes (write \"weapon:this\")"));

    /// <summary><c>{ amount: "40%" }</c> (a share of your health) or <c>{ hp: 65 }</c> (HP), and <c>allies</c>: exactly one amount.</summary>
    private static Result<Outcome, Errors> ReadRestoreHealth(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
                map.CheckKeys(["amount", "hp", "allies"]),
                map.ReadOptional("amount", ReadGameValue),
                map.ReadOptional("hp", ReadGameValue),
                map.ReadOrDefault("allies", ReadBoolean, false),
                (_, amount, hp, allies) => (amount, hp, allies))
            .Bind(heal => (heal.amount.IsSome(), heal.hp.IsSome()) switch
            {
                (true, true) => map.FailAt<Outcome>($"{map.Label} takes amount or hp, not both"),
                (false, false) => map.FailAt<Outcome>($"{map.Label} needs amount (a share of your health) or hp"),
                _ => Succeed<Outcome>(new Outcome.RestoreHealth(
                    heal.amount.Match<HealAmount>(
                        share => new HealAmount.Share(share.Value),
                        _ => heal.hp.Match<HealAmount>(points => new HealAmount.Points(points.Value), _ => new HealAmount.Share(new GameValue.Unknown()))),
                    heal.allies)),
            }));

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
            map.ReadRequired("extra", ReadChargeCount),
            Passive (_, ability, extra) => new Passive.ExtraCharges(ability, extra)));

    private static Result<Passive, Errors> ReadModifyWeaponStats(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(["archetypes", "weapon", "changes"]),
            map.ReadOptional("archetypes", archetypes => ReadNonEmpty(archetypes, archetypes.ReadEach("archetype", ReadSlug))),
            map.ReadOptional("weapon", ReadThisWeapon),
            map.ReadRequired("changes", changes => ReadNonEmpty(changes, changes.ReadEach("change", ReadWeaponStatChange))),
            (_, archetypes, weapon, changes) => (archetypes, weapon, changes))
            .Bind(read => ToChangedWeapons(map, read.archetypes, read.weapon)
                .Map(Passive (weapons) => new Passive.ModifyWeaponStats(weapons, read.changes))));

    /// <summary><c>weapon: this</c>: the weapon holding the element (a weapon perk's own buff: Stats for All).</summary>
    private static Result<Unit, Errors> ReadThisWeapon(YamlValue value) =>
        value.ParseWith<Unit>(text => text == "this"
            ? new Result<Unit, string>.Ok(new Unit.Value())
            : new Result<Unit, string>.Error($"'{text}' isn't a weapon a passive can name (write 'this': the weapon holding the element)"));

    /// <summary>The weapons a change to weapon stats reaches: its archetypes, or this weapon — exactly one of them.</summary>
    private static Result<ChangedWeapons, Errors> ToChangedWeapons(YamlMap map, Optional<ImmutableArray<string>> archetypes, Optional<Unit> weapon) =>
        (archetypes.IsSome(), weapon.IsSome()) switch
        {
            (true, true) => map.FailAt<ChangedWeapons>($"{map.Label} names 'archetypes' or 'weapon', not both"),
            (false, false) => map.FailAt<ChangedWeapons>($"{map.Label} needs 'archetypes' or 'weapon: this'"),
            _ => Succeed(archetypes.Match<ChangedWeapons>(
                some => new ChangedWeapons.OfArchetypes(some.Value),
                _ => new ChangedWeapons.ThisWeapon())),
        };

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

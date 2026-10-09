using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Phrasing;

/// <summary>Kernel: plain-English descriptions of Domain values, shared by every renderer.</summary>
public static class DomainPhrasing
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // ── numbers ────────────────────────────────────────────────────────────────

    public static string FormatPercent(this GameValue value) =>
        value.Match(
            known => FormatPercentNumber(known.Value),
            perModCount => string.Join("|", perModCount.Values.Select(FormatPercentNumber)),
            approximate => "~" + FormatPercentNumber(approximate.Value),
            _ => "?%");

    public static string FormatNumber(this GameValue value) =>
        value.Match(
            known => known.Value.ToString("0.##", Invariant),
            perModCount => string.Join("|", perModCount.Values.Select(v => v.ToString("0.##", Invariant))),
            approximate => "~" + approximate.Value.ToString("0.##", Invariant),
            _ => "?");

    public static string FormatSeconds(this Seconds seconds) =>
        seconds.Value.ToString("0.#", Invariant) + "s";

    private static string FormatPercentNumber(decimal fraction) =>
        (fraction * 100m).ToString("0.#", Invariant) + "%";

    // ── names ──────────────────────────────────────────────────────────────────

    public static string DescribeAbility(this AbilityKind kind) =>
        kind switch
        {
            AbilityKind.Grenade => "grenade",
            AbilityKind.Melee => "melee",
            AbilityKind.ClassAbility => "class ability",
            _ => "super",
        };

    public static string DescribeStatus(this KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition) ? definition.Name : status.Value;

    public static string DescribePickup(this KeywordGlossary glossary, PickupId pickup) =>
        glossary.Pickups.TryGetValue(pickup, out var definition) ? definition.Name : pickup.Value;

    public static string DescribeSummon(this KeywordGlossary glossary, SummonId summon) =>
        glossary.Summons.TryGetValue(summon, out var definition) ? definition.Name : summon.Value;

    /// <summary>"Jolt" → "Jolted", "Sever" → "Severed" — the adjective a player uses for a debuffed target.</summary>
    public static string DescribeDebuffedAdjective(this KeywordGlossary glossary, StatusId status)
    {
        var name = glossary.DescribeStatus(status);
        return name switch
        {
            _ when name.Contains(' ') => name,
            _ when name.EndsWith('e') => name + "d",
            _ when name.EndsWith("ed", StringComparison.Ordinal) => name,
            _ => name + "ed",
        };
    }

    public static Affinity ReadStatusAffinity(this KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition) ? definition.Affinity : Affinity.Neutral;

    public static bool IsStacking(this KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition)
        && definition.MaxStacks.Match(max => max.Value.Value > 1, _ => false);

    // ── damage ─────────────────────────────────────────────────────────────────

    public static string DescribeDamageSource(this KeywordGlossary glossary, DamageSource source) =>
        source.Match(
            _ => "any source",
            _ => "weapon",
            weaponOfType => $"{weaponOfType.Type} weapon",
            _ => "ability",
            abilityOf => abilityOf.Kind.DescribeAbility(),
            ofType => $"{ofType.Type}",
            keywordOf => glossary.DescribeStatus(keywordOf.Status),
            summonOf => glossary.DescribeSummon(summonOf.Summon));

    public static string DescribeOrigin(this KeywordGlossary glossary, DamageOrigin origin, Build build) =>
        origin.Match(
            weapon => DescribeWeapon(build, weapon.Slot, $"{weapon.Type} weapon"),
            ability => Capitalize(ability.Kind.DescribeAbility()),
            keyword => glossary.DescribeStatus(keyword.Status),
            summoned => glossary.DescribeSummon(summoned.Summon));

    // ── triggers, conditions, outcomes, passives ───────────────────────────────

    public static string DescribeTrigger(this KeywordGlossary glossary, Trigger trigger) =>
        trigger.Match(
            cast => DescribeCast(cast.Kind),
            killAny => DescribeKill(glossary, killAny.Via, []),
            killOfTier => $"{DescribeKill(glossary, killOfTier.Via, [])} ({killOfTier.Tier})",
            killDebuffed => DescribeKill(glossary, killDebuffed.Via, killDebuffed.TargetHas),
            killMultiple => DescribeMultiKill(glossary, killMultiple.Via, killMultiple.AtLeast),
            damage => DescribeDamage(glossary, damage.Via, []),
            damageDebuffed => DescribeDamage(glossary, damageDebuffed.Via, damageDebuffed.TargetHas),
            damageMultiple => DescribeMultiHit(glossary, damageMultiple.Via, damageMultiple.AtLeast),
            pickUp => $"Pick up {glossary.DescribePickup(pickUp.Pickup)}",
            buffGained => $"Gain {glossary.DescribeStatus(buffGained.Status)}",
            stacksMaxed => $"Max {glossary.DescribeStatus(stacksMaxed.Status)}");

    private static string DescribeKill(KeywordGlossary glossary, DamageSource via, ImmutableArray<StatusId> targetHas)
    {
        var target = DescribeTargetPhrase(glossary, targetHas);
        return via switch
        {
            DamageSource.AnySource => $"Kill {target}",
            DamageSource.AbilityOf ability => $"{Capitalize(ability.Kind.DescribeAbility())} kill{DescribeOnTarget(target)}",
            DamageSource.OfType type => $"{type.Type} kill{DescribeOnTarget(target)}",
            DamageSource.KeywordOf keyword => $"{glossary.DescribeStatus(keyword.Status)} kill{DescribeOnTarget(target)}",
            _ => $"Kill {target} with {glossary.DescribeDamageSource(via)}",
        };
    }

    private static string DescribeDamage(KeywordGlossary glossary, DamageSource via, ImmutableArray<StatusId> targetHas)
    {
        var target = DescribeTargetPhrase(glossary, targetHas);
        return via switch
        {
            DamageSource.AnySource => $"Damage {target}",
            _ => $"{Capitalize(glossary.DescribeDamageSource(via))} damage{DescribeOnTarget(target)}",
        };
    }

    /// <summary>"Kill 2+ with grenade", "Kill 3+ enemies" (any source).</summary>
    private static string DescribeMultiKill(KeywordGlossary glossary, DamageSource via, TargetCount atLeast) =>
        via is DamageSource.AnySource
            ? $"Kill {atLeast.Value}+ enemies"
            : $"Kill {atLeast.Value}+ with {glossary.DescribeDamageSource(via)}";

    /// <summary>"Hit 3+ enemies with weapon", "Hit 3+ enemies" (any source).</summary>
    private static string DescribeMultiHit(KeywordGlossary glossary, DamageSource via, TargetCount atLeast) =>
        via is DamageSource.AnySource
            ? $"Hit {atLeast.Value}+ enemies"
            : $"Hit {atLeast.Value}+ enemies with {glossary.DescribeDamageSource(via)}";

    private static string DescribeTargetPhrase(KeywordGlossary glossary, ImmutableArray<StatusId> targetHas) =>
        targetHas.IsEmpty
            ? "target"
            : string.Join(" + ", targetHas.Select(glossary.DescribeDebuffedAdjective)) + " target";

    /// <summary>The ability-cast trigger as a player says it ("Grenade thrown"; the action itself is "Throw grenade").</summary>
    private static string DescribeCast(AbilityKind kind) =>
        kind switch
        {
            AbilityKind.Grenade => "Grenade thrown",
            AbilityKind.Melee => "Melee swung",
            AbilityKind.ClassAbility => "Class ability",
            _ => "Super cast",
        };

    private static string DescribeOnTarget(string target) => target == "target" ? "" : $" on {target}";

    private static string DescribeWeapon(Build build, WeaponSlot slot, string fallback) =>
        build.Weapons
            .Select(w => w.Slot == slot ? Optional.Some(w.Name) : Optional.None<string>())
            .FindFirstSome()
            .UnwrapOr(fallback);

    public static string DescribeCondition(this KeywordGlossary glossary, Condition condition) =>
        condition.Match(
            hasBuff => $"while {glossary.DescribeStatus(hasBuff.Status)}",
            lacksBuff => $"without {glossary.DescribeStatus(lacksBuff.Status)}",
            targetHas => $"if target {glossary.DescribeDebuffedAdjective(targetHas.Status)}");

    public static string DescribeConditions(this KeywordGlossary glossary, ImmutableArray<Condition> conditions) =>
        string.Join(", ", conditions.Select(glossary.DescribeCondition));

    public static string DescribeOutcome(this KeywordGlossary glossary, Outcome outcome) =>
        outcome.Match(
            grant => grant.Amount.Match(
                fraction => $"+{fraction.Amount.FormatPercent()} {grant.To.DescribeAbility()} energy",
                _ => $"refills {grant.To.DescribeAbility()}"),
            convert => $"spends {glossary.DescribeStatus(convert.Consumed)} → +{convert.PerStack.FormatPercent()} {convert.To.DescribeAbility()} energy each",
            apply => DescribeBuff(glossary, apply),
            remove => $"consumes {glossary.DescribeStatus(remove.Status)}",
            debuff => $"{glossary.DescribeStatus(debuff.Status)} target" + debuff.Duration.Match(d => $" ({d.Value.FormatSeconds()})", _ => ""),
            spawn => (spawn.Count > 1 ? $"{spawn.Count}× " : "") + glossary.DescribePickup(spawn.Pickup),
            summon => (summon.Count > 1 ? $"{summon.Count}× " : "") + glossary.DescribeSummon(summon.Summon),
            strike => $"{glossary.DescribeStatus(strike.Via)} strike" + (strike.Hit == HitOutcome.Kill ? " (kills)" : ""),
            modify => $"+{modify.Change.FormatPercent()} {glossary.DescribeDamageSource(modify.Against)} damage",
            heal => (heal.IncludesAllies ? "heals you and allies" : "heals you")
                + (heal.Amount is GameValue.Unknown ? "" : $" {heal.Amount.FormatPercent()}"),
            reset => $"refills {reset.Which.DescribeAbility()}");

    /// <summary>
    /// Several outcomes of one rule as one phrase, with values narrowed to the copies equipped and
    /// same-amount energy grants merged: "+~15% grenade, melee and class ability energy".
    /// </summary>
    public static string DescribeOutcomes(this KeywordGlossary glossary, IEnumerable<OutcomeMention> mentions)
    {
        var phrases = mentions
            .Select(mention => NarrowOutcomeToCopies(mention.Outcome, mention.Copies))
            .Select(outcome => new OutcomePhrase(
                glossary.DescribeOutcome(outcome),
                outcome is Outcome.GrantEnergy { Amount: EnergyGrant.Fraction fraction } grant
                    ? Optional.Some(new EnergyPhrase(fraction.Amount.FormatPercent(), grant.To))
                    : Optional.None<EnergyPhrase>()))
            .ToImmutableArray();
        var merged = phrases.Aggregate(ImmutableArray<OutcomePhrase>.Empty, MergeEnergyPhrase);
        return string.Join(" and ", merged.Select(DescribeMergedPhrase));
    }

    private sealed record EnergyPhrase(string Amount, AbilityKind Ability);

    private sealed record OutcomePhrase(string Text, Optional<EnergyPhrase> Energy, ImmutableArray<AbilityKind> MergedAbilities = default);

    private static ImmutableArray<OutcomePhrase> MergeEnergyPhrase(ImmutableArray<OutcomePhrase> merged, OutcomePhrase next)
    {
        var canMerge = !merged.IsEmpty
            && merged[^1].Energy.Bind(last => next.Energy.Map(n => n.Amount == last.Amount)).UnwrapOr(false);
        if (!canMerge)
        {
            return merged.Add(next with { MergedAbilities = next.Energy.Match(e => [e.Value.Ability], _ => ImmutableArray<AbilityKind>.Empty) });
        }

        var last = merged[^1];
        var abilities = last.MergedAbilities.AddRange(next.Energy.Match(e => [e.Value.Ability], _ => ImmutableArray<AbilityKind>.Empty));
        return merged.SetItem(merged.Length - 1, last with { MergedAbilities = abilities });
    }

    private static string DescribeMergedPhrase(OutcomePhrase phrase) =>
        phrase.Energy.Match(
            energy => phrase.MergedAbilities.Length > 1
                ? $"+{energy.Value.Amount} {string.Join(", ", phrase.MergedAbilities[..^1].Select(DescribeAbility))} and {phrase.MergedAbilities[^1].DescribeAbility()} energy"
                : phrase.Text,
            _ => phrase.Text);

    private static Outcome NarrowOutcomeToCopies(Outcome outcome, int copies) =>
        outcome switch
        {
            Outcome.GrantEnergy { Amount: EnergyGrant.Fraction fraction } grant =>
                grant with { Amount = new EnergyGrant.Fraction(fraction.Amount.NarrowToCopies(copies)) },
            Outcome.ConvertStacksToEnergy convert => convert with { PerStack = convert.PerStack.NarrowToCopies(copies) },
            Outcome.ModifyDamage modify => modify with { Change = modify.Change.NarrowToCopies(copies) },
            Outcome.RestoreHealth heal => heal with { Amount = heal.Amount.NarrowToCopies(copies) },
            _ => outcome,
        };

    private static string DescribeBuff(KeywordGlossary glossary, Outcome.ApplyBuff apply)
    {
        var name = glossary.DescribeStatus(apply.Status);
        var duration = apply.Duration.Match(d => $" ({d.Value.FormatSeconds()})", _ => "");
        return glossary.IsStacking(apply.Status) ? $"+{apply.Stacks.Value} {name}{duration}" : $"{name}{duration}";
    }

    public static string DescribePassive(this KeywordGlossary glossary, Passive passive) =>
        passive.Match(
            extra => $"+{extra.Extra.Value} {glossary.DescribeStatus(extra.Status)} per gain",
            charges => $"+{charges.Extra} {charges.Ability.DescribeAbility()} charge",
            modify => $"+{modify.Change.FormatPercent()} {glossary.DescribeDamageSource(modify.Against)} damage",
            resist => $"{resist.Amount.FormatPercent()} damage resistance",
            weapons => $"{string.Join("/", weapons.Archetypes)}: "
                + string.Join(", ", weapons.Changes.Select(c => c.Change is GameValue.Unknown ? $"+{c.Stat}" : $"{c.Stat} {c.Change.FormatPercent()}")));

    // ── events and actions ─────────────────────────────────────────────────────

    public static string DescribeEvent(this KeywordGlossary glossary, GameEvent gameEvent, Build build) =>
        gameEvent.Match(
            cast => DescribeCast(cast.Kind),
            damaged => $"{glossary.DescribeOrigin(damaged.Origin, build)} hit{DescribeOnTarget(DescribeTargetPhrase(glossary, damaged.TargetHas))}",
            killed => $"{glossary.DescribeOrigin(killed.Origin, build)} kill{DescribeOnTarget(DescribeTargetPhrase(glossary, killed.TargetHas))}",
            struck => $"{glossary.DescribeOrigin(struck.Origin, build)} {(struck.Hit == HitOutcome.Kill ? "killed" : "hit")} {DescribeEnemies(struck.Targets)}",
            pickedUp => $"Picked up {glossary.DescribePickup(pickedUp.Pickup)}",
            gained => glossary.IsStacking(gained.Status)
                ? $"{glossary.DescribeStatus(gained.Status)} ×{gained.Stacks.Value}"
                : $"{glossary.DescribeStatus(gained.Status)} gained",
            maxed => $"Max {glossary.DescribeStatus(maxed.Status)}");

    /// <summary>"Grenade (kill)", "Grenade (kill 3)", "Festival Flight (hit 5)", "Class ability", "Pick up Orb of Power".</summary>
    public static string DescribeAction(this KeywordGlossary glossary, PlayerAction action, Build build) =>
        action.Match(
            cast => $"{Capitalize(cast.Kind.ToAbilityKind().DescribeAbility())} ({DescribeHit(cast.Hit, cast.Targets)})",
            _ => "Class ability",
            fire => $"{DescribeWeapon(build, fire.Slot, fire.Slot.ToString())} ({DescribeHit(fire.Hit, fire.Targets)})",
            collect => $"Pick up {glossary.DescribePickup(collect.Pickup)}",
            wait => $"Wait {wait.Duration.FormatSeconds()}");

    /// <summary>"kill", "hit", and with more than one target "kill 3", "hit 5".</summary>
    private static string DescribeHit(HitOutcome hit, TargetCount targets) =>
        (hit == HitOutcome.Kill ? "kill" : "hit") + (targets.Value > 1 ? $" {targets.Value}" : "");

    /// <summary>"1 enemy", "3 enemies".</summary>
    private static string DescribeEnemies(TargetCount targets) =>
        targets.Value == 1 ? "1 enemy" : $"{targets.Value} enemies";

    /// <summary>
    /// The token the CLI accepts for an action: <c>grenade:kill</c>, <c>grenade:kill:3</c>, <c>class</c>,
    /// <c>kinetic</c>, <c>kinetic:hit:5</c>, <c>pickup:orb-of-power</c>, <c>wait:5</c>. The target count is written only
    /// when it is more than one, so one-target tokens read as before and every token round-trips.
    /// </summary>
    public static string ToActionToken(this PlayerAction action) =>
        action.Match(
            cast => cast.Kind.ToString().ToLowerInvariant() + ToHitSuffix(cast.Hit, cast.Targets),
            _ => "class",
            fire => fire.Slot.ToString().ToLowerInvariant() + ToHitSuffix(fire.Hit, fire.Targets),
            collect => $"pickup:{collect.Pickup}",
            wait => $"wait:{wait.Duration.Value.ToString(LosslessDecimal, Invariant)}");

    private static string ToHitSuffix(HitOutcome hit, TargetCount targets) =>
        (hit, targets.Value) switch
        {
            (HitOutcome.Kill, 1) => ":kill",
            (HitOutcome.Kill, var count) => $":kill:{count.ToString(Invariant)}",
            (_, 1) => "",
            (_, var count) => $":hit:{count.ToString(Invariant)}",
        };

    // ── loop analysis ──────────────────────────────────────────────────────────

    /// <summary>"Tempest Strike doesn't stack with Dielectric" — a rule that gave nothing (<see cref="WastedTally"/>).</summary>
    public static string DescribeWasted(this WastedTally wasted) =>
        $"{wasted.SourceName} doesn't stack with {wasted.PartnerName}";

    /// <summary>Every significant digit of a decimal, no trailing zeros: tokens round-trip (<c>wait:2.25</c>).</summary>
    private const string LosslessDecimal = "0.############################";

    public static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}

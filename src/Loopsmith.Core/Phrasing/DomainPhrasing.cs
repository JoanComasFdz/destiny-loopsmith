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
            cast => DescribeCast(cast.Kind, cast.Airborne),
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
    private static string DescribeCast(AbilityKind kind, bool airborne) =>
        (kind, airborne) switch
        {
            (AbilityKind.ClassAbility, true) => "Class ability in the air",
            _ => DescribeCast(kind),
        };

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
            targetHas => $"if target {glossary.DescribeDebuffedAdjective(targetHas.Status)}",
            atMax => $"while {glossary.DescribeStatus(atMax.Status)} at max");

    public static string DescribeConditions(this KeywordGlossary glossary, ImmutableArray<Condition> conditions) =>
        string.Join(", ", conditions.Select(glossary.DescribeCondition));

    public static string DescribeOutcome(this KeywordGlossary glossary, Outcome outcome) =>
        outcome.Match(
            grant => grant.Amount.Match(
                fraction => $"+{fraction.Amount.FormatPercent()} {grant.To.DescribeAbility()} energy",
                _ => $"refills {grant.To.DescribeAbility()}"),
            convert => $"spends {glossary.DescribeStatus(convert.Consumed)} → +{convert.PerStack.FormatPercent()} {convert.To.DescribeAbility()} energy per stack",
            apply => DescribeBuff(glossary, apply),
            remove => $"consumes {glossary.DescribeStatus(remove.Status)}",
            debuff => $"{glossary.DescribeStatus(debuff.Status)} target"
                + glossary.ReadShownDuration(debuff.Status, debuff.Duration).Match(d => $" ({d.Value.FormatSeconds()})", _ => ""),
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

    /// <summary>"+1 Bolt Charge" for a status that stacks (the grant, a fact), "Amplified (15s)" otherwise.</summary>
    private static string DescribeBuff(KeywordGlossary glossary, Outcome.ApplyBuff apply)
    {
        var name = glossary.DescribeStatus(apply.Status);
        var suffix = glossary.ReadShownDuration(apply.Status, apply.Duration).Match(d => $" ({d.Value.FormatSeconds()})", _ => "");
        return glossary.IsStacking(apply.Status) ? $"+{apply.Stacks.Value} {name}{suffix}" : $"{name}{suffix}";
    }

    /// <summary>The duration an outcome shows: the rule's, else the glossary's — a fact, never counted down.</summary>
    private static Optional<Seconds> ReadShownDuration(this KeywordGlossary glossary, StatusId status, Optional<Seconds> stated) =>
        stated.IsSome()
            ? stated
            : glossary.Statuses.TryGetValue(status, out var definition) ? definition.Duration : Optional.None<Seconds>();

    /// <summary>A status's facts from the glossary: "up to x10", "15s" — shown, never counted (ADRs D1).</summary>
    public static string DescribeStatusFacts(this KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition)
            ? string.Join(" · ", new[]
                {
                    definition.MaxStacks.Match(max => max.Value.Value > 1 ? $"up to x{max.Value.Value}" : "", _ => ""),
                    definition.Duration.Match(duration => duration.Value.FormatSeconds(), _ => ""),
                }.Where(fact => fact.Length > 0))
            : "";

    /// <summary>A buff on you as the state shows it: "Bolt Charge", or "Bolt Charge (at max)" once the player declared it.</summary>
    public static string DescribeActiveBuff(this KeywordGlossary glossary, ActiveBuff buff) =>
        glossary.DescribeStatus(buff.Status) + (buff.AtMax ? " (at max)" : "");

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
            cast => DescribeCast(cast.Kind, cast.Airborne),
            damaged => $"{glossary.DescribeOrigin(damaged.Origin, build)} hit{DescribeOnTarget(DescribeTargetPhrase(glossary, damaged.TargetHas))}",
            killed => $"{glossary.DescribeOrigin(killed.Origin, build)} kill{DescribeOnTarget(DescribeTargetPhrase(glossary, killed.TargetHas))}",
            struck => $"{glossary.DescribeOrigin(struck.Origin, build)} {(struck.Hit == HitOutcome.Kill ? "killed" : "hit")} {DescribeEnemies(struck.Targets)}",
            pickedUp => $"Picked up {glossary.DescribePickup(pickedUp.Pickup)}",
            gained => $"{glossary.DescribeStatus(gained.Status)} gained",
            maxed => $"Max {glossary.DescribeStatus(maxed.Status)}");

    /// <summary>
    /// "Grenade (kill)", "Grenade (kill 3)", "Festival Flight (hit 5)", "Class ability", "Pick up Orb of Power",
    /// "Bolt Charge at max", "Amplified ends".
    /// </summary>
    public static string DescribeAction(this KeywordGlossary glossary, PlayerAction action, Build build) =>
        action.Match(
            cast => $"{Capitalize(cast.Kind.ToAbilityKind().DescribeAbility())} ({DescribeHit(cast.Hit, cast.Targets)})",
            use => use.Airborne ? "Class ability (in the air)" : "Class ability",
            fire => $"{DescribeWeapon(build, fire.Slot, fire.Slot.ToString())} ({DescribeHit(fire.Hit, fire.Targets)})",
            collect => $"Pick up {glossary.DescribePickup(collect.Pickup)}",
            declare => glossary.DescribeDeclaration(declare.Declaration));

    public static string DescribeDeclaration(this KeywordGlossary glossary, StateDeclaration declaration) =>
        declaration.Match(
            max => $"{glossary.DescribeStatus(max.Status)} at max",
            end => $"{glossary.DescribeStatus(end.Status)} ends");

    /// <summary>"kill", "hit", and with more than one target "kill 3", "hit 5".</summary>
    private static string DescribeHit(HitOutcome hit, TargetCount targets) =>
        (hit == HitOutcome.Kill ? "kill" : "hit") + (targets.Value > 1 ? $" {targets.Value}" : "");

    /// <summary>"1 enemy", "3 enemies".</summary>
    private static string DescribeEnemies(TargetCount targets) =>
        targets.Value == 1 ? "1 enemy" : $"{targets.Value} enemies";

    /// <summary>
    /// The action's token, the same everywhere (CLI, loop files, the web): <c>grenade:kill</c>, <c>grenade:kill:3</c>,
    /// <c>class</c>, <c>kinetic</c>, <c>kinetic:hit:5</c>, <c>pickup:orb-of-power</c>, <c>max:bolt-charge</c>,
    /// <c>end:amplified</c>. The target count is written only when it is more than one; every token round-trips.
    /// </summary>
    public static string ToActionToken(this PlayerAction action) =>
        action.Match(
            cast => cast.Kind.ToString().ToLowerInvariant() + ToHitSuffix(cast.Hit, cast.Targets),
            use => use.Airborne ? "class:air" : "class",
            fire => fire.Slot.ToString().ToLowerInvariant() + ToHitSuffix(fire.Hit, fire.Targets),
            collect => $"pickup:{collect.Pickup}",
            declare => declare.Declaration.Match(
                max => $"max:{max.Status}",
                end => $"end:{end.Status}"));

    private static string ToHitSuffix(HitOutcome hit, TargetCount targets) =>
        (hit, targets.Value) switch
        {
            (HitOutcome.Kill, 1) => ":kill",
            (HitOutcome.Kill, var count) => $":kill:{count.ToString(Invariant)}",
            (_, 1) => "",
            (_, var count) => $":hit:{count.ToString(Invariant)}",
        };

    // ── loop analysis ──────────────────────────────────────────────────────────

    /// <summary>"Attrition Orbs", or "Attrition Orbs (chance)" when it fired only by chance there (ADRs D8).</summary>
    public static string DescribeMention(this ElementMention mention) =>
        mention.Name + (mention.Likelihood == Likelihood.Chance ? " (chance)" : "");

    /// <summary>"Tempest Strike — doesn't stack with Dielectric": a rule that gave way there (ADRs D6).</summary>
    public static string DescribeWasted(this WastedMention wasted) =>
        $"{wasted.Source.DescribeMention()} — doesn't stack with {wasted.PartnerName}";

    /// <summary>"← #2", "← previous pass #2" (step numbers from 1, as the player reads them).</summary>
    public static string DescribeProvider(this NeedProvider provider) =>
        provider.Match(
            earlier => $"← #{earlier.StepIndex + 1}",
            previous => $"← previous pass #{previous.StepIndex + 1}");

    /// <summary>"Reaper ← #1 [Reaper]", "Orb of Power ← #2 [Reaper; Strand Siphon (chance)]", "Bolt Charge at max ← #6".</summary>
    public static string DescribeNeed(this StepNeed need)
    {
        var elements = need.Provider.Match(earlier => earlier.Elements, previous => previous.Elements);
        var sources = elements.IsEmpty ? "" : $" [{string.Join("; ", elements.Select(DescribeMention))}]";
        return $"{need.What} {need.Provider.DescribeProvider()}{sources}";
    }

    /// <summary>"Reaper ← #1": a need and where it comes from, without the elements (comparisons).</summary>
    public static string DescribeNeedBriefly(this StepNeed need) =>
        $"{need.What} {need.Provider.DescribeProvider()}";

    /// <summary>The verdict on a loop's order (docs/loop-format.md, "Analysis").</summary>
    public static string DescribeVerdict(this LoopVerdict verdict) =>
        verdict.Match(
            _ => "The loop has no steps.",
            repeats => repeats.FirstPassBlocked.Match(
                blocked => $"✓ Repeats — on the first pass, #{blocked.Value.StepIndex + 1} ({blocked.Value.Label}) can't happen yet: {blocked.Value.Reason}",
                _ => "✓ Repeats — each pass ends with what the next one needs"),
            breaks => $"✗ Breaks at #{breaks.Blocked.StepIndex + 1} ({breaks.Blocked.Label}): {breaks.Blocked.Reason}");

    /// <summary>"✓ Repeats", "✗ Breaks at #3", "No steps" — the verdict in a word or two (comparison rows, badges).</summary>
    public static string DescribeShortVerdict(this LoopVerdict verdict) =>
        verdict.Match(
            _ => "No steps",
            _ => "✓ Repeats",
            breaks => $"✗ Breaks at #{breaks.Blocked.StepIndex + 1}");

    public static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}

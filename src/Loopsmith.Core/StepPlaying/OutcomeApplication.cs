using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.StepPlaying;

/// <summary>
/// The state after one outcome, how it was applied, and the events it caused. <see cref="ByChance"/>: those events are a
/// chance whatever the rule (a pickup on the ground only by chance, collected, ADRs D8).
/// </summary>
public sealed record Application(GameState State, AppliedOutcome Applied, ImmutableArray<PendingEvent> Derived, bool ByChance = false);

/// <summary>
/// Where an outcome comes from: the element (and its copies), whether it is a chance there (its rule's, the chain of
/// events that led to it, or a guard held only by chance stacks — ADRs D8, D33), and the stacks its rule spent before it.
/// </summary>
public sealed record OutcomeSource(EquippedElement Equipped, Likelihood Likelihood, Optional<StackSpend> SpentBefore);

public static class OutcomeApplication
{
    /// <summary>
    /// What an outcome changes in the state (a buff, a debuff or a pickup becomes present or goes, a counted buff's stacks
    /// go up or down) and the events it sets off. Numbers are facts shown with their certainty, never added up (ADRs D1);
    /// a counted buff's stacks are the one count (ADRs D33).
    /// </summary>
    public static Application ApplyOutcome(ValidatedBuild build, GameState state, Outcome outcome, OutcomeSource source) =>
        outcome.Match(
            grantEnergy => AnnotateFact(state, outcome, ReadGrantCertainty(grantEnergy.Amount), "amount unknown"),
            convert => ConvertStacksToEnergy(build, state, outcome, convert, source),
            applyBuff => ApplyBuff(build, state, outcome, applyBuff, source),
            removeBuff => RemoveBuff(build, state, outcome, removeBuff),
            debuffTarget => new Application(state.PutDebuff(debuffTarget.Status), ToApplied(outcome, Certainty.Known, Optional.None<string>()), []),
            spawn => Spawn(build, state, outcome, spawn, source.Likelihood),
            spawnSummon => SpawnSummon(build, state, outcome, spawnSummon),
            strikeTarget => StrikeTarget(build, state, outcome, strikeTarget),
            modifyDamage => AnnotateFact(state, outcome, modifyDamage.Change.ReadCertainty(), "value unknown"),
            restoreHealth => AnnotateFact(state, outcome, restoreHealth.Amount.Match(share => share.Value, points => points.Value).ReadCertainty(), "value unknown"),
            resetCooldown => KeepUnchanged(state, outcome, ""),
            collect => Collect(state, outcome, collect),
            least => AnnotateFact(state, outcome, ReadGrantCertainty(least.Amount), "amount unknown"),
            spend => SpendStacks(build, state, outcome, spend, source),
            weapon => StrikeWithWeapon(build, state, outcome, weapon),
            reload => ReloadWeapon(build, state, outcome, reload),
            refill => RefillWeapon(build, state, outcome, refill));

    /// <summary>
    /// <c>full</c> is one whole charge (known); a fraction is as certain as its value; a fraction by archetype not bound to
    /// the weapon's, as its two values together.
    /// </summary>
    private static Certainty ReadGrantCertainty(EnergyGrant grant) =>
        grant.Match(
            fraction => fraction.Amount.ReadCertainty(),
            _ => Certainty.Known,
            byArchetype => byArchetype.Otherwise.ReadCertainty() == Certainty.Known && byArchetype.OnArchetypes.ReadCertainty() == Certainty.Known
                ? Certainty.Known
                : Certainty.Unknown);

    /// <summary>
    /// Reloads the weapon in the slot "this weapon" was bound to, or that the rule names (ADRs D37): a reload event from it,
    /// so its reload rules fire as on a Reload step. No ammo is counted. With no weapon in that slot, or "this weapon" not
    /// bound to one (the catalog's own text), nothing reloads.
    /// </summary>
    private static Application ReloadWeapon(ValidatedBuild build, GameState state, Outcome outcome, Outcome.ReloadWeapon reload) =>
        FindReloadedWeapon(build, reload.Weapon).Match(
            weapon => new Application(
                state,
                ToApplied(outcome, Certainty.Known, Optional.None<string>()),
                [new PendingEvent.Ready(new GameEvent.Reloaded(new DamageOrigin.Weapon(weapon.Value.Slot, weapon.Value.Type, weapon.Value.Ammo)))]),
            _ => KeepUnchanged(state, outcome, "no weapon to reload"));

    /// <summary>
    /// Refills the weapon "this weapon" was bound to, or that the rule names (ADRs D42): a fact, with no reload event, so
    /// no reload rule fires. No ammo is counted. With no weapon there, nothing refills.
    /// </summary>
    private static Application RefillWeapon(ValidatedBuild build, GameState state, Outcome outcome, Outcome.RefillWeapon refill) =>
        FindReloadedWeapon(build, refill.Weapon).Match(
            _ => KeepUnchanged(state, outcome, ""),
            _ => KeepUnchanged(state, outcome, "no weapon to refill"));

    private static Optional<WeaponLoadout> FindReloadedWeapon(ValidatedBuild build, DamageSource weapon) =>
        weapon is DamageSource.WeaponInSlot inSlot
            ? build.Build.Weapons.Select(w => w.Slot == inSlot.Slot ? Optional.Some(w) : Optional.None<WeaponLoadout>()).FindFirstSome()
            : Optional.None<WeaponLoadout>();

    /// <summary>
    /// The buff is consumed when it is present (a real state change); the energy it converts into is a fact. A counted
    /// buff spends every stack, and a table gives the energy for them (a Kickstart by copies and stacks, a finisher by
    /// stacks, ADRs D33).
    /// </summary>
    private static Application ConvertStacksToEnergy(
        ValidatedBuild build, GameState state, Outcome outcome, Outcome.ConvertStacksToEnergy convert, OutcomeSource source)
    {
        if (!state.HasBuff(convert.Consumed))
        {
            return KeepUnchanged(state, outcome, "nothing to consume");
        }

        return state.ReadTally(convert.Consumed).Match(
            tally => SpendAllForEnergy(build, state, outcome, convert, tally.Value, source),
            _ => AnnotateFact(state.EndBuff(build.Catalog.Glossary, convert.Consumed), outcome, ReadStackEnergyCertainty(convert.Energy), "amount unknown"));
    }

    private static Application SpendAllForEnergy(
        ValidatedBuild build, GameState state, Outcome outcome, Outcome.ConvertStacksToEnergy convert, StackTally tally, OutcomeSource source)
    {
        var values = convert.Energy.Match(
            perStack => (AtSure: Optional.None<GameValue>(), WithChance: Optional.None<GameValue>()),
            bySpent => ReadTableValues(bySpent.Table, 0, tally.Sure, tally.Chance),
            byCharges => ReadTableValues(byCharges.Table, source.Equipped.Count.Value, tally.Sure, tally.Chance));
        var spend = new StackSpend(convert.Consumed, tally.Sure, tally.Chance, values.AtSure, values.WithChance);
        var certainty = convert.Energy is StackEnergy.PerStack perStackEnergy
            ? perStackEnergy.Amount.ReadCertainty()
            : values.AtSure.IsSome() || values.WithChance.IsSome() ? Certainty.Known : Certainty.Unknown;
        var caveat = certainty == Certainty.Unknown ? "amount unknown" : "";
        return new Application(state.EndBuff(build.Catalog.Glossary, convert.Consumed), new AppliedOutcome(outcome, certainty, JoinCaveats(caveat), Optional.Some(spend)), []);
    }

    private static Certainty ReadStackEnergyCertainty(StackEnergy energy) =>
        energy is StackEnergy.PerStack perStack ? perStack.Amount.ReadCertainty() : Certainty.Unknown;

    /// <summary>
    /// A table's value for the sure stacks spent (plus <paramref name="extra"/>: the copies, for a table by charges), and for
    /// the sure and chance ones together; none outside the table, and none for the sure stacks when there are none.
    /// </summary>
    private static (Optional<GameValue> AtSure, Optional<GameValue> WithChance) ReadTableValues(StackTable table, int extra, StackCount sure, StackCount chance) =>
        (sure.Value > 0 ? ReadTableValue(table, extra + sure.Value) : Optional.None<GameValue>(),
         chance.Value > 0 ? ReadTableValue(table, extra + sure.Value + chance.Value) : Optional.None<GameValue>());

    private static Optional<GameValue> ReadTableValue(StackTable table, int count) =>
        count >= table.From.Value && count - table.From.Value < table.Values.Length
            ? Optional.Some<GameValue>(new GameValue.Known(table.Values[count - table.From.Value]))
            : Optional.None<GameValue>();

    /// <summary>
    /// Makes the buff present. A grant of a status that stacks always raises <see cref="GameEvent.BuffGained"/> and keeps
    /// a declared maximum (the cap is a fact); a status that doesn't stack raises it only when it wasn't active. Extra
    /// stacks from passives (Spark of Frequency) are named in the caveat, never added (ADRs D1) — but to a counted buff
    /// (ADRs D33), which counts them.
    /// </summary>
    private static Application ApplyBuff(ValidatedBuild build, GameState state, Outcome outcome, Outcome.ApplyBuff apply, OutcomeSource source)
    {
        if (build.Catalog.Glossary.IsCounted(apply.Status))
        {
            return GainStacks(build, state, outcome, apply, source);
        }

        var resolved = ResolveSpentDuration(apply, source.SpentBefore);
        var stacking = build.Catalog.Glossary.IsStacking(apply.Status);
        var wasActive = state.HasBuff(apply.Status);
        var next = state.PutBuff(apply.Status);
        var gained = stacking || !wasActive
            ? ImmutableArray.Create<PendingEvent>(new PendingEvent.Ready(new GameEvent.BuffGained(apply.Status)))
            : [];
        var caveat = JoinCaveats(DescribeExtraStacks(build, state, apply.Status), gained.IsEmpty ? "already active" : "");
        return new Application(next, new AppliedOutcome(resolved.Outcome, Certainty.Known, caveat, resolved.Spend), gained);
    }

    /// <summary>
    /// A duration by the stacks the rule spent before it (Bulwark Finisher's overshield, ADRs D33): the value for the sure
    /// stacks, else for the chance ones; the outcome as authored when it has none.
    /// </summary>
    private static (Outcome Outcome, Optional<StackSpend> Spend) ResolveSpentDuration(Outcome.ApplyBuff apply, Optional<StackSpend> spentBefore) =>
        (apply.Duration, spentBefore) switch
        {
            (BuffDuration.BySpent bySpent, Optional<StackSpend>.Some spent) => ResolveSpentDuration(apply, bySpent.Table, spent.Value),
            _ => (apply, Optional.None<StackSpend>()),
        };

    private static (Outcome Outcome, Optional<StackSpend> Spend) ResolveSpentDuration(Outcome.ApplyBuff apply, StackTable table, StackSpend spent)
    {
        var values = ReadTableValues(table, 0, spent.Sure, spent.Chance);
        var seconds = values.AtSure.IsSome() ? values.AtSure : values.WithChance;
        var duration = seconds.Match<BuffDuration>(
            value => value.Value is GameValue.Known known && Seconds.TryFrom(known.Value, out var resolved)
                ? new BuffDuration.Fixed(resolved)
                : new BuffDuration.Unstated(),
            _ => new BuffDuration.Unstated());
        return (apply with { Duration = duration }, Optional.Some(spent with { AtSure = values.AtSure, WithChance = values.WithChance }));
    }

    /// <summary>
    /// A grant to a counted buff (ADRs D33): its stacks and the extra ones the passives give go to the sure stacks, or —
    /// from a chance — to the chance ones, never past the cap (the rest is wasted: "at cap"). Sure stacks reaching the cap
    /// put the buff at its maximum, which <see cref="GameEvent.StacksMaxed"/> follows, as a declaration's would.
    /// </summary>
    private static Application GainStacks(ValidatedBuild build, GameState state, Outcome outcome, Outcome.ApplyBuff apply, OutcomeSource source)
    {
        var status = apply.Status;
        var before = state.ReadTally(status).UnwrapOr(new StackTally(StackCount.From(0), StackCount.From(0), []));
        var grant = apply.Stacks.ReadStacks(source.Equipped.Count).Value + ListExtraStacks(build, state, status).Sum(extra => extra.Extra);
        var byChance = source.Likelihood == Likelihood.Chance;
        var cap = ReadKnownCap(build, status);
        var sure = LimitToCap(before.Sure.Value + (byChance ? 0 : grant), cap, 0);
        var chance = LimitToCap(before.Chance.Value + (byChance ? grant : 0), cap, sure);   // a sure grant at the cap takes the chance stacks' place
        var wasted = before.Sure.Value + before.Chance.Value + grant - sure - chance;
        var wasAtMax = state.IsAtMax(status);
        var atMax = wasAtMax || IsAtCap(sure, cap);
        var change = new StackChange(
            state.Step, ToOrigin(source.Equipped.Element),
            new StackMove.Gained(StackCount.From(Math.Max(0, sure - before.Sure.Value)), StackCount.From(Math.Max(0, chance - before.Chance.Value)), StackCount.From(Math.Max(0, wasted))),
            StackCount.From(sure), StackCount.From(chance));
        var tally = new StackTally(StackCount.From(sure), StackCount.From(chance), before.Changes.Add(change));
        var maxed = atMax && !wasAtMax
            ? ImmutableArray.Create<PendingEvent>(new PendingEvent.Ready(new GameEvent.StacksMaxed(status)))
            : [];
        var events = ImmutableArray.Create<PendingEvent>(new PendingEvent.Ready(new GameEvent.BuffGained(status))).AddRange(maxed);
        var caveat = JoinCaveats(DescribeExtraStacks(build, state, status), wasted > 0 ? $"at cap: +{wasted} wasted" : "");
        return new Application(state.PutTally(status, tally, atMax), ToApplied(outcome, Certainty.Known, caveat), events);
    }

    /// <summary>The build's cap of a counted buff (Charged Up raises Armor Charge's); none when it isn't known ("?"): no cap applies.</summary>
    private static Optional<int> ReadKnownCap(ValidatedBuild build, StatusId status) =>
        build.ReadStackCap(status).Bind(cap => cap is GameValue.Known known ? Optional.Some((int)known.Value) : Optional.None<int>());

    /// <summary>Stacks up to what the cap leaves beside <paramref name="taken"/>; all of them with no known cap.</summary>
    private static int LimitToCap(int stacks, Optional<int> cap, int taken) =>
        cap.Match(known => Math.Min(stacks, known.Value - taken), _ => stacks);

    private static bool IsAtCap(int sure, Optional<int> cap) =>
        cap.Match(known => sure >= known.Value, _ => false);

    private static StackChangeOrigin ToOrigin(BuildElement element) =>
        new StackChangeOrigin.Element(element.Id, element.Name, element.Affinity);

    /// <summary>
    /// Removes the buff and its declaration; a counted buff spends every stack, sure and chance ones (ADRs D33).
    /// </summary>
    private static Application RemoveBuff(ValidatedBuild build, GameState state, Outcome outcome, Outcome.RemoveBuff remove) =>
        state.HasBuff(remove.Status)
            ? new Application(state.EndBuff(build.Catalog.Glossary, remove.Status), new AppliedOutcome(outcome, Certainty.Known, Optional.None<string>(), ReadWholeSpend(state, remove.Status)), [])
            : KeepUnchanged(state, outcome, "was not active");

    private static Optional<StackSpend> ReadWholeSpend(GameState state, StatusId status) =>
        state.ReadTally(status).Map(tally => new StackSpend(status, tally.Sure, tally.Chance, Optional.None<GameValue>(), Optional.None<GameValue>()));

    /// <summary>
    /// A fixed number of a counted buff's stacks (ADRs D33): sure ones first, then chance ones, never below none; the buff
    /// ends when none are left.
    /// </summary>
    private static Application SpendStacks(ValidatedBuild build, GameState state, Outcome outcome, Outcome.SpendStacks spend, OutcomeSource source) =>
        state.ReadTally(spend.Status).Match(
            tally => SpendFromTally(build, state, outcome, spend, tally.Value, source),
            _ => KeepUnchanged(state, outcome, "nothing to spend"));

    private static Application SpendFromTally(
        ValidatedBuild build, GameState state, Outcome outcome, Outcome.SpendStacks spend, StackTally tally, OutcomeSource source)
    {
        var fromSure = Math.Min(spend.Count.Value, tally.Sure.Value);
        var fromChance = Math.Min(spend.Count.Value - fromSure, tally.Chance.Value);
        var sure = tally.Sure.Value - fromSure;
        var chance = tally.Chance.Value - fromChance;
        var spent = new StackSpend(spend.Status, StackCount.From(fromSure), StackCount.From(fromChance), Optional.None<GameValue>(), Optional.None<GameValue>());
        var applied = new AppliedOutcome(outcome, Certainty.Known, Optional.None<string>(), Optional.Some(spent));
        if (sure + chance == 0)
        {
            return new Application(state.EndBuff(build.Catalog.Glossary, spend.Status), applied, []);
        }

        var change = new StackChange(
            state.Step, ToOrigin(source.Equipped.Element),
            new StackMove.Spent(StackCount.From(fromSure), StackCount.From(fromChance)), StackCount.From(sure), StackCount.From(chance));
        var left = new StackTally(StackCount.From(sure), StackCount.From(chance), tally.Changes.Add(change));
        return new Application(state.PutTally(spend.Status, left, IsAtCap(sure, ReadKnownCap(build, spend.Status))), applied, []);
    }

    /// <summary>A pickup on the ground (only by chance when a chance dropped it, ADRs D8), or one that tracks to you at once.</summary>
    private static Application Spawn(ValidatedBuild build, GameState state, Outcome outcome, Outcome.Spawn spawn, Likelihood likelihood)
    {
        if (build.Catalog.Glossary.IsCollectedAutomatically(spawn.Pickup))
        {
            var pickedUp = Enumerable.Repeat<PendingEvent>(new PendingEvent.Ready(new GameEvent.PickedUp(spawn.Pickup)), spawn.Count);
            return new Application(state, ToApplied(outcome, Certainty.Known, "tracks to you"), pickedUp.ToImmutableArray());
        }

        return new Application(state.PutPickup(spawn.Pickup, likelihood), ToApplied(outcome, Certainty.Known, "on the ground"), []);
    }

    /// <summary>
    /// Picks up what is on the ground (Powerful Attraction's orbs): it goes, and a pickup event follows — a chance when only
    /// a chance dropped it; nothing there, nothing happens.
    /// </summary>
    private static Application Collect(GameState state, Outcome outcome, Outcome.Collect collect) =>
        state.HasPickup(collect.Pickup)
            ? new Application(
                state.DropPickup(collect.Pickup),
                ToApplied(outcome, Certainty.Known, Optional.None<string>()),
                [new PendingEvent.Ready(new GameEvent.PickedUp(collect.Pickup))],
                state.HasOnlyByChance(collect.Pickup))
            : KeepUnchanged(state, outcome, "nothing on the ground");

    private static Application SpawnSummon(ValidatedBuild build, GameState state, Outcome outcome, Outcome.SpawnSummon summon)
    {
        var type = build.Catalog.Glossary.ResolveSummonDamageType(summon.Summon);
        var hits = Enumerable.Repeat<PendingEvent>(new PendingEvent.HitTarget(new DamageOrigin.Summoned(summon.Summon, type), DamageSpan.Blow), summon.Count);
        return new Application(state, ToApplied(outcome, Certainty.Known, Optional.None<string>()), hits.ToImmutableArray());
    }

    private static Application StrikeTarget(ValidatedBuild build, GameState state, Outcome outcome, Outcome.StrikeTarget strike)
    {
        var type = build.Catalog.Glossary.ResolveStrikeDamageType(strike.Via);
        var origin = new DamageOrigin.Keyword(strike.Via, type);
        var hit = ImmutableArray.Create<PendingEvent>(new PendingEvent.HitTarget(origin, DamageSpan.Blow));
        var events = strike.Hit == HitOutcome.Kill ? hit.Add(new PendingEvent.KillTarget(origin)) : hit;
        return new Application(state, ToApplied(outcome, Certainty.Known, Optional.None<string>()), events);
    }

    /// <summary>
    /// This weapon's damage dealt by an effect (Veteran's Wisdom's shockwave): it hits, and kills when it says so, as the
    /// weapon in its slot with the strike's own damage type. A weapon not in the build strikes nothing.
    /// </summary>
    private static Application StrikeWithWeapon(ValidatedBuild build, GameState state, Outcome outcome, Outcome.WeaponStrike strike) =>
        build.Build.FindWeaponStrikeOrigin(strike).Match(
            origin => new Application(state, ToApplied(outcome, Certainty.Known, Optional.None<string>()), ListStrikeEvents(origin.Value, strike.Hit)),
            _ => KeepUnchanged(state, outcome, "no weapon to strike with"));

    private static ImmutableArray<PendingEvent> ListStrikeEvents(DamageOrigin origin, HitOutcome hit)
    {
        var hits = ImmutableArray.Create<PendingEvent>(new PendingEvent.HitTarget(origin, DamageSpan.Blow));
        return hit == HitOutcome.Kill ? hits.Add(new PendingEvent.KillTarget(origin)) : hits;
    }

    /// <summary>A fact shown with its certainty; <paramref name="unknownCaveat"/> marks it when the value is unknown.</summary>
    private static Application AnnotateFact(GameState state, Outcome outcome, Certainty certainty, string unknownCaveat) =>
        new(state, ToApplied(outcome, certainty, JoinCaveats(certainty == Certainty.Unknown ? unknownCaveat : "")), []);

    /// <summary>An outcome that changes nothing, with the reason when there is one ("was not active").</summary>
    private static Application KeepUnchanged(GameState state, Outcome outcome, string caveat) =>
        new(state, ToApplied(outcome, Certainty.Known, JoinCaveats(caveat)), []);

    private static AppliedOutcome ToApplied(Outcome outcome, Certainty certainty, Optional<string> caveat) =>
        new(outcome, certainty, caveat, Optional.None<StackSpend>());

    /// <summary>The passives whose extra stacks apply to a grant of this status now (Spark of Frequency, Stacks on Stacks).</summary>
    public static IEnumerable<(string Name, int Extra)> ListExtraStacks(ValidatedBuild build, GameState state, StatusId status) =>
        build.Equipped
            .SelectMany(e => e.Element.Passives.Select(p => (e.Element.Name, Passive: p)))
            .Where(x => x.Passive.When.IsSatisfiedBy(state))
            .SelectMany(x => x.Passive.Modifier is Passive.ExtraStacks extra && extra.Status == status
                ? new[] { (x.Name, extra.Extra.Value) }
                : []);

    /// <summary>"+1 from Spark of Frequency": the passives whose extra stacks apply to this grant now.</summary>
    private static string DescribeExtraStacks(ValidatedBuild build, GameState state, StatusId status) =>
        string.Join(", ", ListExtraStacks(build, state, status).Select(extra => $"+{extra.Extra} from {extra.Name}"));

    private static Optional<string> JoinCaveats(params string[] caveats)
    {
        var joined = string.Join("; ", caveats.Where(c => c.Length > 0));
        return joined.Length == 0 ? Optional.None<string>() : Optional.Some(joined);
    }
}

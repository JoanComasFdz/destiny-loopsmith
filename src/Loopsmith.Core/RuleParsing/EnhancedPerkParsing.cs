using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using YamlDotNet.RepresentationModel;
using static Loopsmith.Core.RuleParsing.ResultAccumulation;
using static Loopsmith.Core.RuleParsing.ValueReading;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary>What an enhanced trait changes: each value of the perk's rules (a duration, a game value) → the enhanced one.</summary>
internal sealed record EnhancedValues(ImmutableDictionary<Seconds, Seconds> Durations, ImmutableDictionary<GameValue, GameValue> Values);

/// <summary>One entry of <c>values</c>, as written (<c>8s</c>, <c>0.87%</c>): two durations, or two game values.</summary>
[Union]
internal partial record EnhancedPair
{
    partial record DurationPair(Seconds Base, Seconds Enhanced, string Written);
    partial record ValuePair(GameValue Base, GameValue Enhanced, string Written);
}

/// <summary>
/// A weapon perk's <c>enhanced:</c> block — the enhanced trait an enhanced or crafted copy of a weapon rolls, its own plug:
/// <c>{ hash, values: { 8s: 9s }, rules, description }</c>. Its element (<c>&lt;id&gt;-enhanced</c>, "Slice (enhanced)") is the
/// perk — its kind, affinity, source, inert and stat checks — with the Compendium row's ↑ figures in place of the base
/// ones: <c>values</c> maps a value the perk's rules or passives use (a duration such as <c>8s</c>, a game value such as
/// <c>0.87%</c>) to the enhanced one; one they don't use is an error, so a typo can't leave the base figure in, and so is
/// a reason that writes a replaced figure (it would say the base one). Without <c>values</c> the rules are the perk's
/// (the enhanced figures are in its description only). <c>rules</c> are what only the enhanced trait does, after the perk's
/// (Hatchling's second Threadling, at a chance). Other plugs of the same enhanced trait are recognised by name
/// (<see cref="EnhancedPerkRecognition.RecognizeEnhancedPerks"/>).
/// </summary>
internal static class EnhancedPerkParsing
{
    private static readonly ImmutableArray<string> EnhancedKeys = ["hash", "values", "rules", "description"];

    private static readonly EnhancedValues NoValues = new(ImmutableDictionary<Seconds, Seconds>.Empty, ImmutableDictionary<GameValue, GameValue>.Empty);

    /// <summary>The element of the perk's enhanced trait, when its YAML has an <c>enhanced:</c> block.</summary>
    internal static Result<Optional<BuildElement>, Errors> ReadEnhanced(ReferenceScope scope, YamlMap map, BuildElement perk) =>
        map.ReadOptional("enhanced", value => value.ToMap().Bind(block => ReadBlock(scope, block, perk)));

    private static Result<BuildElement, Errors> ReadBlock(ReferenceScope scope, YamlMap block, BuildElement perk) =>
        perk.Kind == ElementKind.WeaponPerk
            ? Combine(
                    block.CheckKeys(EnhancedKeys),
                    block.ReadRequired("hash", ReadItemHashes),
                    block.ReadOrDefault("values", ReadPairs, []),
                    block.ReadOrDefault("rules", rules => rules.ReadEach("rule", rule => RuleBodyParsing.ReadRule(scope, rule)), []),
                    block.ReadOptional("description", YamlReading.ToText),
                    (_, hashes, pairs, rules, description) => (Hashes: hashes, Pairs: pairs, Rules: rules, Description: description))
                .Bind(read => Combine(
                    CheckUsed(block, perk, read.Pairs),
                    CheckOnce(block, read.Pairs),
                    CheckReasons(block, perk, read.Pairs),
                    (_, _, _) => ToEnhanced(perk, read.Hashes, ToValues(read.Pairs), read.Rules, read.Description)))
            : block.FailAt<BuildElement>($"{block.Label}: only a weaponPerk has an enhanced trait, not a {GameNotationParsing.ToVocabularyWord(perk.Kind)}");

    /// <summary><c>{ 8s: 9s, 0.87%: 1.1% }</c>: each pair two durations, or two game values.</summary>
    private static Result<ImmutableArray<EnhancedPair>, Errors> ReadPairs(YamlValue value) =>
        value.ToMap().Bind(map => map.Node.Children.Select(entry => ReadPair(map, entry)).CollectAll());

    private static Result<EnhancedPair, Errors> ReadPair(YamlMap map, KeyValuePair<YamlNode, YamlNode> entry)
    {
        var label = entry.Key is YamlScalarNode { Value: { } name } ? $"{map.Label}.{name}" : map.Label;
        var key = new YamlValue(map.Path, entry.Key, label);
        var enhanced = new YamlValue(map.Path, entry.Value, label);
        return key.ToText().Bind(text => text.Trim().EndsWith('s')
            ? Combine(key.ParseWith(ParseSeconds), enhanced.ParseWith(ParseSeconds), EnhancedPair (from, to) => new EnhancedPair.DurationPair(from, to, text.Trim()))
            : Combine(key.ParseWith(ParseValue), enhanced.ParseWith(ParseValue), EnhancedPair (from, to) => new EnhancedPair.ValuePair(from, to, text.Trim())));
    }

    private static EnhancedValues ToValues(ImmutableArray<EnhancedPair> pairs) =>
        pairs.Aggregate(NoValues, (values, pair) => pair.Match(
            duration => values with { Durations = values.Durations.SetItem(duration.Base, duration.Enhanced) },
            value => values with { Values = values.Values.SetItem(value.Base, value.Enhanced) }));

    private static Result<Seconds, string> ParseSeconds(string text) =>
        GameNotationParsing.ParseDuration(text).Bind(Result<Seconds, string> (duration) => duration.Match<Result<Seconds, string>>(
            some => new Result<Seconds, string>.Ok(some.Value),
            _ => new Result<Seconds, string>.Error($"'{text}' is no duration to replace (an enhanced figure is known: write it)")));

    /// <summary>A known or approximate game value: "?" isn't one the rules could be replaced by (the figure would be unknown — leave it out).</summary>
    private static Result<GameValue, string> ParseValue(string text) =>
        GameNotationParsing.ParseGameValue(text).Bind(Result<GameValue, string> (value) => value is GameValue.Known or GameValue.Approximate
            ? new Result<GameValue, string>.Ok(value)
            : new Result<GameValue, string>.Error($"'{text}' is no figure to replace: write one value (e.g. 1.1%)"));

    /// <summary>Every value to replace is one the perk's rules or passives use (a typo would leave the base figure in).</summary>
    private static Result<Unit, Errors> CheckUsed(YamlMap block, BuildElement perk, ImmutableArray<EnhancedPair> pairs)
    {
        var durations = perk.Rules.SelectMany(rule => rule.Then).SelectMany(ListDurations).ToImmutableHashSet();
        var used = perk.Rules.SelectMany(rule => rule.Then).SelectMany(ListValues)
            .Concat(perk.Passives.SelectMany(passive => ListValues(passive.Modifier)))
            .ToImmutableHashSet();
        var unused = pairs
            .Where(pair => pair.Match(duration => !durations.Contains(duration.Base), value => !used.Contains(value.Base)))
            .Select(DescribeWritten)
            .ToImmutableArray();
        return unused.IsEmpty
            ? Succeed<Unit>(new Unit.Value())
            : block.FailAtKey<Unit>("values", $"{block.Label}.values: {string.Join(", ", unused)} isn't a value of {perk.Id}'s rules");
    }

    /// <summary>A value is replaced once: <c>8s</c> and <c>8.0s</c> are the same duration.</summary>
    private static Result<Unit, Errors> CheckOnce(YamlMap block, ImmutableArray<EnhancedPair> pairs)
    {
        var repeated = pairs
            .GroupBy(pair => pair.Match<object>(duration => duration.Base, value => value.Base))
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" and ", group.Select(DescribeWritten)))
            .ToImmutableArray();
        return repeated.IsEmpty
            ? Succeed<Unit>(new Unit.Value())
            : block.FailAtKey<Unit>("values", $"{block.Label}.values: {string.Join("; ", repeated)} replace the same value (write it once)");
    }

    /// <summary>
    /// No reason writes a figure the enhanced trait replaces ("arms Slice for 8 seconds"): the enhanced trait's would say
    /// the base one. The outcome shows the figure as a fact; the reason says what happens.
    /// </summary>
    private static Result<Unit, Errors> CheckReasons(YamlMap block, BuildElement perk, ImmutableArray<EnhancedPair> pairs)
    {
        var reasons = perk.Rules.SelectMany(rule => ToSequence(rule.Reason))
            .Concat(perk.Passives.SelectMany(passive => ToSequence(passive.Reason)))
            .ToImmutableArray();
        var written = pairs
            .Where(pair => reasons.Any(reason => ListSpellings(pair).Any(spelling => reason.Contains(spelling, StringComparison.Ordinal))))
            .Select(DescribeWritten)
            .ToImmutableArray();
        return written.IsEmpty
            ? Succeed<Unit>(new Unit.Value())
            : block.FailAtKey<Unit>("values", $"{block.Label}.values: a reason of {perk.Id} writes {string.Join(", ", written)}, which its enhanced trait replaces (say it without the figure)");
    }

    /// <summary>How a reason could write the figure: "8 seconds", "8 s", "8s"; "10%".</summary>
    private static ImmutableArray<string> ListSpellings(EnhancedPair pair) =>
        pair.Match<ImmutableArray<string>>(
            duration => [$"{duration.Base.Value} second", $"{duration.Base.Value} s ", $"{duration.Base.Value}s"],
            value => [value.Written]);

    private static string DescribeWritten(EnhancedPair pair) =>
        pair.Match(duration => duration.Written, value => value.Written);

    /// <summary>
    /// The perk as its enhanced trait: its rules and passives with the enhanced figures, then the rules only the enhanced trait
    /// has (Hatchling's second Threadling).
    /// </summary>
    private static BuildElement ToEnhanced(
        BuildElement perk,
        ImmutableArray<ItemHash> hashes,
        EnhancedValues values,
        ImmutableArray<Rule> added,
        Optional<string> description) =>
        perk with
        {
            Id = ElementId.From($"{perk.Id.Value}-enhanced"),
            Name = $"{perk.Name} (enhanced)",
            Hashes = hashes,
            Description = description.Match(own => Optional.Some(own.Value), _ => perk.Description),
            Rules = [.. perk.Rules.Select(rule => rule with { Then = [.. rule.Then.Select(outcome => ReplaceValues(outcome, values))] }), .. added],
            Passives = [.. perk.Passives.Select(passive => passive with { Modifier = ReplaceValues(passive.Modifier, values) })],
        };

    // ── the values of an outcome or a passive, and the same with the enhanced figures ──

    private static IEnumerable<Seconds> ListDurations(Outcome outcome) =>
        outcome switch
        {
            Outcome.ApplyBuff buff => ToSequence(buff.Duration.ReadFixedTime()),
            Outcome.DebuffTarget debuff => ToSequence(debuff.Duration),
            _ => [],
        };

    private static IEnumerable<GameValue> ListValues(Outcome outcome) =>
        outcome.Match(
            energy => ListValues(energy.Amount),
            convert => convert.Energy is StackEnergy.PerStack perStack ? [perStack.Amount] : [],
            _ => [],
            _ => [],
            _ => [],
            _ => [],
            _ => [],
            _ => [],
            damage => [damage.Change],
            health => [health.Amount.Match(share => share.Value, points => points.Value)],
            _ => [],
            _ => [],
            least => ListValues(least.Amount),
            _ => [],
            _ => [],
            _ => []);

    private static IEnumerable<GameValue> ListValues(EnergyGrant grant) =>
        grant.Match(
            fraction => new[] { fraction.Amount },
            _ => [],
            byArchetype => [byArchetype.Otherwise, byArchetype.OnArchetypes]);

    private static IEnumerable<GameValue> ListValues(Passive passive) =>
        passive.Match(
            _ => [],
            _ => [],
            damage => [damage.Change],
            resist => [resist.Amount.Pve, .. resist.Amount.Pvp.Map(pvp => new[] { pvp }).UnwrapOr([])],
            stats => stats.Changes.Select(change => change.Change),
            raise => [raise.Max],
            stat => [stat.Change],
            combatant => [combatant.Amount]);

    private static Outcome ReplaceValues(Outcome outcome, EnhancedValues values) =>
        outcome.Match<Outcome>(
            energy => energy with { Amount = ReplaceValues(energy.Amount, values) },
            convert => convert.Energy is StackEnergy.PerStack perStack
                ? convert with { Energy = new StackEnergy.PerStack(Replace(perStack.Amount, values)) }
                : convert,
            buff => buff.Duration is BuffDuration.Fixed fixedTime
                ? buff with { Duration = new BuffDuration.Fixed(Replace(fixedTime.Time, values)) }
                : buff,
            remove => remove,
            debuff => debuff with { Duration = Replace(debuff.Duration, values) },
            spawn => spawn,
            summon => summon,
            strike => strike,
            damage => damage with { Change = Replace(damage.Change, values) },
            health => health with
            {
                Amount = health.Amount.Match<HealAmount>(
                    share => new HealAmount.Share(Replace(share.Value, values)),
                    points => new HealAmount.Points(Replace(points.Value, values))),
            },
            reset => reset,
            collect => collect,
            least => least with { Amount = ReplaceValues(least.Amount, values) },
            spend => spend,
            weapon => weapon,
            reload => reload);

    private static EnergyGrant ReplaceValues(EnergyGrant grant, EnhancedValues values) =>
        grant.Match<EnergyGrant>(
            fraction => fraction with { Amount = Replace(fraction.Amount, values) },
            full => full,
            byArchetype => byArchetype with
            {
                Otherwise = Replace(byArchetype.Otherwise, values),
                OnArchetypes = Replace(byArchetype.OnArchetypes, values),
            });

    private static Passive ReplaceValues(Passive passive, EnhancedValues values) =>
        passive.Match<Passive>(
            stacks => stacks,
            charges => charges,
            damage => damage with { Change = Replace(damage.Change, values) },
            resist => resist with
            {
                Amount = new PveAndPvp(Replace(resist.Amount.Pve, values), resist.Amount.Pvp.Map(pvp => Replace(pvp, values))),
            },
            stats => stats with { Changes = [.. stats.Changes.Select(change => change with { Change = Replace(change.Change, values) })] },
            raise => raise with { Max = Replace(raise.Max, values) },
            stat => stat with { Change = Replace(stat.Change, values) },
            combatant => combatant with { Amount = Replace(combatant.Amount, values) });

    private static Seconds Replace(Seconds seconds, EnhancedValues values) =>
        values.Durations.TryGetValue(seconds, out var enhanced) ? enhanced : seconds;

    private static Optional<Seconds> Replace(Optional<Seconds> duration, EnhancedValues values) =>
        duration.Map(seconds => Replace(seconds, values));

    private static GameValue Replace(GameValue value, EnhancedValues values) =>
        values.Values.TryGetValue(value, out var enhanced) ? enhanced : value;

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);
}

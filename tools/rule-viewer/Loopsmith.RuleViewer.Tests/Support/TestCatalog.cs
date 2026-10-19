using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.RuleViewer.Tests.Support;

/// <summary>
/// A tiny hand-built catalog so the graph tests don't depend on the YAML slices: a copy of the part of Core's tests'
/// <c>TestCatalog</c> they use.
/// </summary>
public static class TestCatalog
{
    public static StatusId Status(string id) => StatusId.From(id);

    public static PickupId Pickup(string id) => PickupId.From(id);

    public static readonly KeywordGlossary Glossary = new(
        new[]
        {
            new StatusDefinition(Status("bolt-charge"), "Bolt Charge", KeywordKind.Buff, Affinity.Arc, Optional.Some(StackCount.From(3)), Optional.None<Seconds>(), Optional.None<ItemHash>(), Optional.None<DrawnIcon>(), false, Optional.None<string>(), Optional.None<string>(), Optional.None<StatusId>()),
            new StatusDefinition(Status("amplified"), "Amplified", KeywordKind.Buff, Affinity.Arc, Optional.None<StackCount>(), Optional.Some(Seconds.From(10m)), Optional.None<ItemHash>(), Optional.None<DrawnIcon>(), false, Optional.None<string>(), Optional.None<string>(), Optional.None<StatusId>()),
            new StatusDefinition(Status("jolt"), "Jolt", KeywordKind.Debuff, Affinity.Arc, Optional.None<StackCount>(), Optional.Some(Seconds.From(4m)), Optional.None<ItemHash>(), Optional.None<DrawnIcon>(), false, Optional.None<string>(), Optional.None<string>(), Optional.None<StatusId>()),
            new StatusDefinition(Status("armor-charge"), "Armor Charge", KeywordKind.Buff, Affinity.Neutral, Optional.Some(StackCount.From(3)), Optional.None<Seconds>(), Optional.None<ItemHash>(), Optional.None<DrawnIcon>(), true, Optional.Some("1 stack every 10 s"), Optional.None<string>(), Optional.None<StatusId>()),   // counted (ADRs D33)
        }.ToImmutableDictionary(s => s.Id),
        new[]
        {
            new PickupDefinition(Pickup("ionic-trace"), "Ionic Trace", Affinity.Arc, true, Optional.None<ItemHash>()),
            new PickupDefinition(Pickup("orb-of-power"), "Orb of Power", Affinity.Neutral, false, Optional.None<ItemHash>()),
        }.ToImmutableDictionary(p => p.Id),
        ImmutableDictionary<SummonId, SummonDefinition>.Empty,
        [],
        []);

    public static Rule On(Trigger trigger, params Outcome[] then) =>
        new(RuleId.From("test-rule-1"), trigger, [], [.. then], Optional.None<string>(), Likelihood.Always, []);

    public static Rule OnWhen(Trigger trigger, Condition condition, params Outcome[] then) =>
        new(RuleId.From("test-rule-1"), trigger, [condition], [.. then], Optional.None<string>(), Likelihood.Always, []);

    public static Outcome Buff(string status, int stacks = 1) =>
        new Outcome.ApplyBuff(Status(status), new BuffDuration.Unstated(), new StackGrant.Fixed(StackCount.From(stacks)));

    public static Outcome Debuff(string status) =>
        new Outcome.DebuffTarget(Status(status), Optional.None<Seconds>());

    public static Outcome Energy(AbilityKind to, GameValue amount) =>
        new Outcome.GrantEnergy(to, new EnergyGrant.Fraction(amount));

    public static BuildElement Element(
        string id,
        ElementKind kind,
        ImmutableArray<Rule> rules,
        ImmutableArray<PassiveRule> passives = default,
        Optional<AbilityProfile>? ability = null,
        int? fragmentSlots = null) =>
        new(
            ElementId.From(id),
            string.Join(' ', id.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w[1..])),
            kind,
            Affinity.Arc,
            Optional.None<GuardianClass>(),
            [],
            Optional.None<string>(),
            rules,
            passives.IsDefault ? [] : passives,
            ability ?? Optional.None<AbilityProfile>(),
            fragmentSlots is { } slots ? Optional.Some(FragmentSlots.From(slots)) : Optional.None<FragmentSlots>(),
            new Provenance.Authored("test", 1),
            Optional.None<string>(),
            []);
}

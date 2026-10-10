using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Tests.Support;

/// <summary>A tiny hand-built catalog so engine tests don't depend on the YAML slices.</summary>
public static class TestCatalog
{
    public static StatusId Status(string id) => StatusId.From(id);

    public static PickupId Pickup(string id) => PickupId.From(id);

    public static readonly KeywordGlossary Glossary = new(
        new[]
        {
            new StatusDefinition(Status("bolt-charge"), "Bolt Charge", KeywordKind.Buff, Affinity.Arc, Optional.Some(StackCount.From(3)), Optional.None<Seconds>()),
            new StatusDefinition(Status("amplified"), "Amplified", KeywordKind.Buff, Affinity.Arc, Optional.None<StackCount>(), Optional.Some(Seconds.From(10m))),
            new StatusDefinition(Status("jolt"), "Jolt", KeywordKind.Debuff, Affinity.Arc, Optional.None<StackCount>(), Optional.Some(Seconds.From(4m))),
        }.ToImmutableDictionary(s => s.Id),
        new[]
        {
            new PickupDefinition(Pickup("ionic-trace"), "Ionic Trace", Affinity.Arc, true),
            new PickupDefinition(Pickup("orb-of-power"), "Orb of Power", Affinity.Neutral, false),
        }.ToImmutableDictionary(p => p.Id),
        ImmutableDictionary<SummonId, SummonDefinition>.Empty,
        []);

    public static Rule On(Trigger trigger, params Outcome[] then) =>
        new(trigger, [], [.. then], Optional.None<string>(), Likelihood.Always, []);

    public static Rule OnWhen(Trigger trigger, Condition condition, params Outcome[] then) =>
        new(trigger, [condition], [.. then], Optional.None<string>(), Likelihood.Always, []);

    /// <summary>A rule that gives way to <paramref name="partner"/>'s rules on the same event.</summary>
    public static Rule NotStackingWith(this Rule rule, string partner) =>
        rule with { DoesNotStackWith = [ElementId.From(partner)] };

    public static Outcome Buff(string status, int stacks = 1) =>
        new Outcome.ApplyBuff(Status(status), Optional.None<Seconds>(), StackCount.From(stacks));

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
            fragmentSlots is { } slots ? Optional.Some(slots) : Optional.None<int>(),
            new Provenance.Authored("test", 1));

    public static AbilityProfile Profile(AbilityKind kind, int charges = 1, GameValue? chunkScalar = null) =>
        new(kind, charges, chunkScalar ?? new GameValue.Unknown(), new GameValue.Unknown());

    /// <summary>Arc hunter with the given extra elements equipped as fragments / mods.</summary>
    public static ValidatedBuild ValidateBuild(
        ImmutableArray<BuildElement> fragments,
        ImmutableArray<BuildElement> mods = default,
        ImmutableArray<ElementId> modOrder = default,
        BuildElement? grenade = null)
    {
        var abilities = new[]
        {
            grenade ?? Element("test-grenade", ElementKind.Grenade, [], ability: Optional.Some(Profile(AbilityKind.Grenade))),
            Element("test-melee", ElementKind.Melee, [], ability: Optional.Some(Profile(AbilityKind.Melee))),
            Element("test-dodge", ElementKind.ClassAbility, [], ability: Optional.Some(Profile(AbilityKind.ClassAbility))),
            Element("test-super", ElementKind.Super, [], ability: Optional.Some(Profile(AbilityKind.Super))),
            Element("test-aspect", ElementKind.Aspect, [], fragmentSlots: 4),
        };
        var allMods = mods.IsDefault ? [] : mods;
        var catalog = new RuleCatalog(
            CatalogVersion.From("test"),
            Glossary,
            abilities.Concat(fragments).Concat(allMods).ToImmutableDictionary(e => e.Id),
            new ManifestExcerpt(Optional.None<string>(), ImmutableDictionary<DamageType, string>.Empty, ImmutableDictionary<ItemHash, ManifestItem>.Empty));
        var build = new Build(
            "Test build",
            Optional.None<string>(),
            Optional.None<string>(),
            Optional.None<CatalogVersion>(),
            GuardianClass.Hunter,
            Subclass.Arc,
            new AbilityLoadout(Optional.Some(abilities[3].Id), Optional.Some(abilities[0].Id), Optional.Some(abilities[1].Id), Optional.Some(abilities[2].Id)),
            [abilities[4].Id],
            fragments.Select(f => f.Id).ToImmutableArray(),
            Optional.None<ElementId>(),
            [],
            modOrder.IsDefault ? allMods.Select(m => m.Id).ToImmutableArray() : modOrder,
            [],
            [new WeaponLoadout(WeaponSlot.Energy, "Test Rifle", DamageType.Arc, Optional.None<string>(), [], Optional.None<ItemHash>(), [])],
            new StatLine(Optional.None<StatValue>(), Optional.None<StatValue>(), Optional.None<StatValue>(),
                Optional.None<StatValue>(), Optional.None<StatValue>(), Optional.None<StatValue>()),
            []);
        return BuildValidation.ValidateBuild(build, catalog).Match(
            ok => ok.Value,
            error => throw new InvalidOperationException(error.Failure));
    }
}

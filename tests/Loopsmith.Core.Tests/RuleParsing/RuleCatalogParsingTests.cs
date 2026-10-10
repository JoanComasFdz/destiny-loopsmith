using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.RuleParsing;
using static Loopsmith.Core.Tests.RuleParsing.ParsingAssertions;

namespace Loopsmith.Core.Tests.RuleParsing;

public sealed class RuleCatalogParsingTests
{
    private const string GlossaryYaml = """
        statuses:
          - { id: bolt-charge,  name: Bolt Charge,  kind: buff,   affinity: arc,     maxStacks: 10 }
          - { id: amplified,    name: Amplified,    kind: buff,   affinity: arc,     duration: 10s }
          - { id: armor-charge, name: Armor Charge, kind: buff,   affinity: neutral, maxStacks: 3 }
          - { id: jolt,         name: Jolt,         kind: debuff, affinity: arc,     duration: 4s }
          - { id: sever,        name: Sever,        kind: debuff, affinity: strand, duration: "?s" }
        pickups:
          - { id: ionic-trace,  name: Ionic Trace,  affinity: arc,     collectsAutomatically: true }
          - { id: orb-of-power, name: Orb of Power, affinity: neutral, collectsAutomatically: false }
        summons:
          - { id: threadling, name: Threadling, damageType: strand }
        """;

    private const string HunterArcYaml = """
        elements:
          - id: spark-of-shock
            name: Spark of Shock
            kind: fragment
            affinity: arc
            class: hunter
            hash: 1727069364
            description: "Your Arc grenades jolt targets."
            source: clarity/1727069364@2.0625
            rules:
              - on: { abilityCast: grenade }
                then:
                  - { grantEnergy: { to: melee, amount: "15%" } }
                  - { grantEnergy: { to: super, amount: full } }
              - on: { damage: { via: grenade } }
                then:
                  - { debuffTarget: jolt }
                  - { debuffTarget: { status: jolt, duration: 4s } }
                reason: "Your Arc grenades jolt targets."
          - id: flow-state
            name: Flow State
            kind: aspect
            affinity: arc
            class: hunter
            fragmentSlots: 2
            rules:
              - on: { kill: { via: any, targetHas: [jolt] } }
                when: [ { has: amplified }, { lacks: bolt-charge }, { targetHas: sever } ]
                then:
                  - { applyBuff: amplified }
                  - { applyBuff: { status: bolt-charge, stacks: 2, duration: 10s } }
                  - { removeBuff: amplified }
                chance: true
              - on: { kill: { via: "weapon:strand", tier: champion } }
                then: [ { spawn: orb-of-power }, { spawn: { pickup: ionic-trace, count: 2 } } ]
              - on: { kill: { via: ability } }
                then: [ { summon: threadling }, { summon: { summon: threadling, count: 3 } } ]
              - on: { damage: { via: "type:arc", targetHas: [sever] } }
                then: [ { strikeTarget: { via: bolt-charge, hit: kill } } ]
              - on: { pickUp: ionic-trace }
                then: [ { convertStacksToEnergy: { consumed: armor-charge, to: grenade, perStack: "?" } } ]
              - on: { buffGained: bolt-charge }
                then: [ { modifyDamage: { against: "keyword:bolt-charge", change: "+50%" } } ]
              - on: { stacksMaxed: bolt-charge }
                then:
                  - { restoreHealth: { amount: "?", allies: true } }
                  - { restoreHealth: { amount: "25%" } }
                  - { resetCooldown: melee }
            passives:
              - effect: { extraStacks: { status: bolt-charge, extra: 1 } }
                when: [ { has: amplified } ]
                reason: "While Amplified, Bolt Charge sources grant one extra stack."
              - effect: { extraCharges: { ability: grenade, extra: 1 } }
              - effect: { modifyDamage: { against: "summon:threadling", change: "12% | 17% | 20%" } }
              - effect: { resistDamage: "25%" }
              - effect: { modifyWeaponStats: { archetypes: [fusion-rifle], changes: [ { stat: handling, change: "?" } ] } }
        """;

    private const string KeywordsArcYaml = """
        elements:
          - id: bolt-charge
            name: Bolt Charge
            kind: keyword
            affinity: arc
            rules:
              - on: { stacksMaxed: bolt-charge }
                then: [ { strikeTarget: { via: bolt-charge, hit: damage } }, { removeBuff: bolt-charge } ]
          - id: skip-grenade
            name: Skip Grenade
            kind: grenade
            affinity: arc
            class: hunter
            ability: { kind: grenade, charges: 1, chunkScalar: "?", baseCooldown: "?" }
            source: compendium/2026-10-09/Arc#54
          - id: shinobus-vow
            name: Shinobu's Vow
            kind: exoticArmor
            affinity: arc
            source: { creator: "https://www.youtube.com/watch?v=zvd6sNS463E", quote: "Skip grenades refund energy" }
        """;

    private static readonly SourceText Glossary = new("glossary.yaml", GlossaryYaml);
    private static readonly SourceText HunterArc = new("hunter/arc.yaml", HunterArcYaml);
    private static readonly SourceText KeywordsArc = new("keywords/arc.yaml", KeywordsArcYaml);

    private static StatusId ToStatus(string id) => StatusId.From(id);

    private static RuleCatalog ParseValidCatalog() =>
        AssertOk(RuleCatalogParsing.ParseCatalog([Glossary, HunterArc, KeywordsArc]));

    private static string ParseInvalidCatalog(params SourceText[] files) =>
        AssertError(RuleCatalogParsing.ParseCatalog([.. files]));

    private static SourceText ToElementsFile(string path, string yaml) => new(path, yaml);

    // ── The happy path ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Parses_the_glossary()
    {
        var glossary = ParseValidCatalog().Glossary;

        Assert.Equal(5, glossary.Statuses.Count);
        Assert.Equal(
            new StatusDefinition(ToStatus("bolt-charge"), "Bolt Charge", KeywordKind.Buff, Affinity.Arc, Optional.Some(StackCount.From(10)), Optional.None<Seconds>()),
            glossary.Statuses[ToStatus("bolt-charge")]);
        Assert.Equal(
            new StatusDefinition(ToStatus("jolt"), "Jolt", KeywordKind.Debuff, Affinity.Arc, Optional.None<StackCount>(), Optional.Some(Seconds.From(4m))),
            glossary.Statuses[ToStatus("jolt")]);
        Assert.Equal(Optional.None<Seconds>(), glossary.Statuses[ToStatus("sever")].Duration);
        Assert.Equal(
            new PickupDefinition(PickupId.From("ionic-trace"), "Ionic Trace", Affinity.Arc, CollectsAutomatically: true),
            glossary.Pickups[PickupId.From("ionic-trace")]);
        Assert.Equal(
            new PickupDefinition(PickupId.From("orb-of-power"), "Orb of Power", Affinity.Neutral, CollectsAutomatically: false),
            glossary.Pickups[PickupId.From("orb-of-power")]);
        Assert.Equal(
            new SummonDefinition(SummonId.From("threadling"), "Threadling", DamageType.Strand),
            glossary.Summons[SummonId.From("threadling")]);
    }

    [Fact]
    public void Parses_every_element_of_every_file()
    {
        var elements = ParseValidCatalog().Elements;

        Assert.Equal(
            ["bolt-charge", "flow-state", "shinobus-vow", "skip-grenade", "spark-of-shock"],
            elements.Keys.Select(id => id.Value).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Parses_the_element_header()
    {
        var sparkOfShock = ParseValidCatalog().Elements[ElementId.From("spark-of-shock")];

        Assert.Equal("Spark of Shock", sparkOfShock.Name);
        Assert.Equal(ElementKind.Fragment, sparkOfShock.Kind);
        Assert.Equal(Affinity.Arc, sparkOfShock.Affinity);
        Assert.Equal(Optional.Some(GuardianClass.Hunter), sparkOfShock.Class);
        Assert.Equal(Optional.Some(ItemHash.From(1727069364u)), sparkOfShock.Hash);
        Assert.Equal(Optional.Some("Your Arc grenades jolt targets."), sparkOfShock.Description);
        Assert.Equal(new Provenance.Clarity(ItemHash.From(1727069364u), "2.0625"), sparkOfShock.Source);
        Assert.Equal(Optional.None<AbilityProfile>(), sparkOfShock.Ability);
        Assert.Equal(Optional.None<int>(), sparkOfShock.FragmentSlots);
        Assert.Empty(sparkOfShock.Passives);
    }

    [Fact]
    public void Parses_ability_profiles_and_fragment_slots()
    {
        var elements = ParseValidCatalog().Elements;

        Assert.Equal(
            Optional.Some(new AbilityProfile(AbilityKind.Grenade, 1, new GameValue.Unknown(), new GameValue.Unknown())),
            elements[ElementId.From("skip-grenade")].Ability);
        Assert.Equal(Optional.Some(2), elements[ElementId.From("flow-state")].FragmentSlots);
    }

    [Fact]
    public void Parses_every_provenance_form()
    {
        var elements = ParseValidCatalog().Elements;

        Assert.Equal(
            new Provenance.Compendium(SnapshotDate.From(new DateOnly(2026, 10, 9)), "Arc", 54),
            elements[ElementId.From("skip-grenade")].Source);
        Assert.Equal(
            new Provenance.CreatorClaim("https://www.youtube.com/watch?v=zvd6sNS463E", "Skip grenades refund energy"),
            elements[ElementId.From("shinobus-vow")].Source);
        Assert.Equal(new Provenance.Authored("keywords/arc.yaml", 2), elements[ElementId.From("bolt-charge")].Source);
        Assert.Equal(new Provenance.Authored("hunter/arc.yaml", 20), elements[ElementId.From("flow-state")].Source);
    }

    [Fact]
    public void Parses_ability_cast_and_energy_grants()
    {
        var rule = ParseValidCatalog().Elements[ElementId.From("spark-of-shock")].Rules[0];

        Assert.Equal(new Trigger.AbilityCast(AbilityKind.Grenade), rule.On);
        Assert.Empty(rule.When);
        Assert.Equal<Outcome>(
            [
                new Outcome.GrantEnergy(AbilityKind.Melee, new EnergyGrant.Fraction(new GameValue.Known(0.15m))),
                new Outcome.GrantEnergy(AbilityKind.Super, new EnergyGrant.Full()),
            ],
            rule.Then);
        Assert.Equal(Optional.None<string>(), rule.Reason);
        Assert.Equal(Likelihood.Always, rule.Likelihood);
    }

    [Fact]
    public void Parses_damage_and_debuffs_in_both_forms()
    {
        var rule = ParseValidCatalog().Elements[ElementId.From("spark-of-shock")].Rules[1];

        Assert.Equal(new Trigger.Damage(new DamageSource.AbilityOf(AbilityKind.Grenade)), rule.On);
        Assert.Equal<Outcome>(
            [
                new Outcome.DebuffTarget(ToStatus("jolt"), Optional.None<Seconds>()),
                new Outcome.DebuffTarget(ToStatus("jolt"), Optional.Some(Seconds.From(4m))),
            ],
            rule.Then);
        Assert.Equal(Optional.Some("Your Arc grenades jolt targets."), rule.Reason);
    }

    [Fact]
    public void Parses_a_debuffed_kill_with_every_condition_and_buff_outcome()
    {
        var rule = ParseValidCatalog().Elements[ElementId.From("flow-state")].Rules[0];

        var kill = Assert.IsType<Trigger.KillDebuffed>(rule.On);
        Assert.Equal(new DamageSource.AnySource(), kill.Via);
        Assert.Equal([ToStatus("jolt")], kill.TargetHas);
        Assert.Equal<Condition>(
            [
                new Condition.HasBuff(ToStatus("amplified")),
                new Condition.LacksBuff(ToStatus("bolt-charge")),
                new Condition.TargetHas(ToStatus("sever")),
            ],
            rule.When);
        Assert.Equal<Outcome>(
            [
                new Outcome.ApplyBuff(ToStatus("amplified"), Optional.None<Seconds>(), StackCount.From(1)),
                new Outcome.ApplyBuff(ToStatus("bolt-charge"), Optional.Some(Seconds.From(10m)), StackCount.From(2)),
                new Outcome.RemoveBuff(ToStatus("amplified")),
            ],
            rule.Then);
        Assert.Equal(Likelihood.Chance, rule.Likelihood);
    }

    [Fact]
    public void Parses_a_tier_kill_and_spawns_in_both_forms()
    {
        var rule = ParseValidCatalog().Elements[ElementId.From("flow-state")].Rules[1];

        Assert.Equal(new Trigger.KillOfTier(new DamageSource.WeaponOfType(DamageType.Strand), EnemyTier.Champion), rule.On);
        Assert.Equal<Outcome>(
            [
                new Outcome.Spawn(PickupId.From("orb-of-power"), 1),
                new Outcome.Spawn(PickupId.From("ionic-trace"), 2),
            ],
            rule.Then);
    }

    [Fact]
    public void Parses_a_plain_kill_and_summons_in_both_forms()
    {
        var rule = ParseValidCatalog().Elements[ElementId.From("flow-state")].Rules[2];

        Assert.Equal(new Trigger.KillAny(new DamageSource.AnyAbility()), rule.On);
        Assert.Equal<Outcome>(
            [
                new Outcome.SpawnSummon(SummonId.From("threadling"), 1),
                new Outcome.SpawnSummon(SummonId.From("threadling"), 3),
            ],
            rule.Then);
    }

    [Fact]
    public void Parses_debuffed_damage_and_a_strike()
    {
        var rule = ParseValidCatalog().Elements[ElementId.From("flow-state")].Rules[3];

        var damage = Assert.IsType<Trigger.DamageDebuffed>(rule.On);
        Assert.Equal(new DamageSource.OfType(DamageType.Arc), damage.Via);
        Assert.Equal([ToStatus("sever")], damage.TargetHas);
        Assert.Equal<Outcome>([new Outcome.StrikeTarget(ToStatus("bolt-charge"), HitOutcome.Kill)], rule.Then);
    }

    [Fact]
    public void Parses_pick_up_buff_gained_and_stacks_maxed_triggers_with_their_outcomes()
    {
        var rules = ParseValidCatalog().Elements[ElementId.From("flow-state")].Rules;

        Assert.Equal(new Trigger.PickUp(PickupId.From("ionic-trace")), rules[4].On);
        Assert.Equal<Outcome>(
            [new Outcome.ConvertStacksToEnergy(ToStatus("armor-charge"), AbilityKind.Grenade, new GameValue.Unknown())],
            rules[4].Then);

        Assert.Equal(new Trigger.BuffGained(ToStatus("bolt-charge")), rules[5].On);
        Assert.Equal<Outcome>(
            [new Outcome.ModifyDamage(new DamageSource.KeywordOf(ToStatus("bolt-charge")), new GameValue.Known(0.5m))],
            rules[5].Then);

        Assert.Equal(new Trigger.StacksMaxed(ToStatus("bolt-charge")), rules[6].On);
        Assert.Equal<Outcome>(
            [
                new Outcome.RestoreHealth(new GameValue.Unknown(), IncludesAllies: true),
                new Outcome.RestoreHealth(new GameValue.Known(0.25m), IncludesAllies: false),
                new Outcome.ResetCooldown(AbilityKind.Melee),
            ],
            rules[6].Then);
    }

    [Fact]
    public void Parses_every_passive()
    {
        var passives = ParseValidCatalog().Elements[ElementId.From("flow-state")].Passives;

        Assert.Equal(5, passives.Length);

        Assert.Equal(new Passive.ExtraStacks(ToStatus("bolt-charge"), StackCount.From(1)), passives[0].Modifier);
        Assert.Equal<Condition>([new Condition.HasBuff(ToStatus("amplified"))], passives[0].When);
        Assert.Equal(Optional.Some("While Amplified, Bolt Charge sources grant one extra stack."), passives[0].Reason);

        Assert.Equal(new Passive.ExtraCharges(AbilityKind.Grenade, 1), passives[1].Modifier);
        Assert.Empty(passives[1].When);
        Assert.Equal(Optional.None<string>(), passives[1].Reason);

        var modifyDamage = Assert.IsType<Passive.ModifyDamage>(passives[2].Modifier);
        Assert.Equal(new DamageSource.SummonOf(SummonId.From("threadling")), modifyDamage.Against);
        Assert.Equal([0.12m, 0.17m, 0.20m], Assert.IsType<GameValue.PerModCount>(modifyDamage.Change).Values);

        Assert.Equal(new Passive.ResistDamage(new GameValue.Known(0.25m)), passives[3].Modifier);

        var weaponStats = Assert.IsType<Passive.ModifyWeaponStats>(passives[4].Modifier);
        Assert.Equal(["fusion-rifle"], weaponStats.Archetypes);
        Assert.Equal([new WeaponStatChange("handling", new GameValue.Unknown())], weaponStats.Changes);
    }

    [Fact]
    public void Parses_a_keyword_element()
    {
        var boltCharge = ParseValidCatalog().Elements[ElementId.From("bolt-charge")];

        Assert.Equal(ElementKind.Keyword, boltCharge.Kind);
        Assert.Equal<Outcome>(
            [new Outcome.StrikeTarget(ToStatus("bolt-charge"), HitOutcome.Damage), new Outcome.RemoveBuff(ToStatus("bolt-charge"))],
            boltCharge.Rules[0].Then);
    }

    [Fact]
    public void An_empty_elements_list_and_an_empty_glossary_are_valid()
    {
        var catalog = AssertOk(RuleCatalogParsing.ParseCatalog(
            [new SourceText("glossary.yaml", "statuses: []\n"), ToElementsFile("keywords/none.yaml", "elements: []\n")]));

        Assert.Empty(catalog.Glossary.Statuses);
        Assert.Empty(catalog.Elements);
    }

    // ── Catalog version ──────────────────────────────────────────────────────────────────

    [Fact]
    public void The_catalog_version_hashes_the_sorted_path_text_pairs()
    {
        var expectedBytes = new[] { Glossary, HunterArc, KeywordsArc }
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .SelectMany(file => Encoding.UTF8.GetBytes(file.Path).Append((byte)0).Concat(Encoding.UTF8.GetBytes(file.Text)).Append((byte)0))
            .ToArray();
        var expected = "authored-" + Convert.ToHexStringLower(SHA256.HashData(expectedBytes))[..12];

        var catalog = ParseValidCatalog();

        Assert.Equal(expected, catalog.Version.Value);
        Assert.Matches("^authored-[0-9a-f]{12}$", catalog.Version.Value);
    }

    [Fact]
    public void The_catalog_version_does_not_depend_on_file_order()
    {
        var forward = ParseValidCatalog();
        var backward = AssertOk(RuleCatalogParsing.ParseCatalog([KeywordsArc, HunterArc, Glossary]));

        Assert.Equal(forward.Version, backward.Version);
    }

    [Fact]
    public void The_catalog_version_changes_with_the_text()
    {
        var edited = HunterArc with { Text = HunterArcYaml.Replace("\"15%\"", "\"20%\"", StringComparison.Ordinal) };

        var original = ParseValidCatalog();
        var changed = AssertOk(RuleCatalogParsing.ParseCatalog([Glossary, edited, KeywordsArc]));

        Assert.NotEqual(original.Version, changed.Version);
    }

    // ── Errors: every message carries file:line ──────────────────────────────────────────

    [Fact]
    public void A_missing_glossary_is_an_error()
    {
        var message = ParseInvalidCatalog(KeywordsArc);

        Assert.Contains("glossary.yaml: missing", message);
    }

    [Fact]
    public void A_glossary_in_a_subfolder_is_not_the_glossary()
    {
        var message = ParseInvalidCatalog(new SourceText("rules/glossary.yaml", GlossaryYaml));

        Assert.Contains("glossary.yaml: missing", message);
        Assert.Contains("rules/glossary.yaml:1: unknown key 'statuses' in rules file", message);
    }

    [Fact]
    public void An_unknown_key_is_an_error_at_its_line()
    {
        var message = ParseInvalidCatalog(Glossary, ToElementsFile("hunter/arc.yaml", """
            elements:
              - id: spark-of-shock
                name: Spark of Shock
                kind: fragment
                colour: blue
                affinity: arc
            """));

        Assert.Equal(
            "hunter/arc.yaml:5: unknown key 'colour' in element (allowed: id, name, kind, affinity, class, hash, "
            + "fragmentSlots, ability, description, source, rules, passives)",
            message);
    }

    [Fact]
    public void An_unknown_key_in_a_rule_body_is_an_error()
    {
        var message = ParseInvalidCatalog(Glossary, ToElementsFile("hunter/arc.yaml", """
            elements:
              - id: spark-of-shock
                name: Spark of Shock
                kind: fragment
                affinity: arc
                rules:
                  - on: { abilityCast: grenade }
                    then: [ { applyBuff: { status: amplified, stack: 2 } } ]
            """));

        Assert.Contains("hunter/arc.yaml:8: unknown key 'stack' in applyBuff", message);
    }

    [Fact]
    public void An_unknown_status_is_an_error()
    {
        var message = ParseInvalidCatalog(Glossary, ToElementsFile("hunter/arc.yaml", """
            elements:
              - id: spark-of-shock
                name: Spark of Shock
                kind: fragment
                affinity: arc
                rules:
                  - on: { abilityCast: grenade }
                    then:
                      - { applyBuff: overcharged }
            """));

        Assert.Equal("hunter/arc.yaml:9: applyBuff: unknown status 'overcharged' (not in glossary.yaml)", message);
    }

    [Fact]
    public void A_buff_used_as_a_debuff_is_an_error()
    {
        var message = ParseInvalidCatalog(Glossary, ToElementsFile("hunter/arc.yaml", """
            elements:
              - id: spark-of-shock
                name: Spark of Shock
                kind: fragment
                affinity: arc
                rules:
                  - on: { abilityCast: grenade }
                    then:
                      - { debuffTarget: { status: amplified, duration: 4s } }
            """));

        Assert.Equal(
            "hunter/arc.yaml:9: debuffTarget.status: 'amplified' is a buff, but this position needs a debuff",
            message);
    }

    [Fact]
    public void A_debuff_used_as_a_buff_is_an_error_in_triggers_and_guards()
    {
        var message = ParseInvalidCatalog(Glossary, ToElementsFile("hunter/arc.yaml", """
            elements:
              - id: spark-of-shock
                name: Spark of Shock
                kind: fragment
                affinity: arc
                rules:
                  - on: { kill: { targetHas: [amplified] } }
                    when: [ { has: jolt } ]
                    then: [ { removeBuff: sever } ]
            """));

        Assert.Equal(
            string.Join(
                "\n",
                "hunter/arc.yaml:7: kill.targetHas: 'amplified' is a buff, but this position needs a debuff",
                "hunter/arc.yaml:8: has: 'jolt' is a debuff, but this position needs a buff",
                "hunter/arc.yaml:9: removeBuff: 'sever' is a debuff, but this position needs a buff"),
            message);
    }

    [Fact]
    public void Unknown_pickups_summons_and_keyword_sources_are_errors()
    {
        var message = ParseInvalidCatalog(Glossary, ToElementsFile("hunter/arc.yaml", """
            elements:
              - id: spark-of-shock
                name: Spark of Shock
                kind: fragment
                affinity: arc
                rules:
                  - on: { pickUp: firesprite }
                    then: [ { summon: arc-soul } ]
                  - on: { damage: { via: "keyword:scorch" } }
                    then: [ { spawn: { pickup: tangle } } ]
            """));

        Assert.Contains("hunter/arc.yaml:7: pickUp: unknown pickup 'firesprite'", message);
        Assert.Contains("hunter/arc.yaml:8: summon: unknown summon 'arc-soul'", message);
        Assert.Contains("hunter/arc.yaml:9: damage.via: unknown status 'scorch'", message);
        Assert.Contains("hunter/arc.yaml:10: spawn.pickup: unknown pickup 'tangle'", message);
    }

    // ── Airborne class ability use and restarting buffs ──────────────────────────────────

    private static SourceText ToAirMoveFile(string trigger, string buff = "{ applyBuff: amplified }") =>
        ToElementsFile("hunter/arc.yaml", """
            elements:
              - id: ascension
                name: Ascension
                kind: aspect
                affinity: arc
                rules:
                  - on: TRIGGER
                    then: [ BUFF ]
            """.Replace("TRIGGER", trigger).Replace("BUFF", buff));

    [Fact]
    public void Parses_an_airborne_class_ability_trigger()
    {
        var catalog = AssertOk(RuleCatalogParsing.ParseCatalog(
            [Glossary, ToAirMoveFile("{ abilityCast: { ability: classAbility, airborne: true } }", "{ applyBuff: { status: bolt-charge } }")]));

        var rule = catalog.Elements[ElementId.From("ascension")].Rules[0];
        Assert.Equal(new Trigger.AbilityCast(AbilityKind.ClassAbility, Airborne: true), rule.On);
        Assert.Equal(new Outcome.ApplyBuff(ToStatus("bolt-charge"), Optional.None<Seconds>(), StackCount.From(1)), rule.Then[0]);
    }

    [Fact]
    public void Parses_an_atMax_condition_on_a_status_that_stacks()
    {
        var file = ToElementsFile("keywords/arc.yaml", """
            elements:
              - id: bolt-charge
                name: Bolt Charge
                kind: keyword
                affinity: arc
                rules:
                  - on: { damage: { via: ability } }
                    when: [ { atMax: bolt-charge } ]
                    then: [ { removeBuff: bolt-charge } ]
            """);

        var catalog = AssertOk(RuleCatalogParsing.ParseCatalog([Glossary, file]));

        Assert.Equal([new Condition.AtMax(ToStatus("bolt-charge"))], catalog.Elements[ElementId.From("bolt-charge")].Rules[0].When);
    }

    [Theory]
    [InlineData("{ on: { stacksMaxed: amplified }, then: [ { removeBuff: amplified } ] }", "'amplified' doesn't stack (no maxStacks), so it is never at max")]
    [InlineData("{ on: { abilityCast: grenade }, when: [ { atMax: amplified } ], then: [ { removeBuff: amplified } ] }", "'amplified' doesn't stack (no maxStacks), so it is never at max")]
    [InlineData("{ on: { abilityCast: grenade }, then: [ { applyBuff: { status: amplified, restart: true } } ] }", "unknown key 'restart' in applyBuff (allowed: status, stacks, duration)")]
    public void Only_a_status_that_stacks_can_be_at_max_and_a_grant_has_no_restart(string rule, string expected)
    {
        var file = ToElementsFile("hunter/arc.yaml", $$"""
            elements:
              - id: probe
                name: Probe
                kind: fragment
                affinity: arc
                rules: [ {{rule}} ]
            """);

        Assert.Contains(expected, ParseInvalidCatalog(Glossary, file));
    }

    [Fact]
    public void The_map_form_of_a_cast_trigger_without_airborne_is_the_plain_trigger()
    {
        var catalog = AssertOk(RuleCatalogParsing.ParseCatalog([Glossary, ToAirMoveFile("{ abilityCast: { ability: grenade } }")]));

        Assert.Equal(new Trigger.AbilityCast(AbilityKind.Grenade), catalog.Elements[ElementId.From("ascension")].Rules[0].On);
    }

    [Fact]
    public void Only_a_class_ability_can_be_airborne()
    {
        var message = ParseInvalidCatalog(Glossary, ToAirMoveFile("{ abilityCast: { ability: grenade, airborne: true } }"));

        Assert.Contains("only a classAbility can be airborne", message);
        Assert.StartsWith("hunter/arc.yaml:", message);
    }

    // ── Rules that don't stack ───────────────────────────────────────────────────────────

    /// <summary>Tempest Strike (line 2) gives way to <paramref name="partner"/>; Dielectric (line 11) adds <paramref name="dielectricSays"/>.</summary>
    private static SourceText ToStackingFile(string partner, string dielectricSays = "") =>
        ToElementsFile("hunter/arc.yaml", """
            elements:
              - id: tempest-strike
                name: Tempest Strike
                kind: aspect
                affinity: arc
                rules:
                  - on: { abilityCast: grenade }
                    then: [ { applyBuff: bolt-charge } ]
                    doesNotStackWith: PARTNER
                    reason: "Jolted kills give Bolt Charge."
              - id: dielectric
                name: Dielectric
                kind: artifactPerk
                affinity: arc
                rules:
                  - on: { abilityCast: grenade }
                    then: [ { applyBuff: bolt-charge } ]
                    DIELECTRIC
            """.Replace("PARTNER", partner).Replace("DIELECTRIC", dielectricSays));

    [Fact]
    public void Parses_the_elements_a_rule_does_not_stack_with()
    {
        var catalog = AssertOk(RuleCatalogParsing.ParseCatalog([Glossary, ToStackingFile("[dielectric]")]));

        Assert.Equal([ElementId.From("dielectric")], catalog.Elements[ElementId.From("tempest-strike")].Rules[0].DoesNotStackWith);
        Assert.Empty(catalog.Elements[ElementId.From("dielectric")].Rules[0].DoesNotStackWith);
    }

    [Fact]
    public void Does_not_stack_with_an_unknown_element_is_an_error()
    {
        var message = ParseInvalidCatalog(Glossary, ToStackingFile("[dielectrik]"));

        Assert.Equal("hunter/arc.yaml:2: 'tempest-strike': doesNotStackWith 'dielectrik' is not an element of the rules", message);
    }

    [Fact]
    public void Does_not_stack_with_its_own_element_is_an_error()
    {
        var message = ParseInvalidCatalog(Glossary, ToStackingFile("[tempest-strike]"));

        Assert.Equal("hunter/arc.yaml:2: 'tempest-strike': doesNotStackWith names its own element", message);
    }

    [Fact]
    public void Two_elements_that_each_give_way_to_the_other_are_an_error_at_both()
    {
        var message = ParseInvalidCatalog(Glossary, ToStackingFile("[dielectric]", "doesNotStackWith: [tempest-strike]"));

        Assert.Equal(
            "hunter/arc.yaml:2: 'tempest-strike': doesNotStackWith 'dielectric' leads back to 'tempest-strike', so none of those rules would apply; say it only on the side that gives nothing\n"
            + "hunter/arc.yaml:11: 'dielectric': doesNotStackWith 'tempest-strike' leads back to 'dielectric', so none of those rules would apply; say it only on the side that gives nothing",
            message);
    }

    [Fact]
    public void A_longer_circle_of_elements_giving_way_is_an_error_at_each_of_them()
    {
        var bomber = ToElementsFile("mods/armor.yaml", """
            elements:
              - id: bomber
                name: Bomber
                kind: armorMod
                affinity: neutral
                rules:
                  - on: { abilityCast: grenade }
                    then: [ { applyBuff: bolt-charge } ]
                    doesNotStackWith: [tempest-strike]
            """);

        var message = ParseInvalidCatalog(Glossary, ToStackingFile("[dielectric]", "doesNotStackWith: [bomber]"), bomber);

        Assert.Equal(
            [
                "hunter/arc.yaml:2: 'tempest-strike': doesNotStackWith 'dielectric' leads back to 'tempest-strike', so none of those rules would apply; say it only on the side that gives nothing",
                "hunter/arc.yaml:11: 'dielectric': doesNotStackWith 'bomber' leads back to 'dielectric', so none of those rules would apply; say it only on the side that gives nothing",
                "mods/armor.yaml:2: 'bomber': doesNotStackWith 'tempest-strike' leads back to 'bomber', so none of those rules would apply; say it only on the side that gives nothing",
            ],
            message.Split('\n'));
    }

    [Fact]
    public void A_chain_of_elements_giving_way_without_a_circle_parses()
    {
        var bomber = ToElementsFile("mods/armor.yaml", """
            elements:
              - id: bomber
                name: Bomber
                kind: armorMod
                affinity: neutral
                rules:
                  - on: { abilityCast: grenade }
                    then: [ { applyBuff: bolt-charge } ]
            """);

        var catalog = AssertOk(RuleCatalogParsing.ParseCatalog([Glossary, ToStackingFile("[dielectric]", "doesNotStackWith: [bomber]"), bomber]));

        Assert.Equal([ElementId.From("bomber")], catalog.Elements[ElementId.From("dielectric")].Rules[0].DoesNotStackWith);
    }

    [Fact]
    public void Does_not_stack_with_nothing_is_an_error()
    {
        var message = ParseInvalidCatalog(Glossary, ToStackingFile("[]"));

        Assert.Contains("must list at least one element", message);
        Assert.StartsWith("hunter/arc.yaml:9: ", message);
    }

    // ── Multi-target triggers: "hit / kill at least N enemies in one action" (ADRs D4) ──────

    private const string OneForAllYaml = """
        elements:
          - id: one-for-all
            name: One For All
            kind: weaponPerk
            affinity: kinetic
            rules:
              - on: { damage: { via: weapon, atLeast: 3 } }
                then: [ { applyBuff: amplified } ]
                reason: "Hitting three separate targets within a short time grants increased damage."
              - on: { kill: { via: grenade, atLeast: 2 } }
                then: [ { spawn: orb-of-power } ]
              - on: { kill: { atLeast: 1 } }
                then: [ { applyBuff: amplified } ]
        """;

    [Fact]
    public void Parses_hit_and_kill_at_least_N_triggers()
    {
        var catalog = AssertOk(RuleCatalogParsing.ParseCatalog([Glossary, ToElementsFile("weapons/perks.yaml", OneForAllYaml)]));

        var rules = catalog.Elements[ElementId.From("one-for-all")].Rules;
        Assert.Equal(new Trigger.DamageMultiple(new DamageSource.AnyWeapon(), TargetCount.From(3)), rules[0].On);
        Assert.Equal(new Trigger.KillMultiple(new DamageSource.AbilityOf(AbilityKind.Grenade), TargetCount.From(2)), rules[1].On);
        Assert.Equal(new Trigger.KillMultiple(new DamageSource.AnySource(), TargetCount.One), rules[2].On);
    }

    [Theory]
    [InlineData("kill: { via: weapon, atLeast: 2, tier: champion }", "hunter/arc.yaml:7: kill cannot combine 'atLeast' and 'tier'")]
    [InlineData("kill: { via: weapon, atLeast: 2, targetHas: [jolt] }", "hunter/arc.yaml:7: kill cannot combine 'atLeast' and 'targetHas'")]
    [InlineData("damage: { via: weapon, atLeast: 2, targetHas: [jolt] }", "hunter/arc.yaml:7: damage cannot combine 'atLeast' and 'targetHas'")]
    [InlineData("damage: { via: weapon, atLeast: 0 }", "hunter/arc.yaml:7: damage.atLeast: '0' is not a whole number ≥ 1")]
    [InlineData("damage: { via: weapon, atLeast: 21 }", "hunter/arc.yaml:7: damage.atLeast: Target count must be within 1..20")]
    [InlineData("kill: { atLeast: lots }", "hunter/arc.yaml:7: kill.atLeast: 'lots' is not a whole number ≥ 1")]
    public void At_least_is_a_count_of_1_to_20_and_stands_alone(string trigger, string expected)
    {
        var message = ParseInvalidCatalog(Glossary, ToElementsFile("hunter/arc.yaml", $$"""
            elements:
              - id: one-for-all
                name: One For All
                kind: weaponPerk
                affinity: kinetic
                rules:
                  - on: { {{trigger}} }
                    then: [ { spawn: orb-of-power } ]
            """));

        Assert.Equal(expected, message);
    }

    [Fact]
    public void Tier_and_target_has_together_is_an_error()
    {
        var message = ParseInvalidCatalog(Glossary, ToElementsFile("hunter/arc.yaml", """
            elements:
              - id: spark-of-shock
                name: Spark of Shock
                kind: fragment
                affinity: arc
                rules:
                  - on:
                      kill:
                        via: weapon
                        tier: champion
                        targetHas: [jolt]
                    then: [ { spawn: orb-of-power } ]
            """));

        Assert.Equal("hunter/arc.yaml:10: kill cannot combine 'tier' and 'targetHas'", message);
    }

    [Fact]
    public void A_duplicate_element_id_across_files_is_an_error()
    {
        var message = ParseInvalidCatalog(
            Glossary,
            ToElementsFile("hunter/arc.yaml", """
                elements:
                  - { id: spark-of-shock, name: Spark of Shock, kind: fragment, affinity: arc }
                """),
            ToElementsFile("keywords/arc.yaml", """
                elements:
                  - { id: jolt-chain, name: Jolt Chain, kind: keyword, affinity: arc }
                  - { id: spark-of-shock, name: Spark of Shock (again), kind: fragment, affinity: arc }
                """));

        Assert.Equal(
            "keywords/arc.yaml:3: duplicate element id 'spark-of-shock' (first defined at hunter/arc.yaml:2)",
            message);
    }

    [Fact]
    public void A_duplicate_glossary_id_is_an_error()
    {
        var message = ParseInvalidCatalog(new SourceText("glossary.yaml", """
            statuses:
              - { id: jolt, name: Jolt, kind: debuff, affinity: arc }
              - { id: jolt, name: Jolted, kind: debuff, affinity: arc }
            """));

        Assert.Equal("glossary.yaml:3: duplicate status id 'jolt' (first defined at glossary.yaml:2)", message);
    }

    [Fact]
    public void Glossary_entries_are_validated()
    {
        var message = ParseInvalidCatalog(new SourceText("glossary.yaml", """
            statuses:
              - { id: jolt, name: Jolt, kind: curse, affinity: arc, maxStacks: 0 }
            pickups:
              - { id: orb-of-power, name: Orb of Power, affinity: neutral }
            summons:
              - { id: Threadling, name: Threadling, damageType: prismatic }
            """));

        Assert.Contains("glossary.yaml:2: status.kind: 'curse' is not one of: buff, debuff", message);
        Assert.Contains("glossary.yaml:2: status.maxStacks: '0' is not a whole number ≥ 2", message);
        Assert.Contains("glossary.yaml:4: pickup is missing 'collectsAutomatically'", message);
        Assert.Contains("glossary.yaml:6: summon.id: 'Threadling' is not a kebab-case summon id", message);
        Assert.Contains("glossary.yaml:6: summon.damageType: 'prismatic' is not one of: kinetic, arc, solar, void, stasis, strand", message);
    }

    [Fact]
    public void A_broken_glossary_is_reported_without_reference_noise_but_with_element_errors()
    {
        var brokenGlossary = new SourceText("glossary.yaml", """
            statuses:
              - { id: jolt, name: Jolt, kind: debuff, affinity: arcane }
            """);
        var elements = ToElementsFile("hunter/arc.yaml", """
            elements:
              - id: spark-of-shock
                name: Spark of Shock
                kind: fragment
                affinity: arc
                rules:
                  - on: { abilityCast: grenade }
                    then: [ { debuffTarget: jolt }, { teleport: far } ]
            """);

        var message = ParseInvalidCatalog(brokenGlossary, elements);

        Assert.Contains("glossary.yaml:2: status.affinity: 'arcane' is not one of:", message);
        Assert.Contains("hunter/arc.yaml:8: unknown outcome 'teleport'", message);
        Assert.DoesNotContain("unknown status", message);
    }

    [Fact]
    public void Errors_of_every_file_are_reported_together_in_file_and_line_order()
    {
        var message = ParseInvalidCatalog(
            Glossary,
            ToElementsFile("keywords/arc.yaml", """
                elements:
                  - { id: jolt-chain, name: Jolt Chain, kind: keyword, affinity: arcc }
                """),
            ToElementsFile("hunter/arc.yaml", """
                elements:
                  - id: spark-of-shock
                    kind: fragment
                    affinity: arc
                    hash: abc
                  - id: Flow State
                    name: Flow State
                    kind: aspect
                    affinity: arc
                """));

        Assert.Equal(
            string.Join(
                "\n",
                "hunter/arc.yaml:2: element is missing 'name'",
                "hunter/arc.yaml:5: element.hash: 'abc' is not a manifest hash (an unsigned 32-bit number)",
                "hunter/arc.yaml:6: element.id: 'Flow State' is not a kebab-case id",
                "keywords/arc.yaml:2: element.affinity: 'arcc' is not one of: neutral, kinetic, arc, solar, void, stasis, strand, prismatic"),
            message);
    }

    [Fact]
    public void Invalid_yaml_is_an_error_at_its_line()
    {
        var message = ParseInvalidCatalog(Glossary, ToElementsFile("hunter/arc.yaml", """
            elements:
              - id: spark-of-shock
                name: [Spark of Shock
            """));

        Assert.StartsWith("hunter/arc.yaml:", message);
        Assert.Contains("invalid YAML", message);
    }

    [Fact]
    public void A_duplicate_key_is_an_error_at_its_line()
    {
        var message = ParseInvalidCatalog(Glossary, ToElementsFile("hunter/arc.yaml", """
            elements:
              - id: spark-of-shock
                name: Spark of Shock
                name: Spark of Shock
                kind: fragment
                affinity: arc
            """));

        Assert.Equal("hunter/arc.yaml:4: invalid YAML: Duplicate key name", message);
    }

    [Fact]
    public void An_empty_rules_file_is_an_error()
    {
        var message = ParseInvalidCatalog(Glossary, ToElementsFile("mods/armor.yaml", "# nothing yet\n"));

        Assert.Equal("mods/armor.yaml:1: the file is empty", message);
    }

    [Fact]
    public void Trigger_and_outcome_shapes_are_validated()
    {
        var message = ParseInvalidCatalog(Glossary, ToElementsFile("hunter/arc.yaml", """
            elements:
              - id: spark-of-shock
                name: Spark of Shock
                kind: fragment
                affinity: arc
                rules:
                  - on: { abilityCast: grenade, pickUp: ionic-trace }
                    then: []
                  - on: { jump: high }
                    then: [ { grantEnergy: { to: grenade, amount: lots } } ]
                  - on: { damage: { via: laser, tier: boss } }
                    then: [ { spawn: { pickup: orb-of-power, count: 0 } } ]
                    chance: sometimes
            """));

        Assert.Contains("hunter/arc.yaml:7: rule.on must have exactly one key, found 2: abilityCast, pickUp", message);
        Assert.Contains("hunter/arc.yaml:8: rule.then must list at least one outcome", message);
        Assert.Contains("hunter/arc.yaml:9: unknown trigger 'jump'", message);
        Assert.Contains("hunter/arc.yaml:10: grantEnergy.amount: 'lots' is not a game value", message);
        Assert.Contains("hunter/arc.yaml:11: damage.via: 'laser' is not a damage source", message);
        Assert.Contains("hunter/arc.yaml:11: unknown key 'tier' in damage", message);
        Assert.Contains("hunter/arc.yaml:12: spawn.count: '0' is not a whole number ≥ 1", message);
        Assert.Contains("hunter/arc.yaml:13: rule.chance: 'sometimes' is not true or false", message);
    }

    [Fact]
    public void Kind_specific_fields_are_validated()
    {
        var message = ParseInvalidCatalog(Glossary, ToElementsFile("hunter/arc.yaml", """
            elements:
              - id: spark-of-shock
                name: Spark of Shock
                kind: fragment
                affinity: arc
                fragmentSlots: 2
              - id: skip-grenade
                name: Skip Grenade
                kind: grenade
                affinity: arc
                ability: { kind: melee }
              - id: flow-state
                name: Flow State
                kind: aspect
                affinity: arc
                ability: { kind: grenade }
            """));

        Assert.Contains("hunter/arc.yaml:6: fragmentSlots is only allowed on aspects, not on a fragment", message);
        Assert.Contains("hunter/arc.yaml:11: ability.kind 'melee' does not match the element kind 'grenade'", message);
        Assert.Contains("hunter/arc.yaml:16: ability is only allowed on super, grenade, melee and classAbility elements", message);
    }

    [Fact]
    public void A_malformed_source_is_an_error()
    {
        var message = ParseInvalidCatalog(Glossary, ToElementsFile("hunter/arc.yaml", """
            elements:
              - id: spark-of-shock
                name: Spark of Shock
                kind: fragment
                affinity: arc
                source: wiki/spark-of-shock
              - id: flow-state
                name: Flow State
                kind: aspect
                affinity: arc
                source: { creator: "not a url", quote: "trust me" }
            """));

        Assert.Contains("hunter/arc.yaml:6: element.source: 'wiki/spark-of-shock' is not a source", message);
        Assert.Contains("hunter/arc.yaml:11: element.source: creator 'not a url' is not an absolute http(s) URL", message);
    }
}

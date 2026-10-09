using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.RuleParsing;
using static Loopsmith.Core.Tests.RuleParsing.ParsingAssertions;

namespace Loopsmith.Core.Tests.RuleParsing;

public sealed class GameNotationParsingTests
{
    public static TheoryData<string, GameValue> SingleValues => new()
    {
        { "15%", new GameValue.Known(0.15m) },
        { "+50%", new GameValue.Known(0.5m) },
        { "-20%", new GameValue.Known(-0.2m) },
        { "2", new GameValue.Known(2m) },
        { "0.5", new GameValue.Known(0.5m) },
        { " 15% ", new GameValue.Known(0.15m) },
        { "~25%", new GameValue.Approximate(0.25m) },
        { "25%?", new GameValue.Approximate(0.25m) },
        { "~1.5", new GameValue.Approximate(1.5m) },
        { "?", new GameValue.Unknown() },
        { "?%", new GameValue.Unknown() },
    };

    [Theory]
    [MemberData(nameof(SingleValues))]
    public void Parses_a_single_game_value(string text, GameValue expected)
    {
        var value = AssertOk(GameNotationParsing.ParseGameValue(text));

        Assert.Equal(expected, value);
    }

    [Fact]
    public void Unknown_is_never_zero()
    {
        var value = AssertOk(GameNotationParsing.ParseGameValue("?"));

        Assert.IsType<GameValue.Unknown>(value);
        Assert.NotEqual<GameValue>(new GameValue.Known(0m), value);
    }

    [Fact]
    public void Parses_a_value_per_mod_count()
    {
        var value = AssertOk(GameNotationParsing.ParseGameValue("12% | 17% | 20%"));

        var perModCount = Assert.IsType<GameValue.PerModCount>(value);
        Assert.Equal([0.12m, 0.17m, 0.20m], perModCount.Values);
    }

    [Fact]
    public void Parses_pve_and_pvp_parts()
    {
        var value = AssertOk(GameNotationParsing.ParsePveAndPvp("300% [20%]"));

        Assert.Equal(new GameValue.Known(3.0m), value.Pve);
        Assert.Equal(Optional.Some<GameValue>(new GameValue.Known(0.2m)), value.Pvp);
    }

    [Fact]
    public void A_game_value_with_a_pvp_part_is_its_pve_part()
    {
        var value = AssertOk(GameNotationParsing.ParseGameValue("300% [20%]"));

        Assert.Equal(new GameValue.Known(3.0m), value);
    }

    [Fact]
    public void A_value_without_brackets_has_no_pvp_part()
    {
        var value = AssertOk(GameNotationParsing.ParsePveAndPvp("~25%"));

        Assert.Equal(new GameValue.Approximate(0.25m), value.Pve);
        Assert.Equal(Optional.None<GameValue>(), value.Pvp);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("%")]
    [InlineData("15 %")]
    [InlineData("15%%")]
    [InlineData("1e3")]
    [InlineData("1,5")]
    [InlineData("--5")]
    [InlineData("~")]
    [InlineData("~?")]
    [InlineData("12% | ?")]
    [InlineData("12% |")]
    [InlineData("[20%]")]
    [InlineData("300% [20%")]
    [InlineData("300% [20%] [5%]")]
    [InlineData("300% [20%] x")]
    public void Rejects_a_malformed_game_value(string text)
    {
        var message = AssertError(GameNotationParsing.ParseGameValue(text));

        Assert.Contains($"'{text}' is not a game value", message);
    }

    [Theory]
    [InlineData("5s", 5)]
    [InlineData("0.5s", 0.5)]
    [InlineData("10s", 10)]
    public void Parses_a_duration(string text, double seconds)
    {
        var duration = AssertOk(GameNotationParsing.ParseDuration(text));

        Assert.Equal(Optional.Some(Seconds.From((decimal)seconds)), duration);
    }

    [Theory]
    [InlineData("?s")]
    [InlineData("?")]
    public void An_unknown_duration_is_no_duration(string text)
    {
        var duration = AssertOk(GameNotationParsing.ParseDuration(text));

        Assert.Equal(Optional.None<Seconds>(), duration);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("s")]
    [InlineData("-1s")]
    [InlineData("5 s")]
    [InlineData("~5s")]
    [InlineData("5m")]
    public void Rejects_a_malformed_duration(string text)
    {
        var message = AssertError(GameNotationParsing.ParseDuration(text));

        Assert.Contains($"'{text}' is not a duration", message);
    }

    public static TheoryData<string, DamageSource> DamageSources => new()
    {
        { "any", new DamageSource.AnySource() },
        { "weapon", new DamageSource.AnyWeapon() },
        { "weapon:strand", new DamageSource.WeaponOfType(DamageType.Strand) },
        { "ability", new DamageSource.AnyAbility() },
        { "grenade", new DamageSource.AbilityOf(AbilityKind.Grenade) },
        { "melee", new DamageSource.AbilityOf(AbilityKind.Melee) },
        { "classAbility", new DamageSource.AbilityOf(AbilityKind.ClassAbility) },
        { "super", new DamageSource.AbilityOf(AbilityKind.Super) },
        { "type:arc", new DamageSource.OfType(DamageType.Arc) },
        { "keyword:bolt-charge", new DamageSource.KeywordOf(StatusId.From("bolt-charge")) },
        { "summon:threadling", new DamageSource.SummonOf(SummonId.From("threadling")) },
    };

    [Theory]
    [MemberData(nameof(DamageSources))]
    public void Parses_a_damage_source(string text, DamageSource expected)
    {
        var source = AssertOk(GameNotationParsing.ParseDamageSource(text));

        Assert.Equal(expected, source);
    }

    [Theory]
    [InlineData("")]
    [InlineData("laser")]
    [InlineData("Grenade")]
    [InlineData("weapon:prismatic")]
    [InlineData("type:")]
    [InlineData("keyword:Bolt Charge")]
    [InlineData("summon:")]
    [InlineData("element:arc")]
    public void Rejects_a_malformed_damage_source(string text)
    {
        var message = AssertError(GameNotationParsing.ParseDamageSource(text));

        Assert.Contains($"'{text}' is not a damage source", message);
    }

    [Fact]
    public void Parses_a_compendium_provenance()
    {
        var provenance = AssertOk(GameNotationParsing.ParseProvenance("compendium/2026-10-09/Arc#54"));

        Assert.Equal(new Provenance.Compendium(SnapshotDate.From(new DateOnly(2026, 10, 9)), "Arc", 54), provenance);
    }

    [Fact]
    public void A_compendium_tab_may_contain_spaces()
    {
        var provenance = AssertOk(GameNotationParsing.ParseProvenance("compendium/2026-10-09/Exotic Armor#3"));

        Assert.Equal(new Provenance.Compendium(SnapshotDate.From(new DateOnly(2026, 10, 9)), "Exotic Armor", 3), provenance);
    }

    [Fact]
    public void Parses_a_clarity_provenance()
    {
        var provenance = AssertOk(GameNotationParsing.ParseProvenance("clarity/1727069364@2.0625"));

        Assert.Equal(new Provenance.Clarity(ItemHash.From(1727069364u), "2.0625"), provenance);
    }

    [Fact]
    public void Parses_a_creator_claim()
    {
        var provenance = AssertOk(GameNotationParsing.ParseCreatorClaim(
            "https://www.youtube.com/watch?v=zvd6sNS463E",
            "Skip grenades refund energy"));

        Assert.Equal(
            new Provenance.CreatorClaim("https://www.youtube.com/watch?v=zvd6sNS463E", "Skip grenades refund energy"),
            provenance);
    }

    [Theory]
    [InlineData("compendium/2026-13-09/Arc#54")]
    [InlineData("compendium/09-10-2026/Arc#54")]
    [InlineData("compendium/2026-10-09/Arc")]
    [InlineData("compendium/2026-10-09/#54")]
    [InlineData("compendium/2026-10-09/Arc#0")]
    [InlineData("compendium/2026-10-09/Arc#x")]
    [InlineData("clarity/abc@2.0")]
    [InlineData("clarity/1727069364")]
    [InlineData("clarity/1727069364@")]
    [InlineData("clarity/-1@2.0")]
    [InlineData("manual/notes")]
    [InlineData("")]
    public void Rejects_a_malformed_provenance(string text)
    {
        var message = AssertError(GameNotationParsing.ParseProvenance(text));

        Assert.Contains($"'{text}' is not a source", message);
    }

    [Theory]
    [InlineData("not a url", "a quote", "is not an absolute http(s) URL")]
    [InlineData("ftp://example.com/video", "a quote", "is not an absolute http(s) URL")]
    [InlineData("https://example.com/video", " ", "quote must not be empty")]
    public void Rejects_a_malformed_creator_claim(string url, string quote, string expected)
    {
        var message = AssertError(GameNotationParsing.ParseCreatorClaim(url, quote));

        Assert.Contains(expected, message);
    }
}

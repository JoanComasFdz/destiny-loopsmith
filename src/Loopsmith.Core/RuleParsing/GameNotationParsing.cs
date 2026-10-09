using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Vogen;

namespace Loopsmith.Core.RuleParsing;

/// <summary>
/// The text notations of <c>docs/rule-format.md</c>: game values, durations, damage sources and
/// provenance. Pure and total — every input yields a value or a message, never an exception.
/// </summary>
public static class GameNotationParsing
{
    private const string GameValueExamples = "e.g. 15%, +50%, ~25%, 25%?, ?, 12% | 17% | 20%, 300% [20%] or 2";

    /// <summary>A game value; for <c>"300% [20%]"</c> this is the PvE part (see <see cref="ParsePveAndPvp"/>).</summary>
    public static Result<GameValue, string> ParseGameValue(string text) =>
        ParsePveAndPvp(text).Map(value => value.Pve);

    /// <summary><c>"300% [20%]"</c> → PvE <c>Known(3.0)</c>, PvP <c>Known(0.2)</c>; without <c>[…]</c> the PvP part is absent.</summary>
    public static Result<PveAndPvp, string> ParsePveAndPvp(string text)
    {
        var trimmed = text.Trim();
        var open = trimmed.IndexOf('[');
        var parsed = open < 0
            ? ToGameValue(trimmed).Map(pve => new PveAndPvp(pve, Optional.None<GameValue>()))
            : ToPveAndPvp(trimmed, open);
        return parsed.ToResult(() => $"'{text}' is not a game value ({GameValueExamples})");
    }

    /// <summary><c>5s</c>, <c>0.5s</c> → a duration; <c>"?s"</c> (or <c>"?"</c>) → no duration.</summary>
    public static Result<Optional<Seconds>, string> ParseDuration(string text)
    {
        var trimmed = text.Trim();
        return trimmed switch
        {
            "?s" or "?" => new Result<Optional<Seconds>, string>.Ok(Optional.None<Seconds>()),
            _ when trimmed.EndsWith('s')
                && decimal.TryParse(trimmed[..^1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds)
                => Seconds.TryFrom(seconds).ToResult().Map(Optional.Some),
            _ => new Result<Optional<Seconds>, string>.Error($"'{text}' is not a duration (e.g. 5s, 0.5s or \"?s\")"),
        };
    }

    /// <summary><c>any</c>, <c>weapon</c>, <c>weapon:strand</c>, <c>ability</c>, <c>grenade</c>, <c>type:arc</c>, <c>keyword:jolt</c>, <c>summon:threadling</c>.</summary>
    public static Result<DamageSource, string> ParseDamageSource(string text)
    {
        var trimmed = text.Trim();
        var colon = trimmed.IndexOf(':');
        var parsed = colon < 0
            ? ToBareDamageSource(trimmed)
            : ToQualifiedDamageSource(trimmed[..colon], trimmed[(colon + 1)..]);
        return parsed.MapError(reason =>
            $"'{text}' is not a damage source ({reason}; expected any, weapon, weapon:<type>, ability, "
            + "grenade, melee, classAbility, super, type:<type>, keyword:<status> or summon:<summon>)");
    }

    /// <summary><c>compendium/2026-10-09/Arc#54</c> or <c>clarity/1727069364@2.0625</c>.</summary>
    public static Result<Provenance, string> ParseProvenance(string text)
    {
        const string compendium = "compendium/";
        const string clarity = "clarity/";
        var trimmed = text.Trim();
        var parsed = trimmed switch
        {
            _ when trimmed.StartsWith(compendium, StringComparison.Ordinal) => ToCompendium(trimmed[compendium.Length..]),
            _ when trimmed.StartsWith(clarity, StringComparison.Ordinal) => ToClarity(trimmed[clarity.Length..]),
            _ => Optional.None<Provenance>(),
        };
        return parsed.ToResult(() =>
            $"'{text}' is not a source (expected compendium/<yyyy-MM-dd>/<tab>#<row>, "
            + "clarity/<hash>@<version> or { creator: <url>, quote: \"…\" })");
    }

    /// <summary>The <c>{ creator: &lt;url&gt;, quote: "…" }</c> form: an absolute http(s) URL and a non-empty quote.</summary>
    public static Result<Provenance, string> ParseCreatorClaim(string url, string quote) =>
        (IsWebAddress(url), string.IsNullOrWhiteSpace(quote)) switch
        {
            (false, _) => new Result<Provenance, string>.Error($"creator '{url}' is not an absolute http(s) URL"),
            (_, true) => new Result<Provenance, string>.Error("quote must not be empty"),
            _ => new Result<Provenance, string>.Ok(new Provenance.CreatorClaim(url, quote)),
        };

    /// <summary>A closed vocabulary word: the camelCase name of an enum member (<c>classAbility</c>).</summary>
    internal static Result<TEnum, string> ParseVocabularyWord<TEnum>(string text)
        where TEnum : struct, Enum
    {
        var matches = Enum.GetValues<TEnum>().Where(candidate => ToVocabularyWord(candidate) == text).ToImmutableArray();
        return matches.Length == 1
            ? new Result<TEnum, string>.Ok(matches[0])
            : new Result<TEnum, string>.Error(
                $"'{text}' is not one of: {string.Join(", ", Enum.GetValues<TEnum>().Select(ToVocabularyWord))}");
    }

    internal static string ToVocabularyWord<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();
        return string.Concat(name[..1].ToLowerInvariant(), name[1..]);
    }

    /// <summary>A Vogen validation outcome as a <see cref="Result{T, TFailure}"/> (no Vogen exception ever escapes).</summary>
    internal static Result<T, string> ToResult<T>(this ValueObjectOrError<T> outcome) =>
        outcome.IsSuccess
            ? new Result<T, string>.Ok(outcome.ValueObject)
            : new Result<T, string>.Error(outcome.Error.ErrorMessage);

    internal static bool IsWebAddress(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static Optional<PveAndPvp> ToPveAndPvp(string trimmed, int open) =>
        trimmed.EndsWith(']') && trimmed.IndexOf(']') == trimmed.Length - 1 && trimmed.LastIndexOf('[') == open
            ? ToGameValue(trimmed[..open].TrimEnd()).Bind(pve =>
                ToGameValue(trimmed[(open + 1)..^1].Trim()).Map(pvp => new PveAndPvp(pve, Optional.Some(pvp))))
            : Optional.None<PveAndPvp>();

    private static Optional<GameValue> ToGameValue(string side) =>
        side switch
        {
            "?" or "?%" => Optional.Some<GameValue>(new GameValue.Unknown()),
            _ when side.Contains('|') => ToPerModCount(side),
            _ when side.StartsWith('~') || side.EndsWith('?') =>
                ToNumber(StripApproximation(side)).Map(GameValue (value) => new GameValue.Approximate(value)),
            _ => ToNumber(side).Map(GameValue (value) => new GameValue.Known(value)),
        };

    private static Optional<GameValue> ToPerModCount(string side) =>
        side.Split('|')
            .Select(part => ToNumber(part.Trim()).ToResult(() => new Unit.Value()))
            .CombineAll()
            .Match(
                ok => Optional.Some<GameValue>(new GameValue.PerModCount(ok.Value)),
                _ => Optional.None<GameValue>());

    private static string StripApproximation(string side)
    {
        var withoutTilde = side.StartsWith('~') ? side[1..] : side;
        return withoutTilde.EndsWith('?') ? withoutTilde[..^1] : withoutTilde;
    }

    /// <summary><c>15%</c> → 0.15, <c>+50%</c> → 0.5, <c>2</c> → 2 (invariant culture, no exponent or grouping).</summary>
    private static Optional<decimal> ToNumber(string text)
    {
        var isPercent = text.EndsWith('%');
        var digits = isPercent ? text[..^1] : text;
        return decimal.TryParse(
            digits,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var value)
            ? Optional.Some(isPercent ? value / 100m : value)
            : Optional.None<decimal>();
    }

    private static Result<DamageSource, string> ToBareDamageSource(string word) =>
        word switch
        {
            "any" => new Result<DamageSource, string>.Ok(new DamageSource.AnySource()),
            "weapon" => new Result<DamageSource, string>.Ok(new DamageSource.AnyWeapon()),
            "ability" => new Result<DamageSource, string>.Ok(new DamageSource.AnyAbility()),
            _ => ParseVocabularyWord<AbilityKind>(word)
                .Map(DamageSource (kind) => new DamageSource.AbilityOf(kind))
                .MapError(_ => $"unknown source '{word}'"),
        };

    private static Result<DamageSource, string> ToQualifiedDamageSource(string prefix, string argument) =>
        prefix switch
        {
            "weapon" => ParseVocabularyWord<DamageType>(argument).Map(DamageSource (type) => new DamageSource.WeaponOfType(type)),
            "type" => ParseVocabularyWord<DamageType>(argument).Map(DamageSource (type) => new DamageSource.OfType(type)),
            "keyword" => StatusId.TryFrom(argument).ToResult().Map(DamageSource (status) => new DamageSource.KeywordOf(status)),
            "summon" => SummonId.TryFrom(argument).ToResult().Map(DamageSource (summon) => new DamageSource.SummonOf(summon)),
            _ => new Result<DamageSource, string>.Error($"unknown qualifier '{prefix}:'"),
        };

    /// <summary><c>&lt;yyyy-MM-dd&gt;/&lt;tab&gt;#&lt;row&gt;</c>; the tab is everything between the first '/' and the last '#'.</summary>
    private static Optional<Provenance> ToCompendium(string rest)
    {
        var slash = rest.IndexOf('/');
        var hash = rest.LastIndexOf('#');
        return slash > 0
            && hash > slash + 1
            && DateOnly.TryParseExact(rest[..slash], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && int.TryParse(rest[(hash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var row)
            && row >= 1
            && !string.IsNullOrWhiteSpace(rest[(slash + 1)..hash])
            && SnapshotDate.TryFrom(date, out var snapshot)
                ? Optional.Some<Provenance>(new Provenance.Compendium(snapshot, rest[(slash + 1)..hash], row))
                : Optional.None<Provenance>();
    }

    /// <summary><c>&lt;hash&gt;@&lt;version&gt;</c>.</summary>
    private static Optional<Provenance> ToClarity(string rest)
    {
        var at = rest.IndexOf('@');
        return at > 0
            && uint.TryParse(rest[..at], NumberStyles.None, CultureInfo.InvariantCulture, out var hash)
            && !string.IsNullOrWhiteSpace(rest[(at + 1)..])
            && ItemHash.TryFrom(hash, out var itemHash)
                ? Optional.Some<Provenance>(new Provenance.Clarity(itemHash, rest[(at + 1)..]))
                : Optional.None<Provenance>();
    }
}

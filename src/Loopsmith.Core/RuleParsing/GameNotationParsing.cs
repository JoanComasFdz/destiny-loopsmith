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

    /// <summary>
    /// <c>any</c>, <c>weapon</c>, <c>weapon:strand</c>, <c>weapon:harmonic</c> (a weapon of your Super's element), <c>weapon:this</c>
    /// (the weapon holding the element: a weapon mod's, a perk's), <c>weapon:this:arc</c> (it, when it is Arc), <c>ability</c>,
    /// <c>grenade</c>, <c>melee:slide</c> (only that use of the ability: a secondary, Tempest Strike's uppercut),
    /// <c>type:arc</c>, <c>keyword:jolt</c>, <c>summon:threadling</c>.
    /// </summary>
    public static Result<DamageSource, string> ParseDamageSource(string text)
    {
        var trimmed = text.Trim();
        var colon = trimmed.IndexOf(':');
        var parsed = colon < 0
            ? ToBareDamageSource(trimmed)
            : ToQualifiedDamageSource(trimmed[..colon], trimmed[(colon + 1)..]);
        return parsed.MapError(reason =>
            $"'{text}' is not a damage source ({reason}; expected any, weapon, weapon:<type>, weapon:harmonic, weapon:this, weapon:this:<type>, ability, "
            + "grenade, melee, classAbility, super, <ability>:<input> (melee:slide, melee:air, classAbility:air, grenade:hold, super:air, super:combo, super:guard), "
            + "type:<type>, keyword:<status> or summon:<summon>)");
    }

    /// <summary>
    /// The combatants' damage a resistance reduces: <c>combatants</c> (all of it), a damage type (<c>arc</c>),
    /// <c>harmonic</c> (the Super's element), <c>within:5</c> / <c>beyond:30</c> (metres), <c>blast</c>.
    /// </summary>
    public static Result<CombatantDamage, string> ParseCombatantDamage(string text)
    {
        var trimmed = text.Trim();
        var colon = trimmed.IndexOf(':');
        var parsed = colon < 0
            ? ToBareCombatantDamage(trimmed)
            : ToRange(trimmed[..colon], trimmed[(colon + 1)..]);
        return parsed.ToResult(() =>
            $"'{text}' is not the damage a resistance reduces (expected combatants, a damage type (arc, solar, …), harmonic, "
            + "within:<metres>, beyond:<metres> or blast)");
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

    private static Optional<CombatantDamage> ToBareCombatantDamage(string word) =>
        word switch
        {
            "combatants" => Optional.Some<CombatantDamage>(new CombatantDamage.All()),
            "harmonic" => Optional.Some<CombatantDamage>(new CombatantDamage.OfSuperType()),
            "blast" => Optional.Some<CombatantDamage>(new CombatantDamage.Blast()),
            _ => ParseVocabularyWord<DamageType>(word).Match(
                type => Optional.Some<CombatantDamage>(new CombatantDamage.OfType(type.Value)),
                _ => Optional.None<CombatantDamage>()),
        };

    private static Optional<CombatantDamage> ToRange(string prefix, string metres) =>
        decimal.TryParse(metres, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) && value > 0
            ? prefix switch
            {
                "within" => Optional.Some<CombatantDamage>(new CombatantDamage.WithinRange(value)),
                "beyond" => Optional.Some<CombatantDamage>(new CombatantDamage.BeyondRange(value)),
                _ => Optional.None<CombatantDamage>(),
            }
            : Optional.None<CombatantDamage>();

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
            "weapon" when argument == "harmonic" => new Result<DamageSource, string>.Ok(new DamageSource.WeaponOfSuperType()),
            "weapon" when argument == "this" => new Result<DamageSource, string>.Ok(new DamageSource.ThisWeapon()),
            "weapon" when argument.StartsWith("this:", StringComparison.Ordinal) => ParseVocabularyWord<DamageType>(argument["this:".Length..])
                .Map(DamageSource (type) => new DamageSource.ThisWeaponOfType(type)),
            "weapon" when ParseVocabularyWord<AmmoType>(argument.Split(':')[0]).IsOk() => ToAmmoDamageSource(argument),
            "weapon" => ParseVocabularyWord<DamageType>(argument)
                .Map(DamageSource (type) => new DamageSource.WeaponOfType(type))
                .MapError(message => $"{message}, primary, special, heavy, harmonic or this"),
            "slot" => ParseVocabularyWord<WeaponSlot>(argument).Map(DamageSource (slot) => new DamageSource.WeaponInSlot(slot)),
            "type" => ParseVocabularyWord<DamageType>(argument).Map(DamageSource (type) => new DamageSource.OfType(type)),
            "keyword" => StatusId.TryFrom(argument).ToResult().Map(DamageSource (status) => new DamageSource.KeywordOf(status)),
            "summon" => SummonId.TryFrom(argument).ToResult().Map(DamageSource (summon) => new DamageSource.SummonOf(summon)),
            _ => ParseVocabularyWord<AbilityKind>(prefix)
                .MapError(_ => $"unknown qualifier '{prefix}:'")
                .Bind(kind => ParseAbilityInput(kind, argument).Map(DamageSource (input) => new DamageSource.AbilityUsedAs(kind, input))),
        };

    /// <summary>
    /// <c>special</c> (after <c>weapon:</c>): a weapon of that ammo type; <c>special:arc</c>, one of that ammo and damage
    /// type (Spark of Beacons's Special Arc weapon).
    /// </summary>
    private static Result<DamageSource, string> ToAmmoDamageSource(string argument) =>
        argument.IndexOf(':', StringComparison.Ordinal) switch
        {
            < 0 => ParseVocabularyWord<AmmoType>(argument)
                .Map(DamageSource (ammo) => new DamageSource.WeaponOfAmmo(ammo, Optional.None<DamageType>())),
            var colon => ParseVocabularyWord<AmmoType>(argument[..colon])
                .Bind(ammo => ParseVocabularyWord<DamageType>(argument[(colon + 1)..])
                    .Map(DamageSource (type) => new DamageSource.WeaponOfAmmo(ammo, Optional.Some(type)))),
        };

    /// <summary>
    /// The input word of an ability's use — <c>press</c>, <c>slide</c>, <c>air</c>, <c>hold</c>, <c>combo</c>, <c>guard</c> —
    /// when that ability takes it (<see cref="AbilityInputs.CanTake"/>: a melee slides or is airborne, a class ability is
    /// used in the air, a grenade is held, a Super attacks in the air, combos or guards); why not otherwise.
    /// </summary>
    public static Result<AbilityInput, string> ParseAbilityInput(AbilityKind kind, string word) =>
        ParseVocabularyWord<AbilityInput>(word).Bind(Result<AbilityInput, string> (input) => kind.CanTake(input)
            ? new Result<AbilityInput, string>.Ok(input)
            : new Result<AbilityInput, string>.Error(
                $"a {ToVocabularyWord(kind)} isn't used as '{word}' (it takes {string.Join(", ", kind.ListSecondaryInputs().Prepend(AbilityInput.Press).Select(ToVocabularyWord))})"));

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

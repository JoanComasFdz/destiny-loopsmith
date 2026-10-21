using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;

namespace Loopsmith.Core.BuildParsing;

/// <summary>
/// Pure: the stats a player types in the designer's Stats section, read as a build's <c>stats:</c> — each empty for
/// unknown ("?") or a whole number 0..200, like the build file's.
/// </summary>
public static class StatInputReading
{
    /// <summary>The six stats in the order the inputs are in (the game's: Health first, Weapons last).</summary>
    public static readonly ImmutableArray<StatKind> InputOrder = StatReading.GameOrder;

    /// <summary>A stat as typed: empty for unknown ("?"), else a whole number 0..200; or why it isn't one.</summary>
    public static Result<Optional<StatValue>, string> ReadStatInput(StatKind stat, string text) =>
        text.Trim().Length == 0
            ? new Result<Optional<StatValue>, string>.Ok(Optional.None<StatValue>())
            : ParseStat(text.Trim()).Match<Result<Optional<StatValue>, string>>(
                value => new Result<Optional<StatValue>, string>.Ok(Optional.Some(value.Value)),
                _ => new Result<Optional<StatValue>, string>.Error($"{stat}: a whole number from 0 to 200, or empty for \"?\"."));

    /// <summary>
    /// The six inputs (in <see cref="InputOrder"/>; one not given is empty, "?") as a stat line; or what is wrong with
    /// each one that isn't a stat.
    /// </summary>
    public static Result<StatLine, ImmutableArray<string>> ReadStatInputs(ImmutableArray<string> inputs)
    {
        var read = InputOrder
            .Select((stat, index) => (Stat: stat, Value: index < inputs.Length ? ReadStatInput(stat, inputs[index]) : Unknown))
            .ToImmutableArray();
        var problems = read.SelectMany(input => input.Value.Match(_ => [], error => new[] { error.Failure })).ToImmutableArray();
        var values = read.ToImmutableDictionary(input => input.Stat, input => input.Value.Match(ok => ok.Value, _ => Optional.None<StatValue>()));
        return problems.IsEmpty
            ? new Result<StatLine, ImmutableArray<string>>.Ok(new StatLine(
                values[StatKind.Weapons], values[StatKind.Health], values[StatKind.Class],
                values[StatKind.Grenade], values[StatKind.Super], values[StatKind.Melee]))
            : new Result<StatLine, ImmutableArray<string>>.Error(problems);
    }

    private static readonly Result<Optional<StatValue>, string> Unknown =
        new Result<Optional<StatValue>, string>.Ok(Optional.None<StatValue>());

    private static Optional<StatValue> ParseStat(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && StatValue.TryFrom(number).IsSuccess
            ? Optional.Some(StatValue.From(number))
            : Optional.None<StatValue>();
}

using System.Collections.Immutable;
using Dunet;

namespace Loopsmith.Core.Functional;

/// <summary>Success or failure track of a railway. Construct with <c>new Result&lt;T, F&gt;.Ok(x)</c>.</summary>
[Union]
public partial record Result<T, TFailure>
{
    partial record Ok(T Value);
    partial record Error(TFailure Failure);
}

public static class ResultExtensions
{
    public static Result<TOut, TFailure> Map<T, TOut, TFailure>(this Result<T, TFailure> result, Func<T, TOut> map) =>
        result.Match<Result<TOut, TFailure>>(
            ok => new Result<TOut, TFailure>.Ok(map(ok.Value)),
            error => new Result<TOut, TFailure>.Error(error.Failure));

    public static Result<TOut, TFailure> Bind<T, TOut, TFailure>(
        this Result<T, TFailure> result,
        Func<T, Result<TOut, TFailure>> bind) =>
        result.Match(
            ok => bind(ok.Value),
            error => new Result<TOut, TFailure>.Error(error.Failure));

    public static Result<T, TOut> MapError<T, TFailure, TOut>(this Result<T, TFailure> result, Func<TFailure, TOut> map) =>
        result.Match<Result<T, TOut>>(
            ok => new Result<T, TOut>.Ok(ok.Value),
            error => new Result<T, TOut>.Error(map(error.Failure)));

    public static bool IsOk<T, TFailure>(this Result<T, TFailure> result) =>
        result.Match(
            _ => true,
            _ => false);

    /// <summary>
    /// Traverses a sequence of results: Ok with every value, or Error with every failure
    /// (failures are accumulated so a parser can report all problems at once).
    /// </summary>
    public static Result<ImmutableArray<T>, ImmutableArray<TFailure>> CombineAll<T, TFailure>(
        this IEnumerable<Result<T, TFailure>> results)
    {
        var values = ImmutableArray.CreateBuilder<T>();
        var failures = ImmutableArray.CreateBuilder<TFailure>();
        foreach (var result in results)
        {
            result.Match(
                ok => { values.Add(ok.Value); return new Unit.Value(); },
                error => { failures.Add(error.Failure); return new Unit.Value(); });
        }

        return failures.Count == 0
            ? new Result<ImmutableArray<T>, ImmutableArray<TFailure>>.Ok(values.ToImmutable())
            : new Result<ImmutableArray<T>, ImmutableArray<TFailure>>.Error(failures.ToImmutable());
    }
}

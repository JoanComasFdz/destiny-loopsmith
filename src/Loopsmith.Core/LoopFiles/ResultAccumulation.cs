using System.Collections.Immutable;
using Loopsmith.Core.Functional;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.LoopFiles.ParseError>;

namespace Loopsmith.Core.LoopFiles;

/// <summary>
/// Error-accumulating composition of parse results: <c>Combine</c> evaluates every input and reports all of
/// their errors together. (Private to this slice; slices never share helpers sideways.)
/// </summary>
internal static class ResultAccumulation
{
    internal static Result<T, Errors> Succeed<T>(T value) => new Result<T, Errors>.Ok(value);

    internal static Result<T, Errors> Fail<T>(Errors errors) => new Result<T, Errors>.Error(errors);

    internal static Errors ToErrors<T>(this Result<T, Errors> result) =>
        result.Match(_ => Errors.Empty, error => error.Failure);

    internal static Result<Unit, Errors> FailIfAny(Errors errors) =>
        errors.IsEmpty ? Succeed<Unit>(new Unit.Value()) : Fail<Unit>(errors);

    internal static Result<ImmutableArray<T>, Errors> CollectAll<T>(this IEnumerable<Result<T, Errors>> results) =>
        results.CombineAll().MapError(failures => failures.SelectMany(failure => failure).ToImmutableArray());

    internal static Result<TOut, Errors> Combine<T1, T2, TOut>(
        Result<T1, Errors> r1,
        Result<T2, Errors> r2,
        Func<T1, T2, TOut> build) =>
        r1 is Result<T1, Errors>.Ok(var v1) && r2 is Result<T2, Errors>.Ok(var v2)
            ? Succeed(build(v1, v2))
            : Fail<TOut>(r1.ToErrors().AddRange(r2.ToErrors()));

    internal static Result<TOut, Errors> Combine<T1, T2, T3, TOut>(
        Result<T1, Errors> r1,
        Result<T2, Errors> r2,
        Result<T3, Errors> r3,
        Func<T1, T2, T3, TOut> build) =>
        Combine(Combine(r1, r2, (v1, v2) => (v1, v2)), r3, (p, v3) => build(p.v1, p.v2, v3));

    internal static Result<TOut, Errors> Combine<T1, T2, T3, T4, TOut>(
        Result<T1, Errors> r1,
        Result<T2, Errors> r2,
        Result<T3, Errors> r3,
        Result<T4, Errors> r4,
        Func<T1, T2, T3, T4, TOut> build) =>
        Combine(Combine(r1, r2, r3, (v1, v2, v3) => (v1, v2, v3)), r4, (p, v4) => build(p.v1, p.v2, p.v3, v4));

    internal static Result<TOut, Errors> Combine<T1, T2, T3, T4, T5, TOut>(
        Result<T1, Errors> r1,
        Result<T2, Errors> r2,
        Result<T3, Errors> r3,
        Result<T4, Errors> r4,
        Result<T5, Errors> r5,
        Func<T1, T2, T3, T4, T5, TOut> build) =>
        Combine(Combine(r1, r2, r3, r4, (v1, v2, v3, v4) => (v1, v2, v3, v4)), r5, (p, v5) => build(p.v1, p.v2, p.v3, p.v4, v5));

    internal static Result<TOut, Errors> Combine<T1, T2, T3, T4, T5, T6, TOut>(
        Result<T1, Errors> r1,
        Result<T2, Errors> r2,
        Result<T3, Errors> r3,
        Result<T4, Errors> r4,
        Result<T5, Errors> r5,
        Result<T6, Errors> r6,
        Func<T1, T2, T3, T4, T5, T6, TOut> build) =>
        Combine(Combine(r1, r2, r3, r4, r5, (v1, v2, v3, v4, v5) => (v1, v2, v3, v4, v5)), r6, (p, v6) => build(p.v1, p.v2, p.v3, p.v4, p.v5, v6));

    internal static Result<TOut, Errors> Combine<T1, T2, T3, T4, T5, T6, T7, TOut>(
        Result<T1, Errors> r1,
        Result<T2, Errors> r2,
        Result<T3, Errors> r3,
        Result<T4, Errors> r4,
        Result<T5, Errors> r5,
        Result<T6, Errors> r6,
        Result<T7, Errors> r7,
        Func<T1, T2, T3, T4, T5, T6, T7, TOut> build) =>
        Combine(Combine(r1, r2, r3, r4, r5, r6, (v1, v2, v3, v4, v5, v6) => (v1, v2, v3, v4, v5, v6)), r7, (p, v7) => build(p.v1, p.v2, p.v3, p.v4, p.v5, p.v6, v7));
}

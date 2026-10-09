using System.Collections.Immutable;
using Loopsmith.Core.Functional;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary>
/// Error-accumulating composition of parse results. Unlike <c>Bind</c>, which stops at the first
/// failure, <c>Combine</c> evaluates every input and reports all of their errors together, so one
/// run shows every problem of a file. (Private to this slice; slices never share helpers sideways.)
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

    /// <summary>Splits results into the values that parsed and the errors of those that did not.</summary>
    internal static (ImmutableArray<T> Values, Errors Errors) Partition<T>(this IEnumerable<Result<T, Errors>> results)
    {
        var all = results.ToImmutableArray();
        return (all.SelectMany(result => ToValues(result)).ToImmutableArray(), all.SelectMany(result => result.ToErrors()).ToImmutableArray());
    }

    private static ImmutableArray<T> ToValues<T>(Result<T, Errors> result) =>
        result.Match(ok => ImmutableArray.Create(ok.Value), _ => ImmutableArray<T>.Empty);

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

    internal static Result<TOut, Errors> Combine<T1, T2, T3, T4, T5, T6, T7, T8, TOut>(
        Result<T1, Errors> r1,
        Result<T2, Errors> r2,
        Result<T3, Errors> r3,
        Result<T4, Errors> r4,
        Result<T5, Errors> r5,
        Result<T6, Errors> r6,
        Result<T7, Errors> r7,
        Result<T8, Errors> r8,
        Func<T1, T2, T3, T4, T5, T6, T7, T8, TOut> build) =>
        Combine(Combine(r1, r2, r3, r4, r5, r6, r7, (v1, v2, v3, v4, v5, v6, v7) => (v1, v2, v3, v4, v5, v6, v7)), r8, (p, v8) => build(p.v1, p.v2, p.v3, p.v4, p.v5, p.v6, p.v7, v8));

    internal static Result<TOut, Errors> Combine<T1, T2, T3, T4, T5, T6, T7, T8, T9, TOut>(
        Result<T1, Errors> r1,
        Result<T2, Errors> r2,
        Result<T3, Errors> r3,
        Result<T4, Errors> r4,
        Result<T5, Errors> r5,
        Result<T6, Errors> r6,
        Result<T7, Errors> r7,
        Result<T8, Errors> r8,
        Result<T9, Errors> r9,
        Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TOut> build) =>
        Combine(Combine(r1, r2, r3, r4, r5, r6, r7, r8, (v1, v2, v3, v4, v5, v6, v7, v8) => (v1, v2, v3, v4, v5, v6, v7, v8)), r9, (p, v9) => build(p.v1, p.v2, p.v3, p.v4, p.v5, p.v6, p.v7, p.v8, v9));

    internal static Result<TOut, Errors> Combine<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TOut>(
        Result<T1, Errors> r1,
        Result<T2, Errors> r2,
        Result<T3, Errors> r3,
        Result<T4, Errors> r4,
        Result<T5, Errors> r5,
        Result<T6, Errors> r6,
        Result<T7, Errors> r7,
        Result<T8, Errors> r8,
        Result<T9, Errors> r9,
        Result<T10, Errors> r10,
        Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TOut> build) =>
        Combine(Combine(r1, r2, r3, r4, r5, r6, r7, r8, r9, (v1, v2, v3, v4, v5, v6, v7, v8, v9) => (v1, v2, v3, v4, v5, v6, v7, v8, v9)), r10, (p, v10) => build(p.v1, p.v2, p.v3, p.v4, p.v5, p.v6, p.v7, p.v8, p.v9, v10));

    internal static Result<TOut, Errors> Combine<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TOut>(
        Result<T1, Errors> r1,
        Result<T2, Errors> r2,
        Result<T3, Errors> r3,
        Result<T4, Errors> r4,
        Result<T5, Errors> r5,
        Result<T6, Errors> r6,
        Result<T7, Errors> r7,
        Result<T8, Errors> r8,
        Result<T9, Errors> r9,
        Result<T10, Errors> r10,
        Result<T11, Errors> r11,
        Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TOut> build) =>
        Combine(Combine(r1, r2, r3, r4, r5, r6, r7, r8, r9, r10, (v1, v2, v3, v4, v5, v6, v7, v8, v9, v10) => (v1, v2, v3, v4, v5, v6, v7, v8, v9, v10)), r11, (p, v11) => build(p.v1, p.v2, p.v3, p.v4, p.v5, p.v6, p.v7, p.v8, p.v9, p.v10, v11));

    internal static Result<TOut, Errors> Combine<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TOut>(
        Result<T1, Errors> r1,
        Result<T2, Errors> r2,
        Result<T3, Errors> r3,
        Result<T4, Errors> r4,
        Result<T5, Errors> r5,
        Result<T6, Errors> r6,
        Result<T7, Errors> r7,
        Result<T8, Errors> r8,
        Result<T9, Errors> r9,
        Result<T10, Errors> r10,
        Result<T11, Errors> r11,
        Result<T12, Errors> r12,
        Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TOut> build) =>
        Combine(Combine(r1, r2, r3, r4, r5, r6, r7, r8, r9, r10, r11, (v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11) => (v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11)), r12, (p, v12) => build(p.v1, p.v2, p.v3, p.v4, p.v5, p.v6, p.v7, p.v8, p.v9, p.v10, p.v11, v12));

    internal static Result<TOut, Errors> Combine<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TOut>(
        Result<T1, Errors> r1,
        Result<T2, Errors> r2,
        Result<T3, Errors> r3,
        Result<T4, Errors> r4,
        Result<T5, Errors> r5,
        Result<T6, Errors> r6,
        Result<T7, Errors> r7,
        Result<T8, Errors> r8,
        Result<T9, Errors> r9,
        Result<T10, Errors> r10,
        Result<T11, Errors> r11,
        Result<T12, Errors> r12,
        Result<T13, Errors> r13,
        Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TOut> build) =>
        Combine(Combine(r1, r2, r3, r4, r5, r6, r7, r8, r9, r10, r11, r12, (v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12) => (v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12)), r13, (p, v13) => build(p.v1, p.v2, p.v3, p.v4, p.v5, p.v6, p.v7, p.v8, p.v9, p.v10, p.v11, p.v12, v13));
}

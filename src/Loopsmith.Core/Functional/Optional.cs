using Dunet;

namespace Loopsmith.Core.Functional;

/// <summary>Presence or absence of a value — never null-as-absence.</summary>
[Union]
public partial record Optional<T>
{
    partial record Some(T Value);
    partial record None();
}

/// <summary>Case constructors: <c>Optional.Some(x)</c> / <c>Optional.None&lt;T&gt;()</c>.</summary>
public static class Optional
{
    public static Optional<T> Some<T>(T value) => new Optional<T>.Some(value);

    public static Optional<T> None<T>() => new Optional<T>.None();

    public static Optional<T> FromNullable<T>(T? value)
        where T : class =>
        value is null ? None<T>() : Some(value);

    public static Optional<T> FromNullableValue<T>(T? value)
        where T : struct =>
        value.HasValue ? Some(value.Value) : None<T>();
}

public static class OptionalExtensions
{
    public static Optional<TOut> Map<T, TOut>(this Optional<T> optional, Func<T, TOut> map) =>
        optional.Match(
            some => Optional.Some(map(some.Value)),
            _ => Optional.None<TOut>());

    public static Optional<TOut> Bind<T, TOut>(this Optional<T> optional, Func<T, Optional<TOut>> bind) =>
        optional.Match(
            some => bind(some.Value),
            _ => Optional.None<TOut>());

    public static T UnwrapOr<T>(this Optional<T> optional, T fallback) =>
        optional.Match(
            some => some.Value,
            _ => fallback);

    public static bool IsSome<T>(this Optional<T> optional) =>
        optional.Match(
            _ => true,
            _ => false);

    /// <summary>The first present value of a sequence of optionals, if any.</summary>
    public static Optional<T> FindFirstSome<T>(this IEnumerable<Optional<T>> optionals) =>
        optionals.Aggregate(Optional.None<T>(), (found, next) => found.IsSome() ? found : next);

    public static Result<T, TFailure> ToResult<T, TFailure>(this Optional<T> optional, Func<TFailure> failure) =>
        optional.Match<Result<T, TFailure>>(
            some => new Result<T, TFailure>.Ok(some.Value),
            _ => new Result<T, TFailure>.Error(failure()));
}

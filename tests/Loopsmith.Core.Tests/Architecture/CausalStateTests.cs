using System.Collections.Immutable;
using System.Reflection;
using Loopsmith.Core.Domain;

namespace Loopsmith.Core.Tests.Architecture;

/// <summary>
/// ADRs D1: the game state says what is present (a buff, a debuff on the pack, a pickup on the ground) and what the
/// player declared — never a count, an amount or a time. Walks <see cref="GameState"/> member by member (through
/// records, arrays and value objects) and fails on any number but the step's ordinal, so a stack counter, a clock or
/// an energy gauge can't come back unnoticed.
/// </summary>
public sealed class CausalStateTests
{
    private static readonly ImmutableHashSet<string> Allowed = ["GameState.Step"];

    private static readonly ImmutableHashSet<Type> Numbers =
        [typeof(int), typeof(long), typeof(short), typeof(byte), typeof(uint), typeof(ulong), typeof(float), typeof(double), typeof(decimal)];

    [Fact]
    public void Game_state_holds_no_number_but_the_step()
    {
        var numbers = ListLeaves(typeof(GameState), nameof(GameState), [])
            .Where(leaf => Numbers.Contains(leaf.Type) && !Allowed.Contains(leaf.Path))
            .Select(leaf => $"{leaf.Path} ({leaf.Type.Name})");

        Violations.AssertNone(
            "GameState holds what is present and what the player declared — no count, amount or time (ADRs D1).",
            numbers);
    }

    private static IEnumerable<(string Path, Type Type)> ListLeaves(Type type, string path, ImmutableHashSet<Type> seen)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
        {
            return ListLeaves(type.GetGenericArguments()[0], path + "[]", seen);
        }

        var wrapped = type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        var isValueObject = wrapped is not null && type.GetMethod("From", BindingFlags.Public | BindingFlags.Static) is not null;
        if (isValueObject)
        {
            return [(path, wrapped!.PropertyType)];   // a Vogen value object: its underlying value is the leaf
        }

        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || seen.Contains(type)
            || type.Namespace?.StartsWith("Loopsmith.Core", StringComparison.Ordinal) != true)
        {
            return [(path, type)];
        }

        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name != "EqualityContract")
            .SelectMany(property => ListLeaves(property.PropertyType, $"{path}.{property.Name}", seen.Add(type)));
    }
}

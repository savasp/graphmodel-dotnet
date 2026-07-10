// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Querying;

using System.Collections;

/// <summary>
/// The executor's "the terminal asked for a default value" marker (an <c>...OrDefault</c> form
/// over an empty sequence). Distinct from a null result, which for a non-nullable result type is
/// a materialization error rather than a default.
/// </summary>
internal sealed class DefaultResult
{
    /// <summary>The singleton marker instance.</summary>
    public static readonly DefaultResult Instance = new();

    private DefaultResult()
    {
    }
}

/// <summary>
/// Converts the executor's untyped results (scalars, or <c>List&lt;object?&gt;</c> sequences)
/// into the exact type an async terminal requested: typed lists, arrays, sets, or converted
/// scalars.
/// </summary>
internal static class ResultShaper
{
    /// <summary>Shapes an executor result into <typeparamref name="TResult"/>.</summary>
    public static TResult Shape<TResult>(object? value)
    {
        if (value is DefaultResult)
        {
            return default!;
        }

        if (value is null)
        {
            if (typeof(TResult).IsValueType && Nullable.GetUnderlyingType(typeof(TResult)) is null)
            {
                throw new InvalidOperationException(
                    $"The query produced a null value where the non-nullable result type '{typeof(TResult).Name}' was requested. " +
                    "Use a nullable projection to allow null results.");
            }

            return default!;
        }

        if (value is TResult typed)
        {
            return typed;
        }

        var targetType = typeof(TResult);
        if (value is IEnumerable sequence && value is not string)
        {
            var shaped = ShapeSequence(sequence, targetType);
            if (shaped is not null)
            {
                return (TResult)shaped;
            }
        }

        return (TResult)ConvertScalar(value, targetType)!;
    }

    private static object? ShapeSequence(IEnumerable sequence, Type targetType)
    {
        var elementType = ElementTypeOf(targetType);
        if (elementType is null)
        {
            return null;
        }

        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        foreach (var item in sequence)
        {
            list.Add(ConvertScalar(item, elementType));
        }

        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(array, 0);
            return array;
        }

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(HashSet<>))
        {
            return Activator.CreateInstance(targetType, list);
        }

        return targetType.IsInstanceOfType(list) ? list : null;
    }

    private static Type? ElementTypeOf(Type targetType)
    {
        if (targetType.IsArray)
        {
            return targetType.GetElementType();
        }

        if (targetType.IsGenericType)
        {
            var definition = targetType.GetGenericTypeDefinition();
            if (definition == typeof(List<>) || definition == typeof(IList<>) ||
                definition == typeof(IEnumerable<>) || definition == typeof(IReadOnlyList<>) ||
                definition == typeof(ICollection<>) || definition == typeof(IReadOnlyCollection<>) ||
                definition == typeof(HashSet<>))
            {
                return targetType.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static object? ConvertScalar(object? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying.IsInstanceOfType(value))
        {
            return value;
        }

        if (underlying.IsEnum)
        {
            return value is string text
                ? Enum.Parse(underlying, text, ignoreCase: true)
                : Enum.ToObject(underlying, value);
        }

        return Convert.ChangeType(value, underlying, System.Globalization.CultureInfo.InvariantCulture);
    }
}

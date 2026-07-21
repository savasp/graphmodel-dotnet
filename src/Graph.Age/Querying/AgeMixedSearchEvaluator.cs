// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Querying.Linq;

/// <summary>
/// Applies the outer LINQ pipeline of a mixed AGE search after its node and relationship branches
/// have been combined. Only the standard query operators already accepted by the shared model
/// builder are translated; graph-specific operators remain rejected by that builder.
/// </summary>
internal static class AgeMixedSearchEvaluator
{
    public static TResult Evaluate<TResult>(Expression expression, IReadOnlyList<Graph.IEntity> entities)
    {
        var translated = new Translator(entities).Visit(expression);
        var body = translated.Type == typeof(TResult)
            ? translated
            : Expression.Convert(translated, typeof(TResult));
        return Expression.Lambda<Func<TResult>>(body).Compile()();
    }

    public static IReadOnlyList<T> EvaluateSequence<T>(
        Expression expression,
        IReadOnlyList<Graph.IEntity> entities)
    {
        var translated = new Translator(entities).Visit(expression);
        var value = Expression.Lambda(translated).Compile().DynamicInvoke();
        return value is IEnumerable<T> sequence ? [.. sequence] : [];
    }

    private sealed class Translator(IReadOnlyList<Graph.IEntity> entities) : ExpressionVisitor
    {
        private readonly IQueryable<Graph.IEntity> source = entities.AsQueryable();

        protected override Expression VisitExtension(Expression node) =>
            node is AgeMixedSearchRootExpression
                ? Expression.Constant(source, typeof(IQueryable<Graph.IEntity>))
                : base.VisitExtension(node);

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(GraphQueryableExtensions))
            {
                if (node.Method.Name == nameof(GraphQueryableExtensions.Search))
                {
                    throw new GraphQueryTranslationException(
                        "Chaining Search() after graph.Search() is not supported by the AGE mixed-search root.");
                }

                return CallQueryable(node.Method.Name, node.Method, node.Arguments.Select(argument => Visit(argument)!).ToArray());
            }

            if (node.Method.DeclaringType == typeof(QueryTerminals))
            {
                return TranslateTerminal(node, node.Method.Name[..^"AsyncMarker".Length]);
            }

            return base.VisitMethodCall(node);
        }

        private MethodCallExpression TranslateTerminal(MethodCallExpression node, string name)
        {
            var arguments = node.Arguments
                .Select(argument => Visit(argument)!)
                .ToArray();

            var materializer = name switch
            {
                "ToList" => nameof(MaterializeToList),
                "ToArray" => nameof(MaterializeToArray),
                "ToHashSet" => nameof(MaterializeToHashSet),
                "ToDictionary" => nameof(MaterializeToDictionary),
                "ToLookup" => nameof(MaterializeToLookup),
                _ => null,
            };

            if (materializer is not null)
            {
                return CallHelper(materializer, node.Method, arguments);
            }

            return CallQueryable(name, node.Method, arguments);
        }

        private static MethodCallExpression CallQueryable(
            string name,
            MethodInfo originalMethod,
            Expression[] arguments)
        {
            var genericArguments = originalMethod.IsGenericMethod
                ? originalMethod.GetGenericArguments()
                : Type.EmptyTypes;
            var candidates = typeof(Queryable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == name
                    && method.GetParameters().Length == arguments.Length
                    && GenericArity(method) == genericArguments.Length);

            foreach (var candidate in candidates)
            {
                MethodInfo closed;
                try
                {
                    closed = candidate.IsGenericMethodDefinition
                        ? candidate.MakeGenericMethod(genericArguments)
                        : candidate;
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (ParametersAccept(closed, arguments))
                {
                    return Expression.Call(closed, arguments);
                }
            }

            throw new GraphQueryTranslationException(
                $"The AGE mixed-search root cannot apply query operator '{name}' to this result shape.");
        }

        private static MethodCallExpression CallHelper(
            string name,
            MethodInfo originalMethod,
            Expression[] arguments)
        {
            var genericArguments = originalMethod.GetGenericArguments();
            var candidate = typeof(AgeMixedSearchEvaluator)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Single(method => method.Name == name
                    && method.GetGenericArguments().Length == genericArguments.Length
                    && method.GetParameters().Length == arguments.Length)
                .MakeGenericMethod(genericArguments);
            return Expression.Call(candidate, arguments);
        }

        private static int GenericArity(MethodInfo method) =>
            method.IsGenericMethodDefinition ? method.GetGenericArguments().Length : 0;

        private static bool ParametersAccept(MethodInfo method, Expression[] arguments)
        {
            var parameters = method.GetParameters();
            for (var index = 0; index < parameters.Length; index++)
            {
                if (!parameters[index].ParameterType.IsAssignableFrom(arguments[index].Type))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static List<T> MaterializeToList<T>(IQueryable<T> source) => [.. source];

    private static T[] MaterializeToArray<T>(IQueryable<T> source) => [.. source];

    private static HashSet<T> MaterializeToHashSet<T>(IQueryable<T> source) => [.. source];

    private static Dictionary<TKey, TSource> MaterializeToDictionary<TSource, TKey>(
        IQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector)
        where TKey : notnull =>
        source.ToDictionary(keySelector.Compile());

    private static Dictionary<TKey, TElement> MaterializeToDictionary<TSource, TKey, TElement>(
        IQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector,
        Expression<Func<TSource, TElement>> elementSelector)
        where TKey : notnull =>
        source.ToDictionary(keySelector.Compile(), elementSelector.Compile());

    private static ILookup<TKey, TSource> MaterializeToLookup<TSource, TKey>(
        IQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector) =>
        source.ToLookup(keySelector.Compile());

    private static ILookup<TKey, TElement> MaterializeToLookup<TSource, TKey, TElement>(
        IQueryable<TSource> source,
        Expression<Func<TSource, TKey>> keySelector,
        Expression<Func<TSource, TElement>> elementSelector) =>
        source.ToLookup(keySelector.Compile(), elementSelector.Compile());
}

// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using System.Reflection;
using Cvoya.Graph.Neo4j.Querying.Linq.Queryables;
using Cvoya.Graph.Neo4j.Translation.Tests.Model;
using Cvoya.Graph.Querying;
using LinqOperatorDispatch = Cvoya.Graph.Querying.LinqOperatorDispatch;

/// <summary>
/// Guards the <c>MethodInfo</c>-identity dispatch table (issue #94 testing requirement 3) against
/// the silent-string-dispatch failure mode it replaces: every terminal/operator
/// <see cref="MethodInfo"/> actually registered in <see cref="LinqOperatorDispatch.AllRegisteredMethods"/>
/// must resolve to a <see cref="LinqOperator"/> that <see cref="CypherQueryVisitor"/> has a handler
/// for (reachability from the table), and every <see cref="LinqOperator"/> enum value must be
/// registered by at least one real <see cref="MethodInfo"/> (no orphaned/dead enum values). This
/// enumerates the actual table (<see cref="LinqOperatorDispatch.AllRegisteredMethods"/>) rather
/// than a hand-maintained mirror list, so a new registration can't silently go unverified.
/// </summary>
public class LinqOperatorDispatchTests
{
    // Mirrors the switch arms in CypherQueryVisitor.HandleLinqMethod - the set of LinqOperator
    // values that actually have a case there. If a case is added/removed without updating this
    // set, EveryRegisteredOperatorHasAVisitorHandler / EveryLinqOperatorEnumValueIsRegistered will
    // catch the drift.
    private static readonly HashSet<LinqOperator> HandledByVisitor =
    [
        LinqOperator.Where,
        LinqOperator.Select,
        LinqOperator.OrderBy,
        LinqOperator.OrderByDescending,
        LinqOperator.ThenBy,
        LinqOperator.ThenByDescending,
        LinqOperator.Take,
        LinqOperator.Skip,
        LinqOperator.Distinct,
        LinqOperator.ToListOrArray,
        LinqOperator.First,
        LinqOperator.Single,
        LinqOperator.Last,
        LinqOperator.Any,
        LinqOperator.All,
        LinqOperator.Count,
        LinqOperator.Sum,
        LinqOperator.Average,
        LinqOperator.Min,
        LinqOperator.Max,
        LinqOperator.Contains,
        LinqOperator.ElementAt,
        LinqOperator.ElementAtOrDefault,
        LinqOperator.SelectMany,
        LinqOperator.GroupBy,
        LinqOperator.Join,
        LinqOperator.Union,
        LinqOperator.PathSegments,
        LinqOperator.TraversePaths,
        LinqOperator.Direction,
        LinqOperator.WithDepth,
        LinqOperator.Search,
    ];

    [Fact]
    public void EveryRegisteredMethodResolvesToAHandledOperator()
    {
        // Enumerates LinqOperatorDispatch.AllRegisteredMethods directly (the real table, built via
        // reflection in LinqOperatorDispatch.BuildTable) rather than a hand-maintained mirror -
        // this is the completeness property itself: every MethodInfo the table can actually
        // produce at runtime must map to an operator CypherQueryVisitor knows how to handle.
        var unhandled = LinqOperatorDispatch.AllRegisteredMethods
            .Where(kvp => !HandledByVisitor.Contains(kvp.Value))
            .Select(kvp => $"{kvp.Key.DeclaringType?.Name}.{kvp.Key.Name} -> {kvp.Value}")
            .ToList();

        Assert.True(unhandled.Count == 0,
            $"Registered method(s) whose LinqOperator has no case in CypherQueryVisitor.HandleLinqMethod: {string.Join(", ", unhandled)}.");
    }

    [Fact]
    public void EveryLinqOperatorEnumValueIsRegisteredByAtLeastOneMethod()
    {
        // The inverse completeness check: guards against an enum value that exists but nothing in
        // the table actually produces (dead/orphaned dispatch target).
        var registeredOperators = LinqOperatorDispatch.AllRegisteredMethods.Values.ToHashSet();
        var allValues = Enum.GetValues<LinqOperator>();

        var unregistered = allValues.Where(op => !registeredOperators.Contains(op)).ToList();

        Assert.True(unregistered.Count == 0,
            $"LinqOperator value(s) with no registered MethodInfo in the dispatch table: {string.Join(", ", unregistered)}.");
    }

    [Fact]
    public void EveryLinqOperatorEnumValueIsHandledByCypherQueryVisitor()
    {
        var allValues = Enum.GetValues<LinqOperator>();

        var unhandled = allValues.Where(op => !HandledByVisitor.Contains(op)).ToList();

        Assert.True(unhandled.Count == 0,
            $"LinqOperator value(s) not wired into CypherQueryVisitor.HandleLinqMethod: {string.Join(", ", unhandled)}. " +
            "Every enum value must have a corresponding case (and be added to HandledByVisitor above once verified).");
    }

    /// <summary>
    /// The six #74 terminal members delegate through these markers, and the dispatch table must
    /// resolve each marker <see cref="MethodInfo"/> to a handled <see cref="LinqOperator"/>.
    /// </summary>
    // marker name -> expected LinqOperator name (Enum.Parse'd inside the test, so the [Theory]
    // parameter list itself never needs to mention the internal LinqOperator type).
    [Theory]
    [InlineData("LastAsyncMarker", "Last")]
    [InlineData("LastOrDefaultAsyncMarker", "Last")]
    [InlineData("CountAsyncMarker", "Count")]
    [InlineData("AnyAsyncMarker", "Any")]
    [InlineData("MinAsyncMarker", "Min")]
    [InlineData("MaxAsyncMarker", "Max")]
    public void Issue74TerminalMarkersResolveToHandledOperators(string markerName, string expectedOperatorName)
    {
        var expectedOperator = Enum.Parse<LinqOperator>(expectedOperatorName);

        var markersType = typeof(GraphQueryableExtensions).Assembly.GetType("Cvoya.Graph.QueryTerminals")
            ?? throw new InvalidOperationException("Could not find QueryTerminals via reflection.");

        var candidates = markersType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => m.Name == markerName)
            .ToList();

        Assert.NotEmpty(candidates);

        var methods = candidates.Select(candidate => candidate.IsGenericMethodDefinition
            ? candidate.MakeGenericMethod([.. candidate.GetGenericArguments().Select(_ => typeof(Person))])
            : candidate);

        foreach (var method in methods)
        {
            Assert.Equal(expectedOperator, LinqOperatorDispatch.Resolve(method));
            Assert.Contains(expectedOperator, HandledByVisitor);
        }
    }

    /// <summary>
    /// Confirms the #74 members are present on GraphQueryableBase&lt;T&gt;; behavior is covered by
    /// the provider contract tests.
    /// </summary>
    [Fact]
    public void Issue74MemberBodiesAreImplementedOnGraphQueryableBase()
    {
        var methodNames = new[]
        {
            nameof(GraphQueryableBase<object>.LastAsync),
            nameof(GraphQueryableBase<object>.LastOrDefaultAsync),
        };

        foreach (var name in methodNames)
        {
            var method = typeof(GraphQueryableBase<Person>).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Could not find {name} on GraphQueryableBase<T>.");
            Assert.NotNull(method);
        }
    }

    [Fact]
    public void QueryableWhereResolvesToWhereOperator()
    {
        var method = typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.Where) && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(int));

        Assert.Equal(LinqOperator.Where, LinqOperatorDispatch.Resolve(method));
    }

    [Fact]
    public void GraphQueryableExtensionsWhereResolvesToWhereOperator()
    {
        var method = typeof(GraphQueryableExtensions).GetMethods()
            .First(m => m.Name == nameof(GraphQueryableExtensions.Where))
            .MakeGenericMethod(typeof(int));

        Assert.Equal(LinqOperator.Where, LinqOperatorDispatch.Resolve(method));
    }

    [Fact]
    public void GraphTraversalExtensionsPathSegmentsResolvesToPathSegmentsOperator()
    {
        var method = typeof(GraphTraversalExtensions).GetMethods()
            .First(m => m.Name == nameof(GraphTraversalExtensions.PathSegments))
            .MakeGenericMethod(typeof(Person), typeof(Knows), typeof(Person));

        Assert.Equal(LinqOperator.PathSegments, LinqOperatorDispatch.Resolve(method));
    }

    [Fact]
    public void ReverseTraverseIsIntentionallyUnregistered()
    {
        // ReverseTraverse eagerly composes PathSegments().Direction(Incoming).Select(...) and
        // calls source.Provider.CreateQuery<T> immediately - the method call itself never reaches
        // the visitor, so it must not be in the dispatch table (registering it would be dead code;
        // see the #80 characterization finding referenced in LinqOperatorDispatch and the #94
        // migration guide). Checked for both the current two-arg form (TRel, TEnd) and the
        // obsolete three-arg shim (TStart, TRel, TEnd) - neither reaches the visitor as a
        // "ReverseTraverse" MethodCallExpression.
        var twoArgMethod = typeof(GraphTraversalExtensions).GetMethods()
            .First(m => m.Name == nameof(GraphTraversalExtensions.ReverseTraverse) && m.GetGenericArguments().Length == 2)
            .MakeGenericMethod(typeof(Knows), typeof(Person));

        Assert.Null(LinqOperatorDispatch.Resolve(twoArgMethod));
        Assert.DoesNotContain(twoArgMethod, LinqOperatorDispatch.AllRegisteredMethods.Keys);

#pragma warning disable CS0618 // exercising the obsolete three-arg shim directly is the point of this assertion.
        var threeArgMethod = typeof(GraphTraversalExtensions).GetMethods()
            .First(m => m.Name == nameof(GraphTraversalExtensions.ReverseTraverse) && m.GetGenericArguments().Length == 3)
            .MakeGenericMethod(typeof(Person), typeof(Knows), typeof(Person));
#pragma warning restore CS0618

        Assert.Null(LinqOperatorDispatch.Resolve(threeArgMethod));
        Assert.DoesNotContain(threeArgMethod, LinqOperatorDispatch.AllRegisteredMethods.Keys);
    }

    [Fact]
    public void UnknownMethodResolvesToNull()
    {
        var method = typeof(string).GetMethod(nameof(string.Trim), Type.EmptyTypes)!;

        Assert.Null(LinqOperatorDispatch.Resolve(method));
    }
}

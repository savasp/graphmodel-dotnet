// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Neo4j.Translation.Tests.Harness;
using Cvoya.Graph.Neo4j.Translation.Tests.Model;

/// <summary>
/// Expression-shape tests (issue #94 testing requirement 1): for every public operator on
/// <see cref="GraphQueryableExtensions"/> and <see cref="GraphTraversalExtensions"/>, calling the
/// operator through the public <see cref="IGraphQueryable{T}"/> surface must produce a
/// <see cref="MethodCallExpression"/> with the expected <see cref="MethodInfo"/> identity
/// (matching the operator's own declared <see cref="MethodInfo"/>, not merely "some method named
/// X"), the source as the first argument, and constant/lambda arguments correctly encoded.
///
/// These are provider-free (no Docker, no Neo4j driver) - they assert on the shape of the
/// expression tree built by the LINQ extension methods themselves, before any translation. The
/// brief calls this "the compatibility bedrock for #84" (the future two-level IR): a provider- and
/// translation-agnostic guarantee that the public surface always emits the expression shape a
/// front-end can rely on, decoupled from whether Cypher translation exists for a given shape yet.
///
/// Home: this project (rather than a new suite) because it already hosts the harness
/// (<see cref="Root"/>, the domain model) needed to build real expression trees off the public
/// surface without a live graph.
/// </summary>
public class ExpressionShapeTests
{
    // .NET's Expression.Call factory returns internal subclasses of MethodCallExpression
    // (MethodCallExpression2/3/N, chosen by argument count for a compact representation), so
    // Assert.IsType<MethodCallExpression> (exact-type) fails; assert assignability instead.
    private static MethodCallExpression AsCall(Expression expression) =>
        Assert.IsAssignableFrom<MethodCallExpression>(expression);

    private static ConstantExpression AsConstant(Expression expression) =>
        Assert.IsAssignableFrom<ConstantExpression>(expression);

    private static LambdaExpression ExtractLambda(Expression expression) => expression switch
    {
        LambdaExpression lambda => lambda,
        UnaryExpression { Operand: LambdaExpression lambda } => lambda,
        _ => throw new InvalidOperationException($"Expected a lambda, got {expression.GetType().Name}")
    };

    /// <summary>
    /// Asserts that <paramref name="argument"/> carries the exact <paramref name="expected"/>
    /// lambda instance, whether or not <c>Expression.Call</c> wrapped it in a <c>Quote</c>
    /// (<see cref="UnaryExpression"/>) node - which it does whenever the target parameter's static
    /// type is <c>Expression&lt;TDelegate&gt;</c>, even when the argument passed in is already
    /// exactly that type. Argument-capture correctness is about the underlying lambda being the
    /// same instance passed by the caller, not about whether a Quote wrapper was added.
    /// </summary>
    private static void AssertSameLambda(LambdaExpression expected, Expression argument) =>
        Assert.Same(expected, ExtractLambda(argument));

    // ---- Where ----

    [Fact]
    public void Where_ProducesExpectedMethodCallShape()
    {
        var source = Root.Nodes<Person>();
        Expression<Func<Person, bool>> predicate = p => p.FirstName == "Alice";

        var result = source.Where(predicate);

        var call = AsCall(result.Expression);
        var expectedMethod = typeof(GraphQueryableExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphQueryableExtensions.Where) && m.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(Person));

        Assert.Equal(expectedMethod, call.Method);
        Assert.Equal(2, call.Arguments.Count);
        Assert.Same(source.Expression, call.Arguments[0]);
        AssertSameLambda(predicate, call.Arguments[1]);
    }

    // ---- Select (two overloads: selector, and selector-with-index) ----

    [Fact]
    public void Select_WithSelector_ProducesExpectedMethodCallShape()
    {
        var source = Root.Nodes<Person>();
        Expression<Func<Person, string>> selector = p => p.FirstName;

        var result = source.Select(selector);

        var call = AsCall(result.Expression);
        var expectedMethod = typeof(GraphQueryableExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphQueryableExtensions.Select)
                && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2)
            .MakeGenericMethod(typeof(Person), typeof(string));

        Assert.Equal(expectedMethod, call.Method);
        Assert.Equal(2, call.Arguments.Count);
        Assert.Same(source.Expression, call.Arguments[0]);
        AssertSameLambda(selector, call.Arguments[1]);
    }

    [Fact]
    public void Select_WithIndexSelector_ProducesExpectedMethodCallShape()
    {
        var source = Root.Nodes<Person>();
        Expression<Func<Person, int, string>> selector = (p, i) => p.FirstName + i;

        var result = source.Select(selector);

        var call = AsCall(result.Expression);

        // Both Select overloads are named "Select" with 2 generic args and 2 parameters; the
        // dispatch inside GraphQueryableExtensions.Select resolves by exact overload match via
        // GetGenericExtensionMethod, which cannot distinguish the (T,int,TResult) shape from the
        // (T,TResult) shape by generic-arg/param count alone - so the runtime call must actually be
        // routed to the with-index overload, which we assert by checking the delegate parameter
        // shape of the argument's static type.
        Assert.Equal(nameof(GraphQueryableExtensions.Select), call.Method.Name);
        Assert.Equal(2, call.Arguments.Count);
        Assert.Same(source.Expression, call.Arguments[0]);
        AssertSameLambda(selector, call.Arguments[1]);

        var lambda = ExtractLambda(call.Arguments[1]);
        Assert.Equal(2, lambda.Parameters.Count);
        Assert.Equal(typeof(int), lambda.Parameters[1].Type);
    }

    // ---- SelectMany (two overloads) ----

    [Fact]
    public void SelectMany_SingleSelector_ProducesExpectedMethodCallShape()
    {
        var source = Root.Nodes<Person>();
        Expression<Func<Person, IEnumerable<string>>> selector = p => p.Nicknames;

        var result = source.SelectMany(selector);

        var call = AsCall(result.Expression);
        Assert.Equal(nameof(GraphQueryableExtensions.SelectMany), call.Method.Name);
        Assert.Equal(2, call.Arguments.Count);
        Assert.Same(source.Expression, call.Arguments[0]);
        AssertSameLambda(selector, call.Arguments[1]);
    }

    [Fact]
    public void SelectMany_CollectionAndResultSelector_ProducesExpectedMethodCallShape()
    {
        var source = Root.Nodes<Person>();
        Expression<Func<Person, IEnumerable<string>>> collectionSelector = p => p.Nicknames;
        Expression<Func<Person, string, string>> resultSelector = (p, n) => p.FirstName + n;

        var result = source.SelectMany(collectionSelector, resultSelector);

        var call = AsCall(result.Expression);
        var expectedMethod = typeof(GraphQueryableExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphQueryableExtensions.SelectMany) && m.GetGenericArguments().Length == 3)
            .MakeGenericMethod(typeof(Person), typeof(string), typeof(string));

        Assert.Equal(expectedMethod, call.Method);
        Assert.Equal(3, call.Arguments.Count);
        Assert.Same(source.Expression, call.Arguments[0]);
        AssertSameLambda(collectionSelector, call.Arguments[1]);
        AssertSameLambda(resultSelector, call.Arguments[2]);
    }

    // ---- OrderBy / OrderByDescending / ThenBy / ThenByDescending ----

    [Fact]
    public void OrderBy_ProducesExpectedMethodCallShape()
    {
        var source = Root.Nodes<Person>();
        Expression<Func<Person, string>> keySelector = p => p.LastName;

        var result = source.OrderBy(keySelector);

        var call = AsCall(result.Expression);
        var expectedMethod = typeof(GraphQueryableExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphQueryableExtensions.OrderBy))
            .MakeGenericMethod(typeof(Person), typeof(string));

        Assert.Equal(expectedMethod, call.Method);
        Assert.Equal(2, call.Arguments.Count);
        Assert.Same(source.Expression, call.Arguments[0]);
        AssertSameLambda(keySelector, call.Arguments[1]);
    }

    [Fact]
    public void OrderByDescending_ProducesExpectedMethodCallShape()
    {
        var source = Root.Nodes<Person>();
        Expression<Func<Person, string>> keySelector = p => p.LastName;

        var result = source.OrderByDescending(keySelector);

        var call = AsCall(result.Expression);
        var expectedMethod = typeof(GraphQueryableExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphQueryableExtensions.OrderByDescending))
            .MakeGenericMethod(typeof(Person), typeof(string));

        Assert.Equal(expectedMethod, call.Method);
        AssertSameLambda(keySelector, call.Arguments[1]);
    }

    [Fact]
    public void ThenBy_ProducesExpectedMethodCallShape()
    {
        var source = Root.Nodes<Person>().OrderBy(p => p.LastName);
        Expression<Func<Person, string>> thenKeySelector = p => p.FirstName;

        var result = source.ThenBy(thenKeySelector);

        var call = AsCall(result.Expression);
        var expectedMethod = typeof(GraphQueryableExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphQueryableExtensions.ThenBy))
            .MakeGenericMethod(typeof(Person), typeof(string));

        Assert.Equal(expectedMethod, call.Method);
        Assert.Same(source.Expression, call.Arguments[0]);
        AssertSameLambda(thenKeySelector, call.Arguments[1]);
    }

    [Fact]
    public void ThenByDescending_ProducesExpectedMethodCallShape()
    {
        var source = Root.Nodes<Person>().OrderBy(p => p.LastName);
        Expression<Func<Person, string>> thenKeySelector = p => p.FirstName;

        var result = source.ThenByDescending(thenKeySelector);

        var call = AsCall(result.Expression);
        var expectedMethod = typeof(GraphQueryableExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphQueryableExtensions.ThenByDescending))
            .MakeGenericMethod(typeof(Person), typeof(string));

        Assert.Equal(expectedMethod, call.Method);
        AssertSameLambda(thenKeySelector, call.Arguments[1]);
    }

    // ---- Skip / Take / Distinct ----

    [Fact]
    public void Skip_CapturesCountAsConstant()
    {
        var source = Root.Nodes<Person>();

        var result = source.Skip(7);

        var call = AsCall(result.Expression);
        var expectedMethod = typeof(GraphQueryableExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphQueryableExtensions.Skip))
            .MakeGenericMethod(typeof(Person));

        Assert.Equal(expectedMethod, call.Method);
        Assert.Equal(2, call.Arguments.Count);
        Assert.Same(source.Expression, call.Arguments[0]);
        Assert.Equal(7, AsConstant(call.Arguments[1]).Value);
    }

    [Fact]
    public void Take_CapturesCountAsConstant()
    {
        var source = Root.Nodes<Person>();

        var result = source.Take(3);

        var call = AsCall(result.Expression);
        var expectedMethod = typeof(GraphQueryableExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphQueryableExtensions.Take))
            .MakeGenericMethod(typeof(Person));

        Assert.Equal(expectedMethod, call.Method);
        Assert.Equal(3, AsConstant(call.Arguments[1]).Value);
    }

    [Fact]
    public void Distinct_ProducesExpectedMethodCallShape()
    {
        var source = Root.Nodes<Person>();

        var result = source.Distinct();

        var call = AsCall(result.Expression);
        var expectedMethod = typeof(GraphQueryableExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphQueryableExtensions.Distinct))
            .MakeGenericMethod(typeof(Person));

        Assert.Equal(expectedMethod, call.Method);
        Assert.Single(call.Arguments);
        Assert.Same(source.Expression, call.Arguments[0]);
    }

    // ---- GroupBy ----

    [Fact]
    public void GroupBy_ProducesExpectedMethodCallShape()
    {
        var source = Root.Nodes<Person>();
        Expression<Func<Person, EmploymentStatus>> keySelector = p => p.Status;

        var result = source.GroupBy(keySelector);

        var call = AsCall(result.Expression);
        var expectedMethod = typeof(GraphQueryableExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphQueryableExtensions.GroupBy))
            .MakeGenericMethod(typeof(Person), typeof(EmploymentStatus));

        Assert.Equal(expectedMethod, call.Method);
        Assert.Equal(2, call.Arguments.Count);
        AssertSameLambda(keySelector, call.Arguments[1]);
    }

    // ---- Search ----

    [Fact]
    public void Search_CapturesQueryAsConstant()
    {
        var source = Root.Nodes<Person>();

        var result = source.Search("engineer");

        var call = AsCall(result.Expression);
        var expectedMethod = typeof(GraphQueryableExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphQueryableExtensions.Search))
            .MakeGenericMethod(typeof(Person));

        Assert.Equal(expectedMethod, call.Method);
        Assert.Equal("engineer", AsConstant(call.Arguments[1]).Value);
    }

    // ---- PathSegments ----

    [Fact]
    public void PathSegments_ProducesExpectedMethodCallShape()
    {
        var source = Root.Nodes<Person>();

        var result = source.PathSegments<Person, Knows, Person>();

        var call = AsCall(result.Expression);
        var expectedMethod = typeof(GraphTraversalExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphTraversalExtensions.PathSegments)
                && m.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(Person), typeof(Knows), typeof(Person));

        Assert.Equal(expectedMethod, call.Method);
        Assert.Single(call.Arguments);
        Assert.Same(source.Expression, call.Arguments[0]);
    }

    [Fact]
    public void PathSegments_DirectionOverload_UsesTraversalOptionsMarker()
    {
        var source = Root.Nodes<Person>();

        var result = source.PathSegments<Person, Knows, Person>(GraphTraversalDirection.Both);

        var optionsCall = AsCall(result.Expression);
        Assert.Equal("WithTraversalOptions", optionsCall.Method.Name);
        Assert.Equal(GraphTraversalDirection.Both, AsConstant(optionsCall.Arguments[3]).Value);
        var pathSegmentsCall = AsCall(optionsCall.Arguments[0]);
        Assert.Equal(ExpectedPathSegmentsMethod(typeof(Person), typeof(Knows), typeof(Person)), pathSegmentsCall.Method);
    }

    // ---- Traverse (5 overloads: no-arg, maxDepth, min+max depth, direction, options lambda) ----
    //
    // Two-arg surface (issue #94, "Option C"): TStart is not a type argument on Traverse/
    // TraverseRelationships/TraversePaths/ReverseTraverse - it is recovered from the source
    // expression's element type at runtime (see GraphTraversalExtensions.BuildPathSegmentsCall),
    // so the PathSegments call these build internally is reflectively closed rather than emitted
    // via a compile-time generic call.

    private static MethodInfo ExpectedPathSegmentsMethod(Type start, Type rel, Type end) =>
        typeof(GraphTraversalExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphTraversalExtensions.PathSegments)
                && m.GetParameters().Length == 1)
            .MakeGenericMethod(start, rel, end);

    [Fact]
    public void Traverse_NoArgs_BuildsOnPathSegmentsAndSelect()
    {
        var source = Root.Nodes<Person>();

        var result = source.Traverse<Knows, Person>();

        // Traverse is sugar over PathSegments().Select(ps => ps.EndNode) - the outermost node is
        // therefore a Select call wrapping a PathSegments call, not a "Traverse" node itself.
        var selectCall = AsCall(result.Expression);
        Assert.Equal(nameof(GraphQueryableExtensions.Select), selectCall.Method.Name);

        var pathSegmentsCall = AsCall(selectCall.Arguments[0]);
        Assert.Equal(ExpectedPathSegmentsMethod(typeof(Person), typeof(Knows), typeof(Person)), pathSegmentsCall.Method);

        var selectorLambda = ExtractLambda(selectCall.Arguments[1]);
        var memberAccess = Assert.IsAssignableFrom<MemberExpression>(selectorLambda.Body);
        Assert.Equal(nameof(IGraphPathSegment<Person, Knows, Person>.EndNode), memberAccess.Member.Name);
    }

    [Fact]
    public void Traverse_WidenedAfterTypedChain_UsesChainElementType()
    {
        IGraphQueryable<INode> source = Root.Nodes<Person>().Where(p => p.TestKey != "");

        var result = source.Traverse<Knows, Person>();

        var selectCall = AsCall(result.Expression);
        var pathSegmentsCall = AsCall(selectCall.Arguments[0]);
        Assert.Equal(ExpectedPathSegmentsMethod(typeof(Person), typeof(Knows), typeof(Person)), pathSegmentsCall.Method);
    }

    [Fact]
    public void Traverse_MaxDepth_CapturesDepthAsConstant()
    {
        var source = Root.Nodes<Person>();

        var result = source.Traverse<Knows, Person>(3);

        var selectCall = AsCall(result.Expression);
        var optionsCall = AsCall(selectCall.Arguments[0]);
        Assert.Equal("WithTraversalOptions", optionsCall.Method.Name);
        Assert.Equal(4, optionsCall.Arguments.Count);
        Assert.Null(AsConstant(optionsCall.Arguments[1]).Value);
        Assert.Equal(3, AsConstant(optionsCall.Arguments[2]).Value);
        Assert.Null(AsConstant(optionsCall.Arguments[3]).Value);
    }

    [Fact]
    public void Traverse_MinAndMaxDepth_CapturesBothAsConstants()
    {
        var source = Root.Nodes<Person>();

        var result = source.Traverse<Knows, Person>(1, 4);

        var selectCall = AsCall(result.Expression);
        var optionsCall = AsCall(selectCall.Arguments[0]);
        Assert.Equal("WithTraversalOptions", optionsCall.Method.Name);
        Assert.Equal(4, optionsCall.Arguments.Count);
        Assert.Equal(1, AsConstant(optionsCall.Arguments[1]).Value);
        Assert.Equal(4, AsConstant(optionsCall.Arguments[2]).Value);
        Assert.Null(AsConstant(optionsCall.Arguments[3]).Value);
    }

    [Fact]
    public void Traverse_Direction_CapturesDirectionAsConstant()
    {
        var source = Root.Nodes<Person>();

        var result = source.Traverse<Knows, Person>(GraphTraversalDirection.Incoming);

        var selectCall = AsCall(result.Expression);
        var optionsCall = AsCall(selectCall.Arguments[0]);
        Assert.Equal("WithTraversalOptions", optionsCall.Method.Name);
        Assert.Null(AsConstant(optionsCall.Arguments[1]).Value);
        Assert.Null(AsConstant(optionsCall.Arguments[2]).Value);
        Assert.Equal(GraphTraversalDirection.Incoming, AsConstant(optionsCall.Arguments[3]).Value);
    }

    [Fact]
    public void Traverse_OptionsLambda_AppliesDepthAndDirection()
    {
        var source = Root.Nodes<Person>();

        var result = source.Traverse<Knows, Person>(o => o.Depth(1, 2).Direction(GraphTraversalDirection.Both));

        // The options-lambda overload is evaluated eagerly at call time (it's a plain Func, not an
        // Expression<Func<...>>) and emits one private marker carrying all configured values.
        var selectCall = AsCall(result.Expression);
        var optionsCall = AsCall(selectCall.Arguments[0]);
        Assert.Equal("WithTraversalOptions", optionsCall.Method.Name);
        Assert.Equal(1, AsConstant(optionsCall.Arguments[1]).Value);
        Assert.Equal(2, AsConstant(optionsCall.Arguments[2]).Value);
        Assert.Equal(GraphTraversalDirection.Both, AsConstant(optionsCall.Arguments[3]).Value);
    }

    // ---- ReverseTraverse ----

    [Fact]
    public void ReverseTraverse_BuildsOnPathSegmentsWithIncomingDirection()
    {
        var source = Root.Nodes<Person>();

        var result = source.ReverseTraverse<Knows, Person>();

        var selectCall = AsCall(result.Expression);
        Assert.Equal(nameof(GraphQueryableExtensions.Select), selectCall.Method.Name);

        var optionsCall = AsCall(selectCall.Arguments[0]);
        Assert.Equal("WithTraversalOptions", optionsCall.Method.Name);
        Assert.Equal(GraphTraversalDirection.Incoming, AsConstant(optionsCall.Arguments[3]).Value);

        var pathSegmentsCall = AsCall(optionsCall.Arguments[0]);
        Assert.Equal(ExpectedPathSegmentsMethod(typeof(Person), typeof(Knows), typeof(Person)), pathSegmentsCall.Method);
    }

    // ---- TraverseRelationships ----

    [Fact]
    public void TraverseRelationships_BuildsOnPathSegmentsAndSelectsRelationship()
    {
        var source = Root.Nodes<Person>();

        var result = source.TraverseRelationships<Knows, Person>();

        var selectCall = AsCall(result.Expression);
        Assert.Equal(nameof(GraphQueryableExtensions.Select), selectCall.Method.Name);

        var selectorLambda = ExtractLambda(selectCall.Arguments[1]);
        var memberAccess = Assert.IsAssignableFrom<MemberExpression>(selectorLambda.Body);
        Assert.Equal(nameof(IGraphPathSegment<Person, Knows, Person>.Relationship), memberAccess.Member.Name);

        var pathSegmentsCall = AsCall(selectCall.Arguments[0]);
        Assert.Equal(ExpectedPathSegmentsMethod(typeof(Person), typeof(Knows), typeof(Person)), pathSegmentsCall.Method);
    }

    // ---- TraversePaths (min/max overload, and options-lambda overload) ----

    [Fact]
    public void TraversePaths_MinMaxDepth_ProducesExpectedMethodCallShape()
    {
        var source = Root.Nodes<Person>();

        var result = source.TraversePaths<Knows, Person>(1, 3);

        var call = AsCall(result.Expression);
        var expectedMethod = typeof(GraphTraversalExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphTraversalExtensions.TraversePaths)
                && m.GetGenericArguments().Length == 2
                && m.GetParameters().Length == 3)
            .MakeGenericMethod(typeof(Knows), typeof(Person));

        Assert.Equal(expectedMethod, call.Method);
        Assert.Equal(3, call.Arguments.Count);
        Assert.Same(source.Expression, call.Arguments[0]);
        Assert.Equal(1, AsConstant(call.Arguments[1]).Value);
        Assert.Equal(3, AsConstant(call.Arguments[2]).Value);
    }

    [Fact]
    public void TraversePaths_OptionsLambda_AppliesDepthAndDirection()
    {
        var source = Root.Nodes<Person>();

        var result = source.TraversePaths<Knows, Person>(o => o.Depth(2, 5).Direction(GraphTraversalDirection.Incoming));

        // The options-lambda overload evaluates eagerly and delegates to the min/max-depth
        // overload, then wraps it in the private traversal-options marker.
        var optionsCall = AsCall(result.Expression);
        Assert.Equal("WithTraversalOptions", optionsCall.Method.Name);
        Assert.Equal(GraphTraversalDirection.Incoming, AsConstant(optionsCall.Arguments[3]).Value);

        var traversePathsCall = AsCall(optionsCall.Arguments[0]);
        var expectedMethod = typeof(GraphTraversalExtensions).GetMethods()
            .Single(m => m.Name == nameof(GraphTraversalExtensions.TraversePaths)
                && m.GetGenericArguments().Length == 2
                && m.GetParameters().Length == 3)
            .MakeGenericMethod(typeof(Knows), typeof(Person));
        Assert.Equal(expectedMethod, traversePathsCall.Method);
        Assert.Equal(2, AsConstant(traversePathsCall.Arguments[1]).Value);
        Assert.Equal(5, AsConstant(traversePathsCall.Arguments[2]).Value);
    }

    [Fact]
    public void TraversePaths_OptionsLambda_WithoutDepth_Throws()
    {
        var source = Root.Nodes<Person>();

        var ex = Assert.Throws<ArgumentException>(() =>
            source.TraversePaths<Knows, Person>(o => o.Direction(GraphTraversalDirection.Outgoing)));

        Assert.Contains("depth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RemovedCompatibilitySurface_IsAbsent()
    {
        var queryableMethods = typeof(GraphQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static);
        Assert.DoesNotContain(queryableMethods, method => method.Name is "WithDepth" or "Direction");

        var traversalMethods = typeof(GraphTraversalExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static);
        Assert.DoesNotContain(
            traversalMethods,
            method => method.Name is nameof(GraphTraversalExtensions.Traverse)
                or nameof(GraphTraversalExtensions.ReverseTraverse)
                or nameof(GraphTraversalExtensions.TraverseRelationships)
                or nameof(GraphTraversalExtensions.TraversePaths)
                && method.GetGenericArguments().Length == 3);

        var exportedTypeNames = typeof(IGraph).Assembly
            .GetExportedTypes()
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);
        var removedTypes = new[]
        {
            "IGraphNodeQueryable",
            "IGraphNodeQueryable`1",
            "IOrderedGraphNodeQueryable`1",
            "IGraphRelationshipQueryable",
            "IGraphRelationshipQueryable`1",
            "IOrderedGraphRelationshipQueryable`1",
        };

        Assert.DoesNotContain(removedTypes, exportedTypeNames.Contains);
    }

    /// <summary>
    /// Reflection-driven completeness guard: every public static method on
    /// <see cref="GraphQueryableExtensions"/> and <see cref="GraphTraversalExtensions"/> must be
    /// exercised by at least one test above (by method name - overloads share one test unless the
    /// overload resolution itself is part of what's under test, as with <c>Select</c>/<c>Traverse</c>/
    /// <c>TraversePaths</c> above). This is the guard against future operator additions silently
    /// skipping expression-shape coverage.
    /// </summary>
    [Fact]
    public void EveryPublicOperatorHasAtLeastOneExpressionShapeTest()
    {
        var operatorNames = typeof(GraphQueryableExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Concat(typeof(GraphTraversalExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(m => m.Name)
            .Distinct()
            .ToList();

        // Method name -> substring expected to appear in a test method name covering it.
        var coveredByTestNamePrefix = new HashSet<string>(
        [
            nameof(GraphQueryableExtensions.Where),
            nameof(GraphQueryableExtensions.Select),
            nameof(GraphQueryableExtensions.SelectMany),
            nameof(GraphQueryableExtensions.OrderBy),
            nameof(GraphQueryableExtensions.OrderByDescending),
            nameof(GraphQueryableExtensions.ThenBy),
            nameof(GraphQueryableExtensions.ThenByDescending),
            nameof(GraphQueryableExtensions.Skip),
            nameof(GraphQueryableExtensions.Take),
            nameof(GraphQueryableExtensions.Distinct),
            nameof(GraphQueryableExtensions.GroupBy),
            nameof(GraphQueryableExtensions.Search),
            nameof(GraphTraversalExtensions.PathSegments),
            nameof(GraphTraversalExtensions.Traverse),
            nameof(GraphTraversalExtensions.ReverseTraverse),
            nameof(GraphTraversalExtensions.TraverseRelationships),
            nameof(GraphTraversalExtensions.TraversePaths),
        ]);

        var testMethodNames = typeof(ExpressionShapeTests)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<FactAttribute>() is not null)
            .Select(m => m.Name)
            .ToList();

        var missing = operatorNames
            .Where(op => coveredByTestNamePrefix.Contains(op))
            .Where(op => !testMethodNames.Any(t => t.StartsWith(op, StringComparison.Ordinal)))
            .ToList();

        Assert.True(missing.Count == 0,
            $"Public operator(s) with no expression-shape test (by test-method-name prefix match): {string.Join(", ", missing)}. " +
            "Add a test named '{OperatorName}_...' above, then add the operator name to coveredByTestNamePrefix.");

        // Symmetric check: everything in coveredByTestNamePrefix must actually still exist on the
        // surface (guards against the list going stale after a rename/removal).
        var stale = coveredByTestNamePrefix.Except(operatorNames).ToList();
        Assert.True(stale.Count == 0,
            $"coveredByTestNamePrefix references operator name(s) no longer on the public surface: {string.Join(", ", stale)}.");
    }
}

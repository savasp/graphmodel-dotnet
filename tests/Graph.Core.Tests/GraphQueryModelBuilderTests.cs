// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Querying;

[Trait("Area", "QueryModelBuilder")]
public class GraphQueryModelBuilderTests
{
    [Fact]
    public void CoreOperators_ProduceExpectedModelShape()
    {
        var query = Root<Person>()
            .Where(person => person.Age >= 18)
            .OrderBy(person => person.LastName)
            .ThenByDescending(person => person.FirstName)
            .Skip(2)
            .Take(5)
            .Distinct();

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var root = Assert.IsType<NodeRoot>(model.Root);
        Assert.Equal(typeof(Person), root.ElementType);
        Assert.Single(model.Predicates);
        Assert.Equal("src", model.Predicates[0].Alias);
        Assert.Collection(
            model.Ordering,
            ordering => Assert.False(ordering.Descending),
            ordering => Assert.True(ordering.Descending));
        Assert.Equal(2, model.Paging.Skip);
        Assert.Equal(5, model.Paging.Take);
        Assert.True(model.Distinct);
        Assert.Equal(TerminalOperation.ToListOrArray, model.Terminal);

        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void DistinctThenCount_CarriesBothSemantics()
    {
        var source = Root<Person>().Select(person => person.LastName).Distinct();
        var expression = MarkerCall("CountAsyncMarker", [typeof(string)], source.Expression, []);

        var model = GraphQueryModelBuilder.Build(expression);

        Assert.True(model.Distinct);
        Assert.Equal(TerminalOperation.Count, model.Terminal);
        GraphQueryModelValidator.Validate(model);
    }

    [Theory]
    [InlineData("Identity", ProjectionKind.Identity)]
    [InlineData("Scalar", ProjectionKind.Scalar)]
    [InlineData("Anonymous", ProjectionKind.Anonymous)]
    public void Select_ClassifiesProjectionShape(string shape, ProjectionKind expected)
    {
        var source = Root<Person>();
        Expression expression = shape switch
        {
            "Identity" => source.Select(person => person).Expression,
            "Scalar" => source.Select(person => person.FirstName).Expression,
            "Anonymous" => source.Select(person => new { person.FirstName, person.Age }).Expression,
            _ => throw new InvalidOperationException($"Unknown test shape '{shape}'."),
        };

        var model = GraphQueryModelBuilder.Build(expression);

        Assert.NotNull(model.Projection);
        Assert.Equal(expected, model.Projection.Kind);
    }

    [Fact]
    public void PathSegmentsAndModifiers_ProduceTraversalAndPathProjection()
    {
#pragma warning disable CS0618
        var query = Root<Person>()
            .PathSegments<Person, Knows, Company>()
            .Direction(GraphTraversalDirection.Incoming)
            .WithDepth(2, 4);
#pragma warning restore CS0618

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var step = Assert.Single(model.Traversal);
        Assert.Equal("MODEL_BUILDER_KNOWS", step.RelationshipType);
        Assert.Equal(GraphTraversalDirection.Incoming, step.Direction);
        Assert.Equal(new DepthRange(2, 4), step.Depth);
        Assert.Equal(typeof(Company), step.TargetType);
        Assert.NotNull(model.Projection);
        Assert.Equal(ProjectionKind.PathSegment, model.Projection.Kind);

        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void TraversePaths_ProducesVariableDepthTraversal()
    {
        var query = Root<Person>().TraversePaths<Knows, Company>(1, 3);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var step = Assert.Single(model.Traversal);
        Assert.Equal(new DepthRange(1, 3), step.Depth);
        Assert.Equal(typeof(Company), step.TargetType);
    }

    [Fact]
    public void TraversalModifier_UpdatesGraphTraversalWhenComplexNavigationFollowsIt()
    {
#pragma warning disable CS0618
        var query = Root<Person>()
            .PathSegments<Person, Knows, Company>()
            .Where(segment => segment.EndNode.Headquarters.City == "Seattle")
            .WithDepth(2, 4);
#pragma warning restore CS0618

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.Collection(
            model.Traversal,
            graphStep => Assert.Equal(new DepthRange(2, 4), graphStep.Depth),
            propertyStep =>
            {
                Assert.Equal(
                    GraphDataModel.PropertyNameToRelationshipTypeName(nameof(Company.Headquarters)),
                    propertyStep.RelationshipType);
                Assert.Equal(new DepthRange(1, 1), propertyStep.Depth);
            });
    }

    [Fact]
    public void Search_ReplacesNodeRootWithSearchRoot()
    {
        var query = Root<Person>().Search("engineer");

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var root = Assert.IsType<SearchRoot>(model.Root);
        Assert.Equal("engineer", root.Query);
        Assert.Equal(SearchRootTarget.Nodes, root.Target);
        Assert.Equal(typeof(Person), root.ElementType);
    }

    [Fact]
    public void RelationshipQuery_ProducesRelationshipRoot()
    {
        var model = GraphQueryModelBuilder.Build(Root<Knows>().Expression);

        Assert.Equal(typeof(Knows), Assert.IsType<RelationshipRoot>(model.Root).ElementType);
    }

    [Fact]
    public void ComplexPropertyAccess_ProducesPropertyTraversalStep()
    {
        var query = Root<Person>().Where(person => person.Home.City == "Seattle");

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var step = Assert.Single(model.Traversal);
        Assert.Equal(GraphDataModel.PropertyNameToRelationshipTypeName(nameof(Person.Home)), step.RelationshipType);
        Assert.Equal(new DepthRange(1, 1), step.Depth);
        Assert.Equal(typeof(Address), step.TargetType);

        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void ComplexPropertyAccess_ProducesArbitraryDepthTraversalSteps()
    {
        var query = Root<Person>().Where(person => person.Home.Region.Name == "Northwest");

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.Collection(
            model.Traversal,
            home => Assert.Equal("Home", home.RelationshipType),
            region => Assert.Equal("Region", region.RelationshipType));
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void ComplexPropertyAccess_UsesRelationshipTypeOverride()
    {
        var query = Root<Person>().Where(person => person.Mailing.City == "Seattle");

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.Equal("MAILING_ADDRESS", Assert.Single(model.Traversal).RelationshipType);
    }

    [Fact]
    public void ComplexCollectionPredicate_ProducesPropertyTraversalStep()
    {
        var query = Root<Person>().Where(person => person.Offices.Any(office => office.City == "Seattle"));

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.Equal("Offices", Assert.Single(model.Traversal).RelationshipType);
        GraphQueryModelValidator.Validate(model);
    }

    [Theory]
    [InlineData("ToListAsyncMarker", TerminalOperation.ToListOrArray)]
    [InlineData("FirstAsyncMarker", TerminalOperation.First)]
    [InlineData("SingleAsyncMarker", TerminalOperation.Single)]
    [InlineData("AnyAsyncMarker", TerminalOperation.Any)]
    [InlineData("CountAsyncMarker", TerminalOperation.Count)]
    [InlineData("ContainsAsyncMarker", TerminalOperation.Contains)]
    [InlineData("ElementAtAsyncMarker", TerminalOperation.ElementAt)]
    [InlineData("ElementAtOrDefaultAsyncMarker", TerminalOperation.ElementAtOrDefault)]
    public void TerminalMarkers_MapToTerminalOperation(string markerName, TerminalOperation expected)
    {
        var source = Root<Person>();
        var additionalArguments = markerName switch
        {
            "ContainsAsyncMarker" => new Expression[] { Expression.Constant(new Person()) },
            "ElementAtAsyncMarker" or "ElementAtOrDefaultAsyncMarker" => [Expression.Constant(3)],
            _ => [],
        };
        var expression = MarkerCall(markerName, [typeof(Person)], source.Expression, additionalArguments);

        var model = GraphQueryModelBuilder.Build(expression);

        Assert.Equal(expected, model.Terminal);
        if (expected is TerminalOperation.ElementAt or TerminalOperation.ElementAtOrDefault)
        {
            // The index is a first-class terminal operand, not a paging encoding.
            Assert.Equal(3, model.TerminalOperand);
            Assert.Null(model.Paging.Skip);
            Assert.Null(model.Paging.Take);
        }
    }

    [Theory]
    [InlineData("FirstAsyncMarker", TerminalOperation.First)]
    [InlineData("SingleAsyncMarker", TerminalOperation.Single)]
    [InlineData("AnyAsyncMarker", TerminalOperation.Any)]
    [InlineData("AllAsyncMarker", TerminalOperation.All)]
    [InlineData("CountAsyncMarker", TerminalOperation.Count)]
    public void PredicateTerminalMarkers_CarryPredicate(string markerName, TerminalOperation expected)
    {
        var source = Root<Person>();
        Expression<Func<Person, bool>> predicate = person => person.Age >= 18;
        var expression = MarkerCall(
            markerName,
            [typeof(Person)],
            source.Expression,
            [Expression.Quote(predicate)]);

        var model = GraphQueryModelBuilder.Build(expression);

        Assert.Equal(expected, model.Terminal);
        Assert.Same(predicate, Assert.Single(model.Predicates).Predicate);
    }

    [Theory]
    [InlineData("SumAsyncMarker", TerminalOperation.Sum)]
    [InlineData("AverageAsyncMarker", TerminalOperation.Average)]
    [InlineData("MinAsyncMarker", TerminalOperation.Min)]
    [InlineData("MaxAsyncMarker", TerminalOperation.Max)]
    public void AggregateSelectorMarkers_CarrySelector(string markerName, TerminalOperation expected)
    {
        var source = Root<Person>();
        Expression<Func<Person, int>> selector = person => person.Age;
        var expression = MarkerCall(
            markerName,
            [typeof(Person), typeof(int)],
            source.Expression,
            [Expression.Quote(selector)]);

        var model = GraphQueryModelBuilder.Build(expression);

        Assert.Equal(expected, model.Terminal);
        Assert.NotNull(model.Projection);
        Assert.Same(selector, model.Projection.Selector);
    }

    [Fact]
    public void LastWithoutOrdering_ThrowsActionableException()
    {
        var source = Root<Person>();
        var expression = MarkerCall("LastAsyncMarker", [typeof(Person)], source.Expression, []);

        var exception = Assert.Throws<GraphQueryTranslationException>(() => GraphQueryModelBuilder.Build(expression));

        Assert.Contains("requires an explicit OrderBy", exception.Message);
    }

    [Fact]
    public void LastWithOrdering_ProducesLastTerminal()
    {
        var source = Root<Person>().OrderBy(person => person.LastName);
        var expression = MarkerCall("LastAsyncMarker", [typeof(Person)], source.Expression, []);

        var model = GraphQueryModelBuilder.Build(expression);

        Assert.Equal(TerminalOperation.Last, model.Terminal);
    }

    [Fact]
    public void UnsupportedOperatorAfterTraversePaths_ThrowsAtChokePoint()
    {
        var query = Root<Person>()
            .TraversePaths<Knows, Company>(1, 3)
            .Where(path => path.Segments.Count > 0);

        var exception = Assert.Throws<GraphQueryTranslationException>(() => GraphQueryModelBuilder.Build(query.Expression));

        Assert.Contains("chained after 'TraversePaths", exception.Message);
        Assert.Contains("Materialize the paths first", exception.Message);
    }

    [Fact]
    public void SelectMany_ProducesFlatteningFragment()
    {
        var query = Root<Person>().SelectMany(person => person.Nicknames);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.NotNull(model.SelectMany);
        Assert.Equal(typeof(Person), Assert.Single(model.SelectMany.CollectionSelector.Parameters).Type);
        Assert.Null(model.SelectMany.ResultSelector);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void GroupBy_ProducesGroupingFragment()
    {
        var query = Root<Person>().GroupBy(person => person.Age);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.NotNull(model.GroupBy);
        Assert.Equal(typeof(int), model.GroupBy.KeySelector.ReturnType);
        Assert.Null(model.GroupBy.ElementSelector);
        Assert.Null(model.GroupBy.ResultSelector);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void GroupBy_WithElementAndResultSelectors_CarriesBoth()
    {
        var query = Root<Person>().AsQueryable().GroupBy(
            person => person.Age,
            person => person.FirstName,
            (age, names) => age);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.NotNull(model.GroupBy);
        Assert.NotNull(model.GroupBy.ElementSelector);
        Assert.Equal(typeof(string), model.GroupBy.ElementSelector.ReturnType);
        Assert.NotNull(model.GroupBy.ResultSelector);
        Assert.Equal(2, model.GroupBy.ResultSelector.Parameters.Count);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void GroupBy_WithResultSelectorOnly_ClassifiesByArity()
    {
        var query = Root<Person>().AsQueryable().GroupBy(
            person => person.Age,
            (age, people) => age);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.NotNull(model.GroupBy);
        Assert.Null(model.GroupBy.ElementSelector);
        Assert.NotNull(model.GroupBy.ResultSelector);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void Union_ProducesUnionFragmentWithBothBoundaryModels()
    {
        var query = Root<Person>().AsQueryable().Union(Root<Person>().Where(person => person.Age > 30));

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.NotNull(model.Union);
        Assert.Equal(typeof(Person), Assert.IsType<NodeRoot>(model.Union.First.Root).ElementType);
        Assert.Equal(typeof(Person), Assert.IsType<NodeRoot>(model.Union.Second.Root).ElementType);
        Assert.Equal(typeof(Person), model.Union.ElementType);
        Assert.Single(model.Union.Second.Predicates);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void Union_WithProjectedUnrelatedRoots_ValidatesAtStringElementBoundary()
    {
        var query = Root<Person>().Select(person => person.Id)
            .Union(Root<Company>().Select(company => company.Id));

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.Equal(typeof(string), model.Union?.ElementType);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void ChainedUnion_ThrowsInsteadOfProducingLossyModel()
    {
        var query = Root<Person>().AsQueryable()
            .Union(Root<Person>())
            .Union(Root<Person>());

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            GraphQueryModelBuilder.Build(query.Expression));

        Assert.Contains("Union", exception.Message);
    }

    [Fact]
    public void Join_ProducesEquijoinFragment()
    {
        var query = Root<Person>().AsQueryable().Join(
            Root<Company>(),
            person => person.CompanyId,
            company => company.Id,
            (person, company) => company);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.NotNull(model.Join);
        Assert.IsType<NodeRoot>(model.Join.InnerRoot);
        Assert.Equal(typeof(string), model.Join.OuterKeySelector.ReturnType);
        Assert.Equal(typeof(string), model.Join.InnerKeySelector.ReturnType);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void Join_WithComposedInnerSource_ThrowsInsteadOfDroppingOperators()
    {
        var query = Root<Person>().AsQueryable().Join(
            Root<Company>().Where(company => company.Id != ""),
            person => person.CompanyId,
            company => company.Id,
            (person, company) => company);

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            GraphQueryModelBuilder.Build(query.Expression));

        Assert.Contains("bare node or relationship set", exception.Message);
    }

    [Fact]
    public void Join_WithPagedInnerSource_ThrowsInsteadOfDroppingOperators()
    {
        var query = Root<Person>().AsQueryable().Join(
            Root<Company>().Take(3),
            person => person.CompanyId,
            company => company.Id,
            (person, company) => company);

        Assert.Throws<GraphQueryTranslationException>(() => GraphQueryModelBuilder.Build(query.Expression));
    }

    [Fact]
    public void Join_WithUnionInnerSource_ThrowsInsteadOfDroppingUnion()
    {
        var inner = Root<Company>().AsQueryable().Union(Root<Company>());
        var query = Root<Person>().AsQueryable().Join(
            inner,
            person => person.CompanyId,
            company => company.Id,
            (person, company) => company);

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            GraphQueryModelBuilder.Build(query.Expression));

        Assert.Contains("bare node or relationship set", exception.Message);
    }

    [Fact]
    public void OrderingBeforeSearch_ProducesAValidSearchModel()
    {
        var query = Root<Person>().OrderBy(person => person.LastName).Search("engineer");

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.IsType<SearchRoot>(model.Root);
        Assert.Equal("src", Assert.Single(model.Ordering).Alias);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void IndexedWhere_ThrowsActionableException()
    {
        var query = Root<Person>().AsQueryable().Where((person, index) => index < 5);

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            GraphQueryModelBuilder.Build(query.Expression));

        Assert.Contains("indexed", exception.Message);
    }

    [Fact]
    public void IndexedSelect_ThrowsActionableException()
    {
        var query = Root<Person>().AsQueryable().Select((person, index) => person.FirstName);

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            GraphQueryModelBuilder.Build(query.Expression));

        Assert.Contains("indexed", exception.Message);
    }

    [Fact]
    public void ChainedSelect_ComposesProjectionsOverTheRootParameter()
    {
        var query = Root<Person>().Select(person => person.FirstName).Select(name => name.Length);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var projection = model.Projection ?? throw new InvalidOperationException("Expected a projection.");
        Assert.Equal(ProjectionKind.Scalar, projection.Kind);
        var selector = projection.Selector ?? throw new InvalidOperationException("Expected a selector.");
        Assert.Equal(typeof(Person), Assert.Single(selector.Parameters).Type);
        Assert.Equal(typeof(int), selector.ReturnType);
    }

    [Fact]
    public void SelectAfterJoin_ThrowsInsteadOfDiscardingResultSelector()
    {
        var query = Root<Person>().AsQueryable().Join(
                Root<Company>(),
                person => person.CompanyId,
                company => company.Id,
                (person, company) => company)
            .Select(company => company.Id);

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            GraphQueryModelBuilder.Build(query.Expression));

        Assert.Contains("compose chained Select", exception.Message);
    }

    [Fact]
    public void WithDepth_InvalidRange_ThrowsTranslationException()
    {
#pragma warning disable CS0618
        var query = Root<Person>()
            .PathSegments<Person, Knows, Company>()
            .WithDepth(0);
#pragma warning restore CS0618

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            GraphQueryModelBuilder.Build(query.Expression));

        Assert.Contains("depth range [1..0] is invalid", exception.Message);
    }

    [Fact]
    public void SearchAfterTraversal_ProducesSearchFilterOnCurrentScope()
    {
#pragma warning disable CS0618
        var query = Root<Person>()
            .PathSegments<Person, Knows, Company>()
            .Search("engineer");
#pragma warning restore CS0618

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.IsType<NodeRoot>(model.Root);
        var filter = Assert.IsType<SearchRoot>(model.SearchFilter);
        Assert.Equal("engineer", filter.Query);
        Assert.Equal(SearchRootTarget.Nodes, filter.Target);
        Assert.Equal(typeof(Company), filter.ElementType);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void ExpressionDepthLimit_ThrowsActionableException()
    {
        var query = Root<Person>();
        for (var i = 0; i < 12; i++)
        {
            query = query.Where(person => person.Age >= 0);
        }

        var options = new GraphQueryModelBuilderOptions(maxNodeCount: 1_000, maxDepth: 10);

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            GraphQueryModelBuilder.Build(query.Expression, options));

        Assert.Contains("maximum depth of 10", exception.Message);
        Assert.Contains(nameof(GraphQueryModelBuilderOptions.MaxDepth), exception.Message);
    }

    [Fact]
    public void ExpressionNodeLimit_ThrowsActionableException()
    {
        var parameter = Expression.Parameter(typeof(Person), "person");
        Expression body = Expression.Constant(true);
        for (var i = 0; i < 20; i++)
        {
            body = Expression.AndAlso(body, Expression.GreaterThan(
                Expression.Property(parameter, nameof(Person.Age)),
                Expression.Constant(i)));
        }

        var predicate = Expression.Lambda<Func<Person, bool>>(body, parameter);
        var query = Root<Person>().Where(predicate);
        var options = new GraphQueryModelBuilderOptions(maxNodeCount: 25, maxDepth: 100);

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            GraphQueryModelBuilder.Build(query.Expression, options));

        Assert.Contains("maximum of 25 nodes", exception.Message);
        Assert.Contains(nameof(GraphQueryModelBuilderOptions.MaxNodeCount), exception.Message);
    }

    [Fact]
    public void RealisticDeepQuery_PassesDefaultBounds()
    {
        var query = Root<Person>();
        for (var i = 0; i < 40; i++)
        {
            query = query.Where(person => person.Age >= 0);
        }

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.Equal(40, model.Predicates.Count);
    }

    [Fact]
    public void Funcletization_EvaluatesClosureMemberAndParameterlessCall()
    {
        var source = Root<Person>();
        var holder = new LimitHolder { Value = 7 };
        var holderExpression = Expression.Field(
            Expression.Constant(holder),
            nameof(LimitHolder.Value));
        var callExpression = Expression.Call(GetQueryableTakeMethod(), source.Expression, holderExpression);

        var closureModel = GraphQueryModelBuilder.Build(callExpression);

        Assert.Equal(7, closureModel.Paging.Take);

        var methodExpression = Expression.Call(
            GetQueryableTakeMethod(),
            source.Expression,
            Expression.Call(typeof(GraphQueryModelBuilderTests).GetMethod(
                nameof(GetParameterlessLimit),
                BindingFlags.NonPublic | BindingFlags.Static)!));

        var methodModel = GraphQueryModelBuilder.Build(methodExpression);

        Assert.Equal(9, methodModel.Paging.Take);
    }

    [Fact]
    public void Funcletization_RejectsQueryParameterDependentEvaluation()
    {
        var parameter = Expression.Parameter(typeof(int), "queryValue");

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            QueryExpressionEvaluator.Evaluate<int>(parameter, "test value"));

        Assert.Contains("references a query parameter", exception.Message);
        Assert.Contains("parameter-free closure values", exception.Message);
    }

    private static IGraphQueryable<T> Root<T>() => new TestGraphQueryable<T>();

    private static MethodCallExpression MarkerCall(
        string markerName,
        Type[] genericTypes,
        Expression source,
        Expression[] additionalArguments)
    {
        var parameterCount = 1 + additionalArguments.Length;
        var definition = typeof(QueryTerminals)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method =>
                method.Name == markerName &&
                method.GetGenericArguments().Length == genericTypes.Length &&
                method.GetParameters().Length == parameterCount);
        var method = definition.MakeGenericMethod(genericTypes);

        return Expression.Call(method, [source, .. additionalArguments]);
    }

    private static MethodInfo GetQueryableTakeMethod() => typeof(Queryable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method =>
            method.Name == nameof(Queryable.Take) &&
            method.IsGenericMethodDefinition &&
            method.GetParameters() is [_, { ParameterType: { } countType }] &&
            countType == typeof(int))
        .MakeGenericMethod(typeof(Person));

    private static int GetParameterlessLimit() => 9;

    private sealed class LimitHolder
    {
        public int Value;
    }

    private sealed record Address
    {
        public string City { get; init; } = string.Empty;

        public Region Region { get; init; } = new();
    }

    private sealed record Region
    {
        public string Name { get; init; } = string.Empty;
    }

    [Node("MODEL_BUILDER_PERSON")]
    private sealed record Person : Node
    {
        public string FirstName { get; init; } = string.Empty;

        public string LastName { get; init; } = string.Empty;

        public int Age { get; init; }

        public string CompanyId { get; init; } = string.Empty;

        public IReadOnlyList<string> Nicknames { get; init; } = [];

        public Address Home { get; init; } = new();

        [ComplexProperty(RelationshipType = "MAILING_ADDRESS")]
        public Address Mailing { get; init; } = new();

        public IReadOnlyList<Address> Offices { get; init; } = [];
    }

    [Relationship(Label = "MODEL_BUILDER_KNOWS")]
    private sealed record Knows(string Start, string End) : Relationship(Start, End);

    [Node("MODEL_BUILDER_COMPANY")]
    private sealed record Company : Node
    {
        public Address Headquarters { get; init; } = new();
    }

    private sealed class TestGraphQueryable<T> : IOrderedGraphQueryable<T>
    {
        public TestGraphQueryable()
        {
            Provider = new TestGraphQueryProvider();
            Expression = Expression.Constant(this);
        }

        public TestGraphQueryable(TestGraphQueryProvider provider, Expression expression)
        {
            Provider = provider;
            Expression = expression;
        }

        public Type ElementType => typeof(T);

        public Expression Expression { get; }

        public IGraphQueryProvider Provider { get; }

        IQueryProvider IQueryable.Provider => Provider;

        public IGraph Graph => null!;

        public IEnumerator<T> GetEnumerator() => Enumerable.Empty<T>().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class TestGraphQueryProvider : IGraphQueryProvider
    {
        public IGraph Graph => null!;

        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = ExtensionUtils.GetQueryableElementType(expression.Type);
            return (IQueryable)Activator.CreateInstance(
                typeof(TestGraphQueryable<>).MakeGenericType(elementType),
                this,
                expression)!;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
            new TestGraphQueryable<TElement>(this, expression);

        IGraphQueryable<TElement> IGraphQueryProvider.CreateQuery<TElement>(Expression expression) =>
            new TestGraphQueryable<TElement>(this, expression);

        public object? Execute(Expression expression) =>
            throw new NotSupportedException();

        public TResult Execute<TResult>(Expression expression) =>
            throw new NotSupportedException();

        public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<object?> ExecuteAsync(Expression expression, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

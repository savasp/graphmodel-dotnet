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
        Assert.False(model.Distinct);
        Assert.True(Assert.IsType<PostPagingStage>(model.PostPaging).Distinct);
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

    [Fact]
    public void LabelFilters_ProduceScopedAnyAndAllFragments()
    {
        var query = Root<Person>()
            .OfLabel("Employee")
            .OfLabels(GraphLabelMatch.Any, "Manager", "Contractor");

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.Collection(
            model.LabelFilters,
            filter =>
            {
                Assert.Equal("src", filter.Alias);
                Assert.Equal(GraphLabelMatch.All, filter.Match);
                Assert.Equal(["Employee"], filter.Labels);
            },
            filter =>
            {
                Assert.Equal("src", filter.Alias);
                Assert.Equal(GraphLabelMatch.Any, filter.Match);
                Assert.Equal(["Manager", "Contractor"], filter.Labels);
            });
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void EmptyLabelFilter_IsAnIdentityOperation()
    {
        var source = Root<Person>();

        var query = source.OfLabels(GraphLabelMatch.All);

        Assert.Same(source, query);
    }

    [Fact]
    public void LabelFilters_RejectInvalidLabelsAndMatchModes()
    {
        var source = Root<Person>();

        Assert.Throws<ArgumentException>(() => source.OfLabel(" "));
        Assert.Throws<ArgumentException>(() => source.OfLabels(GraphLabelMatch.Any, "Person", ""));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            source.OfLabels((GraphLabelMatch)int.MaxValue, "Person"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LabelFilter_AfterTraverse_ReportsTheImplicitTraversalBoundary(bool useMultipleLabels)
    {
        var traversed = Root<Person>().Traverse<Knows, Person>();
        var query = useMultipleLabels
            ? traversed.OfLabels(GraphLabelMatch.Any, "Person", "Manager")
            : traversed.OfLabel("Person");

        var exception = Assert.Throws<GraphException>(() =>
            GraphQueryModelValidator.Validate(GraphQueryModelBuilder.Build(query.Expression)));

        Assert.Equal(
            "OfLabel and OfLabels cannot be applied after Traverse. Apply label filters to the source before traversal.",
            exception.Message);
    }

    [Theory]
    [InlineData("Select")]
    [InlineData("Skip")]
    [InlineData("Take")]
    public void LabelFilter_AfterExplicitSequenceBoundary_ReportsThatBoundary(string boundary)
    {
        var source = Root<Person>();
        var query = boundary switch
        {
            "Select" => source.Select(person => person),
            "Skip" => source.Skip(5),
            "Take" => source.Take(5),
            _ => throw new InvalidOperationException($"Unknown boundary '{boundary}'."),
        };

        var exception = Assert.Throws<GraphException>(() =>
            GraphQueryModelValidator.Validate(GraphQueryModelBuilder.Build(query.OfLabel("Person").Expression)));

        var semanticBoundary = boundary == "Select" ? "projection" : "paging";
        Assert.Equal(
            $"OfLabel and OfLabels must be applied before {boundary} so label filtering cannot be silently " +
            $"moved across a {semanticBoundary} boundary.",
            exception.Message);
    }

    [Theory]
    [InlineData("Traverse")]
    [InlineData("Select")]
    [InlineData("Skip")]
    [InlineData("Take")]
    public void WhereHasRelationship_AfterSequenceBoundary_ReportsThatBoundary(string boundary)
    {
        var source = Root<Person>();
        var query = boundary switch
        {
            "Traverse" => source.Traverse<Knows, Person>(),
            "Select" => source.Select(person => person),
            "Skip" => source.Skip(5),
            "Take" => source.Take(5),
            _ => throw new InvalidOperationException($"Unknown boundary '{boundary}'."),
        };
        var filtered = query.WhereHasRelationship<Person, Knows>(GraphTraversalDirection.Outgoing);

        var exception = Assert.Throws<GraphException>(() =>
            GraphQueryModelValidator.Validate(GraphQueryModelBuilder.Build(filtered.Expression)));

        var expected = boundary switch
        {
            "Traverse" =>
                "WhereHasRelationship cannot be applied after Traverse. Apply the relationship-existence filter " +
                "to the source before traversal.",
            "Select" =>
                "WhereHasRelationship must be applied before Select so relationship existence cannot be silently " +
                "moved across a projection boundary.",
            _ =>
                $"WhereHasRelationship must be applied before {boundary} so relationship existence cannot be " +
                "silently moved across a paging boundary.",
        };
        Assert.Equal(expected, exception.Message);
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
    public void TraverseOptions_ProduceTraversalAndPathProjection()
    {
        var query = Root<Person>()
            .Traverse<Knows, Company>(options => options
                .Direction(GraphTraversalDirection.Incoming)
                .Depth(2, 4));

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
    public void ShortestPaths_ProduceUnboundedPathTraversalAndEndpointPredicate()
    {
        var query = Root<Person>().AllShortestPaths<Knows, Company>(
            company => company.Id != "closed",
            GraphTraversalDirection.Both);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var step = Assert.Single(model.Traversal);
        Assert.Equal(new DepthRange(1, int.MaxValue), step.Depth);
        Assert.Equal(GraphTraversalDirection.Both, step.Direction);
        Assert.Equal(TraversalPathSelection.AllShortest, step.PathSelection);
        Assert.Equal(typeof(Company), Assert.Single(step.TargetPredicates).Predicate.Parameters[0].Type);
        Assert.Equal(
            new QueryPathShape(typeof(Person), typeof(Knows), typeof(Company)),
            model.PathShape);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void OptionalTraverse_ProducesOptionalStepAndNullableTargetProjection()
    {
        var query = Root<Person>()
            .OptionalTraverse<Knows, Company>(GraphTraversalDirection.Incoming);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var step = Assert.Single(model.Traversal);
        Assert.True(step.IsOptional);
        Assert.Equal(GraphTraversalDirection.Incoming, step.Direction);
        Assert.Equal(ProjectionKind.OptionalTraversal, model.Projection?.Kind);
        Assert.Equal(2, model.Projection?.Selector?.Parameters.Count);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void OptionalTraverse_Select_ComposesOverSourceAndNullableTarget()
    {
        var query = Root<Person>()
            .OptionalTraverse<Knows, Company>()
            .Select(result => new { SourceId = result.Source.Id, TargetId = result.Target == null ? null : result.Target.Id });

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.Equal(ProjectionKind.Anonymous, model.Projection?.Kind);
        Assert.Equal(2, model.Projection?.Selector?.Parameters.Count);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void OptionalTraverse_AfterAnotherTraversal_IsRejectedAtTheSharedBoundary()
    {
        var query = Root<Person>()
            .Traverse<Knows, Person>()
            .OptionalTraverse<Knows, Company>();

        var exception = Assert.Throws<GraphException>(() =>
            GraphQueryModelValidator.Validate(GraphQueryModelBuilder.Build(query.Expression)));

        Assert.Contains("optional", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OptionalTraverse_OrderByOverOptionalResult_IsRejectedAtTheSharedBoundary()
    {
        var query = Root<Person>()
            .OptionalTraverse<Knows, Company>()
            .OrderBy(result => result.Source.Id);

        var exception = Assert.Throws<GraphException>(() =>
            GraphQueryModelValidator.Validate(GraphQueryModelBuilder.Build(query.Expression)));

        Assert.Contains("OrderBy after OptionalTraverse", exception.Message);
    }

    [Fact]
    public void OptionalTraverse_DistinctAndAggregateTerminals_AreRejectedAtTheSharedBoundary()
    {
        var distinct = Root<Person>().OptionalTraverse<Knows, Company>().Distinct();
        var count = MarkerCall(
            "CountAsyncMarker",
            [typeof(OptionalTraversalResult<Company>)],
            Root<Person>().OptionalTraverse<Knows, Company>().Expression,
            []);

        Assert.Throws<GraphException>(() =>
            GraphQueryModelValidator.Validate(GraphQueryModelBuilder.Build(distinct.Expression)));
        Assert.Throws<GraphException>(() =>
            GraphQueryModelValidator.Validate(GraphQueryModelBuilder.Build(count)));
    }

    [Fact]
    public void TraversalOptions_RelationshipPredicateIsCarriedByTraversalStep()
    {
        var query = Root<Person>().TraversePaths<Knows, Company>(options => options
            .Depth(1, 3)
            .WhereRelationship<Knows>(relationship => relationship.Id != "blocked"));

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var predicate = Assert.Single(Assert.Single(model.Traversal).RelationshipPredicates);
        Assert.Equal(typeof(Knows), predicate.Predicate.Parameters[0].Type);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void WhereHasRelationship_ProducesExistenceFragmentWithoutTraversal()
    {
        var query = Root<Person>().WhereHasRelationship<Person, Knows>(
            GraphTraversalDirection.Incoming,
            relationship => relationship.Id != "blocked");

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.Empty(model.Traversal);
        var existence = Assert.Single(model.RelationshipExistence);
        Assert.Equal(typeof(Knows), existence.RelationshipType);
        Assert.Equal(GraphTraversalDirection.Incoming, existence.Direction);
        Assert.Equal("src", existence.SourceAlias);
        Assert.NotNull(existence.Predicate);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void WhereHasRelationship_AfterProjectionOrPaging_IsRejectedAtTheSharedBoundary()
    {
        var afterProjection = Root<Person>()
            .Select(person => person)
            .WhereHasRelationship<Person, Knows>(GraphTraversalDirection.Outgoing);
        var afterPaging = Root<Person>()
            .Take(5)
            .WhereHasRelationship<Person, Knows>(GraphTraversalDirection.Outgoing);

        Assert.Throws<GraphException>(() =>
            GraphQueryModelValidator.Validate(GraphQueryModelBuilder.Build(afterProjection.Expression)));
        Assert.Throws<GraphException>(() =>
            GraphQueryModelValidator.Validate(GraphQueryModelBuilder.Build(afterPaging.Expression)));
    }

    [Fact]
    public void TraversalOptions_AreAppliedBeforeComplexNavigationFollows()
    {
        var query = Root<Person>()
            .Traverse<Knows, Company>(options => options.Depth(2, 4))
            .Where(company => company.Headquarters.City == "Seattle");

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
    public void SearchAsTraversalSource_PreservesRootTraversalAndTargetOperators()
    {
        var query = Root<Person>()
            .Search("engineer")
            .Traverse<Knows, Company>(options => options
                .Depth(1, 2)
                .Direction(GraphTraversalDirection.Incoming))
            .Where(company => company.Id != "")
            .OrderBy(company => company.Id)
            .Skip(1)
            .Take(2)
            .Select(company => company.Id);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var root = Assert.IsType<SearchRoot>(model.Root);
        Assert.Equal(SearchRootTarget.Nodes, root.Target);
        Assert.Equal(typeof(Person), root.ElementType);
        var traversal = Assert.Single(model.Traversal);
        Assert.Equal("n", traversal.SourceAlias);
        Assert.Equal("tgt", traversal.TargetAlias);
        Assert.Equal(new DepthRange(1, 2), traversal.Depth);
        Assert.Equal(GraphTraversalDirection.Incoming, traversal.Direction);
        Assert.Equal("tgt", Assert.Single(model.Predicates).Alias);
        Assert.Equal("tgt", Assert.Single(model.Ordering).Alias);
        Assert.Equal(new Paging(1, 2), model.Paging);
        Assert.Equal(typeof(string), model.Projection?.Selector?.ReturnType);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void SearchAsPathSegmentsSource_PreservesSegmentShape()
    {
        var query = Root<Person>()
            .Search("engineer")
            .PathSegments<Person, Knows, Company>();

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var traversal = Assert.Single(model.Traversal);
        Assert.Equal("n", traversal.SourceAlias);
        Assert.Equal("tgt", traversal.TargetAlias);
        Assert.Equal(ProjectionKind.PathSegment, model.Projection?.Kind);
        Assert.Null(model.PathShape);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void SearchAsTraversePathsSource_PreservesPathShape()
    {
        var query = Root<Person>()
            .Search("engineer")
            .TraversePaths<Knows, Company>(options => options
                .Depth(1, 3)
                .Direction(GraphTraversalDirection.Both));

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var traversal = Assert.Single(model.Traversal);
        Assert.Equal("n", traversal.SourceAlias);
        Assert.Equal(new DepthRange(1, 3), traversal.Depth);
        Assert.Equal(GraphTraversalDirection.Both, traversal.Direction);
        Assert.Equal(new QueryPathShape(typeof(Person), typeof(Knows), typeof(Company)), model.PathShape);
        Assert.Null(model.Projection);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void MixedEntitySearchAsTraversalSource_IsRejectedWithPreciseError()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphQueryable<Person>(provider, new MixedSearchRootExpression());
        var query = source.Traverse<Knows, Company>();

        var exception = Assert.Throws<GraphQueryTranslationException>(
            () => GraphQueryModelBuilder.Build(query.Expression));

        Assert.Contains("Mixed-entity", exception.Message, StringComparison.Ordinal);
        Assert.Contains("single typed node scope", exception.Message, StringComparison.Ordinal);
        Assert.Contains("SearchNodes", exception.Message, StringComparison.Ordinal);
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
    public void WhereAfterTraversePaths_BindsPredicateToPathScope()
    {
        var query = Root<Person>()
            .TraversePaths<Knows, Company>(1, 3)
            .Where(path => path.Segments.Count > 0);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var predicate = Assert.Single(model.Predicates);
        Assert.Equal("p", predicate.Alias);
        Assert.Equal(typeof(IGraphPath), Assert.Single(predicate.Predicate.Parameters).Type);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void UnsupportedOperatorAfterTraversePaths_ThrowsAtChokePoint()
    {
        var query = Root<Person>()
            .TraversePaths<Knows, Company>(1, 3)
            .OrderBy(path => path.Segments.Count);

        var exception = Assert.Throws<GraphQueryTranslationException>(
            () => GraphQueryModelBuilder.Build(query.Expression));

        Assert.Contains("'.OrderBy(...)' chained after 'TraversePaths", exception.Message);
        Assert.Contains("Materialize the paths first", exception.Message);
    }

    [Fact]
    public void WhereAfterTakeOnTraversePaths_ThrowsOrderingGuard()
    {
        var query = Root<Person>()
            .TraversePaths<Knows, Company>(1, 3)
            .Take(1)
            .Where(path => path.Segments.Count > 0);

        var exception = Assert.Throws<GraphQueryTranslationException>(
            () => GraphQueryModelBuilder.Build(query.Expression));

        Assert.Contains("must precede", exception.Message);
    }

    [Fact]
    public void WhereAfterSelectOnTraversePaths_ThrowsOrderingGuard()
    {
        var query = Root<Person>()
            .TraversePaths<Knows, Company>(1, 3)
            .Select(path => path.End)
            .Where(node => node.Id != "");

        var exception = Assert.Throws<GraphQueryTranslationException>(
            () => GraphQueryModelBuilder.Build(query.Expression));

        Assert.Contains("must precede", exception.Message);
    }

    [Fact]
    public void IdentitySelectAfterTraversePaths_IsProjectionNoOp()
    {
        var query = Root<Person>()
            .TraversePaths<Knows, Company>(1, 3)
            .Select(path => path);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.Null(model.Projection);
        Assert.NotNull(model.PathShape);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void TakeThenSkip_FoldsPagingWindowExactly()
    {
        // LINQ: Take(2) bounds the window to 2 rows, then Skip(1) consumes one of them.
        var query = Root<Person>().Take(2).Skip(1);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.Equal(1, model.Paging.Skip);
        Assert.Equal(1, model.Paging.Take);
    }

    [Fact]
    public void RepeatedTake_KeepsTighterLimit()
    {
        var query = Root<Person>().Take(2).Take(5);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.Equal(2, model.Paging.Take);
    }

    [Fact]
    public void RepeatedSkip_Accumulates()
    {
        var query = Root<Person>().Skip(1).Skip(2);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.Equal(3, model.Paging.Skip);
    }

    [Fact]
    public void WhereAfterTake_ProducesPostPagingStage()
    {
        var query = Root<Person>().Take(2).Where(person => person.Age > 21);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.Empty(model.Predicates);
        Assert.Equal(2, model.Paging.Take);
        Assert.Single(Assert.IsType<PostPagingStage>(model.PostPaging).Predicates);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void OrderByAndDistinctAfterTake_StayInPostPagingStage()
    {
        var query = Root<Person>().Take(2).Distinct().OrderBy(person => person.Age);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var postPaging = Assert.IsType<PostPagingStage>(model.PostPaging);
        Assert.True(postPaging.Distinct);
        Assert.Single(postPaging.Ordering);
        Assert.False(model.Distinct);
        Assert.Empty(model.Ordering);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void ThirdSequenceStage_ThrowsInsteadOfFlattening()
    {
        var query = Root<Person>()
            .Take(5)
            .Where(person => person.Age > 21)
            .Skip(1)
            .Where(person => person.Age < 65);

        var exception = Assert.Throws<GraphQueryTranslationException>(
            () => GraphQueryModelBuilder.Build(query.Expression));

        Assert.Contains("third sequence stage", exception.Message);
    }

    [Fact]
    public void PredicateTerminalAfterTake_ThrowsInsteadOfReordering()
    {
        var source = Root<Person>().OrderBy(person => person.Age).Take(3);
        Expression<Func<Person, bool>> predicate = person => person.Age >= 3;
        var expression = MarkerCall(
            "CountAsyncMarker",
            [typeof(Person)],
            source.Expression,
            [predicate]);

        var exception = Assert.Throws<GraphQueryTranslationException>(
            () => GraphQueryModelBuilder.Build(expression));

        Assert.Contains("follows Skip/Take", exception.Message);
        Assert.Contains("Materialize", exception.Message);
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
    public void TypedConcat_ProducesBagPreservingSetOperationAndGraphQueryableResult()
    {
        IGraphQueryable<Person> query = Root<Person>().Concat(Root<Person>().Where(person => person.Age > 30));

        var model = GraphQueryModelBuilder.Build(query.Expression);

        Assert.Equal(SetOperationKind.Concat, model.Union?.Operation);
        Assert.Equal(typeof(Person), model.Union?.ElementType);
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

    [Theory]
    [InlineData(SetOperationKind.Union)]
    [InlineData(SetOperationKind.Concat)]
    public void SameKindLeftChainedSetOperation_ProducesRecursiveBinaryModel(SetOperationKind operation)
    {
        var first = Root<Person>();
        var second = Root<Person>().Where(person => person.Age >= 18);
        var third = Root<Person>().Where(person => person.Age >= 65);
        var query = operation == SetOperationKind.Union
            ? first.Union(second).Union(third)
            : first.Concat(second).Concat(third);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var outer = Assert.IsType<UnionFragment>(model.Union);
        Assert.Equal(operation, outer.Operation);
        Assert.Equal(operation, Assert.IsType<UnionFragment>(outer.First.Union).Operation);
        Assert.Null(outer.Second.Union);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void StandardQueryableUnion_LeftChainUsesTheSameRecursiveModel()
    {
        IQueryable<Person> first = Root<Person>();
        IQueryable<Person> second = Root<Person>().Where(person => person.Age >= 18);
        IQueryable<Person> third = Root<Person>().Where(person => person.Age >= 65);
        var query = first.Union(second).Union(third);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var outer = Assert.IsType<UnionFragment>(model.Union);
        Assert.Equal(SetOperationKind.Union, outer.Operation);
        Assert.Equal(SetOperationKind.Union, Assert.IsType<UnionFragment>(outer.First.Union).Operation);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void StandardQueryableConcat_LeftChainUsesTheSameRecursiveModel()
    {
        IQueryable<Person> first = Root<Person>();
        IQueryable<Person> second = Root<Person>().Where(person => person.Age >= 18);
        IQueryable<Person> third = Root<Person>().Where(person => person.Age >= 65);
        var query = first.Concat(second).Concat(third);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var outer = Assert.IsType<UnionFragment>(model.Union);
        Assert.Equal(SetOperationKind.Concat, outer.Operation);
        Assert.Equal(SetOperationKind.Concat, Assert.IsType<UnionFragment>(outer.First.Union).Operation);
        GraphQueryModelValidator.Validate(model);
    }

    [Theory]
    [InlineData(SetOperationKind.Union, SetOperationKind.Concat)]
    [InlineData(SetOperationKind.Concat, SetOperationKind.Union)]
    public void MixedFlatSetOperationChain_ReportsHowToSupplyGrouping(
        SetOperationKind firstOperation,
        SetOperationKind secondOperation)
    {
        var first = Root<Person>();
        var second = Root<Person>();
        var third = Root<Person>();
        var firstPair = Combine(first, second, firstOperation);
        var query = Combine(firstPair, third, secondOperation);

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            GraphQueryModelBuilder.Build(query.Expression));

        Assert.Equal(
            $"Cannot translate 'GraphQueryableExtensions.{secondOperation}': a flat set-operation chain cannot " +
            $"apply {secondOperation} after {firstOperation}. Nest the intended grouping in the second operand " +
            $"(for example, a.{firstOperation}(b.{secondOperation}(c))) or materialize the first set operation.",
            exception.Message);
    }

    [Fact]
    public void ExplicitlyNestedMixedSetOperations_RetainTheirGrouping()
    {
        var first = Root<Person>();
        var second = Root<Person>();
        var third = Root<Person>();
        var query = first.Union(second.Concat(third));

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var outer = Assert.IsType<UnionFragment>(model.Union);
        Assert.Equal(SetOperationKind.Union, outer.Operation);
        Assert.Equal(SetOperationKind.Concat, Assert.IsType<UnionFragment>(outer.Second.Union).Operation);
        GraphQueryModelValidator.Validate(model);
    }

    [Fact]
    public void NonSetOperatorAfterSetOperation_RemainsRejected()
    {
        var query = Root<Person>().Union(Root<Person>()).Where(person => person.Age >= 18);

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            GraphQueryModelBuilder.Build(query.Expression));

        Assert.Equal(
            "Cannot translate 'GraphQueryableExtensions.Where': operators after Union or Concat are not " +
            "supported; materialize the combined query first.",
            exception.Message);
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
    public void Traverse_InvalidDepthRange_ThrowsTranslationException()
    {
        var query = Root<Person>()
            .Traverse<Knows, Company>(0);

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            GraphQueryModelBuilder.Build(query.Expression));

        Assert.Contains("depth range [1..0] is invalid", exception.Message);
    }

    [Fact]
    public void SearchAfterTraversal_ProducesSearchFilterOnCurrentScope()
    {
        var query = Root<Person>()
            .PathSegments<Person, Knows, Company>()
            .Search("engineer");

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

    // The abstraction is intentional: callers reassign composed IGraphQueryable<T> results.
#pragma warning disable CA1859
    private static IGraphQueryable<T> Root<T>() => new TestGraphQueryable<T>();
#pragma warning restore CA1859

    private static IGraphQueryable<T> Combine<T>(
        IGraphQueryable<T> first,
        IGraphQueryable<T> second,
        SetOperationKind operation) => operation switch
        {
            SetOperationKind.Union => first.Union(second),
            SetOperationKind.Concat => first.Concat(second),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

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

    private sealed class MixedSearchRootExpression : Expression, IGraphSearchRootExpression
    {
        public string SearchQuery => "engineer";

        public Type EntityType => typeof(Person);

        public SearchRootTarget Target => SearchRootTarget.Entities;

        public override ExpressionType NodeType => ExpressionType.Extension;

        public override Type Type => typeof(IGraphQueryable<Person>);
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

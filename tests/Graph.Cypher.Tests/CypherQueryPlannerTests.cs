// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Tests;

using System.Linq.Expressions;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Planning;
using Cvoya.Graph.Cypher.Validation;
using Cvoya.Graph.Querying;
using AstBinaryExpression = Cvoya.Graph.Cypher.Ast.Expressions.BinaryExpression;
using AstUnaryExpression = Cvoya.Graph.Cypher.Ast.Expressions.UnaryExpression;

public class CypherQueryPlannerTests
{
    private readonly CypherQueryPlanner planner = new(TestCypherDialect.Full);

    [Fact]
    public void Plan_UnsupportedCapabilityNamesConstructCapabilityAndDialect()
    {
        var limited = new CypherQueryPlanner(new TestCypherDialect(CapabilitySet.Of(), "LimitedCypher"));

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            limited.Plan(Model(root: new SearchRoot("Ada", SearchRootTarget.Nodes, typeof(Person)))));

        Assert.Contains("FullTextSearch", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(GraphCapability.FullTextSearch), exception.Message, StringComparison.Ordinal);
        Assert.Contains("LimitedCypher", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_DialectCanEvaluateParameterFreeTemporalFunctionOnClient()
    {
        Expression<Func<Person, DateTime>> selector = _ => DateTime.UtcNow;
        var dialect = new TestCypherDialect(
            CapabilitySet.All,
            "ClientTemporal",
            new HashSet<string>(StringComparer.Ordinal) { "temporal.datetime" });

        var statement = new CypherQueryPlanner(dialect).Plan(Model(
            projection: new ProjectionShape(ProjectionKind.Scalar, selector)));

        var parameter = Assert.IsType<QueryParameter>(
            Assert.Single(Assert.IsType<ReturnClause>(statement.Clauses[^1]).Items).Expression);
        var value = Assert.IsType<DateTime>(statement.Parameters[parameter.Name]);
        Assert.Equal(DateTimeKind.Utc, value.Kind);
    }

    [Fact]
    public void Plan_UnsupportedFunctionNamesFunctionAndDialect()
    {
        Expression<Func<Person, DateTime>> selector = _ => DateTime.UtcNow;
        var dialect = new TestCypherDialect(
            CapabilitySet.All,
            "NoTemporal",
            unsupportedFunctions: new HashSet<string>(StringComparer.Ordinal) { "temporal.datetime" });

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            new CypherQueryPlanner(dialect).Plan(Model(
                projection: new ProjectionShape(ProjectionKind.Scalar, selector))));

        Assert.Contains("temporal.datetime", exception.Message, StringComparison.Ordinal);
        Assert.Contains("NoTemporal", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_MissingPatternSizeCapabilityFailsAtTranslation()
    {
        Expression<Func<Person, bool>> predicate = person => person.Offices.Count > 1;

        AssertMissingCapability(
            GraphCapability.PatternSizeProjection,
            Model(predicates: [new PredicateFragment(predicate, "src")]));
    }

    [Fact]
    public void Plan_MissingOptionalTraversalCapabilityFailsAtTranslation()
    {
        Expression<Func<Person, bool>> predicate = person => person.Home!.Region.Name == "Northwest";

        AssertMissingCapability(
            GraphCapability.OptionalTraversal,
            Model(predicates: [new PredicateFragment(predicate, "src")]));
    }

    [Fact]
    public void Plan_MissingMultiLabelCapabilityFailsAtTranslation()
    {
        AssertMissingCapability(
            GraphCapability.MultiLabelMatch,
            Model(root: new NodeRoot(typeof(Animal))));
    }

    [Fact]
    public void Plan_MissingOrderByEntityCapabilityFailsAtTranslation()
    {
        Expression<Func<Person, Person>> ordering = person => person;

        AssertMissingCapability(
            GraphCapability.OrderByEntity,
            Model(ordering: [new OrderingKey(ordering, descending: false)]));
    }

    [Fact]
    public void Plan_MissingCallSubqueriesCapabilityFailsAtTranslation()
    {
        // The correlated collection-projection shape (grouping path segments by their start node)
        // lowers to CALL {} subqueries and therefore requires CallSubqueries.
        Expression<Func<IGraphPathSegment<Person, Knows, Person>, Person>> key = segment => segment.StartNode;
        Expression<Func<IGrouping<Person, IGraphPathSegment<Person, Knows, Person>>, object>> projection = group =>
            new { Friends = group.Select(segment => segment.EndNode.Name).ToList() };
        var traversal = new TraversalStep(
            "KNOWS",
            GraphTraversalDirection.Outgoing,
            new Cvoya.Graph.Querying.DepthRange(1, 1),
            [],
            typeof(Person),
            typeof(Knows),
            isComplexPropertyTraversal: false,
            sourceAlias: "src",
            targetAlias: "tgt");

        AssertMissingCapability(
            GraphCapability.CallSubqueries,
            Model(
                traversal: [traversal],
                projection: new ProjectionShape(ProjectionKind.Scalar, projection),
                groupBy: new GroupByFragment(key, null, null)));
    }

    [Fact]
    public void Plan_ValidatesCapabilitiesInsideScopedCallSubquery()
    {
        Expression<Func<IGraphPathSegment<Person, Knows, Person>, Person>> key = segment => segment.StartNode;
        Expression<Func<IGrouping<Person, IGraphPathSegment<Person, Knows, Person>>, object>> projection = group =>
            new
            {
                Friends = group
                    .OrderBy(segment => segment.EndNode)
                    .Select(segment => segment.EndNode.Name)
                    .ToList()
            };
        var traversal = new TraversalStep(
            "KNOWS",
            GraphTraversalDirection.Outgoing,
            new Cvoya.Graph.Querying.DepthRange(1, 1),
            [],
            typeof(Person),
            typeof(Knows),
            isComplexPropertyTraversal: false,
            sourceAlias: "src",
            targetAlias: "tgt");

        AssertMissingCapability(
            GraphCapability.OrderByEntity,
            Model(
                traversal: [traversal],
                projection: new ProjectionShape(ProjectionKind.Scalar, projection),
                groupBy: new GroupByFragment(key, null, null)));
    }

    [Fact]
    public void Plan_SameNamedUnmarkedDynamicAccessorIsRejected()
    {
        Expression<Func<DynamicNode, bool>> predicate = node =>
            Neo4jDynamicEntityExtensions.HasProperty(node, "name");

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            planner.Plan(Model(
                root: new DynamicRoot(typeof(DynamicNode)),
                predicates: [new PredicateFragment(predicate, "src")])));

        Assert.Contains("Neo4jDynamicEntityExtensions.HasProperty", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_LowersPredicateOrderingPagingAndProjection()
    {
        Expression<Func<Person, bool>> predicate = person => person.Age >= 18;
        Expression<Func<Person, string>> ordering = person => person.Name;
        var model = Model(
            predicates: [new PredicateFragment(predicate, "src")],
            ordering: [new OrderingKey(ordering, descending: true)],
            paging: new Paging(skip: 2, take: 5));

        var statement = planner.Plan(model);

        Assert.Collection(
            statement.Clauses,
            clause => Assert.IsType<MatchClause>(clause),
            clause => Assert.IsType<WhereClause>(clause),
            clause => Assert.IsType<OrderByClause>(clause),
            clause => Assert.IsType<SkipClause>(clause),
            clause => Assert.IsType<LimitClause>(clause),
            clause => Assert.IsType<EntityProjectionClause>(clause));
        Assert.Equal(18, statement.Parameters["p0"]);
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_AllocatesParametersInExpressionOrder()
    {
        Expression<Func<Person, bool>> predicate = person => person.Age >= 18 && person.Name == "Ada";

        var statement = planner.Plan(Model(predicates: [new PredicateFragment(predicate, "src")]));

        Assert.Equal(["p0", "p1"], statement.Parameters.Keys);
        Assert.Equal(18, statement.Parameters["p0"]);
        Assert.Equal("Ada", statement.Parameters["p1"]);
    }

    [Fact]
    public void Plan_LowersTypedTraversalAsRelationshipPattern()
    {
        var traversal = new TraversalStep(
            "KNOWS",
            GraphTraversalDirection.Outgoing,
            new Cvoya.Graph.Querying.DepthRange(1, 3),
            [],
            typeof(Person),
            typeof(Knows));

        var statement = planner.Plan(Model(traversal: [traversal]));

        var match = Assert.IsType<MatchClause>(statement.Clauses[0]);
        var relationship = Assert.IsType<RelationshipPattern>(match.Patterns[0].Elements[1]);
        Assert.Equal("KNOWS", Assert.Single(relationship.Types));
        Assert.Equal(new Cvoya.Graph.Cypher.Ast.DepthRange(1, 3), relationship.Depth);
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_LowersComplexNullAsRelationshipNonExistence()
    {
        Expression<Func<Person, bool>> predicate = person => person.Home == null;

        var statement = planner.Plan(Model(predicates: [new PredicateFragment(predicate, "src")]));

        var where = Assert.IsType<WhereClause>(statement.Clauses[1]);
        var not = Assert.IsType<AstUnaryExpression>(where.Predicate);
        Assert.Equal(CypherUnaryOperator.Not, not.Op);
        var exists = Assert.IsType<PatternSubqueryExpression>(not.Operand);
        Assert.Equal(PatternSubqueryKind.Exists, exists.Kind);
        var relationship = Assert.IsType<RelationshipPattern>(exists.Pattern.Elements[1]);
        Assert.Equal("Home", Assert.Single(relationship.Types));
    }

    [Fact]
    public void Plan_LowersComplexCollectionOperatorsToPatternExpressions()
    {
        Expression<Func<Person, bool>> predicate = person =>
            person.Offices.Any(office => office.City == "Seattle") &&
            person.Offices.All(office => office.IsOpen) &&
            person.Offices.Count > 1;

        var statement = planner.Plan(Model(predicates: [new PredicateFragment(predicate, "src")]));

        var where = Assert.IsType<WhereClause>(statement.Clauses[1]);
        Assert.Contains(Descendants(where.Predicate), expression => expression is PatternSubqueryExpression
        { Kind: PatternSubqueryKind.Exists });
        Assert.Contains(Descendants(where.Predicate), expression => expression is PatternSubqueryExpression
        { Kind: PatternSubqueryKind.Count });
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_LowersArbitraryDepthComplexNavigation()
    {
        Expression<Func<Person, bool>> predicate = person => person.Home!.Region.Name == "Northwest";

        var statement = planner.Plan(Model(predicates: [new PredicateFragment(predicate, "src")]));

        var matches = statement.Clauses.OfType<MatchClause>().ToArray();
        Assert.Equal(3, matches.Length);
        Assert.Equal("Home", Assert.Single(Assert.IsType<RelationshipPattern>(matches[1].Patterns[0].Elements[1]).Types));
        Assert.Equal("Region", Assert.Single(Assert.IsType<RelationshipPattern>(matches[2].Patterns[0].Elements[1]).Types));
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_LowersSearchRootToCallAndTypedLabelPredicate()
    {
        var model = Model(root: new SearchRoot("Ada", SearchRootTarget.Nodes, typeof(Person)));

        var statement = planner.Plan(model);

        var search = Assert.IsType<FullTextSearchClause>(statement.Clauses[0]);
        Assert.Equal(SearchRootTarget.Nodes, search.Target);
        Assert.Equal("p0", search.Query.Name);
        Assert.Equal("n", search.Alias);
        Assert.IsType<WhereClause>(statement.Clauses[1]);
        Assert.Equal("Ada", statement.Parameters["p0"]);
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_LowersDistinctBeforeOrderingAndPaging()
    {
        Expression<Func<Person, string>> projection = person => person.Name;
        Expression<Func<string, string>> ordering = name => name;
        var model = Model(
            projection: new ProjectionShape(ProjectionKind.Scalar, projection),
            ordering: [new OrderingKey(ordering, descending: false)],
            paging: new Paging(skip: 1, take: 2),
            distinct: true);

        var statement = planner.Plan(model);

        Assert.Collection(
            statement.Clauses,
            clause => Assert.IsType<MatchClause>(clause),
            clause => Assert.True(Assert.IsType<WithClause>(clause).Distinct),
            clause => Assert.IsType<OrderByClause>(clause),
            clause => Assert.IsType<SkipClause>(clause),
            clause => Assert.IsType<LimitClause>(clause),
            clause => Assert.IsType<ReturnClause>(clause));
        new CypherAstValidator().Run(statement);
    }

    [Theory]
    [InlineData(TerminalOperation.First, 1)]
    [InlineData(TerminalOperation.Single, 2)]
    public void Plan_LowersCardinalityTerminalToLimit(TerminalOperation terminal, int expectedLimit)
    {
        var statement = planner.Plan(Model(terminal: terminal));

        var limit = Assert.Single(statement.Clauses.OfType<LimitClause>());
        Assert.Equal(expectedLimit, Assert.IsType<Literal>(limit.Count).Value);
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_LowersCountTerminalToAggregateReturn()
    {
        var statement = planner.Plan(Model(terminal: TerminalOperation.Count));

        var item = Assert.Single(Assert.IsType<ReturnClause>(statement.Clauses[^1]).Items);
        Assert.Equal("count", Assert.IsType<FunctionCall>(item.Expression).Name);
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_LowersJoinToTwoMatchesEqualityAndProjection()
    {
        Expression<Func<Knows, string>> outerKey = relationship => relationship.StartNodeId;
        Expression<Func<Person, string>> innerKey = person => person.Id;
        Expression<Func<Knows, Person, string>> result = (_, person) => person.Name;
        var join = new JoinFragment(
            new NodeRoot(typeof(Person)),
            outerKey,
            innerKey,
            result);

        var statement = planner.Plan(Model(
            root: new RelationshipRoot(typeof(Knows)),
            join: join));

        Assert.Equal(2, statement.Clauses.OfType<MatchClause>().Count());
        Assert.IsType<AstBinaryExpression>(Assert.IsType<WhereClause>(statement.Clauses[2]).Predicate);
        Assert.IsType<ReturnClause>(statement.Clauses[^1]);
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_DeduplicatesEqualParameterValues()
    {
        Expression<Func<Person, bool>> predicate = person => person.Age == 18 || person.Name == "Ada" && person.Age == 18;

        var statement = planner.Plan(Model(predicates: [new PredicateFragment(predicate, "src")]));

        Assert.Equal(["p0", "p1"], statement.Parameters.Keys);
        Assert.Equal(18, statement.Parameters["p0"]);
        Assert.Equal("Ada", statement.Parameters["p1"]);
    }

    [Fact]
    public void Plan_NormalizesAndDeduplicatesEnumParameters()
    {
        var status = PersonStatus.Active;
        Expression<Func<Person, bool>> predicate = person =>
            person.Status == status || person.Name == "Ada" && person.Status == status;

        var statement = planner.Plan(Model(predicates: [new PredicateFragment(predicate, "src")]));

        var enumParameter = Assert.Single(statement.Parameters.Values, value => value is int number && number == (int)PersonStatus.Active);
        Assert.IsType<int>(enumParameter);
    }

    [Fact]
    public void Plan_NormalizesLongBackedEnumParametersWithoutOverflow()
    {
        var flag = WideFlag.High;
        Expression<Func<Person, bool>> predicate = person => person.WideFlag == flag;

        var statement = planner.Plan(Model(predicates: [new PredicateFragment(predicate, "src")]));

        Assert.Contains(statement.Parameters.Values, value => value is long number && number == (long)WideFlag.High);
    }

    [Fact]
    public void Plan_LowersSearchFilterAfterTraversalToCallAndAliasPredicate()
    {
        var traversal = new TraversalStep(
            "KNOWS",
            GraphTraversalDirection.Outgoing,
            new Cvoya.Graph.Querying.DepthRange(1, 1),
            [],
            typeof(Person),
            typeof(Knows));
        var model = Model(
            traversal: [traversal],
            searchFilter: new SearchRoot("Ada", SearchRootTarget.Nodes, typeof(Person)));

        var statement = planner.Plan(model);

        var search = Assert.Single(statement.Clauses.OfType<FullTextSearchClause>());
        Assert.Equal(SearchRootTarget.Nodes, search.Target);
        Assert.Equal("p0", search.Query.Name);
        Assert.Equal("searchedNode", search.Alias);
        Assert.Equal("Ada", statement.Parameters["p0"]);
        var where = Assert.Single(statement.Clauses.OfType<WhereClause>());
        Assert.Contains(
            Descendants(where.Predicate),
            expression => expression is AstBinaryExpression
            {
                Op: CypherBinaryOperator.Equal,
                Left: VariableRef { Alias: "tgt" },
                Right: VariableRef { Alias: "searchedNode" },
            });
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_OrderingAfterConstructorProjection_Throws()
    {
        Expression<Func<Person, PersonDto>> projection = person => new PersonDto(person.Name);
        Expression<Func<PersonDto, string>> ordering = dto => dto.Name;
        var model = Model(
            projection: new ProjectionShape(ProjectionKind.Anonymous, projection),
            ordering: [new OrderingKey(ordering, descending: false)]);

        var exception = Assert.Throws<GraphQueryTranslationException>(() => planner.Plan(model));

        Assert.Contains("ordering key cannot be mapped", exception.Message);
    }

    [Fact]
    public void Plan_InsertsWildcardWithBetweenOptionalNavigationAndWhere()
    {
        Expression<Func<Person, bool>> predicate = person => person.Home!.City == "Seattle";

        var statement = planner.Plan(Model(predicates: [new PredicateFragment(predicate, "src")]));

        var navigation = Assert.Single(statement.Clauses.OfType<MatchClause>().Skip(1));
        Assert.True(navigation.Optional);
        var withIndex = statement.Clauses.ToList().FindIndex(clause => clause is WithClause { Wildcard: true });
        var whereIndex = statement.Clauses.ToList().FindIndex(clause => clause is WhereClause);
        Assert.True(withIndex >= 0 && withIndex == whereIndex - 1);
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_ContainsOnComplexCollection_Throws()
    {
        var office = new Address();
        Expression<Func<Person, bool>> predicate = person => person.Offices.Contains(office);

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            planner.Plan(Model(predicates: [new PredicateFragment(predicate, "src")])));

        Assert.Contains("complex-property collection", exception.Message);
    }

    [Theory]
    [InlineData(GraphTraversalDirection.Outgoing, CypherDirection.Outgoing)]
    [InlineData(GraphTraversalDirection.Incoming, CypherDirection.Incoming)]
    [InlineData(GraphTraversalDirection.Both, CypherDirection.Both)]
    public void Plan_CountRelationshipsMarker_LowersToCountPatternSubquery(
        GraphTraversalDirection direction,
        CypherDirection expected)
    {
        // The direction must appear as a constant enum literal in the expression tree (a captured
        // variable would lower to a non-constant node), so each case uses its own inline literal.
        Expression<Func<Person, int>> selector = direction switch
        {
            GraphTraversalDirection.Outgoing => person => person.CountRelationships<Knows>(GraphTraversalDirection.Outgoing),
            GraphTraversalDirection.Incoming => person => person.CountRelationships<Knows>(GraphTraversalDirection.Incoming),
            _ => person => person.CountRelationships<Knows>(GraphTraversalDirection.Both),
        };
        var statement = planner.Plan(Model(projection: new ProjectionShape(ProjectionKind.Scalar, selector)));

        var item = Assert.Single(Assert.IsType<ReturnClause>(statement.Clauses[^1]).Items);
        var subquery = Assert.IsType<PatternSubqueryExpression>(item.Expression);
        Assert.Equal(PatternSubqueryKind.Count, subquery.Kind);
        Assert.Null(subquery.Predicate);

        Assert.Collection(
            subquery.Pattern.Elements,
            element => Assert.IsType<NodePattern>(element),
            element =>
            {
                var relationship = Assert.IsType<RelationshipPattern>(element);
                Assert.Equal(["KNOWS"], relationship.Types);
                Assert.Equal(expected, relationship.Direction);
                Assert.Null(relationship.Depth);
            },
            element => Assert.IsType<NodePattern>(element));
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_CountRelationshipsMarker_HonorsPatternSizeProjectionCapabilityGate()
    {
        Expression<Func<Person, int>> selector = person => person.CountRelationships<Knows>(GraphTraversalDirection.Outgoing);

        AssertMissingCapability(
            GraphCapability.PatternSizeProjection,
            Model(projection: new ProjectionShape(ProjectionKind.Scalar, selector)));
    }

    [Fact]
    public void Plan_CountRelationshipsMarker_NonConstantDirection_Throws()
    {
        // A direction that is not a compile-time constant cannot be resolved into a pattern
        // orientation while translating and is rejected rather than silently guessed.
        var direction = GraphTraversalDirection.Outgoing;
        Expression<Func<Person, int>> selector = person => person.CountRelationships<Knows>(Vary(direction));
        var model = Model(projection: new ProjectionShape(ProjectionKind.Scalar, selector));

        var exception = Assert.Throws<GraphQueryTranslationException>(() => planner.Plan(model));

        Assert.Contains("constant", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(GraphTraversalDirection), exception.Message, StringComparison.Ordinal);
    }

    // Keeps the direction argument out of the expression tree as a plain constant, forcing a
    // non-constant argument node for the rejection test.
    private static GraphTraversalDirection Vary(GraphTraversalDirection direction) => direction;

    [Fact]
    public void Plan_GroupByModelWithoutProjection_ThrowsDefinedTranslationError()
    {
        // A scalar-key grouping needs a group projection (a Select over the grouping or a
        // result-selector overload); grouping alone has no translatable RETURN shape.
        Expression<Func<Person, int>> key = person => person.Age;
        var model = Model(groupBy: new GroupByFragment(key, null, null));

        var exception = Assert.Throws<GraphQueryTranslationException>(() => planner.Plan(model));

        Assert.Contains("GroupBy", exception.Message);
        Assert.Contains("group projection", exception.Message);
    }

    [Fact]
    public void Plan_ScalarGroupBy_LowersToImplicitGroupingWithAndReturn()
    {
        Expression<Func<Person, string>> key = person => person.Name;
        Expression<Func<IGrouping<string, Person>, object>> projection =
            g => new { Name = g.Key, Count = g.Count(), OldestAge = g.Max(p => p.Age) };
        var model = Model(
            groupBy: new GroupByFragment(key, null, null),
            projection: new ProjectionShape(ProjectionKind.Anonymous, projection));

        var statement = planner.Plan(model);

        var with = Assert.Single(statement.Clauses.OfType<WithClause>());
        Assert.False(with.Distinct);
        Assert.Equal("__key", with.Items[0].Alias);
        Assert.Equal("__a0", with.Items[1].Alias);
        Assert.Equal("count", Assert.IsType<FunctionCall>(with.Items[1].Expression).Name);
        Assert.Equal("__a1", with.Items[2].Alias);
        Assert.Equal("max", Assert.IsType<FunctionCall>(with.Items[2].Expression).Name);

        var @return = Assert.Single(statement.Clauses.OfType<ReturnClause>());
        Assert.Equal(["Name", "Count", "OldestAge"], @return.Items.Select(item => item.Alias!).ToArray());
        Assert.Equal("__key", Assert.IsType<VariableRef>(@return.Items[0].Expression).Alias);
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_ScalarGroupByKeyOnly_UsesDistinctWith()
    {
        Expression<Func<Person, string>> key = person => person.Name;
        Expression<Func<IGrouping<string, Person>, string>> projection = g => g.Key;
        var model = Model(
            groupBy: new GroupByFragment(key, null, null),
            projection: new ProjectionShape(ProjectionKind.Scalar, projection));

        var statement = planner.Plan(model);

        var with = Assert.Single(statement.Clauses.OfType<WithClause>());
        Assert.True(with.Distinct);
        Assert.Equal("__key", Assert.Single(with.Items).Alias);
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_ScalarGroupByEntityKey_ThrowsDefinedTranslationError()
    {
        Expression<Func<Person, Person>> key = person => person;
        Expression<Func<IGrouping<Person, Person>, object>> projection = g => new { Count = g.Count() };
        var model = Model(
            groupBy: new GroupByFragment(key, null, null),
            projection: new ProjectionShape(ProjectionKind.Anonymous, projection));

        var exception = Assert.Throws<GraphQueryTranslationException>(() => planner.Plan(model));

        Assert.Contains("scalar value", exception.Message);
    }

    [Fact]
    public void Plan_ScalarGroupByWithOuterOrdering_ThrowsDefinedTranslationError()
    {
        Expression<Func<Person, string>> key = person => person.Name;
        Expression<Func<IGrouping<string, Person>, object>> projection = g => new { Name = g.Key };
        Expression<Func<Person, string>> orderKey = person => person.Name;
        var model = Model(
            groupBy: new GroupByFragment(key, null, null),
            projection: new ProjectionShape(ProjectionKind.Anonymous, projection),
            ordering: [new OrderingKey(orderKey, descending: false, alias: null)]);

        var exception = Assert.Throws<GraphQueryTranslationException>(() => planner.Plan(model));

        Assert.Contains("ordering", exception.Message);
    }

    [Fact]
    public void Plan_ScalarGroupBy_RequiresGroupByAggregationCapability()
    {
        var limited = new CypherQueryPlanner(new TestCypherDialect(CapabilitySet.Of(), "LimitedCypher"));
        Expression<Func<Person, string>> key = person => person.Name;
        Expression<Func<IGrouping<string, Person>, object>> projection = g => new { Name = g.Key, Count = g.Count() };
        var model = Model(
            groupBy: new GroupByFragment(key, null, null),
            projection: new ProjectionShape(ProjectionKind.Anonymous, projection));

        var exception = Assert.Throws<GraphQueryTranslationException>(() => limited.Plan(model));

        Assert.Contains(nameof(GraphCapability.GroupByAggregation), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CorrelatedGroupProjection_AcceptsSupportedCollectionAndAggregateMembers()
    {
        Expression<Func<IGrouping<Person, IGraphPathSegment<Person, Knows, Person>>, object>> projection = group =>
            new
            {
                Name = group.Key.Name,
                Friends = group.Select(segment => segment.EndNode.Name).ToList(),
                FriendCount = group.Count(),
                YoungFriendCount = group.Count(segment => segment.EndNode.Age < 30),
                OldestFriend = group.Max(segment => segment.EndNode.Age),
                Ordered = group.OrderBy(segment => segment.EndNode.Age)
                    .Select(segment => segment.EndNode.Name)
                    .ToArray(),
            };

        Assert.Null(CorrelatedGroupProjectionValidation.Validate(GroupedModel(projection)));
    }

    [Fact]
    public void CorrelatedGroupProjection_AcceptsNestedGroupingMember()
    {
        Expression<Func<IGrouping<Person, IGraphPathSegment<Person, Knows, Person>>, object>> projection = group =>
            new
            {
                Name = group.Key.Name,
                AgeGroups = group
                    .GroupBy(segment => segment.EndNode.Age >= 30)
                    .Select(inner => new { inner.Key, Count = inner.Count() })
                    .ToList(),
            };

        Assert.Null(CorrelatedGroupProjectionValidation.Validate(GroupedModel(projection)));
    }

    [Fact]
    public void CorrelatedGroupProjection_RejectsUnsupportedGroupOperation()
    {
        Expression<Func<IGrouping<Person, IGraphPathSegment<Person, Knows, Person>>, object>> projection = group =>
            new
            {
                Name = group.Key.Name,
                Friends = group.Take(2).Select(segment => segment.EndNode.Name).ToList(),
            };

        var reason = CorrelatedGroupProjectionValidation.Validate(GroupedModel(projection));

        Assert.NotNull(reason);
        Assert.Contains("Take", reason);
        Assert.Contains("Friends", reason);

        // The canonical message deliberately omits the "GroupBy" substring the provider mappings key on,
        // so it surfaces as a GraphQueryTranslationException rather than being downgraded.
        var message = CorrelatedGroupProjectionValidation.BuildMessage(reason!);
        Assert.DoesNotContain("GroupBy", message, StringComparison.Ordinal);

        var exception = Assert.Throws<GraphQueryTranslationException>(() => planner.Plan(GroupedModel(projection)));
        Assert.Equal(message, exception.Message);
    }

    private static GraphQueryModel GroupedModel(LambdaExpression projection)
    {
        Expression<Func<IGraphPathSegment<Person, Knows, Person>, Person>> key = segment => segment.StartNode;
        var traversal = new TraversalStep(
            "KNOWS",
            GraphTraversalDirection.Outgoing,
            new Cvoya.Graph.Querying.DepthRange(1, 1),
            [],
            typeof(Person),
            typeof(Knows),
            isComplexPropertyTraversal: false,
            sourceAlias: "src",
            targetAlias: "tgt");

        return Model(
            traversal: [traversal],
            projection: new ProjectionShape(ProjectionKind.Scalar, projection),
            groupBy: new GroupByFragment(key, null, null));
    }

    [Fact]
    public void Plan_SelectManyModel_ThrowsDefinedTranslationError()
    {
        Expression<Func<Person, IEnumerable<Address>>> collection = person => person.Offices;
        var model = Model(selectMany: new SelectManyFragment(collection, null));

        var exception = Assert.Throws<GraphQueryTranslationException>(() => planner.Plan(model));

        Assert.Contains("SelectMany is not supported by graph query translation yet; see #100.", exception.Message);
    }

    [Fact]
    public void Plan_UnionModel_ThrowsDefinedTranslationError()
    {
        var operand = Model();
        var model = Model(union: new UnionFragment(operand, operand, typeof(Person)));

        var exception = Assert.Throws<GraphQueryTranslationException>(() => planner.Plan(model));

        Assert.Contains("Union is not supported by graph query translation yet.", exception.Message);
    }

    [Fact]
    public void Plan_DistinctScalarAggregate_PipesDistinctProjectionBeforeAggregate()
    {
        Expression<Func<Person, string>> selector = person => person.Name;
        var model = Model(
            projection: new ProjectionShape(ProjectionKind.Scalar, selector),
            terminal: TerminalOperation.Count,
            distinct: true);

        var statement = planner.Plan(model);

        var with = Assert.IsType<WithClause>(statement.Clauses[1]);
        Assert.True(with.Distinct);
        var @return = Assert.IsType<ReturnClause>(statement.Clauses[2]);
        var function = Assert.IsType<FunctionCall>(Assert.Single(@return.Items).Expression);
        Assert.Equal("count", function.Name);
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_DistinctScalarAggregateWithPaging_PipesProjectedValueBeforePaging()
    {
        Expression<Func<Person, int>> selector = person => person.Age;
        var model = Model(
            projection: new ProjectionShape(ProjectionKind.Scalar, selector),
            paging: new Paging(skip: null, take: 2),
            terminal: TerminalOperation.Count,
            distinct: true);

        var statement = planner.Plan(model);

        var with = Assert.Single(statement.Clauses.OfType<WithClause>());
        Assert.True(with.Distinct);
        Assert.IsType<PropertyAccess>(Assert.Single(with.Items).Expression);
        var limit = Assert.Single(statement.Clauses.OfType<LimitClause>());
        Assert.Equal(2, Assert.IsType<Literal>(limit.Count).Value);
        var @return = Assert.Single(statement.Clauses.OfType<ReturnClause>());
        var aggregate = Assert.IsType<FunctionCall>(Assert.Single(@return.Items).Expression);
        Assert.Equal("__value0", Assert.IsType<VariableRef>(Assert.Single(aggregate.Arguments)).Alias);
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_DistinctEntityProjection_PipesDistinctAliasBeforeMaterialization()
    {
        var statement = planner.Plan(Model(distinct: true));

        Assert.Collection(
            statement.Clauses,
            clause => Assert.IsType<MatchClause>(clause),
            clause => Assert.True(Assert.IsType<WithClause>(clause).Distinct),
            clause => Assert.IsType<EntityProjectionClause>(clause));
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_DistinctSingle_AppliesCardinalityLimitAfterDistinct()
    {
        Expression<Func<Person, string>> selector = person => person.Name;
        var statement = planner.Plan(Model(
            projection: new ProjectionShape(ProjectionKind.Scalar, selector),
            terminal: TerminalOperation.Single,
            distinct: true));

        Assert.Collection(
            statement.Clauses,
            clause => Assert.IsType<MatchClause>(clause),
            clause => Assert.True(Assert.IsType<WithClause>(clause).Distinct),
            clause => Assert.Equal(2, Assert.IsType<Literal>(Assert.IsType<LimitClause>(clause).Count).Value),
            clause => Assert.IsType<ReturnClause>(clause));
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_DistinctAggregateOverMultiValueProjection_Throws()
    {
        Expression<Func<Person, object>> selector = person => new { person.Name, person.Age };
        var model = Model(
            projection: new ProjectionShape(ProjectionKind.Anonymous, selector),
            terminal: TerminalOperation.Count,
            distinct: true);

        var exception = Assert.Throws<GraphQueryTranslationException>(() => planner.Plan(model));

        Assert.Contains("multi-value projection", exception.Message);
    }

    [Fact]
    public void Plan_DistinctPagedAggregateOverMultiValueProjection_Throws()
    {
        Expression<Func<Person, object>> selector = person => new { person.Name, person.Age };
        var model = Model(
            projection: new ProjectionShape(ProjectionKind.Anonymous, selector),
            paging: new Paging(skip: null, take: 2),
            terminal: TerminalOperation.Count,
            distinct: true);

        var exception = Assert.Throws<GraphQueryTranslationException>(() => planner.Plan(model));

        Assert.Contains("multi-value projection", exception.Message);
    }

    [Fact]
    public void Plan_PreSearchOrderingAlias_MapsToSearchScope()
    {
        Expression<Func<Person, string>> ordering = person => person.Name;
        var model = Model(
            root: new SearchRoot("Ada", SearchRootTarget.Nodes, typeof(Person)),
            ordering: [new OrderingKey(ordering, descending: false, alias: "src")]);

        var statement = planner.Plan(model);

        var orderBy = Assert.Single(statement.Clauses.OfType<OrderByClause>());
        var property = Assert.IsType<PropertyAccess>(Assert.Single(orderBy.Items).Expression);
        Assert.Equal("n", Assert.IsType<VariableRef>(property.Target).Alias);
        new CypherAstValidator().Run(statement);
    }

    [Theory]
    [InlineData(TerminalOperation.ElementAt)]
    [InlineData(TerminalOperation.ElementAtOrDefault)]
    public void Plan_ElementAt_LowersTerminalIndexAsSkipLimit(TerminalOperation terminal)
    {
        var model = Model(terminal: terminal, terminalOperand: 4);

        var statement = planner.Plan(model);

        var skip = Assert.Single(statement.Clauses.OfType<SkipClause>());
        Assert.Equal(4, Assert.IsType<Literal>(skip.Count).Value);
        var limit = Assert.Single(statement.Clauses.OfType<LimitClause>());
        Assert.Equal(1, Assert.IsType<Literal>(limit.Count).Value);
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_TraversalTargetAlias_BindsPostTraversalPredicate()
    {
        Expression<Func<Person, bool>> predicate = person => person.Age >= 18;
        var traversal = new TraversalStep(
            "KNOWS",
            GraphTraversalDirection.Outgoing,
            new Cvoya.Graph.Querying.DepthRange(1, 1),
            [],
            typeof(Person),
            typeof(Knows),
            isComplexPropertyTraversal: false,
            sourceAlias: "src",
            targetAlias: "friend");
        var model = Model(
            predicates: [new PredicateFragment(predicate, "friend")],
            traversal: [traversal]);

        var statement = planner.Plan(model);

        var match = Assert.IsType<MatchClause>(statement.Clauses[0]);
        var target = Assert.IsType<NodePattern>(match.Patterns[0].Elements[2]);
        Assert.Equal("friend", target.Alias);
        var where = Assert.Single(statement.Clauses.OfType<WhereClause>());
        var binary = Assert.IsType<AstBinaryExpression>(where.Predicate);
        var property = Assert.IsType<PropertyAccess>(binary.Left);
        Assert.Equal("friend", Assert.IsType<VariableRef>(property.Target).Alias);
        new CypherAstValidator().Run(statement);
    }

    private static IEnumerable<CypherExpression> Descendants(CypherExpression expression)
    {
        yield return expression;
        switch (expression)
        {
            case AstBinaryExpression binary:
                foreach (var item in Descendants(binary.Left)) yield return item;
                foreach (var item in Descendants(binary.Right)) yield return item;
                break;
            case AstUnaryExpression unary:
                foreach (var item in Descendants(unary.Operand)) yield return item;
                break;
            case ConjunctionExpression conjunction:
                foreach (var predicate in conjunction.Predicates)
                {
                    foreach (var item in Descendants(predicate)) yield return item;
                }

                break;
        }
    }

    private static GraphQueryModel Model(
        IReadOnlyList<PredicateFragment>? predicates = null,
        IReadOnlyList<TraversalStep>? traversal = null,
        IReadOnlyList<OrderingKey>? ordering = null,
        Paging? paging = null,
        QueryRoot? root = null,
        ProjectionShape? projection = null,
        TerminalOperation terminal = TerminalOperation.ToListOrArray,
        bool distinct = false,
        object? terminalOperand = null,
        JoinFragment? join = null,
        SearchRoot? searchFilter = null,
        GroupByFragment? groupBy = null,
        SelectManyFragment? selectMany = null,
        UnionFragment? union = null) =>
        new(
            root ?? new NodeRoot(typeof(Person)),
            predicates ?? [],
            traversal ?? [],
            projection,
            ordering ?? [],
            paging ?? new Paging(null, null),
            terminal,
            distinct,
            terminalOperand,
            pathShape: null,
            join,
            searchFilter,
            groupBy,
            selectMany,
            union);

    private static void AssertMissingCapability(GraphCapability capability, GraphQueryModel model)
    {
        var dialect = new TestCypherDialect(CapabilitySet.All.Except(capability), "LimitedCypher");

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            new CypherQueryPlanner(dialect).Plan(model));

        Assert.Contains(capability.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("LimitedCypher", exception.Message, StringComparison.Ordinal);
    }

    private enum PersonStatus
    {
        Inactive = 0,
        Active = 1,
    }

    private enum WideFlag : long
    {
        High = 1L << 40,
    }

    private sealed record PersonDto(string Name);

    [Node("Person")]
    private sealed record Person : Node
    {
        public int Age { get; init; }

        public string Name { get; init; } = string.Empty;

        public PersonStatus Status { get; init; }

        public WideFlag WideFlag { get; init; }

        public Address? Home { get; init; }

        public IReadOnlyList<Address> Offices { get; init; } = [];
    }

    private sealed record Address
    {
        public string City { get; init; } = string.Empty;

        public bool IsOpen { get; init; }

        public Region Region { get; init; } = new();
    }

    private sealed record Region
    {
        public string Name { get; init; } = string.Empty;
    }

    [Relationship(Label = "KNOWS")]
    private sealed record Knows(string Start, string End) : Relationship(Start, End);

    private abstract record Animal : Node;

    private sealed record Cat : Animal;

    private sealed record Dog : Animal;
}

internal static class Neo4jDynamicEntityExtensions
{
    public static bool HasProperty(DynamicNode node, string propertyName) =>
        node.Properties.ContainsKey(propertyName);
}

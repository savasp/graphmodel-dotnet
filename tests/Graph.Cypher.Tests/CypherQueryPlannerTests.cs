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
    private readonly CypherQueryPlanner planner = new();

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
        Assert.Equal("KNOWS", relationship.Type);
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
        Assert.Equal("Home", relationship.Type);
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
        Assert.Equal("Home", Assert.IsType<RelationshipPattern>(matches[1].Patterns[0].Elements[1]).Type);
        Assert.Equal("Region", Assert.IsType<RelationshipPattern>(matches[2].Patterns[0].Elements[1]).Type);
        new CypherAstValidator().Run(statement);
    }

    [Fact]
    public void Plan_LowersSearchRootToCallAndTypedLabelPredicate()
    {
        var model = Model(root: new SearchRoot("Ada", SearchRootTarget.Nodes, typeof(Person)));

        var statement = planner.Plan(model);

        var call = Assert.IsType<CallClause>(statement.Clauses[0]);
        Assert.Equal("search.nodes", call.Procedure);
        Assert.Equal("n", Assert.Single(call.Yields).Alias);
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

        var call = Assert.Single(statement.Clauses.OfType<CallClause>());
        Assert.Equal("search.nodes", call.Procedure);
        Assert.Equal("searchedNode", Assert.Single(call.Yields).Alias);
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
        JoinFragment? join = null,
        SearchRoot? searchFilter = null) =>
        new(
            root ?? new NodeRoot(typeof(Person)),
            predicates ?? [],
            traversal ?? [],
            projection,
            ordering ?? [],
            paging ?? new Paging(null, null),
            terminal,
            distinct,
            terminalOperand: null,
            pathShape: null,
            join,
            searchFilter);

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
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Tests;

using System.Linq.Expressions;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Planning;
using Cvoya.Graph.Querying;

[Trait("Area", "GraphCommands")]
public sealed class GraphCommandCypherTests
{
    private readonly TestCypherDialect dialect = TestCypherDialect.Full;

    [Fact]
    public void SelectionPlan_AppliesWindowBeforeNativeIdentityDeduplicationAndTwoCandidateProbe()
    {
        Expression<Func<Person, string>> ordering = person => person.Name;
        var model = Model(
            new NodeRoot(typeof(Person)),
            ordering: [new OrderingKey(ordering, descending: false, "src")],
            paging: new Paging(3, 4));

        var statement = new CypherQueryPlanner(dialect).Plan(
            new GraphElementSelectionModel(model, GraphElementSelectionMode.ExactOne));

        Assert.Collection(
            statement.Clauses,
            clause => Assert.IsType<MatchClause>(clause),
            clause => Assert.True(Assert.IsType<WithClause>(clause).Wildcard),
            clause => Assert.IsType<OrderByClause>(clause),
            clause => Assert.IsType<SkipClause>(clause),
            clause => Assert.IsType<LimitClause>(clause),
            clause =>
            {
                var distinct = Assert.IsType<WithClause>(clause);
                Assert.True(distinct.Distinct);
                var item = Assert.Single(distinct.Items);
                Assert.Equal("__nativeId", item.Alias);
                Assert.IsType<NativeElementIdentity>(item.Expression);
            },
            clause => Assert.Equal(2, Assert.IsType<Literal>(Assert.IsType<LimitClause>(clause).Count).Value),
            clause => Assert.IsType<ReturnClause>(clause));

        var rendered = new CypherRenderer(dialect).Render(statement);
        Assert.Contains("WITH DISTINCT nativeId(src) AS __nativeId", rendered.Text, StringComparison.Ordinal);
        Assert.Equal(["__nativeId"], rendered.ProjectionColumns);
    }

    [Fact]
    public void MutationPlan_RendersFrozenNativeTargetsAndMappedSetters()
    {
        Expression<Func<Person, string>> name = person => person.Name;
        Expression<Func<Person, int>> age = person => person.Age;
        Expression<Func<Person, int>> increment = person => person.Age + 1;
        var mutation = new GraphMutationModel(
            GraphMutationKind.Update,
            new GraphElementSelectionModel(Model(new NodeRoot(typeof(Person))), GraphElementSelectionMode.Set),
            [
                new GraphConstantPropertyAssignment(name, typeof(Person).GetProperty(nameof(Person.Name)), "display_name", false, "updated"),
                new GraphComputedPropertyAssignment(age, typeof(Person).GetProperty(nameof(Person.Age)), nameof(Person.Age), false, increment),
            ],
            cascadeDelete: false);

        var statement = new CypherMutationPlanner(dialect).Plan(mutation, ["native-1", "native-2"]);
        var rendered = new CypherRenderer(dialect).Render(statement);

        Assert.Contains("UNWIND", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("nativeId(target) = __nativeId", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("SET target.display_name", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("target.Age = target.Age +", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("RETURN count(target) AS affectedCount", rendered.Text, StringComparison.Ordinal);
        Assert.Equal(["affectedCount"], rendered.ProjectionColumns);
    }

    [Fact]
    public void ConstraintPreflightAndStagedMutation_ComputeBeforeClearing()
    {
        Expression<Func<Person, string>> name = person => person.Name;
        Expression<Func<Person, string>> swap = person => person.Name == "first" ? "second" : "first";
        var mutation = new GraphMutationModel(
            GraphMutationKind.Update,
            new GraphElementSelectionModel(Model(new NodeRoot(typeof(Person))), GraphElementSelectionMode.Set),
            [new GraphComputedPropertyAssignment(name, typeof(Person).GetProperty(nameof(Person.Name)), nameof(Person.Name), false, swap)],
            cascadeDelete: false);
        var planner = new CypherMutationPlanner(dialect);

        var preflight = new CypherRenderer(dialect).Render(
            planner.PlanConstraintValues(
                mutation,
                ["native-1", "native-2"],
                [nameof(Person.Name)],
                acquireWriteLock: true));
        var staged = new CypherRenderer(dialect).Render(
            planner.Plan(mutation, ["native-1", "native-2"], [nameof(Person.Name)]));

        Assert.Contains("RETURN nativeId(target) AS __nativeId", preflight.Text, StringComparison.Ordinal);
        Assert.Contains(
            "WITH target\nORDER BY nativeId(target)\nSET target.Name = target.Name",
            preflight.Text,
            StringComparison.Ordinal);
        Assert.Contains("AS __constraintValue0", preflight.Text, StringComparison.Ordinal);
        Assert.Contains("WITH target AS target", staged.Text, StringComparison.Ordinal);
        Assert.Contains("AS __finalValue0", staged.Text, StringComparison.Ordinal);
        Assert.Contains("SET target.Name = null", staged.Text, StringComparison.Ordinal);
        Assert.Contains("WITH collect({ target: target, __finalValue0: __finalValue0 }) AS __rows", staged.Text, StringComparison.Ordinal);
        Assert.Contains("SET target.Name = __finalValue0", staged.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(GraphElementKind.Node, true)]
    [InlineData(GraphElementKind.Node, false)]
    [InlineData(GraphElementKind.Relationship, false)]
    public void DeletePlan_UsesElementAppropriateDelete(GraphElementKind kind, bool detach)
    {
        QueryRoot root = kind == GraphElementKind.Node
            ? new NodeRoot(typeof(Person))
            : new RelationshipRoot(typeof(Knows));
        var mutation = new GraphMutationModel(
            GraphMutationKind.Delete,
            new GraphElementSelectionModel(Model(root), GraphElementSelectionMode.Set),
            [],
            cascadeDelete: detach);

        var statement = new CypherMutationPlanner(dialect).Plan(mutation, ["native-1"]);
        var delete = Assert.Single(statement.Clauses.OfType<DeleteClause>());

        Assert.Equal(detach, delete.Detach);
        var rendered = new CypherRenderer(dialect).Render(statement);
        var alias = kind == GraphElementKind.Node ? "target" : "relationship";
        Assert.Contains($"{(detach ? "DETACH " : string.Empty)}DELETE {alias}", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AstValidator_RejectsSetTargetThatIsNotAProperty()
    {
        var statement = new CypherStatement(
            [
                new MatchClause([new PathPattern([new NodePattern("node", [])])], optional: false),
                new SetClause([new SetItem(new VariableRef("node"), new Literal(1))]),
            ],
            new Dictionary<string, object?>());

        var exception = Assert.Throws<GraphException>(() =>
            new Cvoya.Graph.Cypher.Validation.CypherAstValidator().Run(statement));

        Assert.Contains("property access", exception.Message, StringComparison.Ordinal);
    }

    private static GraphQueryModel Model(
        QueryRoot root,
        IReadOnlyList<OrderingKey>? ordering = null,
        Paging? paging = null) => new(
            root,
            predicates: [],
            traversal: [],
            projection: null,
            ordering ?? [],
            paging ?? new Paging(null, null),
            TerminalOperation.ToListOrArray);

    [Node("COMMAND_CYPHER_PERSON")]
    private sealed record Person : Node
    {
        public string Name { get; init; } = string.Empty;

        public int Age { get; init; }
    }

    [Relationship(Label = "COMMAND_CYPHER_KNOWS")]
    private sealed record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);
}

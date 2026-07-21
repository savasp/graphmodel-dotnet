// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Querying;

[Trait("Area", "GraphCommands")]
public sealed class GraphMutationModelBuilderTests
{
    [Fact]
    public void BuildUpdate_PreservesSelectionAndMappedAssignments()
    {
        var query = Root<Person>()
            .Where(person => person.Age >= 18)
            .OrderBy(person => person.LastName)
            .ThenBy(person => person.Age)
            .Skip(2)
            .Take(5);
        var nicknames = new[] { "member" };
        Expression<Func<GraphPropertySetters<Person>, GraphPropertySetters<Person>>> setters = builder => builder
            .SetProperty(person => person.FirstName, "Adult")
            .SetProperty(person => person.Age, person => person.Age + 1)
            .SetProperty(person => person.Nicknames, nicknames);

        var mutation = GraphMutationModelBuilder.Build(UpdateCall(query, setters));

        Assert.Equal(GraphMutationKind.Update, mutation.Kind);
        Assert.Equal(GraphElementKind.Node, mutation.Selection.ElementKind);
        Assert.Equal(GraphElementSelectionMode.Set, mutation.Selection.Mode);
        Assert.Single(mutation.Selection.Query.Predicates);
        Assert.Equal(2, mutation.Selection.Query.Ordering.Count);
        Assert.Equal(2, mutation.Selection.Query.Paging.Skip);
        Assert.Equal(5, mutation.Selection.Query.Paging.Take);
        Assert.Collection(
            mutation.Assignments,
            assignment =>
            {
                var constant = Assert.IsType<GraphConstantPropertyAssignment>(assignment);
                Assert.Equal(nameof(Person.FirstName), constant.StorageName);
                Assert.Equal("Adult", constant.Value);
            },
            assignment =>
            {
                var computed = Assert.IsType<GraphComputedPropertyAssignment>(assignment);
                Assert.Equal(nameof(Person.Age), computed.StorageName);
                Assert.Equal(typeof(int), computed.ValueExpression.ReturnType);
            },
            assignment =>
            {
                var constant = Assert.IsType<GraphConstantPropertyAssignment>(assignment);
                Assert.Equal(nameof(Person.Nicknames), constant.StorageName);
                Assert.Equal(["member"], Assert.IsType<string[]>(constant.Value));
            });
    }

    [Fact]
    public void BuildUpdate_UsesMappedAndDynamicStorageNames()
    {
        Expression<Func<GraphPropertySetters<Person>, GraphPropertySetters<Person>>> typed = builder =>
            builder.SetProperty(person => person.DisplayName, "renamed");
        Expression<Func<GraphPropertySetters<DynamicNode>, GraphPropertySetters<DynamicNode>>> dynamic = builder =>
            builder.SetProperty(node => node.Properties["odd-key"], 42);

        var typedMutation = GraphMutationModelBuilder.Build(UpdateCall(Root<Person>(), typed));
        var dynamicMutation = GraphMutationModelBuilder.Build(UpdateCall(Root<DynamicNode>(), dynamic));

        Assert.Equal("display_name", Assert.Single(typedMutation.Assignments).StorageName);
        var dynamicAssignment = Assert.Single(dynamicMutation.Assignments);
        Assert.True(dynamicAssignment.Dynamic);
        Assert.Equal("odd-key", dynamicAssignment.StorageName);
    }

    [Fact]
    public void BuildMutation_AcceptsEverySupportedEntityPreservingSelectionShape()
    {
        var threshold = DateTime.UnixEpoch;
        var nodeQuery = Root<Person>()
            .OfLabel("Active")
            .WhereHasRelationship<Person, Knows>(
                GraphTraversalDirection.Both,
                relationship => relationship.Since >= threshold)
            .Where(person => person.Age >= 18)
            .OrderBy(person => person.LastName)
            .ThenBy(person => person.Age)
            .Skip(1)
            .Take(2);
        var searchedNodes = Root<Person>()
            .Where(person => person.Age >= 18)
            .Search("Ada")
            .OfLabel("Active");
        var searchedRelationships = Root<Knows>()
            .Search("recent")
            .Where(relationship => relationship.Since >= threshold);

        Assert.Equal(GraphElementKind.Node, GraphMutationModelBuilder.Build(DeleteCall(nodeQuery)).Selection.ElementKind);
        Assert.IsType<SearchRoot>(GraphMutationModelBuilder.Build(DeleteCall(searchedNodes)).Selection.Query.Root);
        Assert.Equal(
            GraphElementKind.Relationship,
            GraphMutationModelBuilder.Build(DeleteCall(searchedRelationships)).Selection.ElementKind);
        Assert.Equal(
            GraphElementKind.Relationship,
            GraphMutationModelBuilder.Build(DeleteCall(Root<DynamicRelationship>())).Selection.ElementKind);
    }

    [Fact]
    public void BuildUpdate_AcceptsComputedDynamicScalarAndRejectsComplexDynamicConstant()
    {
        Expression<Func<GraphPropertySetters<DynamicNode>, GraphPropertySetters<DynamicNode>>> computed = builder =>
            builder.SetProperty(
                node => node.Properties["score"],
                node => (int)node.Properties["score"]! + 1);
        var complex = new Dictionary<string, object?> { ["nested"] = 1 };
        Expression<Func<GraphPropertySetters<DynamicNode>, GraphPropertySetters<DynamicNode>>> invalid = builder =>
            builder.SetProperty(node => node.Properties["payload"], complex);

        var mutation = GraphMutationModelBuilder.Build(UpdateCall(Root<DynamicNode>(), computed));
        Assert.IsType<GraphComputedPropertyAssignment>(Assert.Single(mutation.Assignments));
        Assert.Throws<GraphQueryTranslationException>(() =>
            GraphMutationModelBuilder.Build(UpdateCall(Root<DynamicNode>(), invalid)));
    }

    [Fact]
    public void BuildUpdate_AcceptsOrdinaryUserPropertiesWithLegacyStructuralNames()
    {
        Expression<Func<GraphPropertySetters<UserNamedNode>, GraphPropertySetters<UserNamedNode>>> nodeSetters = builder =>
            builder.SetProperty(node => node.Id, "domain-id");
        Expression<Func<GraphPropertySetters<UserNamedRelationship>, GraphPropertySetters<UserNamedRelationship>>> relationshipSetters = builder => builder
            .SetProperty(relationship => relationship.Id, "domain-id")
            .SetProperty(relationship => relationship.StartNodeId, "domain-start")
            .SetProperty(relationship => relationship.EndNodeId, "domain-end")
            .SetProperty(relationship => relationship.Direction, "domain-direction");

        var nodeMutation = GraphMutationModelBuilder.Build(UpdateCall(Root<UserNamedNode>(), nodeSetters));
        var relationshipMutation = GraphMutationModelBuilder.Build(
            UpdateCall(Root<UserNamedRelationship>(), relationshipSetters));

        Assert.Equal(["domain_id"], nodeMutation.Assignments.Select(assignment => assignment.StorageName));
        Assert.Equal(
            ["domain_id", "domain_start", "domain_end", "domain_direction"],
            relationshipMutation.Assignments.Select(assignment => assignment.StorageName));
    }

    [Fact]
    public void BuildUpdate_RejectsUserPropertyNamedTypeBecauseItRemainsStructuralMetadata()
    {
        Expression<Func<GraphPropertySetters<UserNamedRelationship>, GraphPropertySetters<UserNamedRelationship>>> setters =
            builder => builder.SetProperty(relationship => relationship.Type, "domain-type");

        Assert.Throws<GraphQueryTranslationException>(() =>
            GraphMutationModelBuilder.Build(UpdateCall(Root<UserNamedRelationship>(), setters)));
    }

    [Theory]
    [InlineData("projection")]
    [InlineData("traversal")]
    [InlineData("distinct")]
    [InlineData("post-paging")]
    [InlineData("whole-entity-order")]
    [InlineData("join")]
    [InlineData("select-many")]
    [InlineData("union")]
    [InlineData("concat")]
    public void BuildDelete_RejectsNonEntityPreservingSelection(string shape)
    {
        var root = Root<Person>();
        IQueryable<Person> query = shape switch
        {
            "projection" => root.Select(person => person),
            "traversal" => root.Traverse<Knows, Person>(),
            "distinct" => root.Distinct(),
            "post-paging" => root.Take(2).Where(person => person.Age > 1),
            "whole-entity-order" => root.OrderBy(person => person),
            "join" => root.Join(
                Root<Person>(),
                person => person.Age,
                person => person.Age,
                (left, _) => left),
            "select-many" => root.SelectMany(_ => Root<Person>(), (_, result) => result),
            "union" => root.Union(Root<Person>()),
            "concat" => root.Concat(Root<Person>()),
            _ => throw new InvalidOperationException(),
        };

        Assert.ThrowsAny<GraphException>(() => GraphMutationModelBuilder.Build(DeleteCall(query)));
    }

    [Fact]
    public void BuildDelete_RejectsMixedEntitySearchRoot()
    {
        var query = Root<IEntity>().Search("anything");

        Assert.Throws<GraphQueryTranslationException>(() =>
            GraphMutationModelBuilder.Build(DeleteCall(query)));
    }

    [Fact]
    public void SelectionValidator_RejectsGroupingAndAggregateTerminalShapes()
    {
        var grouped = GraphQueryModelBuilder.Build(Root<Person>().GroupBy(person => person.Age).Expression);
        var aggregate = new GraphQueryModel(
            new NodeRoot(typeof(Person)),
            predicates: [],
            traversal: [],
            projection: null,
            ordering: [],
            new Paging(null, null),
            TerminalOperation.Count);

        Assert.Throws<GraphQueryTranslationException>(() => GraphElementSelectionModelValidator.Validate(
            new GraphElementSelectionModel(grouped, GraphElementSelectionMode.Set)));
        Assert.Throws<GraphQueryTranslationException>(() => GraphElementSelectionModelValidator.Validate(
            new GraphElementSelectionModel(aggregate, GraphElementSelectionMode.Set)));
    }

    [Theory]
    [InlineData(nameof(Person.Ignored))]
    [InlineData(nameof(Person.Home))]
    public void BuildUpdate_RejectsStructuralOrUnsupportedMappedProperties(string propertyName)
    {
        var parameter = Expression.Parameter(typeof(Person), "person");
        var property = Expression.Property(parameter, propertyName);
        var selector = Expression.Lambda(property, parameter);
        var setters = Expression.Parameter(typeof(GraphPropertySetters<Person>), "setters");
        var overload = typeof(GraphPropertySetters<Person>).GetMethods()
            .Single(method => method.Name == nameof(GraphPropertySetters<Person>.SetProperty) &&
                method.GetParameters()[1].ParameterType.IsGenericParameter);
        var call = Expression.Call(
            setters,
            overload.MakeGenericMethod(property.Type),
            Expression.Quote(selector),
            Expression.Default(property.Type));
        var chain = Expression.Lambda(call, setters);

        Assert.Throws<GraphQueryTranslationException>(() =>
            GraphMutationModelBuilder.Build(UpdateCall(Root<Person>(), chain)));
    }

    [Fact]
    public void BuildUpdate_AcceptsExplicitKeyAndUniqueProperties()
    {
        Expression<Func<GraphPropertySetters<Person>, GraphPropertySetters<Person>>> setters = builder => builder
            .SetProperty(person => person.DomainKey, "new-key")
            .SetProperty(person => person.UniqueName, "new-name");

        var mutation = GraphMutationModelBuilder.Build(UpdateCall(Root<Person>(), setters));

        Assert.Equal(
            [nameof(Person.DomainKey), nameof(Person.UniqueName)],
            mutation.Assignments.Select(assignment => assignment.StorageName));
    }

    [Fact]
    public void ConstraintTargetRows_RejectAChangedFrozenTargetSet()
    {
        var rows = new[]
        {
            new GraphMutationConstraintRow(
                "native-1",
                new Dictionary<string, object?>(StringComparer.Ordinal)),
            new GraphMutationConstraintRow(
                "native-3",
                new Dictionary<string, object?>(StringComparer.Ordinal)),
        };

        var exception = Assert.Throws<GraphException>(() =>
            GraphMutationConstraintPlan.ValidateTargetRows(["native-1", "native-2"], rows));

        Assert.Contains("target set changed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildUpdate_AcceptsDomainIdAsOrdinaryMutableData()
    {
        Expression<Func<GraphPropertySetters<Person>, GraphPropertySetters<Person>>> setters = builder =>
            builder.SetProperty(person => person.Id, "changed");

        var mutation = GraphMutationModelBuilder.Build(UpdateCall(Root<Person>(), setters));

        Assert.Equal(nameof(IEntity.Id), Assert.Single(mutation.Assignments).StorageName);
    }

    [Fact]
    public void BuildUpdate_RejectsDuplicateMappedStorageName()
    {
        Expression<Func<GraphPropertySetters<Person>, GraphPropertySetters<Person>>> setters = builder => builder
            .SetProperty(person => person.DisplayName, "first")
            .SetProperty(person => person.DisplayName, "second");

        var exception = Assert.Throws<GraphQueryTranslationException>(() =>
            GraphMutationModelBuilder.Build(UpdateCall(Root<Person>(), setters)));

        Assert.Contains("assigned more than once", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CardinalityException_ExposesEndpointAndFailure()
    {
        var exception = new GraphCardinalityException(GraphEndpointRole.Target, GraphCardinalityFailure.Multiple);

        Assert.Equal(GraphEndpointRole.Target, exception.Role);
        Assert.Equal(GraphCardinalityFailure.Multiple, exception.Failure);
        Assert.Contains("target", exception.Message, StringComparison.Ordinal);
        Assert.Contains("more than one", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TestGraphQueryable<T> Root<T>() where T : class, IEntity => new();

    private static MethodCallExpression UpdateCall<TEntity>(
        IQueryable<TEntity> source,
        LambdaExpression setters)
        where TEntity : class, IEntity
    {
        var definition = typeof(GraphMutationMarkers).GetMethod(
            nameof(GraphMutationMarkers.UpdateMarker),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return Expression.Call(
            definition.MakeGenericMethod(typeof(TEntity)),
            source.Expression,
            Expression.Quote(setters));
    }

    private static MethodCallExpression DeleteCall<TEntity>(IQueryable<TEntity> source)
        where TEntity : class, IEntity
    {
        var definition = typeof(GraphMutationMarkers).GetMethod(
            nameof(GraphMutationMarkers.DeleteMarker),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return Expression.Call(
            definition.MakeGenericMethod(typeof(TEntity)),
            source.Expression,
            Expression.Constant(false));
    }

    [Node("COMMAND_PERSON")]
    private sealed record Person : Node
    {
        public string FirstName { get; init; } = string.Empty;

        public string LastName { get; init; } = string.Empty;

        public int Age { get; init; }

        public string[] Nicknames { get; init; } = [];

        [Property(Label = "display_name")]
        public string DisplayName { get; init; } = string.Empty;

        [Property(Ignore = true)]
        public string Ignored { get; init; } = string.Empty;

        [Property(IsKey = true)]
        public string DomainKey { get; init; } = string.Empty;

        [Property(IsUnique = true)]
        public string UniqueName { get; init; } = string.Empty;

        public Address Home { get; init; } = new();
    }

    private sealed record Address
    {
        public string City { get; init; } = string.Empty;
    }

    [Relationship(Label = "COMMAND_KNOWS")]
    private sealed record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
    {
        public DateTime Since { get; init; }
    }

    private sealed class UserNamedNode : INode
    {
        string IEntity.Id { get; init; } = string.Empty;

        IReadOnlyList<string> INode.Labels => [];

        [Property(Label = "domain_id")]
        public string Id { get; init; } = string.Empty;
    }

    private sealed class UserNamedRelationship : IRelationship
    {
        string IEntity.Id { get; init; } = string.Empty;

        string IRelationship.Type => string.Empty;

        string IRelationship.StartNodeId { get; init; } = string.Empty;

        string IRelationship.EndNodeId { get; init; } = string.Empty;

        [Property(Label = "domain_id")]
        public string Id { get; init; } = string.Empty;

        public string Type { get; init; } = string.Empty;

        [Property(Label = "domain_direction")]
        public string Direction { get; init; } = string.Empty;

        [Property(Label = "domain_start")]
        public string StartNodeId { get; init; } = string.Empty;

        [Property(Label = "domain_end")]
        public string EndNodeId { get; init; } = string.Empty;
    }
}

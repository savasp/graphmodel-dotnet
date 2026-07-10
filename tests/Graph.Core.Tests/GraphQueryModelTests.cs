// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

namespace Cvoya.Graph.Core.Tests;

using System.Linq.Expressions;
using Cvoya.Graph.Querying;

[Trait("Area", "QueryModel")]
public class GraphQueryModelTests
{
    [Fact]
    public void NodeRoot_RejectsNonNodeType()
    {
        var exception = Assert.Throws<ArgumentException>(() => new NodeRoot(typeof(Knows)));

        Assert.Contains(nameof(INode), exception.Message);
    }

    [Fact]
    public void RelationshipRoot_RejectsNonRelationshipType()
    {
        var exception = Assert.Throws<ArgumentException>(() => new RelationshipRoot(typeof(Person)));

        Assert.Contains(nameof(IRelationship), exception.Message);
    }

    [Fact]
    public void SearchRoot_RejectsElementTypeOutsideTarget()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SearchRoot("neo4j", SearchRootTarget.Nodes, typeof(Knows)));

        Assert.Contains(nameof(INode), exception.Message);
    }

    [Fact]
    public void DepthRange_RejectsInvalidRanges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DepthRange(-1, 1));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new DepthRange(2, 1));

        Assert.Contains("Maximum depth", exception.Message);
    }

    [Fact]
    public void Paging_RejectsNegativeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Paging(-1, null));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Paging(null, -1));

        Assert.Contains("Take", exception.Message);
    }

    [Fact]
    public void ProjectionShape_RequiresSelectorForNonIdentity()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ProjectionShape(ProjectionKind.Scalar, selector: null));

        Assert.Equal("selector", exception.ParamName);
    }

    [Fact]
    public void LambdaNodes_RejectInvalidReturnTypes()
    {
        Expression<Func<Person, string>> nonBooleanPredicate = person => person.Name;
        Expression<Action<Person>> voidExpression = person => Ignore(person);

        Assert.Throws<ArgumentException>(() => new PredicateFragment(nonBooleanPredicate, alias: null));
        Assert.Throws<ArgumentException>(() => new ProjectionShape(ProjectionKind.Scalar, voidExpression));
        Assert.Throws<ArgumentException>(() => new OrderingKey(voidExpression, descending: false));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void OptionalNames_RejectBlankValues(string value)
    {
        Assert.Throws<ArgumentException>(() => Predicate<Person>(person => person.Name.Length > 0, value));
        Assert.Throws<ArgumentException>(() => new TraversalStep(
            value,
            GraphTraversalDirection.Outgoing,
            new DepthRange(1, 1),
            [],
            typeof(Person)));
    }

    [Fact]
    public void GraphQueryModel_CopiesListInputs()
    {
        var predicates = new List<PredicateFragment>
        {
            Predicate<Person>(person => person.Name.Length > 0),
        };
        var traversal = new List<TraversalStep>();
        var ordering = new List<OrderingKey>
        {
            OrderBy<Person, string>(person => person.Name),
        };

        var model = new GraphQueryModel(
            new NodeRoot(typeof(Person)),
            predicates,
            traversal,
            projection: null,
            ordering,
            new Paging(skip: null, take: null),
            TerminalOperation.ToListOrArray);

        predicates.Clear();
        traversal.Add(CreateTraversalStep());
        ordering.Clear();

        Assert.Single(model.Predicates);
        Assert.Empty(model.Traversal);
        Assert.Single(model.Ordering);

        Assert.Throws<NotSupportedException>(() => ((IList<PredicateFragment>)model.Predicates).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<TraversalStep>)model.Traversal).Add(CreateTraversalStep()));
        Assert.Throws<NotSupportedException>(() => ((IList<OrderingKey>)model.Ordering).Clear());
    }

    [Fact]
    public void TraversalStep_CopiesAndProtectsRelationshipPredicates()
    {
        var predicates = new List<PredicateFragment>
        {
            Predicate<Knows>(relationship => relationship.Since > 2000),
        };
        var step = new TraversalStep(
            "KNOWS",
            GraphTraversalDirection.Outgoing,
            new DepthRange(1, 1),
            predicates,
            typeof(Person));

        predicates.Clear();

        Assert.Single(step.RelationshipPredicates);
        Assert.Throws<NotSupportedException>(() => ((IList<PredicateFragment>)step.RelationshipPredicates).Clear());
    }

    [Fact]
    public void Validator_ValidModelPasses()
    {
        GraphQueryModelValidator.Validate(CreateValidModel());
    }

    [Fact]
    public void Validator_RejectsTraversalFromRelationshipScope()
    {
        var model = CreateValidModel(
            root: new RelationshipRoot(typeof(Knows)),
            predicates: [],
            projection: null,
            ordering: []);

        var exception = Assert.Throws<GraphException>(() => GraphQueryModelValidator.Validate(model));

        Assert.Contains("Traversal step 0 requires a node input", exception.Message);
    }

    [Fact]
    public void Validator_RejectsRelationshipPredicateOutsideRelationshipScope()
    {
        var traversal = new[]
        {
            new TraversalStep(
                relationshipType: "KNOWS",
                GraphTraversalDirection.Outgoing,
                new DepthRange(1, 1),
                [
                    Predicate<Person>(person => person.Name.Length > 0),
                ],
                typeof(Person)),
        };
        var model = CreateValidModel(traversal: traversal);

        var exception = Assert.Throws<GraphException>(() => GraphQueryModelValidator.Validate(model));

        Assert.Contains("Traversal step 0 relationship predicate 0 parameter", exception.Message);
    }

    [Fact]
    public void Validator_RejectsProjectionSelectorWithUnboundParameter()
    {
        var inScope = Expression.Parameter(typeof(Person), "person");
        var outside = Expression.Parameter(typeof(Person), "outside");
        var body = Expression.Property(outside, nameof(Person.Name));
        var selector = Expression.Lambda(body, inScope);
        var model = CreateValidModel(
            projection: new ProjectionShape(ProjectionKind.Scalar, selector),
            ordering: []);

        var exception = Assert.Throws<GraphException>(() => GraphQueryModelValidator.Validate(model));

        Assert.Contains("Projection selector references parameter 'outside'", exception.Message);
    }

    [Fact]
    public void Validator_RejectsOrderingKeyOutsideCurrentScope()
    {
        var model = CreateValidModel(
            projection: null,
            ordering:
            [
                OrderBy<Knows, int>(relationship => relationship.Since, descending: true),
            ]);

        var exception = Assert.Throws<GraphException>(() => GraphQueryModelValidator.Validate(model));

        Assert.Contains("Ordering key 0 parameter", exception.Message);
    }

    [Fact]
    public void Validator_RejectsPredicateWithoutParameter()
    {
        var model = CreateValidModel(
            predicates:
            [
                new PredicateFragment(Expression.Lambda(Expression.Constant(true)), alias: null),
            ],
            traversal: [],
            projection: null,
            ordering: []);

        var exception = Assert.Throws<GraphException>(() => GraphQueryModelValidator.Validate(model));

        Assert.Contains("Root predicate 0 must declare at least one parameter", exception.Message);
    }

    [Fact]
    public void Validator_AllowsPathSegmentProjectionScope()
    {
        var model = CreateValidModel(
            projection: new ProjectionShape(
                ProjectionKind.PathSegment,
                Selector<IGraphPathSegment, INode>(segment => segment.EndNode)),
            ordering: []);

        GraphQueryModelValidator.Validate(model);
    }

    private static GraphQueryModel CreateValidModel(
        QueryRoot? root = null,
        IReadOnlyList<PredicateFragment>? predicates = null,
        IReadOnlyList<TraversalStep>? traversal = null,
        ProjectionShape? projection = null,
        IReadOnlyList<OrderingKey>? ordering = null)
    {
        return new GraphQueryModel(
            root ?? new NodeRoot(typeof(Person)),
            predicates ??
            [
                Predicate<Person>(person => person.Name.Length > 0),
            ],
            traversal ?? [CreateTraversalStep()],
            projection ?? new ProjectionShape(ProjectionKind.Scalar, Selector<Person, string>(person => person.Name)),
            ordering ??
            [
                OrderBy<Person, string>(person => person.Name),
            ],
            new Paging(skip: 0, take: 10),
            TerminalOperation.ToListOrArray);
    }

    private static TraversalStep CreateTraversalStep()
    {
        return new TraversalStep(
            relationshipType: "KNOWS",
            GraphTraversalDirection.Outgoing,
            new DepthRange(1, 2),
            [
                Predicate<Knows>(relationship => relationship.Since > 2000, alias: "rel"),
            ],
            typeof(Person));
    }

    private static PredicateFragment Predicate<T>(Expression<Func<T, bool>> predicate, string? alias = null)
    {
        return new PredicateFragment(predicate, alias);
    }

    private static OrderingKey OrderBy<T, TResult>(Expression<Func<T, TResult>> keySelector, bool descending = false)
    {
        return new OrderingKey(keySelector, descending);
    }

    private static LambdaExpression Selector<T, TResult>(Expression<Func<T, TResult>> selector)
    {
        return selector;
    }

    private static void Ignore(Person person)
    {
    }

    private sealed record Person : Node
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed record Knows(string Start, string End) : Relationship(Start, End)
    {
        public int Since { get; init; }
    }
}

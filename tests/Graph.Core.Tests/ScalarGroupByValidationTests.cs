// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Linq.Expressions;
using Cvoya.Graph.Querying;

/// <summary>
/// Provider-free tests for the shared scalar-grouping boundary: the aggregate grammar accepted by
/// <see cref="ScalarGroupByValidation.DescribeUnsupported(GraphQueryModel)"/> and the precise
/// reasons it reports for rejected shapes, independent of any provider's planner or interpreter.
/// </summary>
[Trait("Area", "QueryModel")]
public class ScalarGroupByValidationTests
{
    [Fact]
    public void ParameterlessLongCount_SelectOverGrouping_IsSupported()
    {
        Expression<Func<Person, string>> key = person => person.Department;
        Expression<Func<IGrouping<string, Person>, object>> projection =
            g => new { Department = g.Key, Count = g.LongCount() };

        var reason = ScalarGroupByValidation.DescribeUnsupported(Model(
            groupBy: new GroupByFragment(key, null, null),
            projection: new ProjectionShape(ProjectionKind.Anonymous, projection)));

        Assert.Null(reason);
    }

    [Fact]
    public void ParameterlessLongCount_ResultSelector_IsSupported()
    {
        Expression<Func<Person, string>> key = person => person.Department;
        Expression<Func<string, IEnumerable<Person>, object>> resultSelector =
            (department, people) => new { Department = department, Count = people.LongCount() };

        var reason = ScalarGroupByValidation.DescribeUnsupported(Model(
            groupBy: new GroupByFragment(key, null, resultSelector)));

        Assert.Null(reason);
    }

    [Fact]
    public void ParameterlessQueryableLongCount_SelectOverGrouping_IsSupported()
    {
        Expression<Func<Person, string>> key = person => person.Department;
        Expression<Func<IGrouping<string, Person>, object>> projection =
            g => new { Department = g.Key, Count = g.AsQueryable().LongCount() };

        var reason = ScalarGroupByValidation.DescribeUnsupported(Model(
            groupBy: new GroupByFragment(key, null, null),
            projection: new ProjectionShape(ProjectionKind.Anonymous, projection)));

        Assert.Null(reason);
    }

    [Fact]
    public void ParameterlessQueryableLongCount_ResultSelector_IsSupported()
    {
        Expression<Func<Person, string>> key = person => person.Department;
        Expression<Func<string, IEnumerable<Person>, object>> resultSelector =
            (department, people) => new
            {
                Department = department,
                Count = people.AsQueryable().LongCount(),
            };

        var reason = ScalarGroupByValidation.DescribeUnsupported(Model(
            groupBy: new GroupByFragment(key, null, resultSelector)));

        Assert.Null(reason);
    }

    [Theory]
    [InlineData("Count")]
    [InlineData("LongCount")]
    public void PredicateCountForms_ReportFilterBeforeGroupByReason(string method)
    {
        Expression<Func<Person, string>> key = person => person.Department;
        Expression<Func<IGrouping<string, Person>, object>> projection = method == "Count"
            ? g => new { g.Key, Seniors = g.Count(person => person.Age >= 40) }
            : g => new { g.Key, Seniors = g.LongCount(person => person.Age >= 40) };

        var reason = ScalarGroupByValidation.DescribeUnsupported(Model(
            groupBy: new GroupByFragment(key, null, null),
            projection: new ProjectionShape(ProjectionKind.Anonymous, projection)));

        Assert.Equal(
            $"{method}(predicate) over a scalar group is not supported; filter before GroupBy",
            reason);
    }

    [Fact]
    public void PostGroupDistinctOrderingAndPaging_RetainMaterializeFirstReason()
    {
        Expression<Func<Person, string>> key = person => person.Department;
        Expression<Func<IGrouping<string, Person>, object>> projection =
            g => new { Department = g.Key, Count = g.LongCount() };

        foreach (var model in new[]
        {
            Model(
                groupBy: new GroupByFragment(key, null, null),
                projection: new ProjectionShape(ProjectionKind.Anonymous, projection),
                distinct: true),
            Model(
                groupBy: new GroupByFragment(key, null, null),
                projection: new ProjectionShape(ProjectionKind.Anonymous, projection),
                ordering: [new OrderingKey(key, descending: false, alias: null)]),
            Model(
                groupBy: new GroupByFragment(key, null, null),
                projection: new ProjectionShape(ProjectionKind.Anonymous, projection),
                paging: new Paging(1, 2)),
        })
        {
            Assert.Equal(
                "Distinct, ordering, and paging over a grouped result are not supported; " +
                "materialize the grouped query first",
                ScalarGroupByValidation.DescribeUnsupported(model));
        }
    }

    [Fact]
    public void PostGroupFilter_RetainsFilterBeforeGroupByReason()
    {
        Expression<Func<Person, string>> key = person => person.Department;
        Expression<Func<IGrouping<string, Person>, object>> projection =
            g => new { Department = g.Key, Count = g.LongCount() };
        Expression<Func<GroupedRow, bool>> rowFilter = row => row.Count > 1;

        var reason = ScalarGroupByValidation.DescribeUnsupported(Model(
            groupBy: new GroupByFragment(key, null, null),
            projection: new ProjectionShape(ProjectionKind.Anonymous, projection),
            predicates: [new PredicateFragment(rowFilter, alias: null)]));

        Assert.Equal("filtering a grouped result is not supported; apply Where before GroupBy", reason);
    }

    [Fact]
    public void UnrecognizedGroupOperation_ReportsGrammarIncludingLongCount()
    {
        Expression<Func<Person, string>> key = person => person.Department;
        Expression<Func<IGrouping<string, Person>, object>> projection = g => new { Bare = g };

        var reason = ScalarGroupByValidation.DescribeUnsupported(Model(
            groupBy: new GroupByFragment(key, null, null),
            projection: new ProjectionShape(ProjectionKind.Anonymous, projection)));

        Assert.Equal(
            "the scalar group may only be referenced through Key, Count, LongCount, Sum, Average, Min, or Max",
            reason);
    }

    private static GraphQueryModel Model(
        GroupByFragment groupBy,
        ProjectionShape? projection = null,
        IReadOnlyList<PredicateFragment>? predicates = null,
        IReadOnlyList<OrderingKey>? ordering = null,
        Paging? paging = null,
        bool distinct = false) =>
        new(
            new NodeRoot(typeof(Person)),
            predicates ?? [],
            [],
            projection,
            ordering ?? [],
            paging ?? new Paging(null, null),
            TerminalOperation.ToListOrArray,
            distinct,
            null,
            null,
            null,
            null,
            groupBy,
            null,
            null);

    private sealed record GroupedRow(string Department, long Count);

    [Node("SCALAR_GROUP_BY_VALIDATION_PERSON")]
    private sealed record Person : Node
    {
        public string Department { get; init; } = string.Empty;

        public int Age { get; init; }
    }
}

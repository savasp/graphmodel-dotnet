// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using Cvoya.Graph.Neo4j.Translation.Tests.Harness;
using Cvoya.Graph.Neo4j.Translation.Tests.Model;

/// <summary>
/// Characterizes the Cypher produced for correlated collection projections (#120): grouping path
/// segments by their start node and projecting per-group pattern comprehensions and pattern-count
/// subqueries over the traversal.
/// </summary>
public class GroupByTranslationTests : TranslationTestBase
{
    [Fact]
    public Task GroupByStartNode_KeyCountAndCollection()
    {
        var query = Root.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .GroupBy(s => s.StartNode)
            .Select(group => new
            {
                Name = group.Key.FirstName,
                FriendCount = group.Count(),
                FriendNames = group.Select(s => s.EndNode.FirstName).ToList(),
            });
        return VerifyTranslation((IQueryable<object>)query);
    }

    [Fact]
    public Task GroupByStartNode_PredicateCountArrayAndSourceOrdering()
    {
        var query = Root.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .GroupBy(s => s.StartNode)
            .Select(group => new
            {
                Name = group.Key.FirstName,
                YoungFriendCount = group.Count(segment => segment.EndNode.Age < 30),
                Friends = group
                    .OrderBy(segment => segment.StartNode.Age)
                    .Select(segment => new { segment.EndNode.FirstName, segment.EndNode.Age })
                    .ToArray(),
            });
        return VerifyTranslation((IQueryable<object>)query);
    }

    [Fact]
    public Task GroupByStartNode_OuterOrderingThrowsInsteadOfBeingIgnored()
    {
        var query = Root.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .GroupBy(s => s.StartNode)
            .Select(group => new
            {
                Name = group.Key.FirstName,
                Friends = group.Select(segment => segment.EndNode.FirstName).ToList(),
            })
            .OrderBy(result => result.Name);
        return VerifyTranslationThrows((IQueryable<object>)query);
    }

    [Fact]
    public Task GroupByStartNode_FilteredCollectionAndCount()
    {
        var query = Root.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .Where(path => path.EndNode.Age < 30)
            .GroupBy(s => s.StartNode)
            .Select(group => new
            {
                PersonName = group.Key.FirstName,
                YoungFriends = group.Select(g => g.EndNode.FirstName).ToList(),
                YoungFriendCount = group.Count(),
            });
        return VerifyTranslation((IQueryable<object>)query);
    }

    [Fact]
    public Task GroupByStartNode_InnerWhereCollection()
    {
        var query = Root.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .GroupBy(s => s.StartNode)
            .Select(group => new
            {
                PersonName = group.Key.FirstName,
                RecentFriends = group
                    .Where(k => k.Relationship.Since > 5)
                    .Select(k => k.EndNode.FirstName)
                    .ToList(),
            });
        return VerifyTranslation((IQueryable<object>)query);
    }

    [Fact]
    public Task GroupByStartNode_NestedRecordCollection()
    {
        var query = Root.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .GroupBy(s => s.StartNode)
            .Select(group => new
            {
                PersonName = group.Key.FirstName,
                Friends = group.Select(s => new { s.EndNode.FirstName, s.EndNode.Age }).ToList(),
            });
        return VerifyTranslation((IQueryable<object>)query);
    }

    [Fact]
    public Task GroupByStartNode_Aggregates()
    {
        var query = Root.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .GroupBy(s => s.StartNode)
            .Select(group => new
            {
                PersonName = group.Key.FirstName,
                FriendCount = group.Count(),
                AverageFriendAge = group.Average(k => k.EndNode.Age),
                OldestFriend = group.Max(k => k.EndNode.Age),
                YoungestFriend = group.Min(k => k.EndNode.Age),
            });
        return VerifyTranslation((IQueryable<object>)query);
    }

    [Fact]
    public Task GroupByStartNode_OrderedCollection()
    {
        var query = Root.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .GroupBy(s => s.StartNode)
            .Select(group => new
            {
                PersonName = group.Key.FirstName,
                FriendsByAge = group
                    .OrderBy(k => k.EndNode.Age)
                    .Select(k => new { Name = k.EndNode.FirstName, Age = k.EndNode.Age })
                    .ToList(),
            });
        return VerifyTranslation((IQueryable<object>)query);
    }

    [Fact]
    public Task GroupByStartNode_NestedGrouping()
    {
        var query = Root.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .GroupBy(s => s.StartNode)
            .Select(group => new
            {
                PersonName = group.Key.FirstName,
                AgeGroups = group
                    .GroupBy(k => k.EndNode.Age >= 30 ? "Senior" : "Junior")
                    .Select(g => new
                    {
                        Group = g.Key,
                        Count = g.Count(),
                        Names = g.Select(k => k.EndNode.FirstName).ToList(),
                    })
                    .ToList(),
            });
        return VerifyTranslation((IQueryable<object>)query);
    }

    [Fact]
    public Task ScalarKey_KeyAndAggregates()
    {
        var query = Root.Nodes<Person>()
            .GroupBy(p => p.LastName)
            .Select(g => new
            {
                LastName = g.Key,
                Count = g.Count(),
                AverageAge = g.Average(p => p.Age),
                Oldest = g.Max(p => p.Age),
                Youngest = g.Min(p => p.Age),
                TotalAge = g.Sum(p => p.Age),
            });
        return VerifyTranslation((IQueryable<object>)query);
    }

    [Fact]
    public Task ScalarKey_WhereBeforeGroupBy()
    {
        var query = Root.Nodes<Person>()
            .Where(p => p.Age >= 18)
            .GroupBy(p => p.LastName)
            .Select(g => new { g.Key, Count = g.Count() });
        return VerifyTranslation((IQueryable<object>)query);
    }

    [Fact]
    public Task ScalarKey_DistinctKeysWithoutAggregate()
    {
        var query = Root.Nodes<Person>()
            .GroupBy(p => p.LastName)
            .Select(g => g.Key);
        return VerifyTranslation((IQueryable<string>)query);
    }

    [Fact]
    public Task ScalarKey_ResultSelectorOverload()
    {
        var query = Root.Nodes<Person>()
            .GroupBy(p => p.LastName, (last, group) => new { LastName = last, Count = group.Count() });
        return VerifyTranslation((IQueryable<object>)query);
    }

    [Fact]
    public Task ScalarKey_ComputedKeyExpression()
    {
        var query = Root.Nodes<Person>()
            .GroupBy(p => p.Age >= 18 ? "Adult" : "Minor")
            .Select(g => new { Category = g.Key, Count = g.Count() });
        return VerifyTranslation((IQueryable<object>)query);
    }

    [Fact]
    public Task ScalarKey_OuterOrderingThrowsInsteadOfBeingIgnored()
    {
        var query = Root.Nodes<Person>()
            .GroupBy(p => p.LastName)
            .Select(g => new { g.Key, Count = g.Count() })
            .OrderBy(result => result.Key);
        return VerifyTranslationThrows((IQueryable<object>)query);
    }

    [Fact]
    public Task ScalarKey_EntityKeyThrows()
    {
        var query = Root.Nodes<Person>()
            .GroupBy(p => p.HomeAddress)
            .Select(g => new { g.Key, Count = g.Count() });
        return VerifyTranslationThrows((IQueryable<object>)query);
    }
}

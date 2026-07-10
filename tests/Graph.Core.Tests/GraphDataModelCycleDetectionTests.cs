// Copyright 2025 Savas Parastatidis
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


[Trait("Area", "GraphDataModel")]
public class GraphDataModelCycleDetectionTests
{
    public static TheoryData<string, Func<object?>, bool> CycleCases => new()
    {
        { "null", () => null, false },
        { "simple string", () => "value", false },
        { "simple array", () => new[] { 1, 2, 3 }, false },
        { "acyclic node", CreateAcyclicNode, false },
        { "shared diamond", CreateSharedDiamond, false },
        { "deep acyclic chain", () => CreateDeepChain(12), false },
        { "self reference", CreateSelfReference, true },
        { "two node cycle", CreateTwoNodeCycle, true },
        { "deep cycle", CreateDeepCycle, true },
        { "cycle through list", CreateListCycle, true },
        { "cycle through dictionary", CreateDictionaryCycle, true },
        { "list with shared reference", CreateListWithSharedReference, false },
    };

    [Theory]
    [MemberData(nameof(CycleCases))]
    public void HasReferenceCycle_ReturnsExpectedResult(string name, Func<object?> factory, bool expected)
    {
        _ = name;

        Assert.Equal(expected, GraphDataModel.HasReferenceCycle(factory()!));
    }

    [Fact]
    public void EnsureNoReferenceCycle_DoesNotThrowForAcyclicEntity()
    {
        var entity = new CycleEntity { Name = "root", Child = new CycleEntity { Name = "child" } };

        entity.EnsureNoReferenceCycle();
    }

    [Fact]
    public void EnsureNoReferenceCycle_ThrowsGraphExceptionForCycle()
    {
        var entity = new CycleEntity { Name = "root" };
        entity.Child = entity;

        var exception = Assert.Throws<GraphException>(entity.EnsureNoReferenceCycle);
        Assert.Contains(entity.Id, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnforceGraphConstraintsForEntity_ThrowsGraphExceptionForEmptyId()
    {
        var entity = new ConstraintNode { Id = string.Empty };

        var exception = Assert.Throws<GraphException>(() => GraphDataModel.EnforceGraphConstraintsForEntity(entity));

        Assert.Contains("Entity ID cannot be null or empty", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnforceGraphConstraintsForRelationship_ThrowsGraphExceptionForMissingEndpoint()
    {
        var relationship = new ConstraintRelationship(string.Empty, "target");

        var exception = Assert.Throws<GraphException>(() => GraphDataModel.EnforceGraphConstraintsForRelationship(relationship));

        Assert.Contains("Relationship source and target IDs cannot be null or empty", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnforceGraphConstraintsForRelationship_AllowsValidRelationship()
    {
        var relationship = new ConstraintRelationship("source", "target");

        GraphDataModel.EnforceGraphConstraintsForRelationship(relationship);
    }

    private static CycleNode CreateAcyclicNode() => new()
    {
        Name = "root",
        Next = new CycleNode { Name = "child" },
    };

    private static CycleNode CreateSharedDiamond()
    {
        var shared = new CycleNode { Name = "shared" };
        var root = new CycleNode { Name = "root", Next = shared };
        root.Children.Add(shared);
        root.Values["alsoShared"] = shared;
        return root;
    }

    private static CycleNode CreateDeepChain(int depth)
    {
        var root = new CycleNode { Name = "0" };
        var current = root;

        for (var i = 1; i < depth; i++)
        {
            current.Next = new CycleNode { Name = i.ToString() };
            current = current.Next;
        }

        return root;
    }

    private static CycleNode CreateSelfReference()
    {
        var root = new CycleNode { Name = "root" };
        root.Next = root;
        return root;
    }

    private static CycleNode CreateTwoNodeCycle()
    {
        var first = new CycleNode { Name = "first" };
        var second = new CycleNode { Name = "second", Next = first };
        first.Next = second;
        return first;
    }

    private static CycleNode CreateDeepCycle()
    {
        var root = CreateDeepChain(8);
        var current = root;

        while (current.Next is not null)
        {
            current = current.Next;
        }

        current.Next = root;
        return root;
    }

    private static CycleNode CreateListCycle()
    {
        var root = new CycleNode { Name = "root" };
        var child = new CycleNode { Name = "child" };
        root.Children.Add(child);
        child.Children.Add(root);
        return root;
    }

    private static CycleNode CreateDictionaryCycle()
    {
        var root = new CycleNode { Name = "root" };
        root.Values["self"] = root;
        return root;
    }

    private static CycleNode CreateListWithSharedReference()
    {
        var shared = new CycleNode { Name = "shared" };
        var root = new CycleNode { Name = "root" };
        root.Children.Add(shared);
        root.Children.Add(shared);
        return root;
    }

    private sealed class CycleNode
    {
        public string Name { get; init; } = string.Empty;

        public CycleNode? Next { get; set; }

        public List<CycleNode> Children { get; } = new();

        public Dictionary<string, object?> Values { get; } = new();
    }

    private sealed record CycleEntity : Node
    {
        public string Name { get; init; } = string.Empty;

        public CycleEntity? Child { get; set; }
    }

    private sealed record ConstraintNode : Node;

    private sealed record ConstraintRelationship(string Start, string End) : Relationship(Start, End);
}

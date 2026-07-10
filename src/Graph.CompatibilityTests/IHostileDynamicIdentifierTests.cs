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

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Contract coverage for #150: <see cref="DynamicNode.Labels"/> and
/// <see cref="DynamicRelationship.Type"/> are caller-supplied strings that end up in Cypher
/// identifier position (labels, relationship types) rather than parameter-value position. A
/// hostile value there (embedded backtick, an attempted statement break-out, control characters)
/// must either be rejected with a documented validation error, or - if accepted - stored and
/// round-tripped as an inert literal string with no side effects on any other data in the graph.
/// It must never be able to execute as Cypher.
/// </summary>
public interface IHostileDynamicIdentifierTests : IGraphModelTest
{
    /// <summary>
    /// A label containing an embedded backtick is either rejected outright, or - if accepted -
    /// creates exactly the one node with the label stored verbatim, and does not affect any
    /// other node in the graph (in particular, it must not have been able to break out of label
    /// position and execute a second Cypher clause).
    /// </summary>
    [Fact]
    public async Task DynamicNode_WithBacktickInLabel_IsRejectedOrStoredSafelyWithNoSideEffects()
    {
        var sentinel = new Person { FirstName = "Sentinel", LastName = "Untouched" };
        await Graph.CreateNodeAsync(sentinel, null, TestContext.Current.CancellationToken);

        var hostileLabel = "Evil`) DETACH DELETE n //";
        var hostileNode = new DynamicNode
        {
            Labels = [hostileLabel],
            Properties = new Dictionary<string, object?> { ["marker"] = "hostile-label-backtick" }
        };

        try
        {
            await Graph.CreateNodeAsync(hostileNode, null, TestContext.Current.CancellationToken);

            // Accepted: the label must have been stored as the literal string, not executed,
            // and the sentinel node created before it must be untouched.
            var retrieved = await Graph.GetDynamicNodeAsync(hostileNode.Id, null, TestContext.Current.CancellationToken);
            Assert.Contains(hostileLabel, retrieved.Labels);
        }
        catch (GraphException)
        {
            // Rejected: also an acceptable outcome per the issue's documented contract.
        }

        var sentinelStillPresent = await Graph.Nodes<Person>()
            .Where(p => p.Id == sentinel.Id)
            .CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, sentinelStillPresent);
    }

    /// <summary>
    /// A relationship type containing an embedded backtick and a Cypher-looking payload is
    /// either rejected outright, or - if accepted - creates exactly the one relationship with
    /// the type stored verbatim, with no side effects on the two nodes it connects or any other
    /// data in the graph.
    /// </summary>
    [Fact]
    public async Task DynamicRelationship_WithBacktickInType_IsRejectedOrStoredSafelyWithNoSideEffects()
    {
        var start = new Person { FirstName = "Start", LastName = "Node" };
        var end = new Person { FirstName = "End", LastName = "Node" };
        await Graph.CreateNodeAsync(start, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(end, null, TestContext.Current.CancellationToken);

        var hostileType = "KNOWS`]-(a) DETACH DELETE a //";
        var hostileRelationship = new DynamicRelationship(
            start.Id,
            end.Id,
            hostileType,
            new Dictionary<string, object?> { ["marker"] = "hostile-type-backtick" });

        try
        {
            await Graph.CreateRelationshipAsync(hostileRelationship, null, TestContext.Current.CancellationToken);

            var retrieved = await Graph.GetDynamicRelationshipAsync(hostileRelationship.Id, null, TestContext.Current.CancellationToken);
            Assert.Equal(hostileType, retrieved.Type);
        }
        catch (GraphException)
        {
            // Rejected: also an acceptable outcome per the issue's documented contract.
        }

        // Neither endpoint node was affected by the attempted break-out.
        var startStillPresent = await Graph.Nodes<Person>()
            .Where(p => p.Id == start.Id)
            .CountAsync(TestContext.Current.CancellationToken);
        var endStillPresent = await Graph.Nodes<Person>()
            .Where(p => p.Id == end.Id)
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, startStillPresent);
        Assert.Equal(1, endStillPresent);
    }

    /// <summary>
    /// A label containing a newline plus a Cypher-looking payload (no backtick involved) must
    /// also be rejected or safely contained - control characters are never legitimate in a
    /// label, so rejection is the expected and preferred outcome, but the safety property
    /// (no side effects on other data) must hold regardless.
    /// </summary>
    [Fact]
    public async Task DynamicNode_WithControlCharacterInLabel_IsRejectedOrStoredSafelyWithNoSideEffects()
    {
        var sentinel = new Person { FirstName = "Sentinel2", LastName = "Untouched2" };
        await Graph.CreateNodeAsync(sentinel, null, TestContext.Current.CancellationToken);

        var hostileLabel = "Evil\nMATCH (n) DETACH DELETE n";
        var hostileNode = new DynamicNode
        {
            Labels = [hostileLabel],
            Properties = new Dictionary<string, object?> { ["marker"] = "hostile-label-control-char" }
        };

        try
        {
            await Graph.CreateNodeAsync(hostileNode, null, TestContext.Current.CancellationToken);

            var retrieved = await Graph.GetDynamicNodeAsync(hostileNode.Id, null, TestContext.Current.CancellationToken);
            Assert.Contains(hostileLabel, retrieved.Labels);
        }
        catch (GraphException)
        {
            // Rejected: the expected outcome for a control character in a label.
        }

        var sentinelStillPresent = await Graph.Nodes<Person>()
            .Where(p => p.Id == sentinel.Id)
            .CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, sentinelStillPresent);
    }

    /// <summary>
    /// An empty relationship type is never a legitimate identifier and must be rejected with a
    /// documented validation error rather than silently accepted or interpolated.
    /// </summary>
    [Fact]
    public async Task DynamicRelationship_WithEmptyType_ThrowsGraphException()
    {
        var start = new Person { FirstName = "Start2", LastName = "Node2" };
        var end = new Person { FirstName = "End2", LastName = "Node2" };
        await Graph.CreateNodeAsync(start, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(end, null, TestContext.Current.CancellationToken);

        var relationship = new DynamicRelationship(
            start.Id,
            end.Id,
            string.Empty,
            new Dictionary<string, object?>());

        await Assert.ThrowsAsync<GraphException>(
            () => Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken));
    }
}

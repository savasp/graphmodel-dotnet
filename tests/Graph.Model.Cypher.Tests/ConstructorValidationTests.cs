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

namespace Cvoya.Graph.Model.Cypher.Tests;

using Cvoya.Graph.Model.Cypher.Ast;
using Cvoya.Graph.Model.Cypher.Ast.Expressions;

public class ConstructorValidationTests
{
    [Fact]
    public void CypherStatement_RejectsEmptyClauses()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() => new CypherStatement([], new Dictionary<string, object?>()));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DepthRange_RejectsNegativeMinimum()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() => new DepthRange(-1, 1));

        Assert.Contains("minimum", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DepthRange_RejectsInvertedRange()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() => new DepthRange(2, 1));

        Assert.Contains("maximum", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PathPattern_RejectsNonAlternatingElements()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(
            () => new PathPattern(
            [
                new NodePattern("a", []),
                new NodePattern("b", [])
            ]));

        Assert.Contains("alternate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PathPattern_RejectsRelationshipEndpoint()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(
            () => new PathPattern(
            [
                new NodePattern("a", []),
                new RelationshipPattern("r", "KNOWS", CypherDirection.Outgoing, null)
            ]));

        Assert.Contains("node", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequiredNames_RejectWhitespace()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() => new VariableRef(" "));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OptionalNames_RejectEmptyValues()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() => new NodePattern(string.Empty, []));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequiredCollections_RejectNullElements()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(
            () => new MatchClause([null!], optional: false));

        Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructors_CopyInputCollections()
    {
        var labels = new List<string> { "Person" };
        var node = new NodePattern("n", labels);

        labels.Add("Mutated");

        Assert.Equal(["Person"], node.Labels);
    }

    [Fact]
    public void ExpressionConstructors_RejectNullOperands()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() => new PropertyAccess(null!, "name"));

        Assert.Contains("target", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

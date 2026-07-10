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

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using Cvoya.Graph.Neo4j.Translation.Tests.Harness;

/// <summary>
/// Coverage for #150: <c>HasProperty</c>/<c>GetProperty</c> translate a property name into a
/// Cypher identifier (property-access position), not a parameter value, so it cannot go through
/// the normal value-parameterization path. These tests assert that a compile-time-constant
/// property name - including hostile ones (embedded backtick, quotes, braces, statement
/// separators) - is always fully escaped in the generated Cypher, and that a non-constant
/// property name is rejected outright rather than interpolated unescaped.
/// </summary>
public class DynamicEntityMethodTranslationTests : TranslationTestBase
{
    [Fact]
    public Task GetProperty_OrdinaryName()
    {
        var query = Root.Nodes<DynamicNode>().Where(n => n.GetProperty<string>("name") == "Alice");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task HasProperty_OrdinaryName()
    {
        var query = Root.Nodes<DynamicNode>().Where(n => n.HasProperty("name"));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task GetProperty_NameContainsEmbeddedBacktick()
    {
        // A naive `{name}` wrap without backtick-doubling would let this break out of the
        // property-access position; the escaped output must double the embedded backtick.
        var query = Root.Nodes<DynamicNode>().Where(n => n.GetProperty<string>("na`me") == "x");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task HasProperty_NameContainsEmbeddedBacktick()
    {
        var query = Root.Nodes<DynamicNode>().Where(n => n.HasProperty("na`me"));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task GetProperty_NameContainsBacktickBreakoutAttempt()
    {
        // A property name that itself looks like an attempt to break out of backtick-quoting
        // and inject a second clause.
        var query = Root.Nodes<DynamicNode>().Where(n => n.GetProperty<string>("x`}) DETACH DELETE n //") == "y");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task GetProperty_NameContainsQuotesAndBraces()
    {
        var query = Root.Nodes<DynamicNode>().Where(n => n.GetProperty<string>("a\"b'c{d}e") == "x");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task GetProperty_NameContainsStatementSeparator()
    {
        var query = Root.Nodes<DynamicNode>().Where(n => n.GetProperty<string>("a; MATCH (m) DETACH DELETE m") == "x");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task GetProperty_NameContainsNewlineIsRejected()
    {
        // A newline is a control character: never legitimate in a property name, and rejected
        // outright rather than escaped and interpolated.
        var query = Root.Nodes<DynamicNode>().Where(n => n.GetProperty<string>("a\nMATCH (m) DETACH DELETE m") == "x");
        return VerifyTranslationThrows(query);
    }

    [Fact]
    public Task GetProperty_EmptyNameIsRejected()
    {
        var query = Root.Nodes<DynamicNode>().Where(n => n.GetProperty<string>("") == "x");
        return VerifyTranslationThrows(query);
    }

    [Fact]
    public Task GetProperty_NonConstantNameIsRejected()
    {
        // Cypher has no parameterized property-access syntax, so a computed/variable property
        // name has no safe translation and must be rejected rather than interpolated.
        var propertyName = "name";
        var query = Root.Nodes<DynamicNode>().Where(n => n.GetProperty<string>(propertyName + "!") == "x");
        return VerifyTranslationThrows(query);
    }

    [Fact]
    public Task HasProperty_NonConstantNameIsRejected()
    {
        var propertyName = "name";
        var query = Root.Nodes<DynamicNode>().Where(n => n.HasProperty(propertyName + "!"));
        return VerifyTranslationThrows(query);
    }
}

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

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using Cvoya.Graph.Neo4j.Querying.Cypher;

/// <summary>
/// Hostile-input coverage for #150: <see cref="CypherIdentifier"/> is the sole gate between
/// caller-supplied labels, relationship types, property names, and database names and their
/// interpolation into Cypher text (parameters cannot cover identifier position). Every case here
/// either rejects the input with a precise error or emits a fully-escaped identifier - never a
/// raw interpolation.
/// </summary>
public class CypherIdentifierTests
{
    [Theory]
    [InlineData("Person")]
    [InlineData("KNOWS")]
    [InlineData("a")]
    [InlineData("multi word label")]
    [InlineData("unicode-Ñame-é")]
    public void Escape_OrdinaryIdentifier_IsWrappedInBackticks(string identifier)
    {
        var escaped = CypherIdentifier.Escape(identifier);

        Assert.Equal($"`{identifier}`", escaped);
    }

    [Fact]
    public void Escape_EmbeddedBacktick_IsDoubled()
    {
        // The critical case: naive backtick-wrapping without doubling is bypassable, e.g.
        // "a`}) DETACH DELETE n //" would close the identifier and inject a second clause.
        var escaped = CypherIdentifier.Escape("a`b");

        Assert.Equal("`a``b`", escaped);
    }

    [Fact]
    public void Escape_MultipleEmbeddedBackticks_AreAllDoubled()
    {
        var escaped = CypherIdentifier.Escape("`a`b`");

        Assert.Equal("```a``b```", escaped);
    }

    [Fact]
    public void Escape_BacktickBreakoutAttempt_IsFullyNeutralized()
    {
        var hostile = "Person`) DETACH DELETE n //";

        var escaped = CypherIdentifier.Escape(hostile);

        // The escaped form must contain no unescaped (odd-run) backtick that could terminate
        // the quoted identifier early.
        Assert.Equal("`Person``) DETACH DELETE n //`", escaped);
        AssertNoUnescapedBacktick(escaped);
    }

    [Theory]
    [InlineData("\"quoted\"")]
    [InlineData("with 'apostrophe'")]
    [InlineData("with {braces}")]
    [InlineData("statement; separator")]
    public void Escape_QuotesAndBracesAndSeparators_AreEmittedVerbatimInsideBackticks(string identifier)
    {
        // Backtick-quoting does not need to (and must not) touch characters other than
        // backticks - quotes, braces, and semicolons are inert inside a backtick-quoted
        // identifier, so they must appear unchanged.
        var escaped = CypherIdentifier.Escape(identifier);

        Assert.Equal($"`{identifier}`", escaped);
        AssertNoUnescapedBacktick(escaped);
    }

    [Fact]
    public void Escape_Null_Throws()
    {
        var ex = Assert.Throws<GraphException>(() => CypherIdentifier.Escape(null, "node label"));
        Assert.Contains("node label", ex.Message);
    }

    [Fact]
    public void Escape_Empty_Throws()
    {
        Assert.Throws<GraphException>(() => CypherIdentifier.Escape(string.Empty));
    }

    [Fact]
    public void Escape_WhitespaceOnly_Throws()
    {
        Assert.Throws<GraphException>(() => CypherIdentifier.Escape("   "));
    }

    /// <summary>
    /// Hostile identifiers containing control characters, built with numeric char codes (rather
    /// than literal control bytes in source) to keep the source file itself free of raw control
    /// characters.
    /// </summary>
    public static TheoryData<string> ControlCharacterIdentifiers()
    {
        return new TheoryData<string>
        {
            "a\nb", // LF
            "a\rb", // CR
            "a\tb", // TAB
            "a\0b", // NUL
            "a" + (char)7 + "b", // BEL
            "a" + (char)27 + "b", // ESC
            "a" + (char)1 + "b", // SOH
        };
    }

    [Theory]
    [MemberData(nameof(ControlCharacterIdentifiers))]
    public void Escape_ControlCharacters_Throw(string identifier)
    {
        var ex = Assert.Throws<GraphException>(() => CypherIdentifier.Escape(identifier, "relationship type"));
        Assert.Contains("relationship type", ex.Message);
    }

    [Fact]
    public void Escape_StatementSeparatorEmbeddedWithNewline_IsRejected()
    {
        // Newline plus a Cypher-looking payload: must not be usable to break the query even
        // though the payload does not itself contain a backtick.
        var hostile = "a\nMATCH (m) DETACH DELETE m";
        Assert.Throws<GraphException>(() => CypherIdentifier.Escape(hostile));
    }

    [Fact]
    public void Validate_AcceptsOrdinaryIdentifier()
    {
        var exception = Record.Exception(() => CypherIdentifier.Validate("Person"));
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_RejectsControlCharacter()
    {
        Assert.Throws<GraphException>(() => CypherIdentifier.Validate("a\0b"));
    }

    [Fact]
    public void EscapeLabels_JoinsEscapedLabelsWithColon()
    {
        var result = CypherIdentifier.EscapeLabels(["Person", "Employee"]);

        Assert.Equal("`Person`:`Employee`", result);
    }

    [Fact]
    public void EscapeLabels_EscapesEachLabelIndependently()
    {
        var result = CypherIdentifier.EscapeLabels(["a`b", "c"]);

        Assert.Equal("`a``b`:`c`", result);
    }

    [Fact]
    public void EscapeLabels_AnyHostileLabelInTheSetThrows()
    {
        Assert.Throws<GraphException>(() => CypherIdentifier.EscapeLabels(["Person", ""]));
    }

    private static void AssertNoUnescapedBacktick(string escaped)
    {
        // Strip the outer quoting, then confirm every remaining backtick appears as part of a
        // doubled pair (an odd/unescaped backtick would mean the identifier is not safely
        // quoted).
        Assert.StartsWith("`", escaped, StringComparison.Ordinal);
        Assert.EndsWith("`", escaped, StringComparison.Ordinal);
        var inner = escaped[1..^1];

        var i = 0;
        while (i < inner.Length)
        {
            if (inner[i] == '`')
            {
                Assert.True(i + 1 < inner.Length && inner[i + 1] == '`', "Found an unescaped backtick inside an escaped identifier.");
                i += 2;
            }
            else
            {
                i++;
            }
        }
    }
}

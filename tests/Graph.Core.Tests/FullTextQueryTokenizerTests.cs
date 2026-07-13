// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using Cvoya.Graph.Querying;

[Trait("Area", "FullTextSearch")]
public class FullTextQueryTokenizerTests
{
    [Fact]
    public void Tokenize_SplitsOnWhitespaceAndLowercases()
    {
        Assert.Equal(["cloud", "computing"], FullTextQueryTokenizer.Tokenize("Cloud Computing"));
    }

    [Fact]
    public void Tokenize_SplitsOnPunctuationAndMetacharacters()
    {
        // Lucene metacharacters and punctuation are term separators, never syntax.
        Assert.Equal(["vacation"], FullTextQueryTokenizer.Tokenize("vacation~"));
        Assert.Equal(["vacation"], FullTextQueryTokenizer.Tokenize("vacation*"));
        Assert.Equal(["cloud", "computing"], FullTextQueryTokenizer.Tokenize("cloud+computing"));
        Assert.Equal(["a", "b", "c"], FullTextQueryTokenizer.Tokenize("a-b:c"));
    }

    [Fact]
    public void Tokenize_KeepsUnicodeLettersAndDigits()
    {
        Assert.Equal(["café", "münchen"], FullTextQueryTokenizer.Tokenize("Café München"));
        Assert.Equal(["abc123", "42"], FullTextQueryTokenizer.Tokenize("abc123 42"));
    }

    [Fact]
    public void Tokenize_LowercasesWithInvariantCulture()
    {
        Assert.Equal(["istanbul"], FullTextQueryTokenizer.Tokenize("ISTANBUL"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("~*+-:/")]
    public void Tokenize_ReturnsEmptyWhenNoTerms(string query)
    {
        Assert.Empty(FullTextQueryTokenizer.Tokenize(query));
    }
}

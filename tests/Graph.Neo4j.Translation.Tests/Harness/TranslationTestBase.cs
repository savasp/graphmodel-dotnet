// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests.Harness;

using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using VerifyXunit;

/// <summary>
/// Common base for characterization test classes. Deliberately does not inherit
/// <c>VerifyXunit.VerifyBase</c>: that type resolves the calling source file via
/// <c>[CallerFilePath]</c> on its constructor, which only captures the *immediate* call site -
/// for a shared base class, every derived test class's compiler-synthesized implicit
/// <c>: base()</c> call trips Verify's "ctor must be called explicitly" guard. Using the static
/// <c>VerifyXunit.Verifier.Verify(...)</c> API instead, with <c>[CallerFilePath]</c> forwarded
/// through these helper methods, avoids that pitfall while still keeping snapshots named after
/// each test's own source file.
/// </summary>
public abstract class TranslationTestBase
{
    protected static Task VerifyTranslation<T>(
        IQueryable<T> queryable,
        [CallerFilePath] string sourceFile = "") =>
        Verifier.Verify(CypherTranslator.Translate(queryable), sourceFile: sourceFile);

    protected static Task VerifyTranslation(
        Type rootType,
        Expression expression,
        [CallerFilePath] string sourceFile = "") =>
        Verifier.Verify(CypherTranslator.Translate(rootType, expression), sourceFile: sourceFile);

    protected static Task VerifyTranslationThrows<T>(
        IQueryable<T> queryable,
        [CallerFilePath] string sourceFile = "") =>
        Verifier.Verify(CypherTranslator.TranslateExpectingException(queryable), sourceFile: sourceFile);

    protected static Task VerifyTranslationThrows(
        Type rootType,
        Expression expression,
        [CallerFilePath] string sourceFile = "") =>
        Verifier.Verify(CypherTranslator.TranslateExpectingException(rootType, expression), sourceFile: sourceFile);
}

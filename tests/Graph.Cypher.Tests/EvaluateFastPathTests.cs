// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Tests;

using System.Globalization;
using System.Linq.Expressions;
using Cvoya.Graph.Cypher.Planning;

/// <summary>
/// Equivalence coverage for #216: <see cref="ExpressionToCypherAstLowerer.TryEvaluateDirect"/>
/// must produce exactly what <c>Expression.Compile</c> produces for the shapes it claims, and
/// must decline (so the lowerer falls back to compilation) for everything else. Each test runs
/// BOTH paths explicitly - the compiled result is the ground truth, not an assumption.
/// </summary>
public class EvaluateFastPathTests
{
    private static readonly int staticProperty = 41;

    private static int StaticProperty => staticProperty;

    [Fact]
    public void Constant_TakesFastPathAndMatchesCompiledResult()
    {
        var expression = Expression.Constant(42);

        AssertFastPathMatchesCompilation(expression);
    }

    [Fact]
    public void ClosureLocal_TakesFastPathAndMatchesCompiledResult()
    {
        var captured = 7;
        Expression<Func<int>> lambda = () => captured;

        AssertFastPathMatchesCompilation(lambda.Body);
    }

    [Fact]
    public void FieldOfCapturedObject_TakesFastPathAndMatchesCompiledResult()
    {
        var holder = new Holder { Field = 11 };
        Expression<Func<int>> lambda = () => holder.Field;

        // holder itself is a closure field, so this is member-over-member-over-constant.
        AssertFastPathMatchesCompilation(lambda.Body);
    }

    [Fact]
    public void PropertyOfCapturedObject_TakesFastPathAndMatchesCompiledResult()
    {
        var holder = new Holder { Field = 13 };
        Expression<Func<int>> lambda = () => holder.Property;

        AssertFastPathMatchesCompilation(lambda.Body);
    }

    [Fact]
    public void StaticProperty_TakesFastPathAndMatchesCompiledResult()
    {
        Expression<Func<int>> lambda = () => StaticProperty;

        AssertFastPathMatchesCompilation(lambda.Body);
    }

    [Fact]
    public void StaticField_TakesFastPathAndMatchesCompiledResult()
    {
        Expression<Func<int>> lambda = () => staticProperty;

        AssertFastPathMatchesCompilation(lambda.Body);
    }

    [Fact]
    public void NullConstant_TakesFastPathAndMatchesCompiledResult()
    {
        var expression = Expression.Constant(null, typeof(string));

        Assert.True(ExpressionToCypherAstLowerer.TryEvaluateDirect(expression, out var fast));
        Assert.Null(fast);
        Assert.Null(Compile(expression));
    }

    [Fact]
    public void MemberChainDeeperThanOneNestedLevel_FallsBackToCompilation()
    {
        var outer = new Nesting { Holder = new Holder { Field = 17 } };
        Expression<Func<int>> lambda = () => outer.Holder.Field;

        // outer is a closure field, so this is three member hops over the constant.
        Assert.False(ExpressionToCypherAstLowerer.TryEvaluateDirect(lambda.Body, out _));
        Assert.Equal(17, Compile(lambda.Body));
    }

    [Fact]
    public void MethodCall_FallsBackToCompilation()
    {
        Expression<Func<int>> lambda = () => int.Parse("29", CultureInfo.InvariantCulture);

        Assert.False(ExpressionToCypherAstLowerer.TryEvaluateDirect(lambda.Body, out _));
        Assert.Equal(29, Compile(lambda.Body));
    }

    [Fact]
    public void NullInstanceTarget_FallsBackToCompilation()
    {
        Holder? holder = null;
        Expression<Func<int>> lambda = () => holder!.Field;

        // The compiled path's failure shape is the contract; the fast path must not replace it
        // with a reflection error.
        Assert.False(ExpressionToCypherAstLowerer.TryEvaluateDirect(lambda.Body, out _));
    }

    [Fact]
    public void NullableValueReceiver_FallsBackToCompilation()
    {
        int? captured = 31;
        Expression<Func<int>> lambda = () => captured!.Value;

        // Nullable<T> boxes a non-null value as T. Reflection therefore cannot invoke the outer
        // Nullable<T>.Value member on the recursively evaluated boxed target, while compilation
        // preserves the nullable receiver and returns the expected value.
        Assert.False(ExpressionToCypherAstLowerer.TryEvaluateDirect(lambda.Body, out _));
        Assert.Equal(31, Compile(lambda.Body));
    }

    private static void AssertFastPathMatchesCompilation(Expression expression)
    {
        Assert.True(
            ExpressionToCypherAstLowerer.TryEvaluateDirect(expression, out var fast),
            $"Expected the fast path to evaluate '{expression}'.");

        Assert.Equal(Compile(expression), fast);
    }

    private static object? Compile(Expression expression) =>
        Expression.Lambda(expression).Compile().DynamicInvoke();

    private sealed class Holder
    {
        public int Field;

        public int Property => Field + 2;
    }

    private sealed class Nesting
    {
        public Holder Holder = new();
    }
}

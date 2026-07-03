// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");

namespace Cvoya.Graph.Model.Age.Tests;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Model.Age.Querying.Cypher.Execution;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular;
using Xunit;

/// <summary>
/// Regression tests for Issue 4: Wrong-constructor projection aliases.
/// Verifies that GetNewExpressionParameterName in both AGE classes
/// uses newExpr.Constructor instead of newExpr.Type.GetConstructors().FirstOrDefault().
/// </summary>
public sealed class ProjectionAliasFixTests
{
    /// <summary>
    /// A record with 2+ constructors (primary + copy constructor).
    /// Without the fix, GetConstructors().FirstOrDefault() could return
    /// the copy constructor instead of the primary one, causing wrong aliases.
    /// </summary>
    private sealed record TestProjection(string Name, int Value);

    #region ColumnDefinitionBuilder Tests

    [Fact]
    public void ColumnDefinitionBuilder_GetNewExpressionParameterName_UsesNewExprConstructor()
    {
        // Arrange
        var recordType = typeof(TestProjection);

        // Get the primary constructor (the one with string and int parameters)
        var primaryCtor = recordType.GetConstructors()
            .FirstOrDefault(c =>
            {
                var parameters = c.GetParameters();
                return parameters.Length == 2
                    && parameters[0].ParameterType == typeof(string)
                    && parameters[1].ParameterType == typeof(int);
            });

        Assert.NotNull(primaryCtor);

        // Build a NewExpression that uses the primary constructor explicitly
        var nameArg = Expression.Constant("hello");
        var valueArg = Expression.Constant(42);
        var newExpr = Expression.New(primaryCtor, [nameArg, valueArg]);

        Assert.Null(newExpr.Members);
        Assert.NotNull(newExpr.Constructor);
        Assert.Same(primaryCtor, newExpr.Constructor);

        // Act - invoke the private GetNewExpressionParameterName via reflection
        var methodInfo = typeof(ColumnDefinitionBuilder).GetMethod(
            "GetNewExpressionParameterName",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(methodInfo);
        Assert.NotNull(methodInfo); // suppress CS8602

        var param0Name = methodInfo.Invoke(null, [newExpr, 0]);
        var param1Name = methodInfo.Invoke(null, [newExpr, 1]);
        var outOfRangeResult = methodInfo.Invoke(null, [newExpr, 2]);

        // Assert
        Assert.Equal("Name", param0Name);
        Assert.Equal("Value", param1Name);
        Assert.Null(outOfRangeResult);

        // Verify the contract: the primary constructor's parameter names are used,
        // not the copy constructor's parameter name ("original").
        Assert.NotEqual("original", param0Name);
    }

    [Fact]
    public void ColumnDefinitionBuilder_GetNewExpressionParameterName_WithNullConstructor_ReturnsNull()
    {
        // Arrange - use a parameterless new expression (object has only default constructor)
        var newExpr = Expression.New(typeof(object));

        var methodInfo = typeof(ColumnDefinitionBuilder).GetMethod(
            "GetNewExpressionParameterName",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(methodInfo);

        // object's parameterless constructor has no parameters
        var result = methodInfo.Invoke(null, [newExpr, 0]);

        Assert.Null(result);
    }

    #endregion

    #region ProjectionFragmentVisitor Tests

    [Fact]
    public void ProjectionFragmentVisitor_GetNewExpressionParameterName_UsesNewExprConstructor()
    {
        // Arrange
        var recordType = typeof(TestProjection);

        var primaryCtor = recordType.GetConstructors()
            .FirstOrDefault(c =>
            {
                var parameters = c.GetParameters();
                return parameters.Length == 2
                    && parameters[0].ParameterType == typeof(string)
                    && parameters[1].ParameterType == typeof(int);
            });

        Assert.NotNull(primaryCtor);

        var nameArg = Expression.Constant("hello");
        var valueArg = Expression.Constant(42);
        var newExpr = Expression.New(primaryCtor, [nameArg, valueArg]);

        Assert.NotNull(newExpr.Constructor);
        Assert.Same(primaryCtor, newExpr.Constructor);

        // Act - invoke the private GetNewExpressionParameterName via reflection
        var methodInfo = typeof(ProjectionFragmentVisitor).GetMethod(
            "GetNewExpressionParameterName",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(methodInfo);

        var param0Name = methodInfo.Invoke(null, [newExpr, 0]);
        var param1Name = methodInfo.Invoke(null, [newExpr, 1]);
        var outOfRangeResult = methodInfo.Invoke(null, [newExpr, 2]);

        // Assert
        Assert.Equal("Name", param0Name);
        Assert.Equal("Value", param1Name);
        Assert.Null(outOfRangeResult);
        Assert.NotEqual("original", param0Name);
    }

    [Fact]
    public void ProjectionFragmentVisitor_GetNewExpressionParameterName_WithNullConstructor_ReturnsNull()
    {
        // Arrange
        var newExpr = Expression.New(typeof(object));

        var methodInfo = typeof(ProjectionFragmentVisitor).GetMethod(
            "GetNewExpressionParameterName",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(methodInfo);

        var result = methodInfo.Invoke(null, [newExpr, 0]);

        Assert.Null(result);
    }

    #endregion
}

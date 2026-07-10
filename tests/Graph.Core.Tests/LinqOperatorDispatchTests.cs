// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using Cvoya.Graph.Querying;

[Trait("Area", "QueryModelBuilder")]
public class LinqOperatorDispatchTests
{
    [Fact]
    public void EveryOperatorIsRegisteredByAtLeastOneMethod()
    {
        var registered = LinqOperatorDispatch.AllRegisteredMethods.Values.ToHashSet();
        var missing = Enum.GetValues<LinqOperator>().Where(value => !registered.Contains(value)).ToList();

        Assert.True(missing.Count == 0, $"Unregistered LINQ operators: {string.Join(", ", missing)}.");
    }

    [Fact]
    public void EveryRegisteredMethodResolvesByMethodIdentity()
    {
        foreach (var registration in LinqOperatorDispatch.AllRegisteredMethods)
        {
            Assert.Equal(registration.Value, LinqOperatorDispatch.Resolve(registration.Key));
        }
    }

    [Fact]
    public void SameNamedUnknownMethodDoesNotResolve()
    {
        var method = typeof(UnrelatedOperators).GetMethod(nameof(UnrelatedOperators.Where))!;

        Assert.Null(LinqOperatorDispatch.Resolve(method));
    }

    private static class UnrelatedOperators
    {
        public static void Where()
        {
        }
    }
}

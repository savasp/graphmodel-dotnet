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

namespace Cvoya.Graph.Model.Core.Tests;

using Cvoya.Graph.Model.Querying;

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

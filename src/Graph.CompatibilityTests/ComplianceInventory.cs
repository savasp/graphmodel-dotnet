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

namespace Cvoya.Graph.CompatibilityTests;

using System.Reflection;

/// <summary>
/// Reflects over the compatibility suite's test interfaces to answer "how many tests exist, and
/// how many must a provider with a given declared <see cref="CapabilitySet"/> execute".
/// </summary>
/// <remarks>
/// Reflection-based rather than a hand-maintained manifest, so the numbers self-maintain as
/// <c>I*Tests</c> interfaces gain, lose, or re-attribute test methods.
/// </remarks>
public static class ComplianceInventory
{
    private static readonly Lazy<IReadOnlyList<MethodInfo>> testMethods = new(DiscoverTestMethods);

    /// <summary>
    /// Gets the total number of <c>[Fact]</c>/<c>[Theory]</c> methods declared across every public
    /// compatibility test interface in the suite. Data rows of a <c>[Theory]</c> count once here -
    /// this is a count of test methods, not of test cases.
    /// </summary>
    public static int TotalTestMethods => testMethods.Value.Count;

    /// <summary>
    /// Gets the minimum number of test methods that must execute for a provider that declares
    /// <paramref name="declared"/>: every test whose required capabilities (method-level or
    /// declaring-interface-level <see cref="RequiresCapabilityAttribute"/>) are all present in
    /// <paramref name="declared"/>.
    /// </summary>
    /// <param name="declared">The capability set a provider declares.</param>
    /// <returns>The minimum number of test methods expected to execute (not skip).</returns>
    public static int MinimumExecuted(CapabilitySet declared) =>
        testMethods.Value.Count(method => RequiredCapabilities(method).All(declared.Has));

    /// <summary>
    /// Gets the number of test methods expected to skip (via a capability mismatch) for a
    /// provider that declares <paramref name="declared"/>.
    /// </summary>
    /// <param name="declared">The capability set a provider declares.</param>
    /// <returns><see cref="TotalTestMethods"/> minus <see cref="MinimumExecuted(CapabilitySet)"/>.</returns>
    public static int ExpectedCapabilitySkips(CapabilitySet declared) =>
        TotalTestMethods - MinimumExecuted(declared);

    /// <summary>
    /// Gets the capabilities required to run <paramref name="method"/>: the union of any
    /// method-level <see cref="RequiresCapabilityAttribute"/>s and any declared on
    /// <paramref name="method"/>'s declaring interface.
    /// </summary>
    /// <param name="method">A compatibility test method.</param>
    /// <returns>The distinct set of capabilities required to run the method.</returns>
    internal static IReadOnlyCollection<GraphCapability> RequiredCapabilities(MethodInfo method)
    {
        var methodLevel = method.GetCustomAttributes<RequiresCapabilityAttribute>(inherit: false);
        var interfaceLevel = method.DeclaringType is { } declaringType
            ? declaringType.GetCustomAttributes<RequiresCapabilityAttribute>(inherit: false)
            : [];

        return methodLevel
            .Concat(interfaceLevel)
            .Select(attribute => attribute.Capability)
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyList<MethodInfo> DiscoverTestMethods()
    {
        var methods = new List<MethodInfo>();

        foreach (var type in typeof(ComplianceInventory).Assembly.GetTypes())
        {
            if (!type.IsInterface || !type.IsPublic || !typeof(IGraphModelTest).IsAssignableFrom(type))
            {
                continue;
            }

            methods.AddRange(
                type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(method => method.GetCustomAttribute<FactAttribute>(inherit: false) is not null));
        }

        return methods;
    }
}

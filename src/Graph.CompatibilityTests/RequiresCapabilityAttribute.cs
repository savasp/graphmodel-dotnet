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

using Xunit.v3;

/// <summary>
/// Marks a compatibility test method, or a whole test interface, as requiring a
/// <see cref="GraphCapability"/> a provider may not declare support for.
/// </summary>
/// <remarks>
/// Applied at the interface level, every method the interface declares requires the capability
/// (used for whole optional feature areas, e.g. <see cref="IFullTextSearchTests"/>). Applied at
/// the method level, only that individual test requires it. <see cref="CompatibilityTest"/> reads
/// both levels by reflecting the running test's method - its own attributes plus those on its
/// declaring interface - and skips when the provider's declared <see cref="CapabilitySet"/> does
/// not include the required capability.
/// <para>
/// This type is also an <see cref="ITraitAttribute"/>, emitting a
/// <c>("Capability", "&lt;EnumName&gt;")</c> trait so a runner can filter <b>method-level</b>
/// requirements, e.g. <c>--filter-trait Capability=FullTextSearch</c>. Note that xunit.v3 does not
/// surface interface-type-level trait attributes on default-interface-method tests, so trait
/// filtering reflects only method-level requirements; the skip decision above does not rely on
/// traits and covers both levels.
/// </para>
/// </remarks>
/// <param name="capability">The capability the annotated test or interface requires.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class RequiresCapabilityAttribute(GraphCapability capability) : Attribute, ITraitAttribute
{
    /// <summary>
    /// Gets the capability the annotated test or interface requires.
    /// </summary>
    public GraphCapability Capability { get; } = capability;

    /// <inheritdoc/>
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new("Capability", Capability.ToString())];
}

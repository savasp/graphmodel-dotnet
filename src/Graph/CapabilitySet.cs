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

namespace Cvoya.Graph;

/// <summary>
/// An immutable set of the <see cref="GraphCapability"/> values a provider declares support for.
/// </summary>
/// <remarks>
/// Backed by a bit set over <see cref="GraphCapability"/>, so membership tests, unions, and
/// exclusions are constant-time and allocation-free. Two <see cref="CapabilitySet"/> values are
/// equal exactly when they contain the same capabilities.
/// </remarks>
public readonly struct CapabilitySet : IEquatable<CapabilitySet>
{
    private readonly ulong bits;

    private CapabilitySet(ulong bits)
    {
        this.bits = bits;
    }

    /// <summary>
    /// Gets a <see cref="CapabilitySet"/> containing every <see cref="GraphCapability"/> value
    /// currently defined.
    /// </summary>
    public static CapabilitySet All { get; } = new(AllBits());

    /// <summary>
    /// Creates a <see cref="CapabilitySet"/> containing exactly the given capabilities.
    /// </summary>
    /// <param name="capabilities">The capabilities to include.</param>
    /// <returns>A new <see cref="CapabilitySet"/> containing <paramref name="capabilities"/>.</returns>
    public static CapabilitySet Of(params GraphCapability[] capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        var bits = 0UL;
        foreach (var capability in capabilities)
        {
            bits |= BitFor(capability);
        }

        return new CapabilitySet(bits);
    }

    /// <summary>
    /// Creates a new <see cref="CapabilitySet"/> containing this set's capabilities minus the
    /// given ones. Does not modify this instance.
    /// </summary>
    /// <param name="capabilities">The capabilities to remove.</param>
    /// <returns>A new <see cref="CapabilitySet"/> without <paramref name="capabilities"/>.</returns>
    public CapabilitySet Except(params GraphCapability[] capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        var remaining = bits;
        foreach (var capability in capabilities)
        {
            remaining &= ~BitFor(capability);
        }

        return new CapabilitySet(remaining);
    }

    /// <summary>
    /// Determines whether this set contains the given capability.
    /// </summary>
    /// <param name="capability">The capability to test.</param>
    /// <returns><c>true</c> if <paramref name="capability"/> is in this set; otherwise <c>false</c>.</returns>
    public bool Has(GraphCapability capability) => (bits & BitFor(capability)) != 0;

    /// <summary>
    /// Determines whether this set contains the same capabilities as <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The set to compare against.</param>
    /// <returns><c>true</c> if both sets contain exactly the same capabilities.</returns>
    public bool Equals(CapabilitySet other) => bits == other.bits;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CapabilitySet other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => bits.GetHashCode();

    /// <summary>
    /// Determines whether two <see cref="CapabilitySet"/> values contain the same capabilities.
    /// </summary>
    public static bool operator ==(CapabilitySet left, CapabilitySet right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="CapabilitySet"/> values do not contain the same
    /// capabilities.
    /// </summary>
    public static bool operator !=(CapabilitySet left, CapabilitySet right) => !left.Equals(right);

    private static ulong BitFor(GraphCapability capability)
    {
        var index = (int)capability;
        if (index < 0 || index >= 64)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capability),
                capability,
                $"{nameof(GraphCapability)} values must map to a bit index in [0, 64).");
        }

        return 1UL << index;
    }

    private static ulong AllBits()
    {
        var bits = 0UL;
        foreach (var capability in Enum.GetValues<GraphCapability>())
        {
            bits |= BitFor(capability);
        }

        return bits;
    }
}

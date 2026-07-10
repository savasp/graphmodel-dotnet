// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests.Tests;

using System.Reflection;
using System.Text.RegularExpressions;

/// <summary>
/// Pins <see cref="CompatibilityTest.SkipReason(GraphCapability, string)"/>'s exact, parseable
/// output format: <c>Capability '&lt;Name&gt;' not declared by provider '&lt;ProviderName&gt;'
/// (Cvoya.Graph.CompatibilityTests &lt;version&gt;)</c>.
/// </summary>
public sealed class SkipReasonFormatTests
{
    [Theory]
    [InlineData(GraphCapability.FullTextSearch, "Cvoya.Graph.SomeProvider")]
    [InlineData(GraphCapability.NestedTransactions, "Cvoya.Graph.Age")]
    public void SkipReason_MatchesFixedParseableFormat(GraphCapability capability, string providerName)
    {
        var reason = CompatibilityTest.SkipReason(capability, providerName);

        var match = Regex.Match(
            reason,
            "^Capability '(?<capability>[A-Za-z]+)' not declared by provider '(?<provider>.+)' \\(Cvoya\\.Graph\\.Model\\.CompatibilityTests (?<version>.+)\\)$",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        Assert.True(match.Success, $"Skip reason did not match the fixed format: '{reason}'");
        Assert.Equal(capability.ToString(), match.Groups["capability"].Value);
        Assert.Equal(providerName, match.Groups["provider"].Value);
        Assert.NotEmpty(match.Groups["version"].Value);
    }

    [Fact]
    public void SkipReason_EmbedsCapabilityNameVerbatim()
    {
        // The #85 addendum's requirement: TRX skips must be grep-able against dialect error text
        // using the enum member name verbatim, with no transformation (casing, spacing, etc.).
        foreach (var capability in Enum.GetValues<GraphCapability>())
        {
            var reason = CompatibilityTest.SkipReason(capability, "AnyProvider");
            Assert.Contains($"'{capability}'", reason, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SkipReason_EmbedsSuiteAssemblyVersion()
    {
        var reason = CompatibilityTest.SkipReason(GraphCapability.FullTextSearch, "AnyProvider");

        var informational = typeof(CompatibilityTest).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? throw new InvalidOperationException("Suite assembly has no informational version.");

        var plusIndex = informational.IndexOf('+', StringComparison.Ordinal);
        var expectedVersion = plusIndex >= 0 ? informational[..plusIndex] : informational;

        Assert.Contains(expectedVersion, reason, StringComparison.Ordinal);
    }
}

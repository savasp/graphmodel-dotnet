// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests.Tests;

/// <summary>
/// Groups every meta-test that reads or resets <see cref="ComplianceGuard"/>'s static executed
/// ledger so xUnit runs them sequentially against each other. Test collections run in parallel
/// with one another by default, but tests within one collection do not - without this, two such
/// tests running concurrently could observe (or reset) each other's ledger state.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ComplianceLedgerCollection
{
    /// <summary>
    /// The collection name every <see cref="ComplianceGuard"/>-touching test class opts into via
    /// <c>[Collection(ComplianceLedgerCollection.Name)]</c>.
    /// </summary>
    public const string Name = "ComplianceGuard ledger";
}

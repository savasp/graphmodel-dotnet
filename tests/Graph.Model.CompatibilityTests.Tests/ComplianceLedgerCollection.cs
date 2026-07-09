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

namespace Cvoya.Graph.Model.CompatibilityTests.Tests;

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

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

namespace Cvoya.Graph.Model.CompatibilityTests;

/// <summary>
/// Requests the degree of isolation a <see cref="IGraphProviderTestHarness"/> must provide when
/// handing back an <see cref="IGraph"/> for a test.
/// </summary>
public enum StoreIsolation
{
    /// <summary>
    /// Reuse the per-test-class store, but ensure it is empty (e.g. reuse a pooled database and
    /// wipe its data). Cheaper than <see cref="FreshStore"/>; the default for most tests.
    /// </summary>
    CleanSharedStore,

    /// <summary>
    /// Provision a brand-new, empty store. Needed where a data wipe alone does not reset
    /// auxiliary state (for example, full-text index state).
    /// </summary>
    FreshStore
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

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
    FreshStore,

    /// <summary>
    /// Provision an additional, empty store that coexists with every store already handed to the
    /// running test: the result must be a distinct store instance, and previously returned stores
    /// must stay untouched - not reset, replaced, or disposed. Cross-store misuse contracts use
    /// this to hold two live stores of the same provider at once.
    /// </summary>
    IndependentStore
}

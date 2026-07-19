// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Schema;

/// <summary>
/// Derivation properties of the graph-provisioning advisory-lock key (#376). These need no database:
/// the point is that peers provisioning the same graph compute the same number, and that peers
/// provisioning different graphs do not serialize against each other.
/// </summary>
/// <remarks>
/// <see cref="AgeProvisioningConcurrencyTests"/> deliberately takes the lock through this same
/// derivation so it cannot drift from what the store locks - which means it would keep passing if the
/// derivation became process-local, while independent processes silently stopped serializing. These
/// tests are what close that hole.
/// </remarks>
public sealed class AgeProvisioningLockKeyTests
{
    [Fact]
    public void ClassId_IsStableAcrossProcessesAndArchitectures()
    {
        // Pin the complete derivation, including byte order. Comparing two calls in one process would
        // also pass for string.GetHashCode(), despite that method being randomized between processes.
        Assert.Equal(1507293138, AgeProvisioningLock.ClassId);
    }

    [Fact]
    public void ObjectIdFor_IsStableAcrossProcessesAndArchitectures()
    {
        Assert.Equal(31761477, AgeProvisioningLock.ObjectIdFor("cvoya_graph"));
    }

    [Fact]
    public void ObjectIdFor_SeparatesGraphsInTheSameDatabase()
    {
        // Advisory locks are database-wide, so two graphs sharing a PostgreSQL database must not
        // serialize their provisioning against each other.
        Assert.NotEqual(
            AgeProvisioningLock.ObjectIdFor("cvoya_graph"),
            AgeProvisioningLock.ObjectIdFor("other_graph"));
    }

    [Fact]
    public void ClassId_DoesNotCollideWithTheGraphKeySpace()
    {
        // The class scopes provisioning locks; a graph whose name happens to hash into the same value
        // would make unrelated graphs share a key. Not a correctness failure - the two 32-bit keys are
        // compared as a pair - but worth pinning that the two derivations are independent.
        Assert.NotEqual(AgeProvisioningLock.ClassId, AgeProvisioningLock.ObjectIdFor("cvoya_graph"));
    }
}

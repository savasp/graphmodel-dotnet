// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Schema;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

/// <summary>
/// Serializes first-use graph provisioning across every connection in a PostgreSQL database with a
/// transaction-scoped advisory lock keyed by graph name.
/// </summary>
/// <remarks>
/// Provisioning is a check-then-create sequence - probe <c>ag_catalog.ag_graph</c>, create the graph
/// when it is absent, create the physical label tables, create the managed full-text objects - and
/// nothing makes that sequence atomic on its own. Independent stores, workers, or application
/// instances starting together can each observe an absent graph and then collide inside
/// <c>ag_catalog.create_graph</c> (or in the <c>CREATE OR REPLACE FUNCTION</c> and <c>CREATE INDEX</c>
/// statements that follow it), so first use fails intermittently even though the target graph and the
/// credentials are perfectly valid. A store-local gate cannot help there, because those peers share no
/// memory. Holding this lock for the whole sequence makes them run one at a time, so every peer after
/// the first finds the graph already present and only confirms the remaining steps.
/// <para>
/// The lock uses the two-key <c>pg_advisory_xact_lock(int, int)</c> overload. PostgreSQL keeps that key
/// space completely separate from the single-key space that <see cref="Entities.AgeUniquenessLockKey"/>
/// uses, so provisioning and uniqueness enforcement cannot contend on each other by construction rather
/// than by convention.
/// </para>
/// <para>
/// Keys have to agree across processes and cultures - peers provisioning the same graph must compute
/// the same number - so they are derived with SHA-256 rather than <see cref="string.GetHashCode()"/>,
/// which is randomized per process. Truncating to 32 bits admits collisions between unrelated graph
/// names; the only cost is that their provisioning serializes, never that a needed lock is missed.
/// </para>
/// </remarks>
internal static class AgeProvisioningLock
{
    /// <summary>
    /// The advisory-lock class shared by every graph-provisioning lock. It separates provisioning from
    /// any other two-key advisory lock a host application takes in the same database.
    /// </summary>
    internal static readonly int ClassId = Hash("cvoya:age:graph-provisioning");

    /// <summary>
    /// Computes the advisory-lock object identifier for <paramref name="graphName"/>, so graphs sharing
    /// a PostgreSQL database provision independently - advisory locks are database-wide, not
    /// schema-scoped.
    /// </summary>
    internal static int ObjectIdFor(string graphName) => Hash(graphName);

    /// <summary>
    /// Takes the provisioning lock on <paramref name="transaction"/>, blocking until it is held. The
    /// lock releases when that transaction commits or rolls back, so there is no explicit unlock path
    /// to leak on a failure, a cancellation, or a dropped connection.
    /// </summary>
    internal static async Task AcquireAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string graphName,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandLease = command.ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = "SELECT pg_advisory_xact_lock(@class, @object)";
        command.Parameters.Add(new NpgsqlParameter<int>("class", ClassId));
        command.Parameters.Add(new NpgsqlParameter<int>("object", ObjectIdFor(graphName)));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static int Hash(string value) =>
        BinaryPrimitives.ReadInt32BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

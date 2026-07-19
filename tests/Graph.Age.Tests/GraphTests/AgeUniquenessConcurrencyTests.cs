// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests.GraphTests;

using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Entities;
using Cvoya.Graph.Age.Querying.Cypher.Execution;
using Cvoya.Graph.CompatibilityTests;

/// <summary>
/// AGE-specific tests for #365: uniqueness is enforced in the application, not by physical database
/// constraints, so probe-then-write must be serialized by a transaction-scoped advisory lock or two
/// concurrent writers can both commit the same value.
/// </summary>
/// <remarks>
/// The contention is staged deterministically rather than raced. The winner writes but does not
/// commit, which leaves it holding the lock; the loser is then started and observed to be *waiting*
/// on that exact lock key in <c>pg_locks</c> before the winner commits. Nothing here depends on
/// which transaction happens to get scheduled first, so there are no retry loops and no timing
/// tolerances - only a bounded wait for a state that the lock guarantees will be reached.
/// </remarks>
public sealed class AgeUniquenessConcurrencyTests(AgeHarness harness)
    : AgeTest(harness, StoreIsolation.FreshStore)
{
    private static readonly TimeSpan BlockedWaitTimeout = TimeSpan.FromSeconds(30);

    [Node("ConcurrentUniqueAccount")]
    public record ConcurrentUniqueAccount : Node
    {
        [Property(IsUnique = true)]
        public string Email { get; set; } = string.Empty;

        [Property]
        public string DisplayName { get; set; } = string.Empty;
    }

    [Node("ConcurrentKeyedAccount")]
    public record ConcurrentKeyedAccount : Node
    {
        [Property(IsKey = true)]
        public string Tenant { get; set; } = string.Empty;

        [Property(IsKey = true)]
        public string AccountNumber { get; set; } = string.Empty;
    }

    [Relationship("CONCURRENT_UNIQUE_GRANT")]
    public record ConcurrentUniqueGrant : Relationship
    {
        public ConcurrentUniqueGrant() : base(string.Empty, string.Empty) { }

        public ConcurrentUniqueGrant(string startNodeId, string endNodeId)
            : base(startNodeId, endNodeId) { }

        [Property(IsUnique = true)]
        public string GrantCode { get; set; } = string.Empty;
    }

    [Fact]
    public async Task ConcurrentNodeCreates_WithTheSameUniqueValue_LetExactlyOneCommit()
    {
        var ct = TestContext.Current.CancellationToken;
        var failure = await StageContentionAsync(
            (transaction, suffix) => this.Graph.CreateNodeAsync(
                new ConcurrentUniqueAccount
                {
                    Id = $"account-{suffix}",
                    Email = "duplicate@example.com",
                    DisplayName = suffix,
                },
                transaction,
                ct),
            ct);

        Assert.Contains("unique property 'Email'", failure.Message, StringComparison.Ordinal);
        Assert.Equal(1, await CountAsync("ConcurrentUniqueAccount", ct));
    }

    [Fact]
    public async Task ConcurrentNodeCreates_WithTheSameCompositeKey_LetExactlyOneCommit()
    {
        var ct = TestContext.Current.CancellationToken;
        var failure = await StageContentionAsync(
            (transaction, suffix) => this.Graph.CreateNodeAsync(
                new ConcurrentKeyedAccount
                {
                    Id = $"keyed-{suffix}",
                    Tenant = "acme",
                    AccountNumber = "A-1",
                },
                transaction,
                ct),
            ct);

        Assert.Contains("composite key", failure.Message, StringComparison.Ordinal);
        Assert.Equal(1, await CountAsync("ConcurrentKeyedAccount", ct));
    }

    [Fact]
    public async Task ConcurrentNodeUpdates_ClaimingTheSameUniqueValue_LetExactlyOneCommit()
    {
        var ct = TestContext.Current.CancellationToken;

        // Two rows that each start out unique, then both try to move onto the same email.
        await using (var seed = await this.Graph.GetTransactionAsync(ct))
        {
            await this.Graph.CreateNodeAsync(
                new ConcurrentUniqueAccount { Id = "update-a", Email = "a@example.com" }, seed, ct);
            await this.Graph.CreateNodeAsync(
                new ConcurrentUniqueAccount { Id = "update-b", Email = "b@example.com" }, seed, ct);
            await seed.CommitAsync();
        }

        // The winner and loser update different rows onto the same claimed email.
        var ids = new Queue<string>(["update-a", "update-b"]);

        var failure = await StageContentionAsync(
            (transaction, _) => this.Graph.UpdateNodeAsync(
                new ConcurrentUniqueAccount { Id = ids.Dequeue(), Email = "claimed@example.com" },
                transaction,
                ct),
            ct);

        Assert.Contains("unique property 'Email'", failure.Message, StringComparison.Ordinal);
        Assert.Equal(1, await CountByPropertyAsync("ConcurrentUniqueAccount", "Email", "claimed@example.com", ct));
    }

    [Fact]
    public async Task ConcurrentRelationshipCreates_WithTheSameUniqueValue_LetExactlyOneCommit()
    {
        var ct = TestContext.Current.CancellationToken;

        await using (var seed = await this.Graph.GetTransactionAsync(ct))
        {
            await this.Graph.CreateNodeAsync(
                new ConcurrentUniqueAccount { Id = "grant-source", Email = "source@example.com" }, seed, ct);
            await this.Graph.CreateNodeAsync(
                new ConcurrentUniqueAccount { Id = "grant-target-1", Email = "target1@example.com" }, seed, ct);
            await this.Graph.CreateNodeAsync(
                new ConcurrentUniqueAccount { Id = "grant-target-2", Email = "target2@example.com" }, seed, ct);
            await seed.CommitAsync();
        }

        // Distinct endpoints, so only the unique GrantCode can make the two writes conflict.
        var targets = new Queue<string>(["grant-target-1", "grant-target-2"]);

        var failure = await StageContentionAsync(
            (transaction, suffix) => this.Graph.CreateRelationshipAsync(
                new ConcurrentUniqueGrant("grant-source", targets.Dequeue())
                {
                    Id = $"grant-{suffix}",
                    GrantCode = "GRANT-1",
                },
                transaction,
                ct),
            ct);

        Assert.Contains("unique property 'GrantCode'", failure.Message, StringComparison.Ordinal);
        Assert.Equal(1, await CountRelationshipsAsync("CONCURRENT_UNIQUE_GRANT", ct));
    }

    [Fact]
    public async Task ConcurrentDynamicNodeCreates_AgainstARegisteredSchema_LetExactlyOneCommit()
    {
        var ct = TestContext.Current.CancellationToken;
        var failure = await StageContentionAsync(
            (transaction, suffix) => this.Graph.CreateNodeAsync(
                new DynamicNode(
                    $"dynamic-{suffix}",
                    ["ConcurrentUniqueAccount"],
                    new Dictionary<string, object?>
                    {
                        ["Email"] = "dynamic@example.com",
                        ["DisplayName"] = suffix,
                    }),
                transaction,
                ct),
            ct);

        Assert.Contains("unique property 'Email'", failure.Message, StringComparison.Ordinal);
        Assert.Equal(1, await CountAsync("ConcurrentUniqueAccount", ct));
    }

    [Fact]
    public async Task DynamicNodeCreate_DuplicatingACommittedUniqueValue_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;

        await using (var seed = await this.Graph.GetTransactionAsync(ct))
        {
            await this.Graph.CreateNodeAsync(
                new ConcurrentUniqueAccount { Id = "typed-owner", Email = "owned@example.com" }, seed, ct);
            await seed.CommitAsync();
        }

        await using var transaction = await this.Graph.GetTransactionAsync(ct);
        var failure = await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateNodeAsync(
            new DynamicNode(
                "dynamic-duplicate",
                ["ConcurrentUniqueAccount"],
                new Dictionary<string, object?> { ["Email"] = "owned@example.com", ["DisplayName"] = "d" }),
            transaction,
            ct));

        Assert.Contains("unique property 'Email'", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DynamicRelationshipCreate_DuplicatingACommittedUniqueValue_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;

        await using (var seed = await this.Graph.GetTransactionAsync(ct))
        {
            await this.Graph.CreateNodeAsync(
                new ConcurrentUniqueAccount { Id = "dyn-source", Email = "dynsource@example.com" }, seed, ct);
            await this.Graph.CreateNodeAsync(
                new ConcurrentUniqueAccount { Id = "dyn-target", Email = "dyntarget@example.com" }, seed, ct);
            await this.Graph.CreateRelationshipAsync(
                new ConcurrentUniqueGrant("dyn-source", "dyn-target") { Id = "dyn-grant", GrantCode = "DYN-1" },
                seed,
                ct);
            await seed.CommitAsync();
        }

        await using var transaction = await this.Graph.GetTransactionAsync(ct);
        var failure = await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateRelationshipAsync(
            new DynamicRelationship(
                "dyn-source",
                "dyn-target",
                "CONCURRENT_UNIQUE_GRANT",
                new Dictionary<string, object?> { ["GrantCode"] = "DYN-1" })
            {
                Id = "dyn-grant-duplicate",
            },
            transaction,
            ct));

        Assert.Contains("unique property 'GrantCode'", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentCreates_WithDifferentUniqueValues_DoNotBlockEachOther()
    {
        // The negative control: distinct constraint values hash to distinct lock keys, so an
        // over-broad lock (one per label, say) would show up here as a hang rather than as a
        // silently passing suite.
        var ct = TestContext.Current.CancellationToken;

        await using var first = await this.Graph.GetTransactionAsync(ct);
        await this.Graph.CreateNodeAsync(
            new ConcurrentUniqueAccount { Id = "parallel-1", Email = "one@example.com" }, first, ct);

        await using var second = await this.Graph.GetTransactionAsync(ct);
        await this.Graph.CreateNodeAsync(
            new ConcurrentUniqueAccount { Id = "parallel-2", Email = "two@example.com" }, second, ct);

        await first.CommitAsync();
        await second.CommitAsync();

        Assert.Equal(2, await CountAsync("ConcurrentUniqueAccount", ct));
    }

    /// <summary>
    /// Runs <paramref name="write"/> in two transactions staged so the second is provably blocked on
    /// a uniqueness lock the first holds before the first commits, and returns the second's failure.
    /// </summary>
    private async Task<GraphException> StageContentionAsync(
        Func<IGraphTransaction, string, Task> write,
        CancellationToken cancellationToken)
    {
        await using var winner = await this.Graph.GetTransactionAsync(cancellationToken);
        await write(winner, "winner");

        // Read the locks the winner's own write actually took rather than recomputing the key here:
        // the test then cannot drift from the production derivation, and a write that took no lock
        // at all fails loudly instead of silently passing.
        var contended = await HeldAdvisoryLocksAsync(winner, cancellationToken);
        Assert.NotEmpty(contended);

        await using var loser = await this.Graph.GetTransactionAsync(cancellationToken);

        // Started on the thread pool because the call blocks inside PostgreSQL until the winner's
        // lock is released; it cannot be awaited before the winner commits.
        var loserWrite = Task.Run(() => write(loser, "loser"), cancellationToken);

        await WaitUntilWaitingForAsync(contended, cancellationToken);
        await winner.CommitAsync();

        var failure = await Assert.ThrowsAsync<GraphException>(() => loserWrite);
        await loser.RollbackAsync();
        return failure;
    }

    /// <summary>
    /// Returns the advisory locks currently granted to <paramref name="transaction"/>'s own backend,
    /// each rendered as the <c>classid:objid</c> pair PostgreSQL splits a bigint lock key into.
    /// </summary>
    private static async Task<IReadOnlyList<string>> HeldAdvisoryLocksAsync(
        IGraphTransaction transaction,
        CancellationToken cancellationToken)
    {
        return await ((AgeGraphTransaction)transaction).Runner.QueryScalarStringsAsync(
            """
            SELECT classid::text || ':' || objid::text FROM pg_locks
            WHERE locktype = 'advisory' AND granted AND objsubid = 1 AND pid = pg_backend_pid()
            """,
            null,
            cancellationToken);
    }

    /// <summary>
    /// Blocks until some backend is waiting on one of <paramref name="contended"/>. Matching against
    /// the winner's exact keys keeps unrelated advisory waits elsewhere in the database - other test
    /// classes share the container - from satisfying the wait.
    /// </summary>
    private async Task WaitUntilWaitingForAsync(
        IReadOnlyList<string> contended,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + BlockedWaitTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var observer = await this.Graph.GetTransactionAsync(cancellationToken);
            var waiting = await ((AgeGraphTransaction)observer).Runner.QueryScalarStringsAsync(
                """
                SELECT classid::text || ':' || objid::text FROM pg_locks
                WHERE locktype = 'advisory' AND NOT granted AND objsubid = 1
                """,
                null,
                cancellationToken);
            await observer.RollbackAsync();

            if (waiting.Intersect(contended, StringComparer.Ordinal).Any())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        Assert.Fail(
            "No backend ever waited on the uniqueness locks the first transaction holds " +
            $"({string.Join(", ", contended)}), so the uniqueness probe and the write it guards " +
            "are not serialized against a competing transaction.");
    }

    private async Task<int> CountAsync(string label, CancellationToken cancellationToken)
    {
        await using var transaction = await this.Graph.GetTransactionAsync(cancellationToken);
        var result = await ((AgeGraphTransaction)transaction).Runner.RunAsync(
            "MATCH (n) WHERE $label IN coalesce(n.inheritance_labels, []) RETURN count(n) AS c",
            new { label },
            cancellationToken);
        var count = (await result.SingleAsync(cancellationToken))["c"].As<int>();
        await transaction.RollbackAsync();
        return count;
    }

    private async Task<int> CountByPropertyAsync(
        string label,
        string property,
        string value,
        CancellationToken cancellationToken)
    {
        await using var transaction = await this.Graph.GetTransactionAsync(cancellationToken);
        var result = await ((AgeGraphTransaction)transaction).Runner.RunAsync(
            $"MATCH (n) WHERE $label IN coalesce(n.inheritance_labels, []) AND n.{property} = $value RETURN count(n) AS c",
            new { label, value },
            cancellationToken);
        var count = (await result.SingleAsync(cancellationToken))["c"].As<int>();
        await transaction.RollbackAsync();
        return count;
    }

    private async Task<int> CountRelationshipsAsync(string type, CancellationToken cancellationToken)
    {
        await using var transaction = await this.Graph.GetTransactionAsync(cancellationToken);
        var result = await ((AgeGraphTransaction)transaction).Runner.RunAsync(
            "MATCH ()-[r]->() WHERE $type IN coalesce(r.inheritance_labels, []) RETURN count(r) AS c",
            new { type },
            cancellationToken);
        var count = (await result.SingleAsync(cancellationToken))["c"].As<int>();
        await transaction.RollbackAsync();
        return count;
    }
}

---
---

# Best practices

## Model the domain, not provider identity

Inherit from `Node` and `Relationship`, then declare only domain data:

```csharp
[Node(Label = "Person")]
public record Person : Node
{
    public string Email { get; init; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

[Relationship(Label = "KNOWS")]
public record Knows : Relationship
{
    public DateTime Since { get; init; }
}
```

Do not add endpoint IDs, physical element IDs, or relationship direction merely to make
persistence work. Providers keep physical identity private, relationship commands receive endpoint
intent separately, and path segments report orientation.

A property named `Id` or `Direction` is fine when it belongs to the domain. Its name does not
create library behavior.

## Use keys only when the domain has one

Keyless models are valid. When stable lookup uniqueness exists, declare it explicitly:

```csharp
public record Account : Node
{
    [Property(IsKey = true)]
    public string Tenant { get; init; } = string.Empty;

    [Property(IsKey = true)]
    public string AccountNumber { get; init; } = string.Empty;

    public decimal Balance { get; set; }
}
```

All key members form one composite tuple. Do not use a generated GUID as a universal key merely
because an older version required one. Keys are domain constraints, not provider identity or
implicit update targets.

Use `IsUnique` for a property that must be independently unique. Key/unique declarations cannot be
collections, complex values, nullable members, or ignored properties.

## Select endpoints precisely

Use predicates that express a unique domain selection and let the exact-one command guard catch
ambiguity:

```csharp
var source = graph.Nodes<Person>()
    .Where(person => person.Email == "alice@example.com");
var target = graph.Nodes<Person>()
    .Where(person => person.Email == "bob@example.com");

await graph.CreateRelationshipAsync(
    source,
    new Knows { Since = DateTime.UtcNow },
    target);
```

Do not materialize both entities merely to connect them, and do not retain provider element IDs.
For new endpoints, prefer the all-new/hybrid create overloads so node and relationship creation is
atomic.

## Mutate sets, not detached objects

Keep filtering in the provider and update the selected set:

```csharp
var affected = await graph.Nodes<Account>()
    .Where(account => account.Tenant == tenant &&
                      account.AccountNumber == accountNumber)
    .UpdateAsync(setters => setters
        .SetProperty(account => account.Balance, account => account.Balance + deposit));
```

`UpdateAsync` freezes and de-duplicates the target set inside the write transaction. Typed setters
support captured constants and expressions over the current entity. Use the same surface for
scalar, constrained, collection, and complex-property replacement.

For deletes, decide explicitly whether user-defined relationships may be cascaded:

```csharp
await graph.Nodes<Person>()
    .Where(person => person.Email == expiredEmail)
    .DeleteAsync(cascadeDelete: true);
```

## Treat complex properties as owned structure

Complex values are decomposed into owned graph nodes/relationships. Prefer small value objects with
clear ownership and bounded depth:

```csharp
public record Address
{
    public string City { get; init; } = string.Empty;
}

public record Customer : Node
{
    public Address? Home { get; set; }
    public List<Address?> PreviousHomes { get; set; } = [];
}
```

Replacement creates the new owned subtree and removes stale owned state atomically. Do not share
one complex object instance as if it were a separately addressable graph entity; model shared
objects as normal nodes and explicit relationships.

Nullable simple and complex collection elements preserve exact positions and order. Use nullable
element annotations only when null is valid; a null targeting a non-nullable element fails instead
of being silently dropped.

## Filter and project early

Apply predicates before traversal, ordering, and paging, then project only needed values:

```csharp
var page = await graph.Nodes<Person>()
    .Where(person => person.Active)
    .OrderBy(person => person.Name)
    .Select(person => new { person.Email, person.Name })
    .Skip(100)
    .Take(50)
    .ToListAsync();
```

Order by scalar domain properties for portable queries. Whole-entity ordering is an optional
capability: Neo4j supports it; AGE and in-memory do not.

## Bound traversal

Choose the narrowest traversal result:

- `Traverse` for target nodes;
- `PathSegments` for one-hop endpoints, relationship, and orientation;
- `TraversePaths` for full multi-hop paths;
- `ShortestPath`/`AllShortestPaths` only when the provider declares the capability.

```csharp
var recent = await graph.Nodes<Person>()
    .Where(person => person.Email == "alice@example.com")
    .Traverse<Knows, Person>(options => options
        .Depth(1, 3)
        .Direction(GraphTraversalDirection.Both)
        .WhereRelationship<Knows>(
            relationship => relationship.Since >= cutoff))
    .ToListAsync();
```

Every depth is at least one. Deep, bidirectional, variable-length traversal can expand rapidly;
bound it and filter the source first.

When physical orientation matters, use `IGraphPathSegment.Direction`. Do not infer direction from
relationship properties or the order in which a relationship object was created.

## Use the correct async terminal

- Use `SingleAsync` only when the query contract requires exactly one row.
- Use `SingleOrDefaultAsync` for zero-or-one.
- Use `FirstAsync` only after deterministic ordering when "first" is meaningful.
- Add `OrderBy` before `Skip`, `Take`, `First*`, or `Last*` when reproducibility matters.
- Pass cancellation tokens through terminals and graph operations.
- Use `await foreach` for incremental processing when a provider supports streaming.

Do not await query roots such as `graph.Nodes<T>()`; building a query performs no I/O.

## Keep transaction ownership clear

Create every transaction-bound query root with the same transaction:

```csharp
await using var transaction = await graph.GetTransactionAsync(cancellationToken);
try
{
    var selected = graph.Nodes<Account>(transaction)
        .Where(account => account.AccountNumber == accountNumber);

    await selected.UpdateAsync(
        setters => setters.SetProperty(account => account.Balance, newBalance),
        cancellationToken);

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

Transactions belong to the graph/store that created them. Keep them short and avoid external I/O
while they are open. Dispose the provider store to release drivers/pools; `IGraph` does not own
those resources.

## Understand full-text portability

All declaring providers implement case-insensitive whole-token matching with multi-term AND
semantics. Do not rely on ranking, stemming, phrase adjacency, prefix, wildcard, or result order
unless the selected provider documents it.

Add explicit ordering when order matters. Use `[Property(IncludeInFullTextSearch = false)]` for
sensitive or irrelevant strings. Full-text execution differs:

- Neo4j uses provider-owned managed indexes.
- AGE uses PostgreSQL text search over native AGE rows without managed search artifacts.
- In-memory scans searchable strings and is suitable for tests, not performance validation.

## Keep native interoperability one-way

Typed and dynamic queries can read compatible external Neo4j/AGE rows that use mapped native
labels/types and properties. Read-only operations do not provision storage artifacts.

Do not expose provider physical identities or private collection companions in application
contracts. They are not portable and may change without changing the public behavior.

## Test at the right level

- Use `Cvoya.Graph.InMemory` for fast business-logic tests.
- Run the fast repository lane for provider-neutral changes.
- Run Neo4j and AGE lanes serially when provider behavior changes.
- Provider authors bind `Cvoya.Graph.CompatibilityTests` and declare only capabilities they
  actually satisfy.

The in-memory full-text suite runs; it does not skip. Infrastructure unavailability in a provider
TCK run always fails. `GRAPHMODEL_COMPLIANCE_STRICT=1` enforces the method-execution floor rather
than changing test-result semantics.

## Treat pre-v1 storage as a separate data model

Do not open an alpha-era database and assume v1 will migrate it. There is no compatibility reader,
dual write, or automatic backfill. Back up, recreate/reimport into a clean native model, and
validate the result. See [the migration guide](migration-0.x.md).

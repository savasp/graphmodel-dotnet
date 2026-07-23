---
---

# Runtime metadata

CVOYA Graph exposes only the provider-populated metadata needed to describe a materialized graph
element:

- `INode.Labels` contains the node's stored labels.
- `IRelationship.Type` contains the relationship's stored type.

`IEntity` is identity-free. There is no public provider element ID, graph reference, relationship
endpoint ID, or relationship-owned direction.

## Node labels

```csharp
public interface INode : IEntity
{
    IReadOnlyList<string> Labels { get; }
}
```

Typed nodes normally have one mapped primary label derived from `[Node]` or the CLR type name.
Dynamic nodes can expose multiple native labels. Providers populate `Labels` during create/read:

```csharp
var imported = await graph.DynamicNodes()
    .OfLabel("Imported")
    .ToListAsync();

foreach (var node in imported)
{
    Console.WriteLine(string.Join(", ", node.Labels));
}
```

Use `OfLabel`/`OfLabels` when label filtering must run in the provider. Reading `Labels` from a
materialized value is useful for inspection and client-side logic.

## Relationship type

```csharp
public interface IRelationship : IEntity
{
    string Type { get; }
}
```

The type is derived from `[Relationship]` or the CLR type name for typed relationships and comes
directly from native storage for dynamic relationships:

```csharp
var relationships = await graph.Relationships<IRelationship>()
    .ToListAsync();

foreach (var relationship in relationships)
{
    Console.WriteLine(relationship.Type);
}
```

`Type` describes the relationship. It does not identify its endpoints.

## Base records

Application types should normally inherit `Node` or `Relationship`. The base records implement
runtime metadata and nothing more:

```csharp
public abstract record Node : INode
{
    public virtual IReadOnlyList<string> Labels { get; set; } = [];
}

public abstract record Relationship : IRelationship
{
    public virtual string Type { get; set; } = string.Empty;
}
```

```csharp
[Node(Label = "Person")]
public record Person : Node
{
    public string Email { get; init; } = string.Empty;
}

[Relationship(Label = "KNOWS")]
public record Knows : Relationship
{
    public DateTime Since { get; init; }
}
```

The optional analyzer reports CG011 when a model implements `INode` or `IRelationship` directly.
Direct implementation remains possible when an application deliberately wants to implement the
metadata members itself.

## Ordinary `Id` and `Direction` properties

Names do not create conventions:

```csharp
public record ExternalDocument : Node
{
    public string Id { get; init; } = string.Empty;
}

public record RouteInstruction : Relationship
{
    public string Direction { get; init; } = string.Empty;
}
```

Both properties are ordinary mapped values. Add `[Property(IsKey = true)]` to `Id` only when it is
a domain key. A `Direction` property never controls physical relationship orientation.

Provider-native identities such as Neo4j element IDs and AGE graphids stay inside provider
transactions. They may correlate query rows or writes but are not materialized as user data.

## Endpoint and orientation metadata

Endpoints and physical orientation belong to a path segment:

```csharp
var segments = await graph.Nodes<Person>()
    .Where(person => person.Email == "alice@example.com")
    .PathSegments<Person, Knows, Person>(GraphTraversalDirection.Both)
    .ToListAsync();

foreach (var segment in segments)
{
    Person start = segment.StartNode;
    Person end = segment.EndNode;
    Knows relationship = segment.Relationship;
    RelationshipDirection physicalOrientation = segment.Direction;
}
```

`Outgoing` means the stored edge runs from the segment's `StartNode` to its `EndNode`; `Incoming`
means the physical edge runs the other way. A self-loop is reported as `Outgoing`. Reversing a
query traversal can therefore change the segment-relative direction without changing the
relationship object or stored edge.

## Typed and dynamic materialization

Providers adapt native driver values into the shared `GraphValue` wire model. The provider-neutral
materializer then:

1. resolves typed entities from stored labels/types and loadable CLR metadata;
2. populates `Labels` or `Type`;
3. converts scalar and collection values;
4. reconstructs owned complex-property subtrees; and
5. stitches path segments with endpoint/orientation metadata.

Dynamic entities expose stored user properties plus labels/type. Provider identity and private
collection/complex-property companion values never enter their property bags.

Compatible native Neo4j/AGE rows created outside CVOYA Graph can materialize without a universal
root or CLR discriminator. Typed resolution uses the mapped label/type; dynamic resolution exposes
the native shape directly.

## Migration boundary

Pre-v1 code that relied on inherited IDs, endpoint fields, relationship-owned direction, universal
roots, or legacy metadata must move to the identity-free model. The library does not read or
rewrite alpha storage. See [Migrate a 0.x application to the v1 model](migration-0.x.md).

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Querying;

using Cvoya.Graph.Querying;

/// <summary>
/// The executable spec of the <see cref="GraphCapability.FullTextSearch"/> contract floor for the
/// in-memory provider: naive, index-free, obviously correct whole-word matching.
/// </summary>
/// <remarks>
/// An entity matches a query iff <em>every</em> query term (as defined by the shared
/// <see cref="FullTextQueryTokenizer"/>) appears as a whole token in the union of the entity's own
/// searchable property values. Searchable text is: for typed entities, the values of the entity's
/// own <c>[Property(IncludeInFullTextSearch)]</c> string properties (string-only by construction —
/// complex-property value nodes are never part of the owning entity's match set); for dynamic
/// entities, all string property values. Matching is case-insensitive because the tokenizer
/// lowercases. No stemming, ranking, prefix, or substring matching.
/// </remarks>
internal sealed class InMemoryFullTextMatcher(SchemaRegistry schemaRegistry)
{
    private readonly SchemaRegistry _schemaRegistry = schemaRegistry;

    /// <summary>
    /// Determines whether <paramref name="entity"/> matches the already-tokenized query. An empty
    /// token list matches nothing.
    /// </summary>
    public bool Matches(object entity, IReadOnlyList<string> queryTokens)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(queryTokens);

        if (queryTokens.Count == 0)
        {
            return false;
        }

        var haystack = new HashSet<string>(StringComparer.Ordinal);
        CollectSearchableTokens(entity, haystack);

        foreach (var term in queryTokens)
        {
            if (!haystack.Contains(term))
            {
                return false;
            }
        }

        return true;
    }

    private void CollectSearchableTokens(object entity, HashSet<string> tokens)
    {
        switch (entity)
        {
            // Dynamic entities carry all their properties in the public bag; check them before the
            // typed INode/IRelationship cases (a DynamicNode is also an INode).
            case DynamicNode dynamicNode:
                AddStringValues(dynamicNode.Properties, tokens);
                break;
            case DynamicRelationship dynamicRelationship:
                AddStringValues(dynamicRelationship.Properties, tokens);
                break;
            case IRelationship relationship:
                AddSchemaTokens(
                    relationship,
                    _schemaRegistry.GetRelationshipSchema(Labels.GetLabelFromObject(relationship)),
                    tokens);
                break;
            case INode node:
                AddSchemaTokens(
                    node,
                    _schemaRegistry.GetNodeSchema(Labels.GetLabelFromObject(node)),
                    tokens);
                break;
        }
    }

    private static void AddSchemaTokens(object entity, EntitySchemaInfo? schema, HashSet<string> tokens)
    {
        if (schema is null)
        {
            return;
        }

        foreach (var property in schema.Properties.Values)
        {
            // IncludeInFullTextSearch is true only for string properties by construction
            // (SchemaRegistry), so this excludes complex-property navigations automatically.
            if (property.IncludeInFullTextSearch && property.PropertyInfo.GetValue(entity) is string value)
            {
                AddTokens(value, tokens);
            }
        }
    }

    private static void AddStringValues(IReadOnlyDictionary<string, object?> properties, HashSet<string> tokens)
    {
        foreach (var value in properties.Values)
        {
            if (value is string text)
            {
                AddTokens(text, tokens);
            }
        }
    }

    private static void AddTokens(string value, HashSet<string> tokens)
    {
        foreach (var token in FullTextQueryTokenizer.Tokenize(value))
        {
            tokens.Add(token);
        }
    }
}

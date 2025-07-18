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

namespace Cvoya.Graph.Model.Neo4j.Schema;

using Cvoya.Graph.Model.Neo4j.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ILogger = Microsoft.Extensions.Logging.ILogger;

/// <summary>
/// Schema manager that handles Neo4j-specific schema operations using the SchemaRegistry.
/// </summary>
internal class Neo4jSchemaManager
{
    private readonly GraphContext _context;
    private readonly ILogger _logger;
    private readonly SchemaRegistry _schemaRegistry;
    private readonly HashSet<string> _processedSchemas = new();
    private readonly object _schemaLock = new();
    private bool _isSchemaInitialized = false;

    public Neo4jSchemaManager(GraphContext context, SchemaRegistry schemaRegistry)
    {
        _context = context;
        _schemaRegistry = schemaRegistry;
        _logger = context.LoggerFactory?.CreateLogger<Neo4jSchemaManager>() ?? NullLogger<Neo4jSchemaManager>.Instance;
    }

    /// <summary>
    /// Initializes the schema by discovering all entity types and creating the necessary constraints and indexes.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InitializeSchemaAsync(CancellationToken cancellationToken = default)
    {
        lock (_schemaLock)
        {
            if (_isSchemaInitialized)
            {
                _logger.LogDebug("Schema already initialized, skipping initialization");
                return;
            }
        }

        _logger.LogInformation("Initializing Neo4j schema...");

        try
        {
            // Initialize the schema registry if not already done
            if (!_schemaRegistry.IsInitialized)
            {
                _schemaRegistry.Initialize();
                _logger.LogDebug("Schema registry initialized with {NodeCount} node types and {RelationshipCount} relationship types",
                    _schemaRegistry.GetRegisteredNodeLabels().Count(),
                    _schemaRegistry.GetRegisteredRelationshipTypes().Count());
            }

            // Create constraints and indexes for all discovered node types
            foreach (var nodeLabel in _schemaRegistry.GetRegisteredNodeLabels())
            {
                await CreateNodeConstraintsAndIndexesAsync(nodeLabel);
            }

            // Create constraints and indexes for all discovered relationship types
            foreach (var relationshipType in _schemaRegistry.GetRegisteredRelationshipTypes())
            {
                await CreateRelationshipConstraintsAndIndexesAsync(relationshipType);
            }

            // Create general full text indexes
            await CreateGeneralFullTextIndexesAsync();

            lock (_schemaLock)
            {
                _isSchemaInitialized = true;
            }

            _logger.LogInformation("Neo4j schema initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Neo4j schema");
            throw new GraphException("Failed to initialize Neo4j schema", ex);
        }
    }

    /// <summary>
    /// Recreates all indexes in the Neo4j database.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RecreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Recreating Neo4j indexes...");

        try
        {
            // Drop all existing indexes
            await DropAllIndexesAsync();

            // Recreate indexes for all registered node types
            foreach (var nodeLabel in _schemaRegistry.GetRegisteredNodeLabels())
            {
                await CreateNodeIndexesAsync(nodeLabel);
            }

            // Recreate indexes for all registered relationship types
            foreach (var relationshipType in _schemaRegistry.GetRegisteredRelationshipTypes())
            {
                await CreateRelationshipIndexesAsync(relationshipType);
            }

            // Recreate general full text indexes
            await CreateGeneralFullTextIndexesAsync();

            _logger.LogInformation("Neo4j indexes recreated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recreate Neo4j indexes");
            throw new GraphException("Failed to recreate Neo4j indexes", ex);
        }
    }

    private async Task CreateNodeConstraintsAndIndexesAsync(string label)
    {
        lock (_schemaLock)
        {
            if (_processedSchemas.Contains($"node:{label}"))
            {
                _logger.LogDebug("Node schema already processed for label: {Label}", label);
                return;
            }
        }

        var schema = _schemaRegistry.GetNodeSchema(label);
        if (schema == null)
        {
            _logger.LogWarning("No schema found for node label: {Label}", label);
            return;
        }

        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            // Always create unique constraint on Id
            var idConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:{label}) REQUIRE n.Id IS UNIQUE";
            var result = await tx.RunAsync(idConstraint);
            await result.ConsumeAsync();

            // Handle composite key constraints
            if (schema.HasCompositeKey())
            {
                var keyProperties = schema.GetKeyProperties().ToList();
                var keyPropertyNames = keyProperties.Select(p => $"n.{p.Name}").ToList();
                var compositeKeyConstraintName = $"composite_key_{label}_{string.Join("_", keyProperties.Select(p => p.Name))}".ToLowerInvariant();
                var compositeKeyConstraint = $"CREATE CONSTRAINT {compositeKeyConstraintName} IF NOT EXISTS FOR (n:{label}) REQUIRE ({string.Join(", ", keyPropertyNames)}) IS UNIQUE";

                result = await tx.RunAsync(compositeKeyConstraint);
                await result.ConsumeAsync();
                _logger.LogDebug("Created composite key constraint for properties {Properties} on label {Label}", string.Join(", ", keyProperties.Select(p => p.Name)), label);
            }

            // Create constraints based on property configurations
            foreach (var (propertyName, propertySchema) in schema.Properties)
            {
                if (propertySchema.Ignore) continue;

                // Skip individual unique constraints for key properties if we have a composite key
                if (propertySchema.IsUnique && !(schema.HasCompositeKey() && propertySchema.IsKey))
                {
                    var uniqueConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:{label}) REQUIRE n.{propertySchema.Name} IS UNIQUE";
                    result = await tx.RunAsync(uniqueConstraint);
                    await result.ConsumeAsync();
                    _logger.LogDebug("Created unique constraint for property {Property} on label {Label}", propertySchema.Name, label);
                }

                if (propertySchema.IsRequired)
                {
                    var notNullConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:{label}) REQUIRE n.{propertySchema.Name} IS NOT NULL";
                    result = await tx.RunAsync(notNullConstraint);
                    await result.ConsumeAsync();
                    _logger.LogDebug("Created not null constraint for property {Property} on label {Label}", propertySchema.Name, label);
                }
            }

            await tx.CommitAsync();

            lock (_schemaLock)
            {
                _processedSchemas.Add($"node:{label}");
            }

            _logger.LogDebug("Successfully processed node schema for label: {Label}", label);
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task CreateRelationshipConstraintsAndIndexesAsync(string type)
    {
        lock (_schemaLock)
        {
            if (_processedSchemas.Contains($"relationship:{type}"))
            {
                _logger.LogDebug("Relationship schema already processed for type: {Type}", type);
                return;
            }
        }

        var schema = _schemaRegistry.GetRelationshipSchema(type);
        if (schema == null)
        {
            _logger.LogWarning("No schema found for relationship type: {Type}", type);
            return;
        }

        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            // Always create unique constraint on Id
            var idConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR ()-[r:{type}]-() REQUIRE r.Id IS UNIQUE";
            var result = await tx.RunAsync(idConstraint);
            await result.ConsumeAsync();

            // Handle composite key constraints
            if (schema.HasCompositeKey())
            {
                var keyProperties = schema.GetKeyProperties().ToList();
                var keyPropertyNames = keyProperties.Select(p => $"r.{p.Name}").ToList();
                var compositeKeyConstraintName = $"composite_key_{type}_{string.Join("_", keyProperties.Select(p => p.Name))}".ToLowerInvariant();
                var compositeKeyConstraint = $"CREATE CONSTRAINT {compositeKeyConstraintName} IF NOT EXISTS FOR ()-[r:{type}]-() REQUIRE ({string.Join(", ", keyPropertyNames)}) IS UNIQUE";

                result = await tx.RunAsync(compositeKeyConstraint);
                await result.ConsumeAsync();
                _logger.LogDebug("Created composite key constraint for properties {Properties} on relationship type {Type}", string.Join(", ", keyProperties.Select(p => p.Name)), type);
            }

            // Create constraints based on property configurations
            foreach (var (propertyName, propertySchema) in schema.Properties)
            {
                if (propertySchema.Ignore) continue;

                // Skip individual unique constraints for key properties if we have a composite key
                if (propertySchema.IsUnique && !(schema.HasCompositeKey() && propertySchema.IsKey))
                {
                    var uniqueConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR ()-[r:{type}]-() REQUIRE r.{propertySchema.Name} IS UNIQUE";
                    result = await tx.RunAsync(uniqueConstraint);
                    await result.ConsumeAsync();
                    _logger.LogDebug("Created unique constraint for property {Property} on relationship type {Type}", propertySchema.Name, type);
                }

                if (propertySchema.IsRequired)
                {
                    var notNullConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR ()-[r:{type}]-() REQUIRE r.{propertySchema.Name} IS NOT NULL";
                    result = await tx.RunAsync(notNullConstraint);
                    await result.ConsumeAsync();
                    _logger.LogDebug("Created not null constraint for property {Property} on relationship type {Type}", propertySchema.Name, type);
                }
            }

            await tx.CommitAsync();

            lock (_schemaLock)
            {
                _processedSchemas.Add($"relationship:{type}");
            }

            _logger.LogDebug("Successfully processed relationship schema for type: {Type}", type);
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task CreateNodeIndexesAsync(string label)
    {
        var schema = _schemaRegistry.GetNodeSchema(label);
        if (schema == null) return;

        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            foreach (var (propertyName, propertySchema) in schema.Properties)
            {
                if (propertySchema.Ignore) continue;

                if (propertySchema.IsIndexed)
                {
                    var indexName = $"idx_{label}_{propertySchema.Name}".ToLowerInvariant();
                    var createIndex = $"CREATE INDEX {indexName} IF NOT EXISTS FOR (n:{label}) ON (n.{propertySchema.Name})";
                    var result = await tx.RunAsync(createIndex);
                    await result.ConsumeAsync();
                    _logger.LogDebug("Created index {Index} for property {Property} on label {Label}", indexName, propertySchema.Name, label);
                }
            }

            await tx.CommitAsync();
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task CreateRelationshipIndexesAsync(string type)
    {
        var schema = _schemaRegistry.GetRelationshipSchema(type);
        if (schema == null) return;

        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            foreach (var (propertyName, propertySchema) in schema.Properties)
            {
                if (propertySchema.Ignore) continue;

                if (propertySchema.IsIndexed)
                {
                    var indexName = $"idx_{type}_{propertySchema.Name}".ToLowerInvariant();
                    var createIndex = $"CREATE INDEX {indexName} IF NOT EXISTS FOR ()-[r:{type}]-() ON (r.{propertySchema.Name})";
                    var result = await tx.RunAsync(createIndex);
                    await result.ConsumeAsync();
                    _logger.LogDebug("Created index {Index} for property {Property} on relationship type {Type}", indexName, propertySchema.Name, type);
                }
            }

            await tx.CommitAsync();
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task CreateGeneralFullTextIndexesAsync()
    {
        _logger.LogDebug("Creating general full text indexes");

        var nodeLabels = _schemaRegistry.GetRegisteredNodeLabels().ToList();
        var relationshipTypes = _schemaRegistry.GetRegisteredRelationshipTypes().ToList();

        if (nodeLabels.Count > 0)
        {
            await CreateNodeLabelFullTextIndexAsync(nodeLabels);
        }

        if (relationshipTypes.Count > 0)
        {
            await CreateRelationshipTypeFullTextIndexAsync(relationshipTypes);
        }
    }

    private async Task CreateNodeLabelFullTextIndexAsync(IEnumerable<string> labels)
    {
        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            // Create separate full-text indexes for each label since Neo4j doesn't support multiple labels in one index
            foreach (var label in labels)
            {
                var schema = _schemaRegistry.GetNodeSchema(label);
                if (schema == null) continue;

                var stringProps = schema.Properties
                    .Where(p =>
                        !p.Value.Ignore &&
                        p.Value.IncludeInFullTextSearch &&
                        p.Value.PropertyInfo.PropertyType == typeof(string))
                    .Select(p => $"n.{p.Value.Name}")
                    .ToList();

                if (stringProps.Count == 0)
                {
                    _logger.LogDebug("No string properties for full text index on node label: {Label}", label);
                    continue;
                }

                var propsList = string.Join(", ", stringProps);
                var indexName = $"node_fulltext_index_{label.ToLowerInvariant()}";
                var createIndex = $"CREATE FULLTEXT INDEX {indexName} IF NOT EXISTS FOR (n:{label}) ON EACH [{propsList}]";

                var result = await tx.RunAsync(createIndex);
                await result.ConsumeAsync();

                _logger.LogDebug("Created full text index {Index} for node label: {Label}", indexName, label);
            }

            await tx.CommitAsync();
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task CreateRelationshipTypeFullTextIndexAsync(IEnumerable<string> types)
    {
        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            // Create separate full-text indexes for each relationship type since Neo4j doesn't support multiple types in one index
            foreach (var type in types)
            {
                var schema = _schemaRegistry.GetRelationshipSchema(type);
                if (schema == null) continue;

                var stringProps = schema.Properties
                    .Where(p =>
                        !p.Value.Ignore &&
                        p.Value.IncludeInFullTextSearch &&
                        p.Value.PropertyInfo.PropertyType == typeof(string))
                    .Select(p => $"r.{p.Value.Name}")
                    .ToList();

                if (stringProps.Count == 0)
                {
                    _logger.LogDebug("No string properties for full text index on relationship type: {Type}", type);
                    continue;
                }

                var propsList = string.Join(", ", stringProps);
                var indexName = $"relationship_fulltext_index_{type.ToLowerInvariant()}";
                var createIndex = $"CREATE FULLTEXT INDEX {indexName} IF NOT EXISTS FOR ()-[r:{type}]->() ON EACH [{propsList}]";

                var result = await tx.RunAsync(createIndex);
                await result.ConsumeAsync();

                _logger.LogDebug("Created full text index {Index} for relationship type: {Type}", indexName, type);
            }

            await tx.CommitAsync();
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task DropAllIndexesAsync()
    {
        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            // Drop all indexes except constraints
            var dropIndexes = "SHOW INDEXES WHERE type = 'BTREE' OR type = 'FULLTEXT' YIELD name";
            var result = await tx.RunAsync(dropIndexes);
            var records = await result.ToListAsync();

            foreach (var record in records)
            {
                var indexName = record["name"].ToString();
                if (!string.IsNullOrEmpty(indexName))
                {
                    var dropIndex = $"DROP INDEX {indexName} IF EXISTS";
                    await tx.RunAsync(dropIndex);
                    _logger.LogDebug("Dropped index: {IndexName}", indexName);
                }
            }

            await tx.CommitAsync();
            _logger.LogInformation("Dropped {Count} indexes", records.Count);
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Clears the processed schemas cache and resets the initialization state.
    /// </summary>
    public void ClearCache()
    {
        lock (_schemaLock)
        {
            _processedSchemas.Clear();
            _isSchemaInitialized = false;
        }
        _logger.LogDebug("Cleared schema cache and reset initialization state");
    }

    /// <summary>
    /// Gets the schema registry.
    /// </summary>
    /// <returns>The schema registry.</returns>
    public SchemaRegistry GetSchemaRegistry()
    {
        return _schemaRegistry;
    }
}
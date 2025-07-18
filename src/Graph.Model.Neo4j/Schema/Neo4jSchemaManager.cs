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

using System.Linq;
using Cvoya.Graph.Model.Neo4j.Core;
using global::Neo4j.Driver;
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
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    private volatile bool _isSchemaInitialized = false;

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
        // Quick check without lock for performance
        if (_isSchemaInitialized)
        {
            _logger.LogDebug("Schema already initialized, skipping initialization");
            return;
        }

        // Use semaphore for async-safe concurrency control
        await _initializationSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check pattern
            if (_isSchemaInitialized)
            {
                _logger.LogDebug("Schema already initialized, skipping initialization");
                return;
            }

            _logger.LogInformation("Initializing Neo4j schema...");

            // Initialize the schema registry if not already done
            if (!_schemaRegistry.IsInitialized)
            {
                await _schemaRegistry.InitializeAsync(cancellationToken);
                var nodeLabelsCount = (await _schemaRegistry.GetRegisteredNodeLabelsAsync(cancellationToken)).Count();
                var relationshipTypesCount = (await _schemaRegistry.GetRegisteredRelationshipTypesAsync(cancellationToken)).Count();
                _logger.LogDebug("Schema registry initialized with {NodeCount} node types and {RelationshipCount} relationship types",
                    nodeLabelsCount, relationshipTypesCount);
            }

            // Create constraints and indexes for all discovered node types
            var nodeLabels = await _schemaRegistry.GetRegisteredNodeLabelsAsync(cancellationToken);
            foreach (var nodeLabel in nodeLabels)
            {
                await CreateNodeConstraintsAndIndexesAsync(nodeLabel, cancellationToken);
            }

            // Create constraints and indexes for all discovered relationship types
            var relationshipTypes = await _schemaRegistry.GetRegisteredRelationshipTypesAsync(cancellationToken);
            foreach (var relationshipType in relationshipTypes)
            {
                await CreateRelationshipConstraintsAndIndexesAsync(relationshipType, cancellationToken);
            }

            // Create general full text indexes
            await CreateGeneralFullTextIndexesAsync(cancellationToken);

            _isSchemaInitialized = true;
            _logger.LogInformation("Neo4j schema initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Neo4j schema");
            throw new GraphException("Failed to initialize Neo4j schema", ex);
        }
        finally
        {
            _initializationSemaphore.Release();
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
            var nodeLabels = await _schemaRegistry.GetRegisteredNodeLabelsAsync(cancellationToken);
            foreach (var nodeLabel in nodeLabels)
            {
                await CreateNodeIndexesAsync(nodeLabel, cancellationToken);
            }

            // Recreate indexes for all registered relationship types
            var relationshipTypes = await _schemaRegistry.GetRegisteredRelationshipTypesAsync(cancellationToken);
            foreach (var relationshipType in relationshipTypes)
            {
                await CreateRelationshipIndexesAsync(relationshipType, cancellationToken);
            }

            // Recreate general full text indexes
            await CreateGeneralFullTextIndexesAsync(cancellationToken);

            _logger.LogInformation("Neo4j indexes recreated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recreate Neo4j indexes");
            throw new GraphException("Failed to recreate Neo4j indexes", ex);
        }
    }

    private async Task CreateNodeConstraintsAndIndexesAsync(string label, CancellationToken cancellationToken = default)
    {
        var processedKey = $"node:{label}";
        lock (_processedSchemas)
        {
            if (_processedSchemas.Contains(processedKey))
            {
                _logger.LogDebug("Node schema already processed for label: {Label}", label);
                return;
            }
        }

        var schema = await _schemaRegistry.GetNodeSchemaAsync(label, cancellationToken);
        if (schema == null)
        {
            _logger.LogWarning("No schema found for node label: {Label}", label);
            return;
        }

        // First, create constraints in their own transaction
        await CreateNodeConstraintsAsync(label, schema);

        // Then, create indexes in a separate transaction
        await CreateNodeIndexesAsync(label, cancellationToken);

        lock (_processedSchemas)
        {
            _processedSchemas.Add(processedKey);
        }

        _logger.LogDebug("Successfully processed node schema for label: {Label}", label);
    }

    private async Task CreateRelationshipConstraintsAndIndexesAsync(string type, CancellationToken cancellationToken = default)
    {
        var processedKey = $"relationship:{type}";
        lock (_processedSchemas)
        {
            if (_processedSchemas.Contains(processedKey))
            {
                _logger.LogDebug("Relationship schema already processed for type: {Type}", type);
                return;
            }
        }

        var schema = await _schemaRegistry.GetRelationshipSchemaAsync(type, cancellationToken);
        if (schema == null)
        {
            _logger.LogWarning("No schema found for relationship type: {Type}", type);
            return;
        }

        // First, create constraints in their own transaction
        await CreateRelationshipConstraintsAsync(type, schema);

        // Then, create indexes in a separate transaction
        await CreateRelationshipIndexesAsync(type, cancellationToken);

        lock (_processedSchemas)
        {
            _processedSchemas.Add(processedKey);
        }

        _logger.LogDebug("Successfully processed relationship schema for type: {Type}", type);
    }

    private async Task CreateNodeConstraintsAsync(string label, EntitySchemaInfo schema)
    {
        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            // Get existing constraints to avoid conflicts
            var existingConstraints = await GetExistingConstraintsAsync(tx, label, isNode: true);

            // Always create unique constraint on Id if it doesn't exist
            var idConstraintName = $"unique_{label}_Id".ToLowerInvariant();
            if (!existingConstraints.Any(c => c.Contains("Id") && c.Contains("UNIQUE")))
            {
                var idConstraint = $"CREATE CONSTRAINT {idConstraintName} IF NOT EXISTS FOR (n:{label}) REQUIRE n.Id IS UNIQUE";
                var result = await tx.RunAsync(idConstraint);
                await result.ConsumeAsync();
                _logger.LogDebug("Created unique Id constraint for label {Label}", label);
            }

            // Handle composite key constraints
            if (schema.HasCompositeKey())
            {
                var keyProperties = schema.GetKeyProperties().ToList();
                var keyPropertyNames = keyProperties.Select(p => p.Name).ToList();
                var compositeKeyConstraintName = $"composite_key_{label}_{string.Join("_", keyPropertyNames)}".ToLowerInvariant();

                // Check if composite key constraint already exists
                var compositeExists = existingConstraints.Any(c =>
                    keyPropertyNames.All(prop => c.Contains(prop)) && c.Contains("UNIQUE"));

                if (!compositeExists)
                {
                    var cypherPropertyNames = keyPropertyNames.Select(p => $"n.{p}").ToList();
                    var compositeKeyConstraint = $"CREATE CONSTRAINT {compositeKeyConstraintName} IF NOT EXISTS FOR (n:{label}) REQUIRE ({string.Join(", ", cypherPropertyNames)}) IS UNIQUE";

                    var result = await tx.RunAsync(compositeKeyConstraint);
                    await result.ConsumeAsync();
                    _logger.LogDebug("Created composite key constraint for properties {Properties} on label {Label}", string.Join(", ", keyPropertyNames), label);
                }
            }

            // Create constraints based on property configurations
            foreach (var (propertyName, propertySchema) in schema.Properties)
            {
                if (propertySchema.Ignore) continue;

                // Skip individual unique constraints for key properties if we have a composite key
                if (propertySchema.IsUnique && !(schema.HasCompositeKey() && propertySchema.IsKey))
                {
                    var propertyExists = existingConstraints.Any(c =>
                        c.Contains(propertySchema.Name) && c.Contains("UNIQUE"));

                    if (!propertyExists)
                    {
                        var uniqueConstraintName = $"unique_{label}_{propertySchema.Name}".ToLowerInvariant();
                        var uniqueConstraint = $"CREATE CONSTRAINT {uniqueConstraintName} IF NOT EXISTS FOR (n:{label}) REQUIRE n.{propertySchema.Name} IS UNIQUE";
                        var result = await tx.RunAsync(uniqueConstraint);
                        await result.ConsumeAsync();
                        _logger.LogDebug("Created unique constraint for property {Property} on label {Label}", propertySchema.Name, label);
                    }
                }

                if (propertySchema.IsRequired)
                {
                    var requiredExists = existingConstraints.Any(c =>
                        c.Contains(propertySchema.Name) && c.Contains("NOT NULL"));

                    if (!requiredExists)
                    {
                        var notNullConstraintName = $"notnull_{label}_{propertySchema.Name}".ToLowerInvariant();
                        var notNullConstraint = $"CREATE CONSTRAINT {notNullConstraintName} IF NOT EXISTS FOR (n:{label}) REQUIRE n.{propertySchema.Name} IS NOT NULL";
                        var result = await tx.RunAsync(notNullConstraint);
                        await result.ConsumeAsync();
                        _logger.LogDebug("Created not null constraint for property {Property} on label {Label}", propertySchema.Name, label);
                    }
                }
            }

            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Failed to create constraints for node label: {Label}", label);
            throw;
        }
    }

    private async Task CreateRelationshipConstraintsAsync(string type, EntitySchemaInfo schema)
    {
        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            // Get existing constraints to avoid conflicts
            var existingConstraints = await GetExistingConstraintsAsync(tx, type, isNode: false);

            // Always create unique constraint on Id if it doesn't exist
            var idConstraintName = $"unique_rel_{type}_Id".ToLowerInvariant();
            if (!existingConstraints.Any(c => c.Contains("Id") && c.Contains("UNIQUE")))
            {
                var idConstraint = $"CREATE CONSTRAINT {idConstraintName} IF NOT EXISTS FOR ()-[r:{type}]-() REQUIRE r.Id IS UNIQUE";
                var result = await tx.RunAsync(idConstraint);
                await result.ConsumeAsync();
                _logger.LogDebug("Created unique Id constraint for relationship type {Type}", type);
            }

            // Handle composite key constraints
            if (schema.HasCompositeKey())
            {
                var keyProperties = schema.GetKeyProperties().ToList();
                var keyPropertyNames = keyProperties.Select(p => p.Name).ToList();
                var compositeKeyConstraintName = $"composite_key_rel_{type}_{string.Join("_", keyPropertyNames)}".ToLowerInvariant();

                // Check if composite key constraint already exists
                var compositeExists = existingConstraints.Any(c =>
                    keyPropertyNames.All(prop => c.Contains(prop)) && c.Contains("UNIQUE"));

                if (!compositeExists)
                {
                    var cypherPropertyNames = keyPropertyNames.Select(p => $"r.{p}").ToList();
                    var compositeKeyConstraint = $"CREATE CONSTRAINT {compositeKeyConstraintName} IF NOT EXISTS FOR ()-[r:{type}]-() REQUIRE ({string.Join(", ", cypherPropertyNames)}) IS UNIQUE";

                    var result = await tx.RunAsync(compositeKeyConstraint);
                    await result.ConsumeAsync();
                    _logger.LogDebug("Created composite key constraint for properties {Properties} on relationship type {Type}", string.Join(", ", keyPropertyNames), type);
                }
            }

            // Create constraints based on property configurations
            foreach (var (propertyName, propertySchema) in schema.Properties)
            {
                if (propertySchema.Ignore) continue;

                // Skip individual unique constraints for key properties if we have a composite key
                if (propertySchema.IsUnique && !(schema.HasCompositeKey() && propertySchema.IsKey))
                {
                    var propertyExists = existingConstraints.Any(c =>
                        c.Contains(propertySchema.Name) && c.Contains("UNIQUE"));

                    if (!propertyExists)
                    {
                        var uniqueConstraintName = $"unique_rel_{type}_{propertySchema.Name}".ToLowerInvariant();
                        var uniqueConstraint = $"CREATE CONSTRAINT {uniqueConstraintName} IF NOT EXISTS FOR ()-[r:{type}]-() REQUIRE r.{propertySchema.Name} IS UNIQUE";
                        var result = await tx.RunAsync(uniqueConstraint);
                        await result.ConsumeAsync();
                        _logger.LogDebug("Created unique constraint for property {Property} on relationship type {Type}", propertySchema.Name, type);
                    }
                }

                if (propertySchema.IsRequired)
                {
                    var requiredExists = existingConstraints.Any(c =>
                        c.Contains(propertySchema.Name) && c.Contains("NOT NULL"));

                    if (!requiredExists)
                    {
                        var notNullConstraintName = $"notnull_rel_{type}_{propertySchema.Name}".ToLowerInvariant();
                        var notNullConstraint = $"CREATE CONSTRAINT {notNullConstraintName} IF NOT EXISTS FOR ()-[r:{type}]-() REQUIRE r.{propertySchema.Name} IS NOT NULL";
                        var result = await tx.RunAsync(notNullConstraint);
                        await result.ConsumeAsync();
                        _logger.LogDebug("Created not null constraint for property {Property} on relationship type {Type}", propertySchema.Name, type);
                    }
                }
            }

            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Failed to create constraints for relationship type: {Type}", type);
            throw;
        }
    }

    private async Task<List<string>> GetExistingConstraintsAsync(IAsyncTransaction tx, string labelOrType, bool isNode)
    {
        try
        {
            var query = "SHOW CONSTRAINTS YIELD name, labelsOrTypes, properties, type";
            var result = await tx.RunAsync(query);
            var records = await result.ToListAsync();

            var constraints = new List<string>();
            foreach (var record in records)
            {
                var labelsOrTypes = record["labelsOrTypes"].As<List<string>>();
                var properties = record["properties"].As<List<string>>();
                var constraintType = record["type"].As<string>();

                // Check if this constraint applies to our label/type
                if (labelsOrTypes.Contains(labelOrType))
                {
                    var description = $"{constraintType} on {string.Join(",", properties)}";
                    constraints.Add(description);
                }
            }

            return constraints;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve existing constraints for {LabelOrType}, proceeding with creation attempts", labelOrType);
            return new List<string>();
        }
    }



    private async Task CreateNodeIndexesAsync(string label, CancellationToken cancellationToken = default)
    {
        var schema = await _schemaRegistry.GetNodeSchemaAsync(label, cancellationToken);
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

    private async Task CreateRelationshipIndexesAsync(string type, CancellationToken cancellationToken = default)
    {
        var schema = await _schemaRegistry.GetRelationshipSchemaAsync(type, cancellationToken);
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

    private async Task CreateGeneralFullTextIndexesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Creating global full text indexes");

        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            // Collect node labels and string properties from schema registry
            var nodeLabels = new HashSet<string>();
            var nodeStringProps = new HashSet<string>();

            var registeredNodeLabels = await _schemaRegistry.GetRegisteredNodeLabelsAsync(cancellationToken);
            foreach (var nodeLabel in registeredNodeLabels)
            {
                nodeLabels.Add(nodeLabel);
                var schema = await _schemaRegistry.GetNodeSchemaAsync(nodeLabel, cancellationToken);
                if (schema != null)
                {
                    foreach (var prop in schema.Properties.Values)
                    {
                        if (!prop.Ignore && prop.PropertyInfo.PropertyType == typeof(string) && prop.IncludeInFullTextSearch)
                        {
                            nodeStringProps.Add(prop.Name);
                        }
                    }
                }
            }

            // Create node index if we have labels and string properties
            if (nodeLabels.Count > 0 && nodeStringProps.Count > 0)
            {
                var labelList = string.Join("|", nodeLabels);
                var propList = string.Join(", ", nodeStringProps.Select(p => $"n.{p}"));
                var createNodeIndex = $"CREATE FULLTEXT INDEX node_fulltext_index IF NOT EXISTS FOR (n:{labelList}) ON EACH [{propList}]";
                await tx.RunAsync(createNodeIndex);
                _logger.LogDebug("Created global node full-text index with {LabelCount} labels and {PropertyCount} properties", nodeLabels.Count, nodeStringProps.Count);
            }
            else
            {
                _logger.LogDebug("Skipped node full-text index creation - no labels or string properties found");
            }

            // Collect relationship types and string properties from schema registry
            var relTypes = new HashSet<string>();
            var relStringProps = new HashSet<string>();

            var registeredRelationshipTypes = await _schemaRegistry.GetRegisteredRelationshipTypesAsync(cancellationToken);
            foreach (var relType in registeredRelationshipTypes)
            {
                relTypes.Add(relType);
                var schema = await _schemaRegistry.GetRelationshipSchemaAsync(relType, cancellationToken);
                if (schema != null)
                {
                    foreach (var prop in schema.Properties.Values)
                    {
                        if (!prop.Ignore && prop.PropertyInfo.PropertyType == typeof(string) && prop.IncludeInFullTextSearch)
                        {
                            relStringProps.Add(prop.Name);
                        }
                    }
                }
            }

            // Create relationship index if we have types and string properties
            if (relTypes.Count > 0 && relStringProps.Count > 0)
            {
                var typeList = string.Join("|", relTypes);
                var propList = string.Join(", ", relStringProps.Select(p => $"r.{p}"));
                var createRelIndex = $"CREATE FULLTEXT INDEX rel_fulltext_index IF NOT EXISTS FOR ()-[r:{typeList}]-() ON EACH [{propList}]";
                await tx.RunAsync(createRelIndex);
                _logger.LogDebug("Created global relationship full-text index with {TypeCount} types and {PropertyCount} properties", relTypes.Count, relStringProps.Count);
            }
            else
            {
                _logger.LogDebug("Skipped relationship full-text index creation - no types or string properties found");
            }

            await tx.CommitAsync();
            _logger.LogDebug("Global full-text index creation completed");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Failed to create global full-text indexes");
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
        lock (_processedSchemas)
        {
            _processedSchemas.Clear();
        }
        _isSchemaInitialized = false;
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
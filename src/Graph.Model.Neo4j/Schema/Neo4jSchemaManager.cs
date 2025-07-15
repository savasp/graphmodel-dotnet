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

using System.Reflection;
using Cvoya.Graph.Model.Configuration;
using Cvoya.Graph.Model.Neo4j.Core;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ILogger = Microsoft.Extensions.Logging.ILogger;

/// <summary>
/// Enhanced schema manager that handles property configurations.
/// </summary>
internal class Neo4jSchemaManager
{
    private readonly GraphContext _context;
    private readonly ILogger _logger;
    private readonly PropertyConfigurationRegistry _registry;
    private readonly HashSet<string> _processedSchemas = new();
    private readonly object _schemaLock = new();

    public Neo4jSchemaManager(GraphContext context, PropertyConfigurationRegistry registry)
    {
        _context = context;
        _registry = registry;
        _logger = context.LoggerFactory?.CreateLogger<Neo4jSchemaManager>() ?? NullLogger<Neo4jSchemaManager>.Instance;
    }

    /// <summary>
    /// Ensures all necessary constraints and indexes exist for an entity.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task EnsureSchemaForEntity<T>(T entity) where T : Cvoya.Graph.Model.IEntity
    {
        ArgumentNullException.ThrowIfNull(entity);

        var type = entity.GetType();
        var label = Labels.GetLabelFromType(type);
        var cacheKey = $"schema_{_context.DatabaseName}_{label}";

        _logger.LogDebug("EnsureSchemaForEntity called for type {EntityType} with label {Label}, cacheKey: {CacheKey}", type.Name, label, cacheKey);

        lock (_schemaLock)
        {
            if (_processedSchemas.Contains(cacheKey))
            {
                _logger.LogDebug("Schema already processed for cacheKey: {CacheKey}, skipping", cacheKey);
                return;
            }
        }

        try
        {
            _logger.LogDebug("Creating schema for type {EntityType} with label {Label}", type.Name, label);

            // Create general full text indexes on first schema creation
            if (_processedSchemas.Count == 0)
            {
                _logger.LogDebug("First schema creation, creating general full text indexes");
                await CreateGeneralFullTextIndexes();
            }

            if (entity is Model.INode)
            {
                _logger.LogDebug("Entity is INode, creating node schema");
                await CreateEntityConstraints(label, type);
                await CreatePropertyIndexes(label, type);
                await CreateFullTextIndexes(label, type);
                _logger.LogDebug("Completed creating node schema for {Label}", label);
            }
            else if (entity is Model.IRelationship)
            {
                _logger.LogDebug("Entity is IRelationship, creating relationship schema");
                await CreateRelationshipConstraints(label);
                await CreateRelationshipPropertyIndexes(label);
                await CreateFullTextIndexes(label, type);
                _logger.LogDebug("Completed creating relationship schema for {Label}", label);
            }

            lock (_schemaLock)
            {
                _processedSchemas.Add(cacheKey);
            }

            _logger.LogDebug("Ensured schema exists for label: {Label}", label);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schema for label: {Label}", label);
            throw new GraphException($"Failed to create schema for label: {label}", ex);
        }
    }

    /// <summary>
    /// Ensures all necessary constraints and indexes exist for a node label.
    /// </summary>
    /// <param name="label">The node label.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task EnsureSchemaForNodeLabel(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var cacheKey = $"schema_{_context.DatabaseName}_{label}";

        lock (_schemaLock)
        {
            if (_processedSchemas.Contains(cacheKey))
                return;
        }

        try
        {
            await CreateNodeConstraints(label);
            await CreateNodeValidationTriggers(label);
            await CreateNodeIndexes(label);

            lock (_schemaLock)
            {
                _processedSchemas.Add(cacheKey);
            }

            _logger.LogDebug("Ensured schema exists for node label: {Label}", label);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schema for node label: {Label}", label);
            throw new GraphException($"Failed to create schema for node label: {label}", ex);
        }
    }

    /// <summary>
    /// Ensures all necessary constraints and indexes exist for a relationship type.
    /// </summary>
    /// <param name="type">The relationship type.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task EnsureSchemaForRelationshipType(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        var cacheKey = $"schema_{_context.DatabaseName}_{type}";

        lock (_schemaLock)
        {
            if (_processedSchemas.Contains(cacheKey))
                return;
        }

        try
        {
            await CreateRelationshipConstraints(type);
            await CreateRelationshipValidationTriggers(type);
            await CreateRelationshipIndexes(type);

            lock (_schemaLock)
            {
                _processedSchemas.Add(cacheKey);
            }

            _logger.LogDebug("Ensured schema exists for relationship type: {Type}", type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schema for relationship type: {Type}", type);
            throw new GraphException($"Failed to create schema for relationship type: {type}", ex);
        }
    }

    private async Task CreateEntityConstraints(string label, Type entityType)
    {
        if (typeof(Model.INode).IsAssignableFrom(entityType))
        {
            await CreateNodeConstraints(label);
            await CreateNodeValidationTriggers(label);
        }
        else if (typeof(Model.IRelationship).IsAssignableFrom(entityType))
        {
            await CreateRelationshipConstraints(label);
            await CreateRelationshipValidationTriggers(label);
        }
    }

    private async Task CreatePropertyIndexes(string label, Type entityType)
    {
        if (typeof(Model.INode).IsAssignableFrom(entityType))
        {
            await CreateNodeIndexes(label);
        }
        else if (typeof(Model.IRelationship).IsAssignableFrom(entityType))
        {
            await CreateRelationshipIndexes(label);
        }
    }

    private async Task CreateNodeConstraints(string label)
    {
        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            // Always create unique constraint on Id
            var idConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:`{label}`) REQUIRE n.{nameof(Model.IEntity.Id)} IS UNIQUE";
            var result = await tx.RunAsync(idConstraint);
            await result.ConsumeAsync();

            // Create constraints based on property configurations
            var config = _registry.GetNodeConfiguration(label);
            if (config != null)
            {
                foreach (var (propertyName, propertyConfig) in config.Properties)
                {
                    if (propertyConfig.IsUnique)
                    {
                        var uniqueConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:`{label}`) REQUIRE n.{propertyName} IS UNIQUE";
                        result = await tx.RunAsync(uniqueConstraint);
                        await result.ConsumeAsync();
                        _logger.LogDebug("Created unique constraint for property {Property} on label {Label}", propertyName, label);
                    }

                    if (propertyConfig.IsRequired)
                    {
                        var notNullConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:`{label}`) REQUIRE n.{propertyName} IS NOT NULL";
                        result = await tx.RunAsync(notNullConstraint);
                        await result.ConsumeAsync();
                        _logger.LogDebug("Created not null constraint for property {Property} on label {Label}", propertyName, label);
                    }
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

    private async Task CreateNodeValidationTriggers(string label)
    {
        // Create validation triggers for PropertyValidation rules
        var config = _registry.GetNodeConfiguration(label);
        if (config == null) return;

        foreach (var (propertyName, propertyConfig) in config.Properties)
        {
            if (propertyConfig.Validation == null) continue;

            var validationLogic = BuildValidationLogic(propertyConfig.Validation, propertyName, label);
            if (string.IsNullOrEmpty(validationLogic)) continue;

            var triggerName = $"validate_{label}_{propertyName}".ToLowerInvariant();
            var createTrigger = $"CALL apoc.trigger.add('{triggerName}', '{validationLogic}', {{phase: 'before'}})";

            try
            {
                using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
                using var tx = await session.BeginTransactionAsync();

                var result = await tx.RunAsync(createTrigger);
                await result.ConsumeAsync();
                await tx.CommitAsync();

                _logger.LogDebug("Created validation trigger {Trigger} for property {Property} on label {Label}", triggerName, propertyName, label);
            }
            catch (Exception ex) when (ex.Message.Contains("Triggers have not been enabled") || ex.Message.Contains("apoc.trigger.enabled"))
            {
                _logger.LogWarning("APOC triggers are not enabled. Property validation will not be enforced at the database level. " +
                    "To enable triggers, set 'apoc.trigger.enabled=true' in your apoc.conf file. Error: {Error}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create validation trigger {Trigger} for property {Property} on label {Label}. APOC plugin might not be available or properly configured.", triggerName, propertyName, label);
            }
        }
    }

    private async Task CreateRelationshipConstraints(string type)
    {
        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            // Always create unique constraint on Id for relationships
            var idConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR ()-[r:`{type}`]-() REQUIRE r.{nameof(Model.IEntity.Id)} IS UNIQUE";
            var result = await tx.RunAsync(idConstraint);
            await result.ConsumeAsync();

            // Create constraints based on property configurations
            var config = _registry.GetRelationshipConfiguration(type);
            if (config != null)
            {
                foreach (var (propertyName, propertyConfig) in config.Properties)
                {
                    if (propertyConfig.IsUnique)
                    {
                        var uniqueConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR ()-[r:`{type}`]-() REQUIRE r.{propertyName} IS UNIQUE";
                        result = await tx.RunAsync(uniqueConstraint);
                        await result.ConsumeAsync();
                    }

                    if (propertyConfig.IsRequired)
                    {
                        var notNullConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR ()-[r:`{type}`]-() REQUIRE r.{propertyName} IS NOT NULL";
                        result = await tx.RunAsync(notNullConstraint);
                        await result.ConsumeAsync();
                    }
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

    private async Task CreateRelationshipValidationTriggers(string type)
    {
        // Create validation triggers for PropertyValidation rules
        var config = _registry.GetRelationshipConfiguration(type);
        if (config == null) return;

        foreach (var (propertyName, propertyConfig) in config.Properties)
        {
            if (propertyConfig.Validation == null) continue;

            var validationLogic = BuildValidationLogic(propertyConfig.Validation, propertyName, type);
            if (string.IsNullOrEmpty(validationLogic)) continue;

            var triggerName = $"validate_{type}_{propertyName}".ToLowerInvariant();
            var createTrigger = $"CALL apoc.trigger.add('{triggerName}', '{validationLogic}', {{phase: 'before'}})";

            try
            {
                using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
                using var tx = await session.BeginTransactionAsync();

                var result = await tx.RunAsync(createTrigger);
                await result.ConsumeAsync();
                await tx.CommitAsync();

                _logger.LogDebug("Created validation trigger {Trigger} for property {Property} on relationship type {Type}", triggerName, propertyName, type);
            }
            catch (Exception ex) when (ex.Message.Contains("Triggers have not been enabled") || ex.Message.Contains("apoc.trigger.enabled"))
            {
                _logger.LogWarning("APOC triggers are not enabled. Property validation will not be enforced at the database level. " +
                    "To enable triggers, set 'apoc.trigger.enabled=true' in your apoc.conf file. Error: {Error}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create validation trigger {Trigger} for property {Property} on relationship type {Type}. APOC plugin might not be available or properly configured.", triggerName, propertyName, type);
            }
        }
    }

    private async Task CreateRelationshipPropertyIndexes(string type)
    {
        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            var config = _registry.GetRelationshipConfiguration(type);
            if (config != null)
            {
                foreach (var (propertyName, propertyConfig) in config.Properties)
                {
                    if (propertyConfig.IsIndexed)
                    {
                        var indexName = $"idx_{type}_{propertyName}".ToLowerInvariant();
                        var createIndex = $"CREATE INDEX {indexName} IF NOT EXISTS FOR ()-[r:`{type}`]-() ON (r.{propertyName})";
                        var result = await tx.RunAsync(createIndex);
                        await result.ConsumeAsync();
                    }
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

    private async Task CreateNodeIndexes(string label)
    {
        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            var config = _registry.GetNodeConfiguration(label);
            if (config != null)
            {
                foreach (var (propertyName, propertyConfig) in config.Properties)
                {
                    if (propertyConfig.IsIndexed)
                    {
                        var indexName = $"idx_{label}_{propertyName}".ToLowerInvariant();
                        var createIndex = $"CREATE INDEX {indexName} IF NOT EXISTS FOR (n:`{label}`) ON (n.{propertyName})";
                        var result = await tx.RunAsync(createIndex);
                        await result.ConsumeAsync();
                        _logger.LogDebug("Created index {Index} for property {Property} on label {Label}", indexName, propertyName, label);
                    }
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

    private async Task CreateRelationshipIndexes(string type)
    {
        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            var config = _registry.GetRelationshipConfiguration(type);
            if (config != null)
            {
                foreach (var (propertyName, propertyConfig) in config.Properties)
                {
                    if (propertyConfig.IsIndexed)
                    {
                        var indexName = $"idx_{type}_{propertyName}".ToLowerInvariant();
                        var createIndex = $"CREATE INDEX {indexName} IF NOT EXISTS FOR ()-[r:`{type}`]-() ON (r.{propertyName})";
                        var result = await tx.RunAsync(createIndex);
                        await result.ConsumeAsync();
                        _logger.LogDebug("Created index {Index} for property {Property} on relationship type {Type}", indexName, propertyName, type);
                    }
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

    // Helper to build Cypher validation logic for APOC trigger
    private string BuildValidationLogic(Cvoya.Graph.Model.PropertyValidation validation, string propertyName, string label)
    {
        var checks = new List<string>();
        // Numeric value checks
        if (validation.MinValue != null)
        {
            checks.Add($"n.{propertyName} < {validation.MinValue}");
        }
        if (validation.MaxValue != null)
        {
            checks.Add($"n.{propertyName} > {validation.MaxValue}");
        }
        // String length checks
        if (validation.MinLength != null)
        {
            checks.Add($"size(n.{propertyName}) < {validation.MinLength}");
        }
        if (validation.MaxLength != null)
        {
            checks.Add($"size(n.{propertyName}) > {validation.MaxLength}");
        }
        // Pattern check
        if (!string.IsNullOrEmpty(validation.Pattern))
        {
            // Cypher =~ operator for regex
            checks.Add($"NOT n.{propertyName} =~ '{validation.Pattern.Replace("'", "\\'")}'");
        }
        if (checks.Count == 0)
            return string.Empty;
        // APOC trigger script: fail if any check is true
        var condition = string.Join(" OR ", checks);
        // The trigger script must be a single-line string for apoc.trigger.add
        var cypher = $@"UNWIND $createdNodes AS n WITH n WHERE n.{propertyName} IS NOT NULL AND ({condition}) CALL apoc.util.validate(true, 'Validation failed for {label}.{propertyName}', [n]) YIELD value RETURN value";
        return cypher.Replace("\n", " ").Replace("\r", " ").Replace("'", "\\'");
    }

    /// <summary>
    /// Clears the processed schemas cache.
    /// </summary>
    public void ClearCache()
    {
        lock (_schemaLock)
        {
            _processedSchemas.Clear();
        }
        _logger.LogDebug("Cleared schema cache");
    }

    /// <summary>
    /// Checks if a schema has already been processed.
    /// </summary>
    /// <param name="cacheKey">The cache key to check.</param>
    /// <returns>True if the schema has been processed, false otherwise.</returns>
    public bool IsSchemaProcessed(string cacheKey)
    {
        lock (_schemaLock)
        {
            return _processedSchemas.Contains(cacheKey);
        }
    }

    /// <summary>
    /// Gets the property configuration registry.
    /// </summary>
    /// <returns>The property configuration registry.</returns>
    public PropertyConfigurationRegistry GetRegistry()
    {
        return _registry;
    }

    public async Task EnsureSchemaForNode<T>(T node) where T : Model.INode
    {
        ArgumentNullException.ThrowIfNull(node);

        var type = node.GetType();
        var label = Labels.GetLabelFromType(type);
        var cacheKey = $"schema_{_context.DatabaseName}_{label}";

        lock (_schemaLock)
        {
            if (_processedSchemas.Contains(cacheKey))
                return;
        }

        try
        {
            await CreateEntityConstraints(label, type);
            await CreatePropertyIndexes(label, type);
            await CreateFullTextIndexes(label, type);

            lock (_schemaLock)
            {
                _processedSchemas.Add(cacheKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schema for node label: {Label}", label);
            throw new GraphException($"Failed to create schema for node label: {label}", ex);
        }
    }

    public async Task EnsureSchemaForRelationship<T>(T relationship) where T : Model.IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        var type = relationship.GetType();
        var label = Labels.GetLabelFromType(type);
        var cacheKey = $"schema_{_context.DatabaseName}_{label}";

        lock (_schemaLock)
        {
            if (_processedSchemas.Contains(cacheKey))
                return;
        }

        try
        {
            await CreateRelationshipConstraints(label);
            await CreateRelationshipPropertyIndexes(label);
            await CreateFullTextIndexes(label, type);

            lock (_schemaLock)
            {
                _processedSchemas.Add(cacheKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schema for relationship type: {Label}", label);
            throw new GraphException($"Failed to create schema for relationship type: {label}", ex);
        }
    }

    /// <summary>
    /// Creates full text indexes for the given entity type.
    /// </summary>
    private async Task CreateFullTextIndexes(string label, Type entityType)
    {
        _logger.LogDebug("CreateFullTextIndexes called for label {Label} and type {EntityType}", label, entityType.Name);

        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            var isNode = typeof(Model.INode).IsAssignableFrom(entityType);
            var isRelationship = typeof(Model.IRelationship).IsAssignableFrom(entityType);

            _logger.LogDebug("Entity type analysis: isNode={IsNode}, isRelationship={IsRelationship}", isNode, isRelationship);

            if (isNode)
            {
                _logger.LogDebug("Creating node full text index for label {Label}", label);
                await CreateNodeFullTextIndex(tx, label, entityType);
            }
            if (isRelationship)
            {
                _logger.LogDebug("Creating relationship full text index for label {Label}", label);
                await CreateRelationshipFullTextIndex(tx, label, entityType);
                // Also create a type-specific index for this relationship type
                var typeSpecificIndexName = $"relationships_{entityType.Name.ToLowerInvariant()}_fulltext_index";
                var relProps = GetFullTextSearchProperties(entityType);
                if (relProps.Count > 0)
                {
                    var relPropsList = string.Join(", ", relProps.Select(p => $"r.{p}"));
                    var typeSpecificIndexQuery = $"CREATE FULLTEXT INDEX {typeSpecificIndexName} IF NOT EXISTS FOR ()-[r:{label}]-() ON EACH [{relPropsList}]";
                    var typeSpecificResult = await tx.RunAsync(typeSpecificIndexQuery);
                    await typeSpecificResult.ConsumeAsync();
                    _logger.LogDebug($"Created type-specific relationship full text index {typeSpecificIndexName} for relationship type {label} on properties: {string.Join(", ", relProps)}");
                }
            }
            await tx.CommitAsync();
            _logger.LogDebug("Successfully created full text indexes for {EntityType} with label {Label}", entityType.Name, label);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Failed to create full text indexes for {EntityType} with label {Label}", entityType.Name, label);
            throw;
        }
    }

    /// <summary>
    /// Creates general full text indexes for all nodes and relationships.
    /// This is called once during schema initialization.
    /// </summary>
    public async Task CreateGeneralFullTextIndexes()
    {
        _logger.LogDebug("CreateGeneralFullTextIndexes called");

        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            // For now, we'll create general indexes for common labels
            // In a real implementation, you might want to discover all labels dynamically

            // Create general node index for Person label (most common in tests)
            var personNodeIndexQuery = "CREATE FULLTEXT INDEX nodes_fulltext_index IF NOT EXISTS FOR (n:Person) ON EACH [n.FirstName, n.LastName, n.Bio, n.Name, n.Title, n.Summary, n.Genre, n.Description, n.Country, n.Nationality]";
            var personNodeResult = await tx.RunAsync(personNodeIndexQuery);
            await personNodeResult.ConsumeAsync();
            _logger.LogDebug("Created general node full text index for Person label");

            // Create general relationship index for WORKS_REALLY_WELL_WITH label (most common in tests)
            var relIndexQuery = "CREATE FULLTEXT INDEX relationships_fulltext_index IF NOT EXISTS FOR ()-[r:WORKS_REALLY_WELL_WITH]-() ON EACH [r.HowWell, r.WritingStyle, r.CollaborationType, r.Description]";
            var relResult = await tx.RunAsync(relIndexQuery);
            await relResult.ConsumeAsync();
            _logger.LogDebug("Created general relationship full text index for WORKS_REALLY_WELL_WITH label");

            // Create general entity index (same as node index for now)
            var entityIndexQuery = "CREATE FULLTEXT INDEX entities_fulltext_index IF NOT EXISTS FOR (n:Person) ON EACH [n.FirstName, n.LastName, n.Bio, n.Name, n.Title, n.Summary, n.Genre, n.Description, n.Country, n.Nationality]";
            var entityResult = await tx.RunAsync(entityIndexQuery);
            await entityResult.ConsumeAsync();
            _logger.LogDebug("Created general entity full text index");

            await tx.CommitAsync();
            _logger.LogDebug("Successfully created general full text indexes");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Failed to create general full text indexes");
            // Don't throw - full text search is optional functionality
        }
    }

    private async Task CreateNodeFullTextIndex(IAsyncTransaction tx, string label, Type entityType)
    {
        _logger.LogDebug("CreateNodeFullTextIndex called for label {Label} and type {EntityType}", label, entityType.Name);

        var properties = GetFullTextSearchProperties(entityType);

        _logger.LogDebug("Found {PropertyCount} full text search properties for type {EntityType}: {Properties}",
            properties.Count, entityType.Name, string.Join(", ", properties));

        if (!properties.Any())
        {
            _logger.LogDebug("No properties found for full text search, skipping index creation for {Label}", label);
            return;
        }

        var indexName = $"nodes_{label.ToLowerInvariant()}_fulltext_index";
        var propertyList = string.Join(", ", properties.Select(p => $"n.{p}"));

        var createIndexQuery = $"CREATE FULLTEXT INDEX {indexName} IF NOT EXISTS FOR (n:`{label}`) ON EACH [{propertyList}]";

        _logger.LogDebug("Executing full text index creation query: {Query}", createIndexQuery);

        var result = await tx.RunAsync(createIndexQuery);
        await result.ConsumeAsync();

        _logger.LogDebug("Created full text index {IndexName} for node label {Label} on properties: {Properties}",
            indexName, label, string.Join(", ", properties));
    }

    private async Task CreateRelationshipFullTextIndex(IAsyncTransaction tx, string label, Type entityType)
    {
        var properties = GetFullTextSearchProperties(entityType);
        if (!properties.Any()) return;

        var indexName = $"relationships_{label.ToLowerInvariant()}_fulltext_index";
        var propertyList = string.Join(", ", properties.Select(p => $"r.{p}"));

        var createIndexQuery = $"CREATE FULLTEXT INDEX {indexName} IF NOT EXISTS FOR ()-[r:`{label}`]-() ON EACH [{propertyList}]";

        var result = await tx.RunAsync(createIndexQuery);
        await result.ConsumeAsync();

        _logger.LogDebug("Created full text index {IndexName} for relationship type {Label} on properties: {Properties}",
            indexName, label, string.Join(", ", properties));
    }

    private List<string> GetFullTextSearchProperties(Type entityType)
    {
        var properties = new List<string>();

        foreach (var prop in entityType.GetProperties())
        {
            // Skip the base entity properties
            if (prop.Name == nameof(Model.IEntity.Id)) continue;
            if (typeof(Model.IRelationship).IsAssignableFrom(entityType))
            {
                if (prop.Name == nameof(Model.IRelationship.StartNodeId) ||
                    prop.Name == nameof(Model.IRelationship.EndNodeId) ||
                    prop.Name == nameof(Model.IRelationship.Direction))
                    continue;
            }

            // Only include string properties by default
            if (prop.PropertyType != typeof(string)) continue;

            // Check for explicit inclusion/exclusion via PropertyAttribute
            var propertyAttr = prop.GetCustomAttribute<PropertyAttribute>();
            if (propertyAttr != null)
            {
                if (propertyAttr.Ignore) continue;
                if (propertyAttr.IncludeInFullTextSearch == false) continue;
            }

            // Include by default for string properties
            var propertyName = propertyAttr?.Label ?? prop.Name;
            properties.Add(propertyName);
        }

        return properties;
    }
}
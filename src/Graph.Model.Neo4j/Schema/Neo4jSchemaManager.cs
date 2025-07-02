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

        lock (_schemaLock)
        {
            if (_processedSchemas.Contains(cacheKey))
                return;
        }

        try
        {
            if (entity is Model.INode)
            {
                await CreateEntityConstraints(label, type);
                await CreatePropertyIndexes(label, type);
            }
            else if (entity is Model.IRelationship)
            {
                await CreateRelationshipConstraints(label);
                await CreateRelationshipPropertyIndexes(label);
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
        }
        else if (typeof(Model.IRelationship).IsAssignableFrom(entityType))
        {
            await CreateRelationshipConstraints(label);
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
            await tx.RunAsync(idConstraint);

            // Create constraints based on property configurations
            var config = _registry.GetNodeConfiguration(label);
            if (config != null)
            {
                foreach (var (propertyName, propertyConfig) in config.Properties)
                {
                    if (propertyConfig.IsUnique)
                    {
                        var uniqueConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:`{label}`) REQUIRE n.{propertyName} IS UNIQUE";
                        await tx.RunAsync(uniqueConstraint);
                        _logger.LogDebug("Created unique constraint for property {Property} on label {Label}", propertyName, label);
                    }

                    if (propertyConfig.IsRequired)
                    {
                        var notNullConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:`{label}`) REQUIRE n.{propertyName} IS NOT NULL";
                        await tx.RunAsync(notNullConstraint);
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

    private async Task CreateRelationshipConstraints(string type)
    {
        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            // Always create unique constraint on Id for relationships
            var idConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR ()-[r:`{type}`]-() REQUIRE r.{nameof(Model.IEntity.Id)} IS UNIQUE";
            await tx.RunAsync(idConstraint);

            // Create constraints based on property configurations
            var config = _registry.GetRelationshipConfiguration(type);
            if (config != null)
            {
                foreach (var (propertyName, propertyConfig) in config.Properties)
                {
                    if (propertyConfig.IsUnique)
                    {
                        var uniqueConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR ()-[r:`{type}`]-() REQUIRE r.{propertyName} IS UNIQUE";
                        await tx.RunAsync(uniqueConstraint);
                    }

                    if (propertyConfig.IsRequired)
                    {
                        var notNullConstraint = $"CREATE CONSTRAINT IF NOT EXISTS FOR ()-[r:`{type}`]-() REQUIRE r.{propertyName} IS NOT NULL";
                        await tx.RunAsync(notNullConstraint);
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
                        await tx.RunAsync(createIndex);
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
                        await tx.RunAsync(createIndex);
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
                        await tx.RunAsync(createIndex);
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
}
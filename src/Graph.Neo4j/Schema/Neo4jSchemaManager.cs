// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Schema;

using System.Linq;
using Cvoya.Graph;
using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Cypher;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using SchemaSnapshot = System.Collections.Generic.IReadOnlyDictionary<string, Neo4jSchemaObjectDescriptor>;

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
    private Task<bool>? _supportsPropertyExistenceConstraintsTask;
    private volatile bool _isSchemaInitialized;

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
            _logger.LogDebugNeo4jSchemaManager45();
            return;
        }

        // Use semaphore for async-safe concurrency control
        await _initializationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check pattern
            if (_isSchemaInitialized)
            {
                _logger.LogDebugNeo4jSchemaManager56();
                return;
            }

            _logger.LogInformationNeo4jSchemaManager60();

            // Initialize the schema registry if not already done
            if (!_schemaRegistry.IsInitialized)
            {
                await _schemaRegistry.InitializeAsync(cancellationToken).ConfigureAwait(false);
                var nodeLabelsCount = (await _schemaRegistry.GetRegisteredNodeLabelsAsync(cancellationToken).ConfigureAwait(false)).Count();
                var relationshipTypesCount = (await _schemaRegistry.GetRegisteredRelationshipTypesAsync(cancellationToken).ConfigureAwait(false)).Count();
                _logger.LogDebugNeo4jSchemaManager68(nodeLabelsCount, relationshipTypesCount);
            }

            // Read the installed schema once so re-initialization over an existing equivalent
            // schema skips creation without a per-object conflict round trip.
            var existingSchema = await GetExistingSchemaSnapshotAsync(cancellationToken).ConfigureAwait(false);

            // Create constraints and indexes for all discovered node types
            var nodeLabels = await _schemaRegistry.GetRegisteredNodeLabelsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var nodeLabel in nodeLabels)
            {
                await CreateNodeConstraintsAndIndexesAsync(nodeLabel, existingSchema, cancellationToken).ConfigureAwait(false);
            }

            // Create constraints and indexes for all discovered relationship types
            var relationshipTypes = await _schemaRegistry.GetRegisteredRelationshipTypesAsync(cancellationToken).ConfigureAwait(false);
            foreach (var relationshipType in relationshipTypes)
            {
                await CreateRelationshipConstraintsAndIndexesAsync(relationshipType, existingSchema, cancellationToken).ConfigureAwait(false);
            }

            // Create general full text indexes
            await CreateGeneralFullTextIndexesAsync(existingSchema, cancellationToken).ConfigureAwait(false);

            _isSchemaInitialized = true;
            _logger.LogInformationNeo4jSchemaManager90();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorNeo4jSchemaManager98(ex);
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
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformationNeo4jSchemaManager116();

        try
        {
            // Drop all existing indexes
            await DropAllIndexesAsync(cancellationToken).ConfigureAwait(false);

            // Recreate indexes for all registered node types. No snapshot: the indexes were just
            // dropped, so anything a create conflicts with came from a concurrent peer and is
            // handled by the conflict path.
            var nodeLabels = await _schemaRegistry.GetRegisteredNodeLabelsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var nodeLabel in nodeLabels)
            {
                await CreateNodeIndexesAsync(nodeLabel, existingSchema: null, cancellationToken).ConfigureAwait(false);
            }

            // Recreate indexes for all registered relationship types
            var relationshipTypes = await _schemaRegistry.GetRegisteredRelationshipTypesAsync(cancellationToken).ConfigureAwait(false);
            foreach (var relationshipType in relationshipTypes)
            {
                await CreateRelationshipIndexesAsync(relationshipType, existingSchema: null, cancellationToken).ConfigureAwait(false);
            }

            // Recreate general full text indexes
            await CreateGeneralFullTextIndexesAsync(existingSchema: null, cancellationToken).ConfigureAwait(false);

            _logger.LogInformationNeo4jSchemaManager140();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorNeo4jSchemaManager148(ex);
            throw new GraphException("Failed to recreate Neo4j indexes", ex);
        }
    }

    private async Task CreateNodeConstraintsAndIndexesAsync(
        string label,
        SchemaSnapshot? existingSchema,
        CancellationToken cancellationToken = default)
    {
        var processedKey = $"node:{label}";
        lock (_processedSchemas)
        {
            if (_processedSchemas.Contains(processedKey))
            {
                _logger.LogDebugNeo4jSchemaManager160(label);
                return;
            }
        }

        var schema = await _schemaRegistry.GetNodeSchemaAsync(label, cancellationToken).ConfigureAwait(false);
        if (schema == null)
        {
            _logger.LogWarningNeo4jSchemaManager168(label);
            return;
        }

        // First, create constraints in their own transaction
        await CreateNodeConstraintsAsync(label, schema, existingSchema, cancellationToken).ConfigureAwait(false);

        // Then, create indexes in a separate transaction
        await CreateNodeIndexesAsync(label, existingSchema, cancellationToken).ConfigureAwait(false);

        lock (_processedSchemas)
        {
            _processedSchemas.Add(processedKey);
        }

        _logger.LogDebugNeo4jSchemaManager183(label);
    }

    private async Task CreateRelationshipConstraintsAndIndexesAsync(
        string type,
        SchemaSnapshot? existingSchema,
        CancellationToken cancellationToken = default)
    {
        var processedKey = $"relationship:{type}";
        lock (_processedSchemas)
        {
            if (_processedSchemas.Contains(processedKey))
            {
                _logger.LogDebugNeo4jSchemaManager193(type);
                return;
            }
        }

        var schema = await _schemaRegistry.GetRelationshipSchemaAsync(type, cancellationToken).ConfigureAwait(false);
        if (schema == null)
        {
            _logger.LogWarningNeo4jSchemaManager201(type);
            return;
        }

        // First, create constraints in their own transaction
        await CreateRelationshipConstraintsAsync(type, schema, existingSchema, cancellationToken).ConfigureAwait(false);

        // Then, create indexes in a separate transaction
        await CreateRelationshipIndexesAsync(type, existingSchema, cancellationToken).ConfigureAwait(false);

        lock (_processedSchemas)
        {
            _processedSchemas.Add(processedKey);
        }

        _logger.LogDebugNeo4jSchemaManager216(type);
    }

    private async Task CreateNodeConstraintsAsync(
        string label,
        EntitySchemaInfo schema,
        SchemaSnapshot? existingSchema,
        CancellationToken cancellationToken)
    {
        var supportsPropertyExistenceConstraints =
            await SupportsPropertyExistenceConstraintsAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var idConstraintName = $"unique_{label}_Id".ToLowerInvariant();
            var createdIdConstraint = await CreateSchemaObjectAsync(
                new Neo4jSchemaObjectCreation(
                    new Neo4jSchemaObjectDescriptor(
                        idConstraintName,
                        Neo4jSchemaObjectKind.NodeUniquenessConstraint,
                        Neo4jSchemaEntityType.Node,
                        [label],
                        ["Id"]),
                    $"CREATE CONSTRAINT {CypherIdentifier.EscapeIfNeeded(idConstraintName, "constraint name")} FOR (n:{EscapedLabel(label)}) REQUIRE n.Id IS UNIQUE"),
                existingSchema,
                cancellationToken).ConfigureAwait(false);
            if (createdIdConstraint)
            {
                _logger.LogDebugNeo4jSchemaManager239(label);
            }

            if (schema.HasCompositeKey())
            {
                var keyPropertyNames = schema.GetKeyProperties().Select(property => property.Name).ToList();
                var compositeKeyConstraintName =
                    $"composite_key_{label}_{string.Join("_", keyPropertyNames)}".ToLowerInvariant();
                var cypherPropertyNames = keyPropertyNames.Select(property => EscapedProperty("n", property));

                var createdCompositeKeyConstraint = await CreateSchemaObjectAsync(
                    new Neo4jSchemaObjectCreation(
                        new Neo4jSchemaObjectDescriptor(
                            compositeKeyConstraintName,
                            Neo4jSchemaObjectKind.NodeUniquenessConstraint,
                            Neo4jSchemaEntityType.Node,
                            [label],
                            keyPropertyNames),
                        $"CREATE CONSTRAINT {CypherIdentifier.EscapeIfNeeded(compositeKeyConstraintName, "constraint name")} FOR (n:{EscapedLabel(label)}) REQUIRE ({string.Join(", ", cypherPropertyNames)}) IS UNIQUE"),
                    existingSchema,
                    cancellationToken).ConfigureAwait(false);

                if (createdCompositeKeyConstraint && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebugNeo4jSchemaManager261(string.Join(", ", keyPropertyNames), label);
                }
            }

            foreach (var propertySchema in schema.Properties.Values)
            {
                if (propertySchema.Ignore)
                {
                    continue;
                }

                if (propertySchema.IsUnique && !(schema.HasCompositeKey() && propertySchema.IsKey))
                {
                    var uniqueConstraintName =
                        $"unique_{label}_{propertySchema.Name}".ToLowerInvariant();
                    var createdUniqueConstraint = await CreateSchemaObjectAsync(
                        new Neo4jSchemaObjectCreation(
                            new Neo4jSchemaObjectDescriptor(
                                uniqueConstraintName,
                                Neo4jSchemaObjectKind.NodeUniquenessConstraint,
                                Neo4jSchemaEntityType.Node,
                                [label],
                                [propertySchema.Name]),
                            $"CREATE CONSTRAINT {CypherIdentifier.EscapeIfNeeded(uniqueConstraintName, "constraint name")} FOR (n:{EscapedLabel(label)}) REQUIRE {EscapedProperty("n", propertySchema.Name)} IS UNIQUE"),
                        existingSchema,
                        cancellationToken).ConfigureAwait(false);
                    if (createdUniqueConstraint)
                    {
                        _logger.LogDebugNeo4jSchemaManager283(propertySchema.Name, label);
                    }
                }

                if (!propertySchema.IsRequired)
                {
                    continue;
                }

                // Complex properties are represented by separate nodes and cannot use property
                // existence constraints on the owning node.
                if (!GraphDataModel.IsSimple(propertySchema.PropertyInfo.PropertyType))
                {
                    _logger.LogDebugNeo4jSchemaManager293(propertySchema.Name, label);
                    continue;
                }

                if (!supportsPropertyExistenceConstraints)
                {
                    _logger.LogDebugNeo4jSchemaManager299(propertySchema.Name, label);
                    continue;
                }

                var notNullConstraintName =
                    $"notnull_{label}_{propertySchema.Name}".ToLowerInvariant();
                var createdNotNullConstraint = await CreateSchemaObjectAsync(
                    new Neo4jSchemaObjectCreation(
                        new Neo4jSchemaObjectDescriptor(
                            notNullConstraintName,
                            Neo4jSchemaObjectKind.NodePropertyExistenceConstraint,
                            Neo4jSchemaEntityType.Node,
                            [label],
                            [propertySchema.Name]),
                        $"CREATE CONSTRAINT {CypherIdentifier.EscapeIfNeeded(notNullConstraintName, "constraint name")} FOR (n:{EscapedLabel(label)}) REQUIRE {EscapedProperty("n", propertySchema.Name)} IS NOT NULL"),
                    existingSchema,
                    cancellationToken).ConfigureAwait(false);
                if (createdNotNullConstraint)
                {
                    _logger.LogDebugNeo4jSchemaManager313(propertySchema.Name, label);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorNeo4jSchemaManager323(ex, label);
            throw;
        }
    }

    private async Task CreateRelationshipConstraintsAsync(
        string type,
        EntitySchemaInfo schema,
        SchemaSnapshot? existingSchema,
        CancellationToken cancellationToken)
    {
        var supportsPropertyExistenceConstraints =
            await SupportsPropertyExistenceConstraintsAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var idConstraintName = $"unique_rel_{type}_Id".ToLowerInvariant();
            var createdIdConstraint = await CreateSchemaObjectAsync(
                new Neo4jSchemaObjectCreation(
                    new Neo4jSchemaObjectDescriptor(
                        idConstraintName,
                        Neo4jSchemaObjectKind.RelationshipUniquenessConstraint,
                        Neo4jSchemaEntityType.Relationship,
                        [type],
                        ["Id"]),
                    $"CREATE CONSTRAINT {CypherIdentifier.EscapeIfNeeded(idConstraintName, "constraint name")} FOR ()-[r:{EscapedType(type)}]-() REQUIRE r.Id IS UNIQUE"),
                existingSchema,
                cancellationToken).ConfigureAwait(false);
            if (createdIdConstraint)
            {
                _logger.LogDebugNeo4jSchemaManager348(type);
            }

            if (schema.HasCompositeKey())
            {
                var keyPropertyNames = schema.GetKeyProperties().Select(property => property.Name).ToList();
                var compositeKeyConstraintName =
                    $"composite_key_rel_{type}_{string.Join("_", keyPropertyNames)}".ToLowerInvariant();
                var cypherPropertyNames = keyPropertyNames.Select(property => EscapedProperty("r", property));

                var createdCompositeKeyConstraint = await CreateSchemaObjectAsync(
                    new Neo4jSchemaObjectCreation(
                        new Neo4jSchemaObjectDescriptor(
                            compositeKeyConstraintName,
                            Neo4jSchemaObjectKind.RelationshipUniquenessConstraint,
                            Neo4jSchemaEntityType.Relationship,
                            [type],
                            keyPropertyNames),
                        $"CREATE CONSTRAINT {CypherIdentifier.EscapeIfNeeded(compositeKeyConstraintName, "constraint name")} FOR ()-[r:{EscapedType(type)}]-() REQUIRE ({string.Join(", ", cypherPropertyNames)}) IS UNIQUE"),
                    existingSchema,
                    cancellationToken).ConfigureAwait(false);

                if (createdCompositeKeyConstraint && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebugNeo4jSchemaManager370(string.Join(", ", keyPropertyNames), type);
                }
            }

            foreach (var propertySchema in schema.Properties.Values)
            {
                if (propertySchema.Ignore)
                {
                    continue;
                }

                if (propertySchema.IsUnique && !(schema.HasCompositeKey() && propertySchema.IsKey))
                {
                    var uniqueConstraintName =
                        $"unique_rel_{type}_{propertySchema.Name}".ToLowerInvariant();
                    var createdUniqueConstraint = await CreateSchemaObjectAsync(
                        new Neo4jSchemaObjectCreation(
                            new Neo4jSchemaObjectDescriptor(
                                uniqueConstraintName,
                                Neo4jSchemaObjectKind.RelationshipUniquenessConstraint,
                                Neo4jSchemaEntityType.Relationship,
                                [type],
                                [propertySchema.Name]),
                            $"CREATE CONSTRAINT {CypherIdentifier.EscapeIfNeeded(uniqueConstraintName, "constraint name")} FOR ()-[r:{EscapedType(type)}]-() REQUIRE {EscapedProperty("r", propertySchema.Name)} IS UNIQUE"),
                        existingSchema,
                        cancellationToken).ConfigureAwait(false);
                    if (createdUniqueConstraint)
                    {
                        _logger.LogDebugNeo4jSchemaManager392(propertySchema.Name, type);
                    }
                }

                if (!propertySchema.IsRequired)
                {
                    continue;
                }

                if (!supportsPropertyExistenceConstraints)
                {
                    _logger.LogDebugNeo4jSchemaManager400(propertySchema.Name, type);
                    continue;
                }

                var notNullConstraintName =
                    $"notnull_rel_{type}_{propertySchema.Name}".ToLowerInvariant();
                var createdNotNullConstraint = await CreateSchemaObjectAsync(
                    new Neo4jSchemaObjectCreation(
                        new Neo4jSchemaObjectDescriptor(
                            notNullConstraintName,
                            Neo4jSchemaObjectKind.RelationshipPropertyExistenceConstraint,
                            Neo4jSchemaEntityType.Relationship,
                            [type],
                            [propertySchema.Name]),
                        $"CREATE CONSTRAINT {CypherIdentifier.EscapeIfNeeded(notNullConstraintName, "constraint name")} FOR ()-[r:{EscapedType(type)}]-() REQUIRE {EscapedProperty("r", propertySchema.Name)} IS NOT NULL"),
                    existingSchema,
                    cancellationToken).ConfigureAwait(false);
                if (createdNotNullConstraint)
                {
                    _logger.LogDebugNeo4jSchemaManager414(propertySchema.Name, type);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorNeo4jSchemaManager424(ex, type);
            throw;
        }
    }

    private async Task<bool> SupportsPropertyExistenceConstraintsAsync(CancellationToken cancellationToken)
    {
        var detectionTask = _supportsPropertyExistenceConstraintsTask ??= DetectPropertyExistenceConstraintSupportAsync();
        return await detectionTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> DetectPropertyExistenceConstraintSupportAsync()
    {
        try
        {
            var session = _context.Driver.AsyncSession(builder => builder.WithDatabase("system"));
            await using var sessionLease = session.ConfigureAwait(false);
            var result = await session.RunAsync("CALL dbms.components() YIELD edition RETURN edition").ConfigureAwait(false);
            var record = await result.SingleAsync().ConfigureAwait(false);
            var edition = record["edition"].As<string>();

            var supported = !edition.Equals("community", StringComparison.OrdinalIgnoreCase);
            if (!supported)
            {
                _logger.LogInformationNeo4jSchemaManager447();
            }

            return supported;
        }
        catch (Neo4jException ex)
        {
            _logger.LogWarningNeo4jSchemaManager454(ex);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarningNeo4jSchemaManager459(ex);
            return true;
        }
    }

    /// <summary>
    /// Ensures the requested schema object exists. Returns <see langword="true"/> when a create
    /// (or recreate) statement was executed, and <see langword="false"/> when an equivalent
    /// object was already installed.
    /// </summary>
    private async Task<bool> CreateSchemaObjectAsync(
        Neo4jSchemaObjectCreation requested,
        SchemaSnapshot? existingSchema,
        CancellationToken cancellationToken,
        bool recreateIncompatibleIndex = false)
    {
        if (existingSchema is not null
            && existingSchema.TryGetValue(requested.Descriptor.Name, out var snapshot))
        {
            if (requested.Descriptor.IsEquivalentTo(snapshot))
            {
                _logger.LogDebugNeo4jSchemaManager478(requested.Descriptor.Name);
                return false;
            }

            if (CanRecreateIncompatibleIndex(recreateIncompatibleIndex, snapshot))
            {
                return await RecreateIncompatibleIndexAsync(requested, cancellationToken).ConfigureAwait(false);
            }
        }

        try
        {
            await ExecuteManagedWriteAsync(
                async transaction =>
                {
                    var result = await transaction.RunAsync(requested.Cypher).ConfigureAwait(false);
                    await result.ConsumeAsync().ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Neo4jException ex) when (Neo4jSchemaErrorClassifier.IsPotentialEquivalentConflict(ex))
        {
            Neo4jSchemaObjectDescriptor? installed = null;
            try
            {
                installed = await GetSchemaObjectAsync(requested.Descriptor, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception metadataException)
            {
                // Preserve the original actionable Neo4j conflict if the defensive metadata
                // reread itself fails.
                _logger.LogWarningNeo4jSchemaManager491(metadataException, requested.Descriptor.Name);
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }

            if (requested.Descriptor.IsEquivalentTo(installed))
            {
                _logger.LogDebugNeo4jSchemaManager478(requested.Descriptor.Name);
                return false;
            }

            if (CanRecreateIncompatibleIndex(recreateIncompatibleIndex, installed))
            {
                try
                {
                    return await RecreateIncompatibleIndexAsync(requested, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception recreateException)
                {
                    // Preserve the original actionable Neo4j conflict if the recreate fails.
                    _logger.LogWarningNeo4jSchemaManager512(recreateException, requested.Descriptor.Name);
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Whether an incompatible installed object may be replaced by the requested definition.
    /// Only the provider-owned general full-text indexes opt in: their definition is derived
    /// from the whole registered model, so it legitimately drifts as the model evolves, and the
    /// provider owns their fixed names. Everything else keeps the actionable Neo4j error.
    /// </summary>
    private static bool CanRecreateIncompatibleIndex(
        bool recreateIncompatibleIndex,
        Neo4jSchemaObjectDescriptor? installed)
    {
        return recreateIncompatibleIndex && installed?.Kind == Neo4jSchemaObjectKind.FullTextIndex;
    }

    private async Task<bool> RecreateIncompatibleIndexAsync(
        Neo4jSchemaObjectCreation requested,
        CancellationToken cancellationToken)
    {
        _logger.LogInformationNeo4jSchemaManager502(requested.Descriptor.Name);

        await ExecuteManagedWriteAsync(
            async transaction =>
            {
                var dropIndex =
                    $"DROP INDEX {CypherIdentifier.EscapeIfNeeded(requested.Descriptor.Name, "index name")} IF EXISTS";
                var result = await transaction.RunAsync(dropIndex).ConfigureAwait(false);
                await result.ConsumeAsync().ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        // Re-enter without the recreate option so a concurrent peer racing on the same name
        // resolves through the equivalence check instead of drop/create ping-pong.
        return await CreateSchemaObjectAsync(requested, existingSchema: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads all installed constraints plus the managed index types (range and full-text) in two
    /// round trips, keyed by name. Returns <see langword="null"/> when the metadata read fails so
    /// callers fall back to create-first behavior.
    /// </summary>
    private async Task<SchemaSnapshot?> GetExistingSchemaSnapshotAsync(CancellationToken cancellationToken)
    {
        const string constraintsQuery =
            "SHOW CONSTRAINTS YIELD name, type, entityType, labelsOrTypes, properties RETURN name, type, entityType, labelsOrTypes, properties";
        const string indexesQuery = """
            SHOW INDEXES YIELD name, type, entityType, labelsOrTypes, properties, owningConstraint
            WHERE (type = 'RANGE' OR type = 'FULLTEXT') AND owningConstraint IS NULL
            RETURN name, type, entityType, labelsOrTypes, properties
            """;

        try
        {
            var existingSchema = new Dictionary<string, Neo4jSchemaObjectDescriptor>(StringComparer.Ordinal);
            await CollectSchemaObjectsAsync(constraintsQuery, isConstraint: true, existingSchema, cancellationToken).ConfigureAwait(false);
            await CollectSchemaObjectsAsync(indexesQuery, isConstraint: false, existingSchema, cancellationToken).ConfigureAwait(false);
            return existingSchema;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarningNeo4jSchemaManager466(ex);
            return null;
        }
    }

    private async Task CollectSchemaObjectsAsync(
        string query,
        bool isConstraint,
        Dictionary<string, Neo4jSchemaObjectDescriptor> existingSchema,
        CancellationToken cancellationToken)
    {
        var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        await using var sessionLease = session.ConfigureAwait(false);
        var executionTask = session.ExecuteReadAsync(
            async transaction =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await transaction.RunAsync(query).ConfigureAwait(false);
                return await result.ToListAsync(cancellationToken).ConfigureAwait(false);
            });
        var records = await executionTask.WaitAsync(cancellationToken).ConfigureAwait(false);

        foreach (var record in records)
        {
            var descriptor = MapSchemaObject(record, isConstraint);
            if (descriptor is not null)
            {
                existingSchema[descriptor.Name] = descriptor;
            }
        }
    }

    private static Neo4jSchemaObjectDescriptor? MapSchemaObject(IRecord record, bool isConstraint)
    {
        var entityType = Neo4jSchemaMetadata.GetEntityType(record["entityType"].As<string?>());
        if (entityType is null)
        {
            return null;
        }

        var metadataType = record["type"].As<string>();
        var kind = isConstraint
            ? Neo4jSchemaMetadata.GetConstraintKind(metadataType)
            : Neo4jSchemaMetadata.GetIndexKind(metadataType);
        var labelsOrTypesValue = record["labelsOrTypes"];
        var propertiesValue = record["properties"];

        return new Neo4jSchemaObjectDescriptor(
            record["name"].As<string>(),
            kind,
            entityType.Value,
            labelsOrTypesValue is null ? [] : labelsOrTypesValue.As<List<string>>(),
            propertiesValue is null ? [] : propertiesValue.As<List<string>>());
    }

    private async Task ExecuteManagedWriteAsync(
        Func<IAsyncQueryRunner, Task> work,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        await using var sessionLease = session.ConfigureAwait(false);
        var executionTask = session.ExecuteWriteAsync(
            async transaction =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await work(transaction).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            });

        // ExecuteWriteAsync owns retry timing but has no CancellationToken overload. WaitAsync
        // keeps the public cancellation contract, while disposing the session interrupts any
        // in-flight retry once the canceled wait leaves this scope.
        await executionTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Neo4jSchemaObjectDescriptor?> GetSchemaObjectAsync(
        Neo4jSchemaObjectDescriptor requested,
        CancellationToken cancellationToken)
    {
        var isConstraint = requested.Kind is
            Neo4jSchemaObjectKind.NodeUniquenessConstraint or
            Neo4jSchemaObjectKind.RelationshipUniquenessConstraint or
            Neo4jSchemaObjectKind.NodePropertyExistenceConstraint or
            Neo4jSchemaObjectKind.RelationshipPropertyExistenceConstraint;
        var query = isConstraint
            ? "SHOW CONSTRAINTS YIELD name, type, entityType, labelsOrTypes, properties WHERE name = $name RETURN name, type, entityType, labelsOrTypes, properties"
            : "SHOW INDEXES YIELD name, type, entityType, labelsOrTypes, properties WHERE name = $name RETURN name, type, entityType, labelsOrTypes, properties";

        var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        await using var sessionLease = session.ConfigureAwait(false);
        var executionTask = session.ExecuteReadAsync(
            async transaction =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await transaction.RunAsync(query, new { name = requested.Name }).ConfigureAwait(false);
                var records = await result.ToListAsync(cancellationToken).ConfigureAwait(false);
                var record = records.SingleOrDefault();
                return record is null ? null : MapSchemaObject(record, isConstraint);
            });

        return await executionTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task CreateNodeIndexesAsync(
        string label,
        SchemaSnapshot? existingSchema,
        CancellationToken cancellationToken = default)
    {
        var schema = await _schemaRegistry.GetNodeSchemaAsync(label, cancellationToken).ConfigureAwait(false);
        if (schema == null) return;

        foreach (var propertySchema in schema.Properties.Values)
        {
            if (propertySchema.Ignore
                || !propertySchema.IsIndexed
                || (propertySchema.IsUnique && !(schema.HasCompositeKey() && propertySchema.IsKey)))
            {
                continue;
            }

            var indexName = $"idx_{label}_{propertySchema.Name}".ToLowerInvariant();
            var created = await CreateSchemaObjectAsync(
                new Neo4jSchemaObjectCreation(
                    new Neo4jSchemaObjectDescriptor(
                        indexName,
                        Neo4jSchemaObjectKind.RangeIndex,
                        Neo4jSchemaEntityType.Node,
                        [label],
                        [propertySchema.Name]),
                    $"CREATE INDEX {CypherIdentifier.EscapeIfNeeded(indexName, "index name")} FOR (n:{EscapedLabel(label)}) ON ({EscapedProperty("n", propertySchema.Name)})"),
                existingSchema,
                cancellationToken).ConfigureAwait(false);
            if (created)
            {
                _logger.LogDebugNeo4jSchemaManager521(indexName, propertySchema.Name, label);
            }
        }
    }

    private async Task CreateRelationshipIndexesAsync(
        string type,
        SchemaSnapshot? existingSchema,
        CancellationToken cancellationToken = default)
    {
        var schema = await _schemaRegistry.GetRelationshipSchemaAsync(type, cancellationToken).ConfigureAwait(false);
        if (schema == null) return;

        foreach (var propertySchema in schema.Properties.Values)
        {
            if (propertySchema.Ignore
                || !propertySchema.IsIndexed
                || (propertySchema.IsUnique && !(schema.HasCompositeKey() && propertySchema.IsKey)))
            {
                continue;
            }

            var indexName = $"idx_{type}_{propertySchema.Name}".ToLowerInvariant();
            var created = await CreateSchemaObjectAsync(
                new Neo4jSchemaObjectCreation(
                    new Neo4jSchemaObjectDescriptor(
                        indexName,
                        Neo4jSchemaObjectKind.RangeIndex,
                        Neo4jSchemaEntityType.Relationship,
                        [type],
                        [propertySchema.Name]),
                    $"CREATE INDEX {CypherIdentifier.EscapeIfNeeded(indexName, "index name")} FOR ()-[r:{EscapedType(type)}]-() ON ({EscapedProperty("r", propertySchema.Name)})"),
                existingSchema,
                cancellationToken).ConfigureAwait(false);
            if (created)
            {
                _logger.LogDebugNeo4jSchemaManager557(indexName, propertySchema.Name, type);
            }
        }
    }

    private async Task CreateGeneralFullTextIndexesAsync(
        SchemaSnapshot? existingSchema,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebugNeo4jSchemaManager572();

        try
        {
            var nodeLabels = new HashSet<string>();
            var nodeStringProps = new HashSet<string>();

            var registeredNodeLabels = await _schemaRegistry.GetRegisteredNodeLabelsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var nodeLabel in registeredNodeLabels)
            {
                nodeLabels.Add(nodeLabel);
                var schema = await _schemaRegistry.GetNodeSchemaAsync(nodeLabel, cancellationToken).ConfigureAwait(false);
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

            if (nodeLabels.Count > 0 && nodeStringProps.Count > 0)
            {
                var orderedLabels = nodeLabels.Order(StringComparer.Ordinal).ToList();
                var orderedProperties = nodeStringProps.Order(StringComparer.Ordinal).ToList();
                var labelList = string.Join("|", orderedLabels.Select(EscapedLabel));
                var propList = string.Join(", ", orderedProperties.Select(property => EscapedProperty("n", property)));

                var createdNodeIndex = await CreateSchemaObjectAsync(
                    new Neo4jSchemaObjectCreation(
                        new Neo4jSchemaObjectDescriptor(
                            "node_fulltext_index",
                            Neo4jSchemaObjectKind.FullTextIndex,
                            Neo4jSchemaEntityType.Node,
                            orderedLabels,
                            orderedProperties),
                        $"CREATE FULLTEXT INDEX node_fulltext_index FOR (n:{labelList}) ON EACH [{propList}]"),
                    existingSchema,
                    cancellationToken,
                    recreateIncompatibleIndex: true).ConfigureAwait(false);
                if (createdNodeIndex)
                {
                    _logger.LogDebugNeo4jSchemaManager609(nodeLabels.Count, nodeStringProps.Count);
                }
            }
            else
            {
                _logger.LogDebugNeo4jSchemaManager613();
            }

            var relTypes = new HashSet<string>();
            var relStringProps = new HashSet<string>();

            var registeredRelationshipTypes = await _schemaRegistry.GetRegisteredRelationshipTypesAsync(cancellationToken).ConfigureAwait(false);
            foreach (var relType in registeredRelationshipTypes)
            {
                relTypes.Add(relType);
                var schema = await _schemaRegistry.GetRelationshipSchemaAsync(relType, cancellationToken).ConfigureAwait(false);
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

            if (relTypes.Count > 0 && relStringProps.Count > 0)
            {
                var orderedTypes = relTypes.Order(StringComparer.Ordinal).ToList();
                var orderedProperties = relStringProps.Order(StringComparer.Ordinal).ToList();
                var typeList = string.Join("|", orderedTypes.Select(EscapedType));
                var propList = string.Join(", ", orderedProperties.Select(property => EscapedProperty("r", property)));

                var createdRelationshipIndex = await CreateSchemaObjectAsync(
                    new Neo4jSchemaObjectCreation(
                        new Neo4jSchemaObjectDescriptor(
                            "rel_fulltext_index",
                            Neo4jSchemaObjectKind.FullTextIndex,
                            Neo4jSchemaEntityType.Relationship,
                            orderedTypes,
                            orderedProperties),
                        $"CREATE FULLTEXT INDEX rel_fulltext_index FOR ()-[r:{typeList}]-() ON EACH [{propList}]"),
                    existingSchema,
                    cancellationToken,
                    recreateIncompatibleIndex: true).ConfigureAwait(false);
                if (createdRelationshipIndex)
                {
                    _logger.LogDebugNeo4jSchemaManager644(relTypes.Count, relStringProps.Count);
                }
            }
            else
            {
                _logger.LogDebugNeo4jSchemaManager648();
            }

            _logger.LogDebugNeo4jSchemaManager652();
        }
        catch (Exception ex)
        {
            _logger.LogErrorNeo4jSchemaManager657(ex);
            throw;
        }
    }

    private async Task DropAllIndexesAsync(CancellationToken cancellationToken)
    {
        var droppedCount = 0;
        await ExecuteManagedWriteAsync(
            async transaction =>
            {
                // Drop only the index types this schema manager creates, and skip indexes owned
                // by constraints because Neo4j rejects dropping those directly.
                const string dropIndexes = """
                    SHOW INDEXES YIELD name, type, owningConstraint
                    WHERE (type = 'RANGE' OR type = 'FULLTEXT') AND owningConstraint IS NULL
                    RETURN name
                    """;
                var result = await transaction.RunAsync(dropIndexes).ConfigureAwait(false);
                var records = await result.ToListAsync(cancellationToken).ConfigureAwait(false);
                droppedCount = records.Count;

                foreach (var record in records)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var indexName = record["name"].As<string>();
                    var dropIndex =
                        $"DROP INDEX {CypherIdentifier.EscapeIfNeeded(indexName, "index name")} IF EXISTS";
                    var dropResult = await transaction.RunAsync(dropIndex).ConfigureAwait(false);
                    await dropResult.ConsumeAsync().ConfigureAwait(false);
                    _logger.LogDebugNeo4jSchemaManager690(indexName);
                }
            },
            cancellationToken).ConfigureAwait(false);
        _logger.LogInformationNeo4jSchemaManager695(droppedCount);
    }

    // Labels, relationship types, and constraint/index names come from user model metadata;
    // they must be escaped before interpolation, exactly like the entity managers do (#214).
    private static string EscapedLabel(string label) =>
        CypherIdentifier.EscapeIfNeeded(label, "node label");

    private static string EscapedType(string type) =>
        CypherIdentifier.EscapeIfNeeded(type, "relationship type");

    // Property names can be custom [Property(Label = ...)] storage names containing spaces,
    // punctuation, or backticks; escape them before interpolating into constraint/index DDL, while
    // leaving the descriptor's stored property list on the original name for discovery comparison (#367).
    private static string EscapedProperty(string alias, string propertyName) =>
        $"{alias}.{CypherIdentifier.EscapeIfNeeded(propertyName, "property name")}";

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
        _logger.LogDebugNeo4jSchemaManager722();
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

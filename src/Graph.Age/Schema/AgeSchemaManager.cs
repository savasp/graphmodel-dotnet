// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Schema;

using Cvoya.Graph.Age.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Initializes provider-neutral schema metadata for the AGE provider.</summary>
/// <remarks>
/// AGE stores entity properties inside <c>agtype</c>, so PostgreSQL property constraints and
/// per-property indexes cannot express the CVOYA schema rules. The CRUD layer enforces those rules
/// before writes; this manager owns metadata discovery and creates the coarse, blob-level full-text
/// GIN indexes (<see cref="AgeFullTextIndex"/>) that accelerate search.
/// </remarks>
internal sealed class AgeSchemaManager
{
    private readonly AgeGraphContext context;
    private readonly SchemaRegistry schemaRegistry;
    private readonly ILogger<AgeSchemaManager> logger;
    private readonly SemaphoreSlim initializationGate = new(1, 1);
    private volatile bool initialized;

    public AgeSchemaManager(AgeGraphContext context, SchemaRegistry schemaRegistry)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        logger = context.LoggerFactory?.CreateLogger<AgeSchemaManager>() ?? NullLogger<AgeSchemaManager>.Instance;
    }

    public async Task InitializeSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return;
        }

        await initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // No recheck of `initialized` here: the registry's own IsInitialized guard makes a
            // second pass through this block a no-op.
            if (!schemaRegistry.IsInitialized)
            {
                await schemaRegistry.InitializeAsync(cancellationToken).ConfigureAwait(false);
            }

            initialized = true;
            logger.LogDebugAgeSchemaManager49();
        }
        finally
        {
            initializationGate.Release();
        }
    }

    public async Task RecreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        await InitializeSchemaAsync(cancellationToken).ConfigureAwait(false);

        // Provisioning creates the physical entity tables. Recreate means rebuild, rather than merely
        // ensuring names exist: this refreshes existing expression indexes if the immutable extraction
        // function changes between library versions.
        var connection = await context.Store.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var connectionLease = connection.ConfigureAwait(false);
        await context.Store.EnsureGraphProvisionedAsync(connection, cancellationToken).ConfigureAwait(false);
        await AgeFullTextIndex.RecreateAsync(connection, context.GraphName, cancellationToken).ConfigureAwait(false);

        logger.LogInformationAgeSchemaManager60();
    }

    public SchemaRegistry GetSchemaRegistry() => schemaRegistry;
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Schema;

using Cvoya.Graph.Age.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Initializes provider-neutral schema metadata for the AGE provider.</summary>
/// <remarks>
/// AGE stores entity properties inside <c>agtype</c>, so PostgreSQL property constraints and
/// indexes cannot express the CVOYA schema rules. The CRUD layer enforces those rules before
/// writes; this manager owns metadata discovery and keeps the public index-recreation operation
/// deterministic until AGE exposes property-level indexing.
/// </remarks>
internal sealed class AgeSchemaManager
{
    private readonly SchemaRegistry schemaRegistry;
    private readonly ILogger<AgeSchemaManager> logger;
    private readonly SemaphoreSlim initializationGate = new(1, 1);
    private bool initialized;

    public AgeSchemaManager(AgeGraphContext context, SchemaRegistry schemaRegistry)
    {
        ArgumentNullException.ThrowIfNull(context);
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
            if (initialized)
            {
                return;
            }

            if (!schemaRegistry.IsInitialized)
            {
                await schemaRegistry.InitializeAsync(cancellationToken).ConfigureAwait(false);
            }

            initialized = true;
            logger.LogDebug("Initialized AGE schema metadata");
        }
        finally
        {
            initializationGate.Release();
        }
    }

    public async Task RecreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        await InitializeSchemaAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("AGE property indexes are unavailable because entity properties are stored in agtype");
    }

    public SchemaRegistry GetSchemaRegistry() => schemaRegistry;
}

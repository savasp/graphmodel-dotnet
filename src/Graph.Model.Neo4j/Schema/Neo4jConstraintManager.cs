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

namespace Cvoya.Graph.Model.Neo4j;

using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


/// <summary>
/// Manages Neo4j constraints to ensure data integrity.
/// </summary>
internal class Neo4jConstraintManager
{
    private readonly HashSet<string> _processedConstraints = [];
    private readonly object _constraintLock = new();
    private readonly GraphContext _context;
    private readonly ILogger _logger;

    public Neo4jConstraintManager(GraphContext context)
    {
        _context = context;
        _logger = context.LoggerFactory?.CreateLogger<Neo4jConstraintManager>() ?? NullLogger<Neo4jConstraintManager>.Instance;
    }

    /// <summary>
    /// Ensures that all necessary constraints exist for the specified label.
    /// </summary>
    public async Task EnsureConstraintsForType<T>(T entity)
        where T : Model.IEntity
    {
        var type = entity.GetType();
        ArgumentNullException.ThrowIfNull(type.FullName);

        var label = Labels.GetLabelFromType(type);

        // Create a more specific cache key that includes database name to avoid cross-test pollution
        var cacheKey = $"constraint_{_context.DatabaseName}_{label}_Id";

        lock (_constraintLock)
        {
            if (_processedConstraints.Contains(cacheKey))
                return;
            _processedConstraints.Add(cacheKey);
        }

        // Create the session and transaction outside the provider
        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        try
        {
            // Use CREATE CONSTRAINT IF NOT EXISTS - this is idempotent and won't fail if constraint already exists
            var cypher = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:`{label}`) REQUIRE n.{nameof(Model.IEntity.Id)} IS UNIQUE";
            await tx.RunAsync(cypher);
            await tx.CommitAsync();

            _logger.LogDebug("Ensured unique constraint exists for label: {Label}", label);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create constraint for label: {Label}", label);
            await tx.RollbackAsync();

            // Remove from cache on failure so we can retry
            lock (_constraintLock)
            {
                _processedConstraints.Remove(cacheKey);
            }

            throw new GraphException($"Failed to create constraint for label: {label}", ex);
        }

        // TODO: Introduce property-specific attributes to control indexing
        // and other constraints/behaviors.
        /*
                var properties = // Get simple properties

                foreach (var prop in properties)
                {
                    if (prop.Name == nameof(Model.IEntity.Id)) continue;
                    var name = prop.GetCustomAttribute<PropertyAttribute>()?.Label ?? prop.Name;
                    var propCypher = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:`{label}`) REQUIRE n.{name} IS NOT NULL";
                    await tx.RunAsync(propCypher);
                }
        */
    }
}
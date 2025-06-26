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
    private readonly HashSet<string> _processedTypes = [];
    private readonly object _constraintLock = new();
    private bool _constraintsLoaded = false;
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

        await LoadExistingConstraints();

        lock (_constraintLock)
        {
            if (_processedTypes.Contains(type.FullName))
                return;
            _processedTypes.Add(type.FullName);
        }

        var label = Labels.GetLabelFromType(type);

        // Create the session and transaction outside the provider
        using var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        using var tx = await session.BeginTransactionAsync();

        // Always add unique constraint for the identifier property
        var cypher = $"CREATE CONSTRAINT IF NOT EXISTS FOR (n:`{label}`) REQUIRE n.{nameof(Model.IEntity.Id)} IS UNIQUE";
        await tx.RunAsync(cypher);

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
        await tx.CommitAsync();
    }

    /// <summary>
    /// Loads the existing constraints from Neo4j
    /// </summary>
    public async Task LoadExistingConstraints()
    {
        if (_constraintsLoaded) return;
        lock (_constraintLock)
        {
            if (_constraintsLoaded) return;
            _constraintsLoaded = true;
        }

        var cypher = "SHOW CONSTRAINTS";
        var session = _context.Driver.AsyncSession(builder => builder.WithDatabase(_context.DatabaseName));
        await using var _ = session;

        var tx = await session.BeginTransactionAsync();
        await using var __ = tx;

        try
        {
            var cursor = await tx.RunAsync(cypher);
            while (await cursor.FetchAsync())
            {
                var record = cursor.Current;
                if (record.Values.TryGetValue("labelsOrTypes", out var labelsOrTypesObj) &&
                    labelsOrTypesObj is IEnumerable<object> labelsOrTypes)
                {
                    foreach (var label in labelsOrTypes)
                    {
                        if (label is string s)
                        {
                            _processedTypes.Add(s);
                        }
                    }
                }
            }

            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load existing constraints from Neo4j.");
            throw new GraphException("Failed to load existing constraints from Neo4j.", ex);
        }
    }
}
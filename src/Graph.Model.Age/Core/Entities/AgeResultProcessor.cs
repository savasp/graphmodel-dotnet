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

namespace Cvoya.Graph.Model.Age.Core.Entities;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Serialization;
using Microsoft.Extensions.Logging;
using Npgsql;
using Npgsql.Age.Types;

/// <summary>
/// Age-specific result processor. Delegates multi-column row reading to EntityResultReader.
/// </summary>
internal sealed class AgeResultProcessor
{
    private readonly EntityFactory _entityFactory;
    private readonly AgeEntityMapper _entityMapper;
    private readonly ILogger<AgeResultProcessor> _logger;
    private readonly EntityResultReader _entityResultReader;

    public AgeResultProcessor(EntityFactory entityFactory, AgeEntityMapper entityMapper, ILoggerFactory? loggerFactory = null)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _entityMapper = entityMapper ?? throw new ArgumentNullException(nameof(entityMapper));
        _logger = loggerFactory?.CreateLogger<AgeResultProcessor>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AgeResultProcessor>.Instance;
        _entityResultReader = new EntityResultReader(entityFactory, entityMapper, _logger);
    }

    public async Task<List<EntityInfo>> ProcessAsync(
        NpgsqlDataReader reader,
        Type elementType,
        CancellationToken cancellationToken,
        LambdaExpression? projectionExpression = null,
        Type? projectionResultType = null,
        string? aggregationType = null)
    {
        var results = new List<EntityInfo>();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                // For multi-column projections, skip single-column Agtype read and go straight
                // to ReadMultiColumnRowAsync which handles all columns including nulls
                if (reader.FieldCount > 1 && !typeof(INode).IsAssignableFrom(elementType) && !typeof(IRelationship).IsAssignableFrom(elementType))
                {
                    var entityInfo = await _entityResultReader.ReadMultiColumnRowAsync(reader, elementType, cancellationToken).ConfigureAwait(false);
                    if (entityInfo != null)
                        results.Add(entityInfo);
                    continue;
                }

                // Single column result - read the Agtype value
                if (await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false))
                    continue;

                var agtype = reader.GetFieldValue<Agtype>(0);
                _logger.LogDebug("ProcessAsync: Read Agtype IsVertex={IsVertex}, IsEdge={IsEdge}",
                    agtype.IsVertex, agtype.IsEdge);

                if (agtype.IsVertex && typeof(INode).IsAssignableFrom(elementType))
                {
                    var vertex = agtype.GetVertex();
                    var entityInfo = _entityMapper.MapVertex(vertex, elementType);
                    results.Add(entityInfo);
                }
                else if (agtype.IsEdge && typeof(IRelationship).IsAssignableFrom(elementType))
                {
                    var edge = agtype.GetEdge();
                    var entityInfo = _entityMapper.MapEdge(edge, elementType);
                    results.Add(entityInfo);
                }
                else if (!typeof(INode).IsAssignableFrom(elementType) && !typeof(IRelationship).IsAssignableFrom(elementType))
                {
                    // For projections (single or multi-column), read all columns and create
                    // an EntityInfo with properties matching column names.
                    // This enables ResultMaterializer.CreateAnonymousTypeObject to construct
                    // the anonymous type or complex type instances from the EntityInfo.
                    var entityInfo = await _entityResultReader.ReadMultiColumnRowAsync(reader, elementType, cancellationToken).ConfigureAwait(false);
                    if (entityInfo != null)
                    {
                        results.Add(entityInfo);
                    }
                    else
                    {
                        // Fallback: scalar value for single-column non-projection results
                        _logger.LogDebug("ProcessAsync: Agtype is not Vertex/Edge, treating as scalar value. Type={Type}",
                            agtype.GetType().Name);

                        var agTypeStr = agtype.ToString();
                        _logger.LogDebug("ProcessAsync: Scalar value string='{Str}', elementType={Type}", agTypeStr, elementType.Name);
                        object? scalarValue = agTypeStr;

                        // Try to convert to the target element type
                        if (elementType == typeof(string)) { /* Already a string, keep as-is */ }
                        else if (elementType == typeof(int) && int.TryParse(agTypeStr, out var intVal)) scalarValue = intVal;
                        else if (elementType == typeof(long) && long.TryParse(agTypeStr, out var longVal)) scalarValue = longVal;
                        else if (elementType == typeof(double) && double.TryParse(agTypeStr, out var doubleVal)) scalarValue = doubleVal;
                        else if (elementType == typeof(bool) && bool.TryParse(agTypeStr, out var boolVal)) scalarValue = boolVal;

                        var simpleProps = new Dictionary<string, Property>(StringComparer.Ordinal)
                        {
                            ["Value"] = new Property(null!, "Value", false, new SimpleValue(scalarValue ?? (object)agTypeStr!, scalarValue?.GetType() ?? typeof(object)))
                        };
                        var fallbackEntityInfo = new EntityInfo(elementType, "ScalarValue", Array.Empty<string>(), simpleProps, new Dictionary<string, Property>(StringComparer.Ordinal));
                        results.Add(fallbackEntityInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProcessAsync: Error processing record");
                throw;
            }
        }

        return results;
    }
}

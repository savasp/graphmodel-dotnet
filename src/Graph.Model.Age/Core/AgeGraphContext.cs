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

namespace Cvoya.Graph.Model.Age.Core;

using Microsoft.Extensions.Logging;
using Npgsql;

/// <summary>
/// Central coordination point for AGE specific services. The class currently acts as a placeholder until the concrete data and query managers are implemented.
/// </summary>
internal sealed class AgeGraphContext
{
    public AgeGraphContext(AgeGraph graph, NpgsqlDataSource dataSource, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        Graph = graph;
        DataSource = dataSource;
        LoggerFactory = loggerFactory;
    }

    internal AgeGraph Graph { get; }

    internal NpgsqlDataSource DataSource { get; }

    internal ILoggerFactory LoggerFactory { get; }
}

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

using Cvoya.Graph.Provider.Model;

namespace Cvoya.Graph.Provider.Neo4j.Tests;

/// <summary>
/// Interface for Neo4j test infrastructure.
/// </summary>
public interface ITestInfrastructure : IAsyncDisposable
{
    /// <summary>
    /// Gets the Neo4j graph provider.
    /// </summary>
    IGraphProvider GraphProvider { get; }

    /// <summary>
    /// Resets the Neo4j database by deleting all nodes and relationships.
    /// </summary>
    Task ResetDatabase();

    /// <summary>
    /// Ensures that the Neo4j infrastructure is started and ready to accept connections.
    /// </summary>
    /// <returns>The connection string to connect to the Neo4j database.</returns>
    Task<string> EnsureReady();
}

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

namespace Cvoya.Graph.Provider.Neo4j.Tests;

/// <summary>
/// Interface for Neo4j test infrastructure.
/// </summary>
public interface ITestInfrastructure : IAsyncDisposable
{
    /// <summary>
    /// Sets up the Neo4j test infrastructure.
    /// </summary>
    Task Setup();

    /// <summary>
    /// Gets the Neo4j graph provider.
    /// </summary>
    /// <value>The Neo4j graph provider.</value>
    Neo4jGraphProvider GraphProvider { get; }

    /// <summary>
    /// Resets the Neo4j database to a clean state for testing purposes.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ResetDatabase();
}

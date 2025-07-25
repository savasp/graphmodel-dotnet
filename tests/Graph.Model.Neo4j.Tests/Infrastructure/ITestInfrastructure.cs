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

namespace Cvoya.Graph.Model.Neo4j.Tests;

/// <summary>
/// Interface for Neo4j test infrastructure.
/// </summary>
public interface ITestInfrastructure : IAsyncDisposable, IAsyncLifetime
{
    /// <summary>
    /// Gets the connection string for the Neo4j database.
    /// </summary>
    string ConnectionString { get; }

    /// <summary>
    /// Gets the username for the Neo4j database.
    /// </summary>
    string Username { get; }

    /// <summary>
    /// Gets the password for the Neo4j database.
    /// </summary>
    string Password { get; }
}

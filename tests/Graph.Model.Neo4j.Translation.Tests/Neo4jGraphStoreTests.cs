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

namespace Cvoya.Graph.Model.Neo4j.Translation.Tests;

using Cvoya.Graph.Model.Neo4j;

public class Neo4jGraphStoreTests
{
    [Fact]
    public void Constructor_WithoutPasswordArgumentOrEnvironment_ThrowsClearException()
    {
        var originalPassword = Environment.GetEnvironmentVariable("NEO4J_PASSWORD");

        try
        {
            Environment.SetEnvironmentVariable("NEO4J_PASSWORD", null);

            var exception = Assert.Throws<InvalidOperationException>(
                () => new Neo4jGraphStore(
                    "bolt://localhost:7687",
                    "neo4j",
                    password: null));

            Assert.Contains("password argument or the NEO4J_PASSWORD environment variable", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NEO4J_PASSWORD", originalPassword);
        }
    }
}

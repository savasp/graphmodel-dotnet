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

namespace Cvoya.Graph.Model.Serialization.CodeGen.Tests;

/// <summary>
/// Snapshot tests for <c>EntitySerializerGenerator</c> covering a simple node, a relationship,
/// a node with a nested complex property, and a node with a collection of complex properties.
/// </summary>
public class EntitySerializerGeneratorTests
{
    [Fact]
    public Task SimpleNode()
    {
        const string source = """
            using Cvoya.Graph.Model;

            namespace TestNamespace;

            [Node("Person")]
            public record Person : Node
            {
                public string FirstName { get; set; } = string.Empty;
                public string LastName { get; set; } = string.Empty;
                public int Age { get; set; }
            }
            """;

        return Verifier.Verify(GeneratorTestHelpers.RunGenerator(source));
    }

    [Fact]
    public Task Relationship()
    {
        const string source = """
            using Cvoya.Graph.Model;

            namespace TestNamespace;

            [Relationship("KNOWS")]
            public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
            {
                public int Since { get; set; }
            }
            """;

        return Verifier.Verify(GeneratorTestHelpers.RunGenerator(source));
    }

    [Fact]
    public Task NodeWithNestedComplexProperty()
    {
        const string source = """
            using Cvoya.Graph.Model;

            namespace TestNamespace;

            public record Address
            {
                public string Street { get; set; } = string.Empty;
                public string City { get; set; } = string.Empty;
            }

            [Node("Person")]
            public record Person : Node
            {
                public string FirstName { get; set; } = string.Empty;
                public Address? HomeAddress { get; set; }
            }
            """;

        return Verifier.Verify(GeneratorTestHelpers.RunGenerator(source));
    }

    [Fact]
    public Task NodeWithCollectionOfComplexProperties()
    {
        const string source = """
            using System.Collections.Generic;
            using Cvoya.Graph.Model;

            namespace TestNamespace;

            public record PhoneNumber
            {
                public string CountryCode { get; set; } = string.Empty;
                public string Number { get; set; } = string.Empty;
            }

            [Node("Person")]
            public record Person : Node
            {
                public string FirstName { get; set; } = string.Empty;
                public List<PhoneNumber> PhoneNumbers { get; set; } = new();
            }
            """;

        return Verifier.Verify(GeneratorTestHelpers.RunGenerator(source));
    }
}

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

namespace Cvoya.Graph.Model.Core.Tests;


[Trait("Area", "SchemaRegistry")]
public class SchemaRegistryTests
{
    public static TheoryData<string, string, bool, bool, bool, bool, bool, bool, int?, int?, string?> PropertySchemaCases => new()
    {
        { nameof(RegistryNode.Key), "node_key", true, true, true, true, false, true, 2, 20, "^[a-z]+$" },
        { nameof(RegistryNode.IndexedName), "indexed_name", false, true, false, false, false, false, int.MinValue, int.MaxValue, string.Empty },
        { nameof(RegistryNode.UniqueName), nameof(RegistryNode.UniqueName), false, false, true, false, false, true, int.MinValue, int.MaxValue, string.Empty },
        { nameof(RegistryNode.RequiredName), nameof(RegistryNode.RequiredName), false, false, false, true, false, true, int.MinValue, int.MaxValue, string.Empty },
        { nameof(RegistryNode.IgnoredName), nameof(RegistryNode.IgnoredName), false, false, false, false, true, true, int.MinValue, int.MaxValue, string.Empty },
        { nameof(RegistryNode.SearchDisabled), nameof(RegistryNode.SearchDisabled), false, false, false, false, false, false, int.MinValue, int.MaxValue, string.Empty },
        { nameof(RegistryNode.NonStringSearchRequested), nameof(RegistryNode.NonStringSearchRequested), false, false, false, false, false, false, int.MinValue, int.MaxValue, string.Empty },
        { nameof(RegistryNode.DefaultSearchString), nameof(RegistryNode.DefaultSearchString), false, false, false, false, false, true, null, null, null },
        { nameof(RegistryNode.DefaultNumber), nameof(RegistryNode.DefaultNumber), false, false, false, false, false, false, null, null, null },
    };

    [Fact]
    public async Task InitializeAsync_DiscoversTestAssemblyNodeAndRelationshipTypes()
    {
        using var registry = new SchemaRegistry();
        var cancellationToken = TestContext.Current.CancellationToken;

        await registry.InitializeAsync(cancellationToken);

        var nodeSchema = await registry.GetNodeSchemaAsync("CoreRegistryNode", cancellationToken);
        var relationshipSchema = await registry.GetRelationshipSchemaAsync("CORE_REGISTRY_REL", cancellationToken);

        Assert.True(registry.IsInitialized);
        Assert.NotNull(nodeSchema);
        Assert.Equal(typeof(RegistryNode), nodeSchema.Type);
        Assert.Equal("CoreRegistryNode", nodeSchema.Label);
        Assert.NotNull(relationshipSchema);
        Assert.Equal(typeof(RegistryRelationship), relationshipSchema.Type);
        Assert.Equal("CORE_REGISTRY_REL", relationshipSchema.Label);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SynchronousLookup_ThrowsBeforeInitialization(bool nodeLookup)
    {
        using var registry = new SchemaRegistry();

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = nodeLookup
                ? registry.GetNodeSchema("CoreRegistryNode")
                : registry.GetRelationshipSchema("CORE_REGISTRY_REL");
        });

        Assert.Contains("must be initialized", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnknownLookup_ReturnsNullAfterInitialization(bool nodeLookup)
    {
        using var registry = new SchemaRegistry();
        await registry.InitializeAsync(TestContext.Current.CancellationToken);

        var schema = nodeLookup
            ? registry.GetNodeSchema("MissingNode")
            : registry.GetRelationshipSchema("MISSING_REL");

        Assert.Null(schema);
    }

    [Fact]
    public async Task InitializeAsync_IsSafeToCallRepeatedlyAndConcurrently()
    {
        using var registry = new SchemaRegistry();
        var cancellationToken = TestContext.Current.CancellationToken;

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => registry.InitializeAsync(cancellationToken)));
        await registry.InitializeAsync(cancellationToken);

        Assert.True(registry.IsInitialized);
        Assert.NotNull(registry.GetNodeSchema("CoreRegistryNode"));
        Assert.NotNull(registry.GetRelationshipSchema("CORE_REGISTRY_REL"));
    }

    [Fact]
    public async Task RegisteredLabelsAndTypes_IncludeDiscoveredTestEntities()
    {
        using var registry = new SchemaRegistry();
        var cancellationToken = TestContext.Current.CancellationToken;
        await registry.InitializeAsync(cancellationToken);

        var nodeLabels = await registry.GetRegisteredNodeLabelsAsync(cancellationToken);
        var relationshipTypes = await registry.GetRegisteredRelationshipTypesAsync(cancellationToken);

        Assert.Contains("CoreRegistryNode", nodeLabels);
        Assert.Contains("CORE_REGISTRY_REL", relationshipTypes);
    }

    [Theory]
    [MemberData(nameof(PropertySchemaCases))]
    public async Task PropertySchemas_MapAttributeFlagsAndDefaults(
        string propertyName,
        string expectedName,
        bool expectedIsKey,
        bool expectedIsIndexed,
        bool expectedIsUnique,
        bool expectedIsRequired,
        bool expectedIgnore,
        bool expectedIncludeInFullTextSearch,
        int? expectedMinLength,
        int? expectedMaxLength,
        string? expectedPattern)
    {
        using var registry = new SchemaRegistry();
        await registry.InitializeAsync(TestContext.Current.CancellationToken);

        var schema = registry.GetNodeSchema("CoreRegistryNode");
        Assert.NotNull(schema);

        var property = schema.Properties[propertyName];
        Assert.Equal(expectedName, property.Name);
        Assert.Equal(expectedIsKey, property.IsKey);
        Assert.Equal(expectedIsIndexed, property.IsIndexed);
        Assert.Equal(expectedIsUnique, property.IsUnique);
        Assert.Equal(expectedIsRequired, property.IsRequired);
        Assert.Equal(expectedIgnore, property.Ignore);
        Assert.Equal(expectedIncludeInFullTextSearch, property.IncludeInFullTextSearch);
        Assert.Equal(expectedMinLength, property.Validation.MinLength);
        Assert.Equal(expectedMaxLength, property.Validation.MaxLength);
        Assert.Equal(expectedPattern, property.Validation.Pattern);
    }

    [Fact]
    public async Task EntitySchemaInfo_KeyHelpersReflectDiscoveredKeyProperties()
    {
        using var registry = new SchemaRegistry();
        await registry.InitializeAsync(TestContext.Current.CancellationToken);

        var schema = registry.GetNodeSchema("CoreRegistryNode");
        Assert.NotNull(schema);

        Assert.True(schema.HasKey());
        Assert.False(schema.HasCompositeKey());
        var key = Assert.Single(schema.GetKeyProperties());
        Assert.Equal("node_key", key.Name);
    }

    [Fact]
    public async Task ClearAsync_RemovesSchemasAndResetsInitializationFlag()
    {
        using var registry = new SchemaRegistry();
        var cancellationToken = TestContext.Current.CancellationToken;
        await registry.InitializeAsync(cancellationToken);

        await registry.ClearAsync(cancellationToken);

        Assert.False(registry.IsInitialized);
        Assert.Null(await registry.GetNodeSchemaAsync("CoreRegistryNode", cancellationToken));
        Assert.Null(await registry.GetRelationshipSchemaAsync("CORE_REGISTRY_REL", cancellationToken));
    }

    [Node("CoreRegistryNode")]
    private sealed record RegistryNode : Node
    {
        [Property(Label = "node_key", IsKey = true, MinLength = 2, MaxLength = 20, Pattern = "^[a-z]+$")]
        public string Key { get; init; } = string.Empty;

        [Property(Label = "indexed_name", IsIndexed = true, IncludeInFullTextSearch = false)]
        public string IndexedName { get; init; } = string.Empty;

        [Property(IsUnique = true)]
        public string UniqueName { get; init; } = string.Empty;

        [Property(IsRequired = true)]
        public string RequiredName { get; init; } = string.Empty;

        [Property(Ignore = true)]
        public string IgnoredName { get; init; } = string.Empty;

        [Property(IncludeInFullTextSearch = false)]
        public string SearchDisabled { get; init; } = string.Empty;

        [Property(IncludeInFullTextSearch = true)]
        public int NonStringSearchRequested { get; init; }

        public string DefaultSearchString { get; init; } = string.Empty;

        public int DefaultNumber { get; init; }
    }

    [Relationship("CORE_REGISTRY_REL")]
    private sealed record RegistryRelationship(string Start, string End) : Relationship(Start, End)
    {
        [Property(IsIndexed = true)]
        public DateOnly Since { get; init; }
    }
}

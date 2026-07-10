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

namespace Cvoya.Graph.Core.Tests;

using System.Reflection;

[Trait("Area", "SchemaRegistry")]
[Collection("SchemaRegistry")]
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

    [Fact]
    public async Task InitializeAsync_AgainstCurrentlyLoadedAssemblies_DoesNotThrow()
    {
        // Canary for #155: SchemaRegistry.InitializeAsync scans every assembly in
        // AppDomain.CurrentDomain and throws a single aggregated GraphException the moment any two
        // node types (or two relationship types, or two properties within one type) resolve to the
        // same label - see RuntimeLabelCollisionTests for the mirrored allow/deny matrix. This test
        // deliberately asserts nothing more specific than "it doesn't throw": its only job is to fail
        // loudly, in this project, the moment an in-tree fixture collides with another - e.g. two
        // identically-named nested test types added to different files - rather than that surfacing
        // only in another test project or in CI. It is safe alongside RuntimeLabelCollisionTests'
        // isolated (AssemblyLoadContext-compiled) fixtures precisely because those are never ordinary
        // members of this assembly, so a real scan of this assembly never sees them.
        using var registry = new SchemaRegistry();

        var exception = await Record.ExceptionAsync(
            () => registry.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Null(exception);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RegisteredLookup_DoesNotBlockOnSemaphore(bool nodeLookup)
    {
        // #139: a hit on an already-registered label/type must return without ever awaiting the
        // internal semaphore. Prove it structurally by holding the semaphore for the entire call
        // and asserting the lookup still completes promptly.
        using var registry = new SchemaRegistry();
        var cancellationToken = TestContext.Current.CancellationToken;
        await registry.InitializeAsync(cancellationToken);

        // Warm the fast path for a synchronous miss-then-hit sanity check.
        Assert.NotNull(nodeLookup
            ? await registry.GetNodeSchemaAsync("CoreRegistryNode", cancellationToken)
            : await registry.GetRelationshipSchemaAsync("CORE_REGISTRY_REL", cancellationToken));

        var semaphore = GetSemaphore(registry);
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var lookupTask = nodeLookup
                ? registry.GetNodeSchemaAsync("CoreRegistryNode", cancellationToken)
                : registry.GetRelationshipSchemaAsync("CORE_REGISTRY_REL", cancellationToken);

            var completed = await Task.WhenAny(lookupTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));

            Assert.Same(lookupTask, completed);
            Assert.NotNull(await lookupTask);
        }
        finally
        {
            semaphore.Release();
        }
    }

    [Fact]
    public async Task ParallelReadsAndConcurrentRegistration_AreSafeAndConsistent()
    {
        // #139: many concurrent readers (some hitting the fast path, some missing and
        // triggering a rescan) racing a fresh registration must never throw, deadlock, or
        // observe a torn/partial EntitySchemaInfo.
        using var registry = new SchemaRegistry();
        var cancellationToken = TestContext.Current.CancellationToken;
        await registry.InitializeAsync(cancellationToken);

        var readers = Enumerable.Range(0, 64).Select(async i =>
        {
            for (var iteration = 0; iteration < 50; iteration++)
            {
                var schema = i % 2 == 0
                    ? await registry.GetNodeSchemaAsync("CoreRegistryNode", cancellationToken)
                    : await registry.GetRelationshipSchemaAsync("CORE_REGISTRY_REL", cancellationToken);

                Assert.NotNull(schema);
                Assert.NotNull(schema.Type);
                Assert.False(string.IsNullOrEmpty(schema.Label));
            }
        });

        var concurrentRegistration = Task.Run(async () =>
        {
            for (var iteration = 0; iteration < 10; iteration++)
            {
                await registry.InitializeAsync(cancellationToken);
            }
        }, cancellationToken);

        await Task.WhenAll([.. readers, concurrentRegistration]);

        Assert.True(registry.IsInitialized);
    }

    private static SemaphoreSlim GetSemaphore(SchemaRegistry registry)
    {
        var field = typeof(SchemaRegistry).GetField("_semaphore", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("SchemaRegistry._semaphore field was not found.");

        return (SemaphoreSlim)field.GetValue(registry)!;
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

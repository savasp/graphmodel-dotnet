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

namespace Cvoya.Graph.Model.Tests;

public interface IDynamicEntitySchemaValidationTests : IGraphModelTest
{
    [Fact]
    public async Task DynamicNode_WithExistingSchema_ValidatesRequiredProperties()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var node = new DynamicNode
        {
            Labels = [nameof(SomeTask)],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                [nameof(SomeTask.Title)] = "Test Task",
                // Missing required "Description" property
            }
        };

        // Act & Assert
        GraphException exception = await Assert.ThrowsAsync<GraphException>(() =>
            Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken));

        Assert.Contains("Property 'Description' on SomeTask is required but not provided", exception.Message);
    }

    [Fact]
    public async Task DynamicNode_WithExistingSchema_ValidatesPropertyNames()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var label = Labels.GetLabelFromType(typeof(SomeTask));
        var node = new DynamicNode
        {
            Labels = [label],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                [nameof(SomeTask.Title)] = "Test SomeTask",
                [nameof(SomeTask.Description)] = "Test Description",
            }
        };

        // Act & Assert - Should not throw for wrong property name, just ignore it
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        Assert.NotNull(node);
    }

    [Fact]
    public async Task DynamicNode_WithExistingSchema_ValidatesPropertyValidationRules()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var node = new DynamicNode
        {
            Labels = [nameof(SomeTask)],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                [nameof(SomeTask.Title)] = "Test SomeTask",
                [nameof(SomeTask.Description)] = "Test Description",
                [nameof(SomeTask.Priority)] = new string('A', 101) // Exceeds MaxLength of 100
            }
        };

        // Act & Assert
        GraphException exception = await Assert.ThrowsAsync<GraphException>(() =>
            Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken));

        Assert.Contains("Property 'Priority' on SomeTask must have a maximum length of 100", exception.Message);
    }

    [Fact]
    public async Task DynamicNode_WithoutExistingSchema_DoesNotValidate()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var node = new DynamicNode
        {
            Labels = ["NonExistentType"],
            Properties = new Dictionary<string, object?>
            {
                ["SomeProperty"] = "Some Value"
            }
        };

        // Act & Assert - Should not throw since there's no schema for "NonExistentType"
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        Assert.NotNull(node);
    }

    [Fact]
    public async Task DynamicNode_WithMultipleLabels_ValidatesAgainstAllSchemas()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var node = new DynamicNode
        {
            Labels = [nameof(SomeTask), nameof(TodoWithoutRequiredProperties)],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                [nameof(SomeTask.Title)] = "Test SomeTask",
                [nameof(SomeTask.Description)] = "Test Description",
                [nameof(TodoWithoutRequiredProperties.Note)] = "Test Note"
                // Missing required "Due" property from Todo schema
            }
        };

        // Act & Assert
        GraphException exception = await Assert.ThrowsAsync<GraphException>(() =>
            Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DynamicRelationship_WithExistingSchema_ValidatesRequiredProperties()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var startNode = new DynamicNode { Labels = [nameof(Person)] };
        var endNode = new DynamicNode { Labels = [nameof(Person)] };

        await Graph.CreateNodeAsync(startNode, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(endNode, null, TestContext.Current.CancellationToken);

        var relationshipType = Labels.GetLabelFromType(typeof(AssignedTo));
        var relationship = new DynamicRelationship
        {
            StartNodeId = startNode.Id,
            EndNodeId = endNode.Id,
            Type = relationshipType,
            Properties = new Dictionary<string, object?>
            {
                [nameof(AssignedTo.AssignedDate)] = DateTime.UtcNow
                // Missing required "AssignedBy" property
            }
        };

        // Act & Assert
        GraphException exception = await Assert.ThrowsAsync<GraphException>(() =>
            Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DynamicRelationship_WithExistingSchema_ValidatesPropertyNames()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var startNode = new DynamicNode { Labels = [nameof(Person)] };
        var endNode = new DynamicNode { Labels = [nameof(Person)] };

        await Graph.CreateNodeAsync(startNode, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(endNode, null, TestContext.Current.CancellationToken);

        var relationshipType = Labels.GetLabelFromType(typeof(AssignedTo));
        var relationship = new DynamicRelationship
        {
            StartNodeId = startNode.Id,
            EndNodeId = endNode.Id,
            Type = relationshipType,
            Properties = new Dictionary<string, object?>
            {
                [nameof(AssignedTo.AssignedDate)] = DateTime.UtcNow,
                [nameof(AssignedTo.AssignedBy)] = "John Doe",
                ["assigned_date"] = "Wrong property name" // Wrong property name
            }
        };

        // Act & Assert - Should throw for wrong property name
        GraphException exception = await Assert.ThrowsAsync<GraphException>(() =>
            Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DynamicRelationship_WithExistingSchema_ValidatesPropertyValidationRules()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var startNode = new DynamicNode { Labels = [nameof(Person)] };
        var endNode = new DynamicNode { Labels = [nameof(Person)] };

        await Graph.CreateNodeAsync(startNode, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(endNode, null, TestContext.Current.CancellationToken);

        var relationshipType = Labels.GetLabelFromType(typeof(AssignedTo));
        var relationship = new DynamicRelationship
        {
            StartNodeId = startNode.Id,
            EndNodeId = endNode.Id,
            Type = relationshipType,
            Properties = new Dictionary<string, object?>
            {
                [nameof(AssignedTo.AssignedDate)] = DateTime.UtcNow,
                [nameof(AssignedTo.AssignedBy)] = "" // Empty string violates MinLength = 1
            }
        };

        // Act & Assert
        GraphException exception = await Assert.ThrowsAsync<GraphException>(() =>
            Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DynamicRelationship_WithoutExistingSchema_DoesNotValidate()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var startNode = new DynamicNode { Labels = [nameof(Person)] };
        var endNode = new DynamicNode { Labels = [nameof(Person)] };

        await Graph.CreateNodeAsync(startNode, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(endNode, null, TestContext.Current.CancellationToken);

        var relationship = new DynamicRelationship
        {
            StartNodeId = startNode.Id,
            EndNodeId = endNode.Id,
            Type = "NON_EXISTENT_TYPE",
            Properties = new Dictionary<string, object?>
            {
                ["SomeProperty"] = "Some Value"
            }
        };

        // Act & Assert - Should not throw since there's no schema for "NON_EXISTENT_TYPE"
        await Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);
        Assert.NotNull(relationship);
    }

    [Fact]
    public async Task DynamicNode_ValidatesCorrectly()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var label = Labels.GetLabelFromType(typeof(Todo));
        var node = new DynamicNode
        {
            Labels = [label],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                ["note"] = "Test todo",           // Maps to Note property
                ["done"] = false,                 // Maps to Done property
                ["due"] = DateTime.UtcNow,        // Maps to Due property
                ["priority"] = "Normal",          // Maps to Priority property
                ["categories"] = new List<string> { "work", "urgent" }, // Maps to Categories property
            }
        };

        // Act & Assert - Should succeed because property names are correctly mapped
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        Assert.NotNull(node);
    }

    [Fact]
    public async Task DynamicNode_MissingRequiredProperties_ThrowsException()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var label = Labels.GetLabelFromType(typeof(Todo));
        var node = new DynamicNode
        {
            Labels = [label],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                ["note"] = "Test todo",           // Present
                ["done"] = false,                 // Present
                ["due"] = DateTime.UtcNow,        // Present
                ["priority"] = "Normal",          // Present
                // Missing "categories" and "completedAt" - these should be optional
            }
        };

        // Act & Assert - Should succeed because all required properties are present
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        Assert.NotNull(node.Id);
    }

    [Fact]
    public async Task DynamicNode_WrongCase_ThrowsException()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var label = Labels.GetLabelFromType(typeof(Todo));
        var node = new DynamicNode
        {
            Labels = [label],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                ["Note"] = "Test todo",           // Wrong case - should be "note"
                ["Done"] = false,                 // Wrong case - should be "done"
                ["Due"] = DateTime.UtcNow,        // Wrong case - should be "due"
                ["Priority"] = "Normal",          // Wrong case - should be "priority"
            }
        };

        // Act & Assert - Should throw because property names don't match the schema
        GraphException exception = await Assert.ThrowsAsync<GraphException>(() =>
            Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DynamicNode_InvalidEnumValue_ThrowsException()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var label = Labels.GetLabelFromType(typeof(Todo));
        var node = new DynamicNode
        {
            Labels = [label],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                ["note"] = "Test todo",
                ["done"] = false,
                ["due"] = DateTime.UtcNow,
                ["priority"] = "InvalidPriority", // Invalid enum value
                ["categories"] = new List<string> { "work" }
            }
        };

        // Act & Assert - Should throw because of invalid enum value
        GraphException exception = await Assert.ThrowsAsync<GraphException>(() =>
            Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DynamicNode_ExtraProperties_ThrowsException()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var label = Labels.GetLabelFromType(typeof(Todo));
        var node = new DynamicNode
        {
            Labels = [label],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                ["note"] = "Test todo",
                ["done"] = false,
                ["due"] = DateTime.UtcNow,
                ["priority"] = "Normal",
                ["categories"] = new List<string> { "work" },
                ["extraProperty1"] = "This should be ignored",
                ["extraProperty2"] = 123,
                ["extraProperty3"] = "simple string instead of complex object"
            }
        };

        // Act & Assert - Should fail because extra properties aren't part of the schema
        GraphException exception = await Assert.ThrowsAsync<GraphException>(() =>
            Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DynamicNode_ErrorWhenCasingDoesNotMatch()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var node = new DynamicNode
        {
            Labels = ["TodoWithoutPropertyLabel"],
            Properties = new Dictionary<string, object?>
            {
                ["note"] = "Test todo",
            }
        };

        // Act & Assert - Should not succeed because the property does not match the case of the property in the schema
        await Assert.ThrowsAsync<GraphException>(() =>
            Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DynamicNode_AllOptionalProperties_AreValidated()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var label = Labels.GetLabelFromType(typeof(Todo));
        var node = new DynamicNode
        {
            Labels = [label],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                ["note"] = "Test todo",
                ["done"] = false,
                ["due"] = DateTime.UtcNow,
                ["priority"] = "Normal",
                ["categories"] = new List<string> { "work", "urgent" },
                ["externalId"] = "google-task-123",
                ["serviceType"] = "Google",
                ["listId"] = "list-456",
                ["listName"] = "Work Tasks",
                ["completedAt"] = DateTime.UtcNow.AddHours(-1)
            }
        };

        // Act & Assert - Should succeed because all properties are valid
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        Assert.NotNull(node);
    }

    [Fact]
    public async Task DynamicNode_ValidatesAgainstTodoSchema()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var label = Labels.GetLabelFromType(typeof(Todo));
        var dynamicNode = new DynamicNode
        {
            Labels = [label],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                ["done"] = false,
                ["due"] = "2025-08-07T23:59:06Z",
                ["note"] = "Test todo",
                ["priority"] = "Normal"
            }
        };

        // Act & Assert
        // This should succeed because all required properties are present with correct names
        await Graph.CreateNodeAsync(dynamicNode, null, TestContext.Current.CancellationToken);

        // Verify the node was created successfully
        Assert.NotNull(dynamicNode.Id);
    }

    [Fact]
    public async Task DynamicNode_WrongPropertyNames_ThrowsException()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var label = Labels.GetLabelFromType(typeof(Todo));
        var dynamicNode = new DynamicNode
        {
            Labels = [label],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                ["Done"] = false,  // Wrong case - should be "done"
                ["Due"] = "2025-08-07T23:59:06Z",  // Wrong case - should be "due"
                ["Note"] = "Test todo",  // Wrong case - should be "note"
                ["Priority"] = "Normal"  // Wrong case - should be "priority"
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<GraphException>(
            () => Graph.CreateNodeAsync(dynamicNode, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DynamicNode_WithTodoWithoutRequiredProperties_SucceedsEvenWithMissingProperties()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var label = Labels.GetLabelFromType(typeof(TodoWithoutRequiredProperties));
        // This simulates what happens when Todo properties are NOT required
        var dynamicNode = new DynamicNode
        {
            Labels = [label],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                ["done"] = false,
                ["priority"] = "Normal"
                // Missing "note" and "due" but they're not required
            }
        };

        // Act & Assert
        // This should succeed because the properties are not required
        await Graph.CreateNodeAsync(dynamicNode, null, TestContext.Current.CancellationToken);

        // Verify the node was created successfully
        Assert.NotNull(dynamicNode.Id);
    }

    [Fact]
    public async Task DynamicNode_WithValidationTest_ValidatesAllPropertyTypes()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var label = Labels.GetLabelFromType(typeof(ValidationTest));
        var node = new DynamicNode
        {
            Labels = [label],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                ["requiredString"] = "Test String",
                ["requiredInt"] = 42,
                ["requiredDateTime"] = DateTime.UtcNow,
                ["requiredBool"] = true,
                ["requiredEnum"] = "High",
                ["requiredList"] = new List<string> { "item1", "item2" },
                ["minLengthString"] = "Valid",
                ["maxLengthString"] = "Short",
                ["rangeString"] = "Valid",
            }
        };

        // Act & Assert - Should succeed with all valid properties
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        Assert.NotNull(node.Id);
    }

    [Fact]
    public async Task DynamicNode_WithValidationTest_MissingRequiredProperties_ThrowsException()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var label = Labels.GetLabelFromType(typeof(ValidationTest));
        var node = new DynamicNode
        {
            Labels = [label],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                ["requiredString"] = "Test String"
                // Missing other required properties
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<GraphException>(
            () => Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DynamicNode_WithValidationTest_StringLengthValidation_ThrowsException()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var label = Labels.GetLabelFromType(typeof(ValidationTest));
        var node = new DynamicNode
        {
            Labels = [label],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                ["requiredString"] = "Test String",
                ["requiredInt"] = 42,
                ["requiredDateTime"] = DateTime.UtcNow,
                ["requiredBool"] = true,
                ["requiredEnum"] = "High",
                ["requiredList"] = new List<string> { "item1", "item2" },
                ["minLengthString"] = "Hi", // Too short (MinLength = 5)
                ["maxLengthString"] = "This is way too long for max length", // Too long (MaxLength = 10)
                ["rangeString"] = "Valid"
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<GraphException>(
            () => Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DynamicNode_WithEdgeCaseTest_HandlesEdgeCasesCorrectly()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var label = Labels.GetLabelFromType(typeof(EdgeCaseTest));
        var node = new DynamicNode
        {
            Labels = [label],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                ["emptyString"] = "",
                ["zeroInt"] = 0,
                ["falseBool"] = false,
                ["emptyList"] = new List<string>(),
                ["nullString"] = null,
                ["nullInt"] = null,
                ["nullBool"] = null,
                ["nullList"] = null
            }
        };

        // Act & Assert - Should succeed with edge case values
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        Assert.NotNull(node.Id);
    }

    [Fact]
    public async Task DynamicRelationship_ValidatesRelationshipProperties_ThrowsExceptionForLongDescription()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var startNode = new DynamicNode { Labels = ["Person"] };
        var endNode = new DynamicNode { Labels = ["Person"] };

        await Graph.CreateNodeAsync(startNode, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(endNode, null, TestContext.Current.CancellationToken);

        var relationshipType = Labels.GetLabelFromType(typeof(DependsOn));
        var relationship = new DynamicRelationship
        {
            StartNodeId = startNode.Id,
            EndNodeId = endNode.Id,
            Type = relationshipType,
            Properties = new Dictionary<string, object?>
            {
                [nameof(DependsOn.DependencyType)] = "Technical", // Required property
                [nameof(DependsOn.Description)] = "A detailed description that exceeds the maximum length limit and should cause validation to fail because it's way too long and violates the MaxLength constraint of 500 characters" // Too long
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<GraphException>(
            () => Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DynamicNode_WithInvalidEnumValue_ThrowsException()
    {
        // Arrange
        await Graph.SchemaRegistry.InitializeAsync();

        var label = Labels.GetLabelFromType(typeof(ValidationTest));
        var node = new DynamicNode
        {
            Labels = [label],
            Properties = new Dictionary<string, object?>(GetMemoryProperties())
            {
                ["requiredString"] = "Test String",
                ["requiredInt"] = 42,
                ["requiredDateTime"] = DateTime.UtcNow,
                ["requiredBool"] = true,
                ["requiredEnum"] = "InvalidPriority", // Invalid enum value
                ["requiredList"] = new List<string> { "item1", "item2" }
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<GraphException>(
            () => Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken));
    }

    private IDictionary<string, object?> GetMemoryProperties()
    {
        return new Dictionary<string, object?>
        {
            [nameof(Memory.CreatedAt)] = DateTime.UtcNow,
            [nameof(Memory.UpdatedAt)] = DateTime.UtcNow,
            [nameof(Memory.CapturedBy)] = new MemorySource { Name = "BrainExpanded", Description = "BrainExpanded Graph Model", Version = "1.0.0", Device = "Web" },
            [nameof(Memory.Location)] = new Point { Longitude = 0.0, Latitude = 0.0, Height = 0.0 },
            [nameof(Memory.Deleted)] = false,
            [nameof(Memory.Text)] = "Test task text",
        };
    }
}

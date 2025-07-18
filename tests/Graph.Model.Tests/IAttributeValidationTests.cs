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

using System.Threading.Tasks;
using Xunit.Sdk;

namespace Cvoya.Graph.Model.Tests;

public interface IAttributeValidationTests : IGraphModelTest
{
    [Node("CustomPersonLabel")]
    public class PersonWithCustomLabel : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    [Node("Employee", "Person", "User")]
    public class PersonWithMultipleLabels : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
    }

    public class PersonWithoutLabel : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    [Node("IndexedPerson")]
    public class PersonWithCustomPropertyLabels : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [Property(Label = "first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Property(Label = "last_name")]
        public string LastName { get; set; } = string.Empty;

        [Property(Label = "email")]
        public string Email { get; set; } = string.Empty;

        [Property]
        public int Age { get; set; }
    }

    [Node("PersonWithIgnoredProps")]
    public class PersonWithIgnoredProperties : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [Property]
        public string FirstName { get; set; } = string.Empty;

        [Property(Ignore = true)]
        public string InternalNote { get; set; } = string.Empty;

        [Property(Ignore = true)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string LastName { get; set; } = string.Empty; // No attribute, should still be persisted
    }

    [Relationship("CUSTOM_WORKS_WITH")]
    public class CustomWorksWith : IRelationship
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string StartNodeId { get; init; } = string.Empty;
        public string EndNodeId { get; init; } = string.Empty;
        public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;

        [Property(Label = "start_date")]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        [Property(Label = "project_name")]
        public string ProjectName { get; set; } = string.Empty;
    }

    public class WorksWithoutLabel : IRelationship
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string StartNodeId { get; init; } = string.Empty;
        public string EndNodeId { get; init; } = string.Empty;
        public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;
        public DateTime Since { get; set; } = DateTime.UtcNow;
    }

    [Node("PersonWithKeyProperties")]
    public class PersonWithKeyProperties : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [Property(IsKey = true)]
        public string EmployeeId { get; set; } = string.Empty;

        [Property(IsKey = true)]
        public string DepartmentCode { get; set; } = string.Empty;

        [Property]
        public string FirstName { get; set; } = string.Empty;

        [Property]
        public string LastName { get; set; } = string.Empty;
    }

    [Node("PersonWithIndexedProperties")]
    public class PersonWithIndexedProperties : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [Property(IsIndexed = true)]
        public string Email { get; set; } = string.Empty;

        [Property(IsIndexed = true)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Property(IsIndexed = false)]
        public string Notes { get; set; } = string.Empty;

        [Property] // Default should be false
        public string Address { get; set; } = string.Empty;
    }

    public class PersonWithNoAttributes : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    [Node("PersonWithRequiredProperties")]
    public class PersonWithRequiredProperties : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [Property(IsRequired = true)]
        public string FirstName { get; set; } = string.Empty;

        [Property(IsRequired = true)]
        public string LastName { get; set; } = string.Empty;

        [Property(IsRequired = false)]
        public string? MiddleName { get; set; }

        [Property] // Default should be false
        public string? Nickname { get; set; }
    }

    [Node("PersonWithUniqueProperties")]
    public class PersonWithUniqueProperties : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [Property(IsUnique = true)]
        public string Email { get; set; } = string.Empty;

        [Property(IsUnique = true)]
        public string SocialSecurityNumber { get; set; } = string.Empty;

        [Property(IsUnique = false)]
        public string Department { get; set; } = string.Empty;

        [Property] // Default should be false
        public string FirstName { get; set; } = string.Empty;
    }

    [Node("PersonWithFullTextSearchProperties")]
    public class PersonWithFullTextSearchProperties : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [Property(IncludeInFullTextSearch = true)]
        public string FirstName { get; set; } = string.Empty;

        [Property(IncludeInFullTextSearch = true)]
        public string LastName { get; set; } = string.Empty;

        [Property(IncludeInFullTextSearch = false)]
        public string InternalNotes { get; set; } = string.Empty;

        [Property] // Default should be true for string properties
        public string Email { get; set; } = string.Empty;

        [Property] // Default should be false for non-string properties
        public int Age { get; set; }
    }

    [Node("PersonWithValidationProperties")]
    public class PersonWithValidationProperties : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [Property(MinLength = 2, MaxLength = 50)]
        public string FirstName { get; set; } = string.Empty;

        [Property(MinLength = 2, MaxLength = 50)]
        public string LastName { get; set; } = string.Empty;

        [Property(Pattern = @"^[^@]+@[^@]+\.[^@]+$")]
        public string Email { get; set; } = string.Empty;
    }

    [Node("PersonWithCompositeKeyProperties")]
    public class PersonWithCompositeKeyProperties : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [Property(IsKey = true)]
        public string CompanyId { get; set; } = string.Empty;

        [Property(IsKey = true)]
        public string EmployeeNumber { get; set; } = string.Empty;

        [Property(IsKey = true)]
        public string LocationCode { get; set; } = string.Empty;

        [Property]
        public string FirstName { get; set; } = string.Empty;

        [Property]
        public string LastName { get; set; } = string.Empty;
    }

    [Node("PersonWithMixedProperties")]
    public class PersonWithMixedProperties : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [Property(IsKey = true, IsRequired = true, IsIndexed = true, IsUnique = true)]
        public string EmployeeId { get; set; } = string.Empty;

        [Property(IsRequired = true, IsIndexed = true, IncludeInFullTextSearch = true)]
        public string FirstName { get; set; } = string.Empty;

        [Property(IsRequired = true, IsIndexed = true, IncludeInFullTextSearch = true)]
        public string LastName { get; set; } = string.Empty;

        [Property(IsUnique = true, Pattern = @"^[^@]+@[^@]+\.[^@]+$")]
        public string Email { get; set; } = string.Empty;

        [Property(IsIndexed = true)]
        public int Age { get; set; }

        [Property(Ignore = true)]
        public string InternalNotes { get; set; } = string.Empty;
    }

    [Relationship("RelationshipWithKeyProperties")]
    public class RelationshipWithKeyProperties : IRelationship
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string StartNodeId { get; init; } = string.Empty;
        public string EndNodeId { get; init; } = string.Empty;
        public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;

        [Property(IsKey = true)]
        public string RelationshipId { get; set; } = string.Empty;

        [Property(IsKey = true)]
        public string TypeCode { get; set; } = string.Empty;

        [Property]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [Relationship("RelationshipWithValidationProperties")]
    public class RelationshipWithValidationProperties : IRelationship
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string StartNodeId { get; init; } = string.Empty;
        public string EndNodeId { get; init; } = string.Empty;
        public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;

        public double Strength { get; set; }

        [Property(MinLength = 1, MaxLength = 1000)]
        public string Notes { get; set; } = string.Empty;
    }

    [Fact]
    public void PropertyWithIsKey_ImpliesOtherProperties()
    {
        // Test that IsKey = true implies IsUnique = true, IsRequired = true, and IsIndexed = true
        var schema = Graph.SchemaRegistry.GetNodeSchema("PersonWithKeyProperties");
        Assert.NotNull(schema);

        var employeeIdSchema = schema.Properties["EmployeeId"];
        Assert.True(employeeIdSchema.IsKey);
        Assert.True(employeeIdSchema.IsUnique);
        Assert.True(employeeIdSchema.IsRequired);
        Assert.True(employeeIdSchema.IsIndexed);

        var departmentCodeSchema = schema.Properties["DepartmentCode"];
        Assert.True(departmentCodeSchema.IsKey);
        Assert.True(departmentCodeSchema.IsUnique);
        Assert.True(departmentCodeSchema.IsRequired);
        Assert.True(departmentCodeSchema.IsIndexed);
    }

    [Fact]
    public async Task PropertyWithIsIndexed_CreatesIndex()
    {
        var person = new PersonWithIndexedProperties
        {
            Email = "test@example.com",
            PhoneNumber = "123-456-7890",
            Notes = "Some notes",
            Address = "123 Main St"
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Verify the node was created successfully
        var retrieved = await Graph.GetNodeAsync<PersonWithIndexedProperties>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("test@example.com", retrieved.Email);
        Assert.Equal("123-456-7890", retrieved.PhoneNumber);
    }

    [Fact]
    public async Task PropertyWithIsRequired_ValidatesRequiredFields()
    {
        var person = new PersonWithRequiredProperties
        {
            FirstName = "", // Empty string should fail
            LastName = null! // Null should fail
        };

        // This should throw an exception due to required field validation
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        });

        // Test with valid required fields
        var validPerson = new PersonWithRequiredProperties
        {
            FirstName = "John",
            LastName = "Doe",
            MiddleName = "Robert", // Optional
            Nickname = "Johnny" // Optional
        };

        await Graph.CreateNodeAsync(validPerson, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<PersonWithRequiredProperties>(validPerson.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("John", retrieved.FirstName);
        Assert.Equal("Doe", retrieved.LastName);
        Assert.Equal("Robert", retrieved.MiddleName);
        Assert.Equal("Johnny", retrieved.Nickname);
    }

    [Fact]
    public async Task PropertyWithIsUnique_EnforcesUniqueness()
    {
        var person1 = new PersonWithUniqueProperties
        {
            Email = "unique@example.com",
            SocialSecurityNumber = "123-45-6789",
            Department = "Engineering",
            FirstName = "John"
        };

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);

        // Try to create another person with the same unique email
        var person2 = new PersonWithUniqueProperties
        {
            Email = "unique@example.com", // Same email
            SocialSecurityNumber = "987-65-4321", // Different SSN
            Department = "Engineering", // Same department (not unique)
            FirstName = "Jane"
        };

        // This should throw an exception due to unique constraint violation
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        });

        // Try to create another person with the same unique SSN
        var person3 = new PersonWithUniqueProperties
        {
            Email = "different@example.com", // Different email
            SocialSecurityNumber = "123-45-6789", // Same SSN
            Department = "Engineering", // Same department (not unique)
            FirstName = "Bob"
        };

        // This should also throw an exception
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(person3, null, TestContext.Current.CancellationToken);
        });

        // Creating with same department (not unique) should work
        var person4 = new PersonWithUniqueProperties
        {
            Email = "another@example.com",
            SocialSecurityNumber = "111-22-3333",
            Department = "Engineering", // Same department (not unique)
            FirstName = "Alice"
        };

        await Graph.CreateNodeAsync(person4, null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PropertyWithIncludeInFullTextSearch_ConfiguresSearchIndex()
    {
        var person = new PersonWithFullTextSearchProperties
        {
            FirstName = "John",
            LastName = "Doe",
            InternalNotes = "Confidential information",
            Email = "john.doe@example.com",
            Age = 30
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Verify the node was created successfully and all properties are set
        var retrieved = await Graph.GetNodeAsync<PersonWithFullTextSearchProperties>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(person.FirstName, retrieved.FirstName);
        Assert.Equal(person.LastName, retrieved.LastName);
        Assert.Equal(person.Email, retrieved.Email);
        Assert.Equal(person.InternalNotes, retrieved.InternalNotes);
        Assert.Equal(30, retrieved.Age);

        // Now, if we search for "John", we should find this person
        var searchResults = await Graph.SearchNodes<PersonWithFullTextSearchProperties>("John").ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(searchResults, p => p.Id == person.Id);

        // Searching for "Confidential" should not return this person since InternalNotes is excluded from indexing
        searchResults = await Graph.SearchNodes<PersonWithFullTextSearchProperties>("Confidential").ToListAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(searchResults, p => p.Id == person.Id);
    }

    [Fact]
    public async Task PropertyWithValidation_ValidatesConstraints()
    {
        // Test validation failure
        var invalidPerson = new PersonWithValidationProperties
        {
            FirstName = "A", // Too short (min length 2)
            LastName = "B", // Too short (min length 2)
            Email = "invalid-email", // Invalid email pattern
        };

        // This should throw validation exceptions
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(invalidPerson, null, TestContext.Current.CancellationToken);
        });

        // Test with valid data
        var validPerson = new PersonWithValidationProperties
        {
            FirstName = "John", // Valid length
            LastName = "Doe", // Valid length
            Email = "john.doe@example.com", // Valid email pattern
        };

        await Graph.CreateNodeAsync(validPerson, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<PersonWithValidationProperties>(validPerson.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("John", retrieved.FirstName);
        Assert.Equal("Doe", retrieved.LastName);
        Assert.Equal("john.doe@example.com", retrieved.Email);
    }

    [Fact]
    public async Task PropertyWithCompositeKey_CreatesMultipleKeyConstraints()
    {
        var person1 = new PersonWithCompositeKeyProperties
        {
            CompanyId = "COMP001",
            EmployeeNumber = "EMP001",
            LocationCode = "LOC001",
            FirstName = "John",
            LastName = "Doe"
        };

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);

        // Try to create another person with the same composite key
        var person2 = new PersonWithCompositeKeyProperties
        {
            CompanyId = "COMP001", // Same company
            EmployeeNumber = "EMP001", // Same employee number
            LocationCode = "LOC001", // Same location
            FirstName = "Jane",
            LastName = "Smith"
        };

        // This should throw an exception due to composite key constraint violation
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        });

        // Creating with different composite key should work
        var person3 = new PersonWithCompositeKeyProperties
        {
            CompanyId = "COMP001", // Same company
            EmployeeNumber = "EMP002", // Different employee number
            LocationCode = "LOC001", // Same location
            FirstName = "Jane",
            LastName = "Smith"
        };

        await Graph.CreateNodeAsync(person3, null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PropertyWithMixedAttributes_CombinesAllBehaviors()
    {
        var person = new PersonWithMixedProperties
        {
            EmployeeId = "EMP001",
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Age = 30,
            InternalNotes = "This should be ignored"
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<PersonWithMixedProperties>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("EMP001", retrieved.EmployeeId);
        Assert.Equal("John", retrieved.FirstName);
        Assert.Equal("Doe", retrieved.LastName);
        Assert.Equal("john.doe@example.com", retrieved.Email);
        Assert.Equal(30, retrieved.Age);

        // InternalNotes should have default value since it's ignored
        Assert.Equal(string.Empty, retrieved.InternalNotes);

        // Try to create another person with the same unique EmployeeId
        var person2 = new PersonWithMixedProperties
        {
            EmployeeId = "EMP001", // Same unique key
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com",
            Age = 25
        };

        // This should throw an exception due to unique constraint violation
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task RelationshipWithKeyProperties_WorksCorrectly()
    {
        var person1 = new PersonWithCustomLabel { FirstName = "Alice", LastName = "Wonder" };
        var person2 = new PersonWithCustomLabel { FirstName = "Bob", LastName = "Builder" };

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        var relationship = new RelationshipWithKeyProperties
        {
            StartNodeId = person1.Id,
            EndNodeId = person2.Id,
            RelationshipId = "REL001",
            TypeCode = "FRIEND",
            CreatedAt = DateTime.UtcNow
        };

        await Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetRelationshipAsync<RelationshipWithKeyProperties>(relationship.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("REL001", retrieved.RelationshipId);
        Assert.Equal("FRIEND", retrieved.TypeCode);

        // Try to create another relationship with the same composite key
        var relationship2 = new RelationshipWithKeyProperties
        {
            StartNodeId = person1.Id,
            EndNodeId = person2.Id,
            RelationshipId = "REL001", // Same relationship ID
            TypeCode = "FRIEND", // Same type code
            CreatedAt = DateTime.UtcNow
        };

        // This should throw an exception due to composite key constraint violation
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateRelationshipAsync(relationship2, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task RelationshipWithValidationProperties_ValidatesConstraints()
    {
        var person1 = new PersonWithCustomLabel { FirstName = "Alice", LastName = "Wonder" };
        var person2 = new PersonWithCustomLabel { FirstName = "Bob", LastName = "Builder" };

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        // Test validation failure
        var invalidRelationship = new RelationshipWithValidationProperties
        {
            StartNodeId = person1.Id,
            EndNodeId = person2.Id,
            Strength = 15.0, // Too high (max 10.0)
            Notes = "" // Too short (min length 1)
        };

        // This should throw validation exceptions
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateRelationshipAsync(invalidRelationship, null, TestContext.Current.CancellationToken);
        });

        // Test with valid data
        var validRelationship = new RelationshipWithValidationProperties
        {
            StartNodeId = person1.Id,
            EndNodeId = person2.Id,
            Strength = 7.5, // Valid strength
            Notes = "Good working relationship" // Valid notes
        };

        await Graph.CreateRelationshipAsync(validRelationship, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetRelationshipAsync<RelationshipWithValidationProperties>(validRelationship.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(7.5, retrieved.Strength);
        Assert.Equal("Good working relationship", retrieved.Notes);
    }

    [Fact]
    public void SchemaRegistry_ReflectsAllPropertyAttributes()
    {
        // Test that the schema registry correctly reflects all property attributes
        var nodeSchema = Graph.SchemaRegistry.GetNodeSchema("PersonWithMixedProperties");
        Assert.NotNull(nodeSchema);

        // Check EmployeeId properties (IsKey = true implies all others)
        var employeeIdSchema = nodeSchema.Properties["EmployeeId"];
        Assert.True(employeeIdSchema.IsKey);
        Assert.True(employeeIdSchema.IsUnique);
        Assert.True(employeeIdSchema.IsRequired);
        Assert.True(employeeIdSchema.IsIndexed);

        // Check FirstName properties
        var firstNameSchema = nodeSchema.Properties["FirstName"];
        Assert.False(firstNameSchema.IsKey);
        Assert.False(firstNameSchema.IsUnique);
        Assert.True(firstNameSchema.IsRequired);
        Assert.True(firstNameSchema.IsIndexed);
        Assert.True(firstNameSchema.IncludeInFullTextSearch);

        // Check Email properties
        var emailSchema = nodeSchema.Properties["Email"];
        Assert.False(emailSchema.IsKey);
        Assert.True(emailSchema.IsUnique);
        Assert.False(emailSchema.IsRequired);
        Assert.False(emailSchema.IsIndexed);
        Assert.True(emailSchema.IncludeInFullTextSearch);

        // Check Age properties
        var ageSchema = nodeSchema.Properties["Age"];
        Assert.False(ageSchema.IsKey);
        Assert.False(ageSchema.IsUnique);
        Assert.False(ageSchema.IsRequired);
        Assert.True(ageSchema.IsIndexed);
        Assert.False(ageSchema.IncludeInFullTextSearch); // Non-string property

        // Check InternalNotes properties
        var notesSchema = nodeSchema.Properties["InternalNotes"];
        Assert.True(notesSchema.Ignore);
    }

    [Fact]
    public async Task PropertyAttributeDefaults_AreCorrect()
    {
        // We need to initialize the schema registry since it is lazily initialized upon
        // first use of the graph instance.
        // TODO: Change this behavior by adding an explicit required for an initialization method
        // in the IGraph interface.
        await Graph.SchemaRegistry.InitializeAsync();

        // Since IEntity.Id is marked as IsKey, all INode and IRelationship instances will have
        // at least one Property attribute.
        var nodeSchema = Graph.SchemaRegistry.GetNodeSchema("PersonWithNoAttributes");
        Assert.NotNull(nodeSchema);

        // Check default values for properties without explicit attributes
        var addressSchema = nodeSchema.Properties["Address"];
        Assert.False(addressSchema.IsKey);
        Assert.False(addressSchema.IsUnique);
        Assert.False(addressSchema.IsRequired);
        Assert.False(addressSchema.IsIndexed);
        Assert.True(addressSchema.IncludeInFullTextSearch); // String property default
        Assert.False(addressSchema.Ignore);
        Assert.NotNull(addressSchema.Validation);

        Assert.Null(addressSchema.Validation.MinLength);
        Assert.Null(addressSchema.Validation.MaxLength);
        Assert.Null(addressSchema.Validation.Pattern);

        // Check explicit false values
        var notesSchema = nodeSchema.Properties["Notes"];
        Assert.False(notesSchema.IsIndexed); // Explicitly set to false
    }

    [Fact]
    public void FullTextSearchDefaults_AreCorrect()
    {
        var nodeSchema = Graph.SchemaRegistry.GetNodeSchema("PersonWithFullTextSearchProperties");
        Assert.NotNull(nodeSchema);

        // String properties should default to IncludeInFullTextSearch = true
        var emailSchema = nodeSchema.Properties["Email"];
        Assert.True(emailSchema.IncludeInFullTextSearch);

        // Non-string properties should default to IncludeInFullTextSearch = false
        var ageSchema = nodeSchema.Properties["Age"];
        Assert.False(ageSchema.IncludeInFullTextSearch);
    }

    [Fact]
    public async Task NodeWithCustomLabel_CreatedSuccessfully()
    {
        var person = new PersonWithCustomLabel
        {
            FirstName = "John",
            LastName = "Doe"
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<PersonWithCustomLabel>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("John", retrieved.FirstName);
        Assert.Equal("Doe", retrieved.LastName);
    }

    [Fact]
    public async Task NodeWithMultipleLabels_CreatedSuccessfully()
    {
        var person = new PersonWithMultipleLabels
        {
            FirstName = "Jane",
            LastName = "Smith",
            Department = "Engineering"
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<PersonWithMultipleLabels>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("Jane", retrieved.FirstName);
        Assert.Equal("Smith", retrieved.LastName);
        Assert.Equal("Engineering", retrieved.Department);
    }

    [Fact]
    public async Task NodeWithoutLabel_CreatedSuccessfully()
    {
        var person = new PersonWithoutLabel
        {
            FirstName = "Bob",
            LastName = "Johnson"
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<PersonWithoutLabel>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("Bob", retrieved.FirstName);
        Assert.Equal("Johnson", retrieved.LastName);
    }

    [Fact]
    public async Task NodeWithCustomPropertyLabels_CreatedAndQueryable()
    {
        var people = new[]
        {
            new PersonWithCustomPropertyLabels
            {
                FirstName = "Alice",
                LastName = "Brown",
                Email = "alice@example.com",
                Age = 30
            },
            new PersonWithCustomPropertyLabels
            {
                FirstName = "Bob",
                LastName = "Brown",
                Email = "bob@example.com",
                Age = 25
            }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        // Query by property with custom label
        var brownPeople = await Graph.Nodes<PersonWithCustomPropertyLabels>()
            .Where(p => p.LastName == "Brown")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, brownPeople.Count);
        Assert.Contains(brownPeople, p => p.FirstName == "Alice");
        Assert.Contains(brownPeople, p => p.FirstName == "Bob");

        // Query by email property
        var alice = await Graph.Nodes<PersonWithCustomPropertyLabels>()
            .Where(p => p.Email == "alice@example.com")
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(alice);
        Assert.Equal("Alice", alice.FirstName);
    }

    [Fact]
    public async Task NodeWithIgnoredProperties_IgnoredPropertiesNotPersisted()
    {
        var person = new PersonWithIgnoredProperties
        {
            FirstName = "Charlie",
            LastName = "Davis",
            InternalNote = "This should not be saved",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<PersonWithIgnoredProperties>(person.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal("Charlie", retrieved.FirstName);
        Assert.Equal("Davis", retrieved.LastName);

        // Ignored properties should have default values
        Assert.Equal(string.Empty, retrieved.InternalNote);
        Assert.NotEqual(person.CreatedAt, retrieved.CreatedAt);
    }

    [Fact]
    public async Task RelationshipWithCustomLabel_CreatedSuccessfully()
    {
        var person1 = new PersonWithCustomLabel { FirstName = "Alice", LastName = "Wonder" };
        var person2 = new PersonWithCustomLabel { FirstName = "Bob", LastName = "Builder" };

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        var relationship = new CustomWorksWith
        {
            StartNodeId = person1.Id,
            EndNodeId = person2.Id,
            StartDate = DateTime.UtcNow.AddMonths(-6),
            ProjectName = "Project Alpha"
        };

        await Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetRelationshipAsync<CustomWorksWith>(relationship.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(person1.Id, retrieved.StartNodeId);
        Assert.Equal(person2.Id, retrieved.EndNodeId);
        Assert.Equal("Project Alpha", retrieved.ProjectName);
    }

    [Fact]
    public async Task RelationshipWithoutLabel_CreatedSuccessfully()
    {
        var person1 = new PersonWithoutLabel { FirstName = "Eve", LastName = "Adams" };
        var person2 = new PersonWithoutLabel { FirstName = "Frank", LastName = "White" };

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        var relationship = new WorksWithoutLabel
        {
            StartNodeId = person1.Id,
            EndNodeId = person2.Id,
            Since = DateTime.UtcNow.AddYears(-1)
        };

        await Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetRelationshipAsync<WorksWithoutLabel>(relationship.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(person1.Id, retrieved.StartNodeId);
        Assert.Equal(person2.Id, retrieved.EndNodeId);
    }

    [Fact]
    public async Task PropertyWithCustomName_MappedCorrectly()
    {
        var person = new PersonWithCustomPropertyLabels
        {
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            Age = 30
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<PersonWithCustomPropertyLabels>(person.Id, null, TestContext.Current.CancellationToken);

        // Properties with custom names should still be retrievable
        Assert.Equal("Test", retrieved.FirstName);
        Assert.Equal("User", retrieved.LastName);
        Assert.Equal("test@example.com", retrieved.Email);
        Assert.Equal(30, retrieved.Age);
    }

    [Fact]
    public async Task QueryByCustomLabelProperty_WorksCorrectly()
    {
        var people = Enumerable.Range(1, 100)
            .Select(i => new PersonWithCustomPropertyLabels
            {
                FirstName = $"Person{i}",
                LastName = i % 10 == 0 ? "Smith" : $"LastName{i}",
                Email = $"person{i}@example.com",
                Age = 20 + (i % 40)
            })
            .ToArray();

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        // Query by property with custom label
        var smiths = await Graph.Nodes<PersonWithCustomPropertyLabels>()
            .Where(p => p.LastName == "Smith")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(10, smiths.Count);

        // Query by property with custom label
        var person50 = await Graph.Nodes<PersonWithCustomPropertyLabels>()
            .Where(p => p.FirstName == "Person50")
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(person50);
        Assert.Equal("person50@example.com", person50.Email);
    }

    [Fact]
    public async Task ComplexQueryWithMultipleCustomLabelProperties_WorksCorrectly()
    {
        var people = new[]
        {
            new PersonWithCustomPropertyLabels
            {
                FirstName = "Alice",
                LastName = "Smith",
                Email = "alice.smith@company.com",
                Age = 30
            },
            new PersonWithCustomPropertyLabels
            {
                FirstName = "Bob",
                LastName = "Smith",
                Email = "bob.smith@company.com",
                Age = 25
            },
            new PersonWithCustomPropertyLabels
            {
                FirstName = "Alice",
                LastName = "Johnson",
                Email = "alice.johnson@company.com",
                Age = 35
            }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        // Query using multiple properties with custom labels
        var aliceSmith = await Graph.Nodes<PersonWithCustomPropertyLabels>()
            .Where(p => p.FirstName == "Alice" && p.LastName == "Smith")
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(aliceSmith);
        Assert.Equal("alice.smith@company.com", aliceSmith.Email);
        Assert.Equal(30, aliceSmith.Age);
    }

    [Fact]
    public async Task UpdateNodeWithAttributes_PreservesAttributeBehavior()
    {
        var person = new PersonWithIgnoredProperties
        {
            FirstName = "Original",
            LastName = "Name",
            InternalNote = "Should be ignored",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Update with new values
        person.FirstName = "Updated";
        person.LastName = "Name";
        person.InternalNote = "Still ignored";
        person.CreatedAt = DateTime.UtcNow;

        await Graph.UpdateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<PersonWithIgnoredProperties>(person.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal("Updated", retrieved.FirstName);
        Assert.Equal("Name", retrieved.LastName);

        // Ignored properties should still have default values
        Assert.Equal(string.Empty, retrieved.InternalNote);
    }
}
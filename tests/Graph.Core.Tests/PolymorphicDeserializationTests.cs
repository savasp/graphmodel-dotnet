// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Reflection;
using Cvoya.Graph.Serialization;

/// <summary>
/// Exercises the <see cref="EntityInfo.ActualType"/> dispatch mechanism <see cref="EntityFactory"/> uses to
/// support polymorphic node deserialization: the caller may request a base type, but the factory dispatches
/// to whichever serializer is registered for the entity's <see cref="EntityInfo.ActualType"/> (which a
/// provider discovers from stored labels/metadata — out of scope here) and returns the derived instance.
///
/// Hierarchy: <see cref="Animal"/> &lt;- <see cref="Dog"/> &lt;- <see cref="PoliceDog"/> (3 levels), each
/// level adding its own properties.
/// </summary>
[Trait("Area", "PolymorphicDeserialization")]
public class PolymorphicDeserializationTests
{
    static PolymorphicDeserializationTests()
    {
        EntitySerializerRegistry.Instance.Register<Animal>(new AnimalSerializer());
        EntitySerializerRegistry.Instance.Register<Dog>(new DogSerializer());
        EntitySerializerRegistry.Instance.Register<PoliceDog>(new PoliceDogSerializer());
    }

    public static TheoryData<Animal> DerivedInstances => new()
    {
        new Dog { Name = "Rex", Breed = "Labrador" },
        new PoliceDog { Name = "K9", Breed = "Shepherd", Badge = "K9-42" },
    };

    [Theory]
    [MemberData(nameof(DerivedInstances))]
    public void SerializeAsDerived_DeserializeViaBase_ReturnsDerivedTypeWithAllLevelPropertiesIntact(Animal animal)
    {
        var factory = new EntityFactory();

        // Serialize using the concrete runtime type's own serializer, as a provider would.
        var entityInfo = factory.Serialize(animal);

        // Request deserialization via the *base* type parameter, but EntityFactory.Deserialize
        // dispatches by entity.ActualType, not by the caller's requested T.
        var result = factory.Deserialize<Animal>(entityInfo);

        Assert.Equal(animal.GetType(), entityInfo.ActualType);
        Assert.IsType(animal.GetType(), result);
        Assert.Equal(animal.Name, result.Name);

        switch (animal)
        {
            case PoliceDog policeDog:
                var roundTrippedPoliceDog = Assert.IsType<PoliceDog>(result);
                Assert.Equal(policeDog.Breed, roundTrippedPoliceDog.Breed);
                Assert.Equal(policeDog.Badge, roundTrippedPoliceDog.Badge);
                break;
            case Dog dog:
                var roundTrippedDog = Assert.IsType<Dog>(result);
                Assert.Equal(dog.Breed, roundTrippedDog.Breed);
                break;
        }
    }

    [Fact]
    public void SerializeAsDerived_DeserializeViaMidHierarchyType_ReturnsMostDerivedType()
    {
        var factory = new EntityFactory();
        var policeDog = new PoliceDog { Name = "K9", Breed = "Shepherd", Badge = "K9-42" };

        var entityInfo = factory.Serialize(policeDog);

        // Request via the mid-hierarchy type (Dog), still get the most-derived PoliceDog back.
        var result = factory.Deserialize<Dog>(entityInfo);

        var roundTripped = Assert.IsType<PoliceDog>(result);
        Assert.Equal(policeDog.Name, roundTripped.Name);
        Assert.Equal(policeDog.Breed, roundTripped.Breed);
        Assert.Equal(policeDog.Badge, roundTripped.Badge);
    }

    [Fact]
    public void CollectionOfBaseTypedComplexProperty_PreservesMixedDerivedInstancesOrderAndTypes()
    {
        var factory = new EntityFactory();
        EntitySerializerRegistry.Instance.Register<Kennel>(new KennelSerializer());

        var kennel = new Kennel
        {
            Name = "Central Kennel",
            Animals =
            [
                new Dog { Name = "Rex", Breed = "Labrador" },
                new PoliceDog
                {
                    Name = "K9",
                    Breed = "Shepherd",
                    Badge = "K9-42",
                    Handler = new Handler { Name = "Officer Diaz" },
                },
                new Dog { Name = "Fido", Breed = "Poodle" },
            ],
        };

        var entityInfo = factory.Serialize(kennel);
        var roundTripped = factory.Deserialize<Kennel>(entityInfo);

        Assert.Equal(kennel.Animals.Count, roundTripped.Animals.Count);

        // Order preserved.
        for (var i = 0; i < kennel.Animals.Count; i++)
        {
            Assert.Equal(kennel.Animals[i].GetType(), roundTripped.Animals[i].GetType());
            Assert.Equal(kennel.Animals[i].Name, roundTripped.Animals[i].Name);
        }

        // Types preserved, including the mixed derived instance.
        Assert.IsType<Dog>(roundTripped.Animals[0]);
        var policeDog = Assert.IsType<PoliceDog>(roundTripped.Animals[1]);
        Assert.Equal("K9-42", policeDog.Badge);

        // Nested complex property on the derived element is intact.
        Assert.NotNull(policeDog.Handler);
        Assert.Equal("Officer Diaz", policeDog.Handler!.Name);

        Assert.IsType<Dog>(roundTripped.Animals[2]);
    }

    public record Animal : Node
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;
    }

    public record Dog : Animal
    {
        public string Breed { get; init; } = string.Empty;
    }

    public sealed record PoliceDog : Dog
    {
        public string Badge { get; init; } = string.Empty;
        public Handler? Handler { get; init; }
    }

    public sealed record Handler
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed record Kennel : Node
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public List<Animal> Animals { get; init; } = new();
    }

    private class AnimalSerializer : IEntitySerializer
    {
        public virtual Type EntityType => typeof(Animal);

        public virtual EntityInfo Serialize(object obj)
        {
            var animal = (Animal)obj;
            return new EntityInfo(
                animal.GetType(),
                "Animal",
                animal.Labels.ToList(),
                BuildSimpleProperties(animal),
                new Dictionary<string, Property>());
        }

        protected virtual Dictionary<string, Property> BuildSimpleProperties(Animal animal)
        {
            return new Dictionary<string, Property>
            {
                [nameof(Animal.Id)] = SimpleProperty(typeof(Animal), nameof(Animal.Id), animal.Id, typeof(string)),
                [nameof(Animal.Labels)] = CollectionProperty(typeof(Node), nameof(Node.Labels), animal.Labels, typeof(string)),
                [nameof(Animal.Name)] = SimpleProperty(typeof(Animal), nameof(Animal.Name), animal.Name, typeof(string)),
            };
        }

        public virtual object Deserialize(EntityInfo entity)
        {
            return new Animal
            {
                Id = ReadSimple<string>(entity, nameof(Animal.Id)) ?? string.Empty,
                Labels = ReadSimpleCollection<string>(entity, nameof(Animal.Labels)),
                Name = ReadSimple<string>(entity, nameof(Animal.Name)) ?? string.Empty,
            };
        }

        public virtual EntitySchema GetSchema()
        {
            return new EntitySchema(
                typeof(Animal),
                "Animal",
                false,
                false,
                new Dictionary<string, PropertySchema>
                {
                    [nameof(Animal.Id)] = SimpleSchema(typeof(Animal), nameof(Animal.Id)),
                    [nameof(Animal.Name)] = SimpleSchema(typeof(Animal), nameof(Animal.Name)),
                },
                new Dictionary<string, PropertySchema>());
        }

        protected static Property SimpleProperty(Type declaringType, string name, object? value, Type valueType)
        {
            var serializedValue = value is null ? null : new SimpleValue(value, valueType);
            return new Property(GetProperty(declaringType, name), name, value is null, serializedValue);
        }

        protected static Property CollectionProperty<T>(Type declaringType, string name, IEnumerable<T> values, Type elementType)
        {
            return new Property(
                GetProperty(declaringType, name),
                name,
                false,
                new SimpleCollection(values.Select(v => new SimpleValue(v!, elementType)).ToList(), elementType));
        }

        protected static PropertySchema SimpleSchema(Type declaringType, string name)
        {
            return new PropertySchema(GetProperty(declaringType, name), name, PropertyType.Simple);
        }

        protected static T? ReadSimple<T>(EntityInfo entity, string propertyName)
        {
            if (!entity.SimpleProperties.TryGetValue(propertyName, out var property) ||
                property.Value is not SimpleValue simpleValue ||
                simpleValue.Object is null)
            {
                return default;
            }

            return (T)simpleValue.Object;
        }

        protected static List<T> ReadSimpleCollection<T>(EntityInfo entity, string propertyName)
        {
            if (!entity.SimpleProperties.TryGetValue(propertyName, out var property) ||
                property.Value is not SimpleCollection simpleCollection)
            {
                return new List<T>();
            }

            return simpleCollection.Values.Select(v => (T)v.Object).ToList();
        }

        protected static PropertyInfo GetProperty(Type type, string name)
        {
            return type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Property {type.Name}.{name} was not found.");
        }
    }

    private sealed class DogSerializer : AnimalSerializer
    {
        public override Type EntityType => typeof(Dog);

        protected override Dictionary<string, Property> BuildSimpleProperties(Animal animal)
        {
            var properties = base.BuildSimpleProperties(animal);
            var dog = (Dog)animal;
            properties[nameof(Dog.Breed)] = SimpleProperty(typeof(Dog), nameof(Dog.Breed), dog.Breed, typeof(string));
            return properties;
        }

        public override object Deserialize(EntityInfo entity)
        {
            return new Dog
            {
                Id = ReadSimple<string>(entity, nameof(Animal.Id)) ?? string.Empty,
                Labels = ReadSimpleCollection<string>(entity, nameof(Animal.Labels)),
                Name = ReadSimple<string>(entity, nameof(Animal.Name)) ?? string.Empty,
                Breed = ReadSimple<string>(entity, nameof(Dog.Breed)) ?? string.Empty,
            };
        }

        public override EntitySchema GetSchema()
        {
            var baseSchema = base.GetSchema();
            var simpleProperties = new Dictionary<string, PropertySchema>(baseSchema.SimpleProperties)
            {
                [nameof(Dog.Breed)] = SimpleSchema(typeof(Dog), nameof(Dog.Breed)),
            };

            return new EntitySchema(typeof(Dog), "Dog", false, false, simpleProperties, new Dictionary<string, PropertySchema>());
        }
    }

    private sealed class PoliceDogSerializer : AnimalSerializer
    {
        public override Type EntityType => typeof(PoliceDog);

        protected override Dictionary<string, Property> BuildSimpleProperties(Animal animal)
        {
            var properties = base.BuildSimpleProperties(animal);
            var policeDog = (PoliceDog)animal;
            properties[nameof(Dog.Breed)] = SimpleProperty(typeof(Dog), nameof(Dog.Breed), policeDog.Breed, typeof(string));
            properties[nameof(PoliceDog.Badge)] = SimpleProperty(typeof(PoliceDog), nameof(PoliceDog.Badge), policeDog.Badge, typeof(string));
            return properties;
        }

        public override EntityInfo Serialize(object obj)
        {
            var baseEntity = base.Serialize(obj);
            var policeDog = (PoliceDog)obj;

            var complexProperties = new Dictionary<string, Property>(baseEntity.ComplexProperties);
            if (policeDog.Handler is { } handler)
            {
                var handlerEntity = new EntityInfo(
                    typeof(Handler),
                    "Handler",
                    [],
                    new Dictionary<string, Property>
                    {
                        [nameof(Handler.Name)] = SimpleProperty(typeof(Handler), nameof(Handler.Name), handler.Name, typeof(string)),
                    },
                    new Dictionary<string, Property>());

                complexProperties[nameof(PoliceDog.Handler)] = new Property(
                    GetProperty(typeof(PoliceDog), nameof(PoliceDog.Handler)),
                    nameof(PoliceDog.Handler),
                    false,
                    handlerEntity);
            }

            return baseEntity with { ComplexProperties = complexProperties };
        }

        public override object Deserialize(EntityInfo entity)
        {
            Handler? handler = null;
            if (entity.ComplexProperties.TryGetValue(nameof(PoliceDog.Handler), out var handlerProperty) &&
                handlerProperty.Value is EntityInfo handlerEntity)
            {
                handler = new Handler
                {
                    Name = ReadSimple<string>(handlerEntity, nameof(Handler.Name)) ?? string.Empty,
                };
            }

            return new PoliceDog
            {
                Id = ReadSimple<string>(entity, nameof(Animal.Id)) ?? string.Empty,
                Labels = ReadSimpleCollection<string>(entity, nameof(Animal.Labels)),
                Name = ReadSimple<string>(entity, nameof(Animal.Name)) ?? string.Empty,
                Breed = ReadSimple<string>(entity, nameof(Dog.Breed)) ?? string.Empty,
                Badge = ReadSimple<string>(entity, nameof(PoliceDog.Badge)) ?? string.Empty,
                Handler = handler,
            };
        }

        public override EntitySchema GetSchema()
        {
            var simpleProperties = new Dictionary<string, PropertySchema>
            {
                [nameof(Animal.Id)] = SimpleSchema(typeof(Animal), nameof(Animal.Id)),
                [nameof(Animal.Name)] = SimpleSchema(typeof(Animal), nameof(Animal.Name)),
                [nameof(Dog.Breed)] = SimpleSchema(typeof(Dog), nameof(Dog.Breed)),
                [nameof(PoliceDog.Badge)] = SimpleSchema(typeof(PoliceDog), nameof(PoliceDog.Badge)),
            };

            return new EntitySchema(typeof(PoliceDog), "PoliceDog", false, false, simpleProperties, new Dictionary<string, PropertySchema>());
        }
    }

    private sealed class KennelSerializer : IEntitySerializer
    {
        public Type EntityType => typeof(Kennel);

        public EntityInfo Serialize(object obj)
        {
            var kennel = (Kennel)obj;

            var simpleProperties = new Dictionary<string, Property>
            {
                [nameof(Kennel.Id)] = new Property(GetProperty(typeof(Kennel), nameof(Kennel.Id)), nameof(Kennel.Id), false, new SimpleValue(kennel.Id, typeof(string))),
                [nameof(Kennel.Name)] = new Property(GetProperty(typeof(Kennel), nameof(Kennel.Name)), nameof(Kennel.Name), false, new SimpleValue(kennel.Name, typeof(string))),
            };

            var animalEntities = kennel.Animals
                .Select(animal =>
                {
                    var serializer = EntitySerializerRegistry.Instance.GetSerializer(animal.GetType())
                        ?? throw new InvalidOperationException($"No serializer registered for {animal.GetType().Name}.");
                    return serializer.Serialize(animal);
                })
                .ToList();

            var complexProperties = new Dictionary<string, Property>
            {
                [nameof(Kennel.Animals)] = new Property(
                    GetProperty(typeof(Kennel), nameof(Kennel.Animals)),
                    nameof(Kennel.Animals),
                    false,
                    new EntityCollection(typeof(Animal), animalEntities)),
            };

            return new EntityInfo(typeof(Kennel), "Kennel", [], simpleProperties, complexProperties);
        }

        public object Deserialize(EntityInfo entity)
        {
            var animals = new List<Animal>();
            if (entity.ComplexProperties.TryGetValue(nameof(Kennel.Animals), out var property) &&
                property.Value is EntityCollection collection)
            {
                foreach (var animalEntity in collection.Entities)
                {
                    Assert.NotNull(animalEntity);
                    var serializer = EntitySerializerRegistry.Instance.GetSerializer(animalEntity.ActualType)
                        ?? throw new InvalidOperationException($"No serializer registered for {animalEntity.ActualType.Name}.");
                    animals.Add((Animal)serializer.Deserialize(animalEntity));
                }
            }

            return new Kennel
            {
                Id = entity.SimpleProperties.TryGetValue(nameof(Kennel.Id), out var idProperty) && idProperty.Value is SimpleValue idValue
                    ? idValue.Object?.ToString() ?? string.Empty
                    : string.Empty,
                Name = entity.SimpleProperties.TryGetValue(nameof(Kennel.Name), out var nameProperty) && nameProperty.Value is SimpleValue nameValue
                    ? nameValue.Object?.ToString() ?? string.Empty
                    : string.Empty,
                Animals = animals,
            };
        }

        public EntitySchema GetSchema()
        {
            return new EntitySchema(
                typeof(Kennel),
                "Kennel",
                false,
                false,
                new Dictionary<string, PropertySchema>
                {
                    [nameof(Kennel.Id)] = new(GetProperty(typeof(Kennel), nameof(Kennel.Id)), nameof(Kennel.Id), PropertyType.Simple),
                    [nameof(Kennel.Name)] = new(GetProperty(typeof(Kennel), nameof(Kennel.Name)), nameof(Kennel.Name), PropertyType.Simple),
                },
                new Dictionary<string, PropertySchema>
                {
                    [nameof(Kennel.Animals)] = new(
                        GetProperty(typeof(Kennel), nameof(Kennel.Animals)),
                        nameof(Kennel.Animals),
                        PropertyType.ComplexCollection,
                        ElementType: typeof(Animal)),
                });
        }

        private static PropertyInfo GetProperty(Type type, string name)
        {
            return type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Property {type.Name}.{name} was not found.");
        }
    }
}

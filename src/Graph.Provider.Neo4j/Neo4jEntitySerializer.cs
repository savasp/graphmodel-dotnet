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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cvoya.Graph.Provider.Model;

namespace Cvoya.Graph.Client.Neo4j
{
    /// <summary>
    /// Provides serialization logic for Neo4j nodes and relationships.
    /// </summary>
    public static class Neo4jEntitySerializer
    {
        public static Dictionary<string, object?> SerializeProperties(object entity)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var prop in entity.GetType().GetProperties())
            {
                var value = prop.GetValue(entity);
                if (value == null) continue;
                if (value.GetType().IsValueType || value is string)
                {
                    dict[prop.Name] = value;
                }
                else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(value.GetType()) && value is not string)
                {
                    var list = new List<object?>();
                    foreach (var item in (System.Collections.IEnumerable)value)
                    {
                        if (item == null) list.Add(null);
                        else if (item.GetType().IsValueType || item is string) list.Add(item);
                        else list.Add(SerializeProperties(item));
                    }
                    dict[prop.Name] = list;
                }
                else
                {
                    dict[prop.Name] = SerializeProperties(value);
                }
            }
            return dict;
        }

        private static bool IsRelationshipType(Type type)
        {
            if (!type.IsGenericType) return false;
            var genericDef = type.GetGenericTypeDefinition();
            return genericDef.FullName != null && genericDef.FullName.StartsWith("Cvoya.Graph.Provider.Model.IRelationship");
        }

        public static (Dictionary<string, object?>, List<(object propertyNode, string propertyName)>) SerializeNodeWithComplexProperties(object entity)
        {
            var dict = new Dictionary<string, object?>();
            var complexNodes = new List<(object, string)>();
            foreach (var prop in entity.GetType().GetProperties())
            {
                var value = prop.GetValue(entity);
                if (value == null) continue;
                if (value.GetType().IsValueType || value is string)
                {
                    dict[prop.Name] = value;
                }
                else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(value.GetType()) && value is not string)
                {
                    // For collections, only support primitive collections for now
                    var list = new List<object?>();
                    foreach (var item in (System.Collections.IEnumerable)value)
                    {
                        if (item == null) list.Add(null);
                        else if (item.GetType().IsValueType || item is string) list.Add(item);
                        else throw new NotSupportedException($"Nested collections of complex types are not supported: {prop.Name}");
                    }
                    dict[prop.Name] = list;
                }
                else if (!IsRelationshipType(prop.PropertyType))
                {
                    // Complex type: create a node for the property, but do not require an Identifier property
                    complexNodes.Add((value, prop.Name));
                    // Store a placeholder or null in the parent node's property; the relationship will represent the link
                    dict[prop.Name] = null;
                }
                else
                {
                    dict[prop.Name] = value;
                }
            }
            return (dict, complexNodes);
        }
    }
}

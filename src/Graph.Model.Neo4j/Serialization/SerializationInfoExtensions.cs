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

namespace Cvoya.Graph.Model.Neo4j.Serialization;

internal static class SerializationInfoExtensions
{
    extension(IReadOnlyDictionary<string, IntermediateRepresentation> serializedEntity)
    {
        public IReadOnlyDictionary<string, IntermediateRepresentation> ComplexProperties =>
            serializedEntity.Values
                .Where(info => GraphDataModel.IsComplex(info.PropertyInfo.PropertyType) || GraphDataModel.IsCollectionOfComplex(info.PropertyInfo.PropertyType))
                .ToDictionary(info => info.PropertyInfo.Name);

        public IReadOnlyDictionary<string, IntermediateRepresentation> SimpleProperties =>
            serializedEntity.Values
                .Where(info => GraphDataModel.IsSimple(info.PropertyInfo.PropertyType) || GraphDataModel.IsCollectionOfSimple(info.PropertyInfo.PropertyType))
                .ToDictionary(info => info.PropertyInfo.Name);
    }
}
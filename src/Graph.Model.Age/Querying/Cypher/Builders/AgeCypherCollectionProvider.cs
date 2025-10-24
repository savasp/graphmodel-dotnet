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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Builders;

using Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;

/// <summary>
/// Apache AGE implementation of <see cref="ICypherCollectionProvider"/> that only relies on
/// vanilla openCypher functions supported by AGE.
/// </summary>
internal sealed class AgeCypherCollectionProvider : ICypherCollectionProvider
{
    public string ToSet(string collectionExpression)
        => $"[item IN {collectionExpression} WHERE item IS NOT NULL | DISTINCT item]";

    public string Size(string collectionExpression)
        => $"size({collectionExpression})";

    public string Contains(string collectionExpression, string valueExpression)
        => $"{valueExpression} IN {collectionExpression}";
}

// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

namespace Cvoya.Graph.Neo4j.Querying.Linq.Queryables;

using System.Linq.Expressions;
using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Linq.Providers;


internal sealed class GraphQueryable<T> : GraphQueryableBase<T>, IGraphQueryable<T>, IOrderedGraphQueryable<T>
{
    public GraphQueryable(GraphQueryProvider provider, GraphContext context, GraphTransaction? transaction, Expression expression)
        : base(typeof(T), provider, context, transaction, expression, GraphQueryableKind.General)
    {
    }
}

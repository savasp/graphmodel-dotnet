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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Execution;

/// <summary>
/// Identifies the kind of aggregation or element-access operation
/// detected in a LINQ expression tree.
/// </summary>
public enum AggregationKind
{
    /// <summary>No aggregation operation.</summary>
    None,
    /// <summary>Count / LongCount (including async variants).</summary>
    Count,
    /// <summary>LongCount (including async variants).</summary>
    LongCount,
    /// <summary>Any (including async variants).</summary>
    Any,
    /// <summary>All (including async variants).</summary>
    All,
    /// <summary>Sum (including async variants).</summary>
    Sum,
    /// <summary>Average (including async variants).</summary>
    Average,
    /// <summary>Min (including async variants).</summary>
    Min,
    /// <summary>Max (including async variants).</summary>
    Max,
    /// <summary>First / FirstOrDefault (including async variants).</summary>
    First,
    /// <summary>FirstOrDefault (including async variants).</summary>
    FirstOrDefault,
    /// <summary>Last / LastOrDefault (including async variants).</summary>
    Last,
    /// <summary>LastOrDefault (including async variants).</summary>
    LastOrDefault,
    /// <summary>Single / SingleOrDefault (including async variants).</summary>
    Single,
    /// <summary>SingleOrDefault (including async variants).</summary>
    SingleOrDefault,
    /// <summary>ToDictionary (including async variants).</summary>
    ToDictionary,
}

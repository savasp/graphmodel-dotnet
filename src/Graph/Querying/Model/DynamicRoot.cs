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

namespace Cvoya.Graph.Querying;

/// <summary>
/// Represents a dynamically typed query root.
/// </summary>
public sealed record DynamicRoot : QueryRoot
{
    /// <summary>
    /// Initializes a dynamically typed query root.
    /// </summary>
    /// <param name="elementType">The dynamic element type, when known.</param>
    public DynamicRoot(Type? elementType = null)
    {
        ElementType = elementType;
    }

    /// <summary>Gets the dynamic element type, when known.</summary>
    public Type? ElementType { get; }
}

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

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>Defines supported entity wire-projection shapes.</summary>
public enum EntityProjectionShape
{
    /// <summary>A single node projection.</summary>
    Node,

    /// <summary>A start-node, relationship, and end-node path segment.</summary>
    PathSegment,
}

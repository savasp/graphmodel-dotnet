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

using Cvoya.Graph.Model;

// ==== DOMAIN MODEL ====

[Node("Person")]
public record Person : Node
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
}

[Node("Department")]
public record Department : Node
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

[Node("Company")]
public record Company : Node
{
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public DateTime Founded { get; set; }
}

[Relationship("WORKS_AT")]
public record WorksAt(string sourceId, string targetId) : Relationship(sourceId, targetId)
{
    public DateTime StartDate { get; set; }
    public decimal Salary { get; set; }
}

[Relationship("PART_OF")]
public record PartOf(string sourceId, string targetId) : Relationship(sourceId, targetId)
{
    public DateTime Since { get; set; }
}

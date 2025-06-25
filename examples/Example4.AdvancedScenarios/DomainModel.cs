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

[Node(Label = "Blog")]
public record Blog : Node
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

[Node(Label = "Content")]
public record Content : Node
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
}

[Relationship(Label = "CONTAINED_IN")]
public record ContainedIn(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId, Direction: RelationshipDirection.Bidirectional)
{
    public ContainedIn() : this(string.Empty, string.Empty) { }
}

[Node(Label = "Article")]
public record Article : Content
{
    public DateTime PublishedDate { get; set; }
    public int WordCount { get; set; }
}

[Node(Label = "Video")]
public record Video : Content
{
    public int Duration { get; set; }
    public int Views { get; set; }
}

[Relationship(Label = "CONTAINS")]
public record Contains(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId, Direction: RelationshipDirection.Bidirectional)
{
    public Contains() : this(string.Empty, string.Empty) { }
}

[Relationship(Label = "REFERENCES")]
public record References(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId, Direction: RelationshipDirection.Bidirectional)
{
    public References() : this(string.Empty, string.Empty) { }
    public string Context { get; set; } = string.Empty;
}

[Node(Label = "Tag")]
public record Tag : Node
{
    public string Name { get; set; } = string.Empty;
}

[Relationship(Label = "REFERENCE")]
public record Reference(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId, Direction: RelationshipDirection.Bidirectional)
{
    public Reference() : this(string.Empty, string.Empty) { }
    public string Context { get; set; } = string.Empty;
}

[Relationship(Label = "TAGGED_WITH")]
public record TaggedContent(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId, Direction: RelationshipDirection.Bidirectional)
{
    public TaggedContent() : this(string.Empty, string.Empty) { }
    public string TagName { get; set; } = string.Empty;
}

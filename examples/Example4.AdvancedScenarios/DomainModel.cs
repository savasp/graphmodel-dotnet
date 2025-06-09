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

[Node("Blog")]
public record Blog : Node
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

[Node("Content")]
public record Content : Node
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public List<ContainedIn> ContainedIn { get; set; } = new List<ContainedIn>();
    public List<TaggedWith> Tags { get; set; } = new List<TaggedWith>();
}

[Relationship("CONTAINS")]
public record ContainedIn(string sourceId, string targetId) : Relationship(sourceId, targetId, IsBidirectional: true)
{
}

[Node("Article")]
public record Article : Content
{
    public DateTime PublishedDate { get; set; }
    public int WordCount { get; set; }
    public List<Reference> References { get; set; } = new List<Reference>();
}

[Node("Video")]
public record Video : Content
{
    public int Duration { get; set; }
    public int Views { get; set; }
}

[Relationship("CONTAINS")]
public record Contains(string sourceId, string targetId) : Relationship(sourceId, targetId, IsBidirectional: true)
{
}

[Relationship("REFERENCES")]
public record References(string sourceId, string targetId) : Relationship(sourceId, targetId, IsBidirectional: true)
{
    public string Context { get; set; } = string.Empty;
}

[Node("Tag")]
public record Tag : Node
{
    public string Name { get; set; } = string.Empty;
}

[Relationship("REFERENCE")]
public record Reference(string sourceId, string targetId) : Relationship(sourceId, targetId, IsBidirectional: true)
{
    public string Context { get; set; } = string.Empty;
}

[Relationship("TAGGED_WITH")]
public record TaggedContent(string sourceId, string targetId) : Relationship(sourceId, targetId, IsBidirectional: true)
{
    public string TagName { get; set; } = string.Empty;
}

[Relationship("TAGGED_WITH")]
public record TaggedWith(string sourceId, string targetId) : Relationship(sourceId, targetId, IsBidirectional: true)
{
    public string TagName { get; set; } = string.Empty;
}

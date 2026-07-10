// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;

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
public record ContainedIn(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId)
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
public record Contains(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId)
{
    public Contains() : this(string.Empty, string.Empty) { }
}

[Relationship(Label = "REFERENCES")]
public record References(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId)
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
public record Reference(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId)
{
    public Reference() : this(string.Empty, string.Empty) { }
    public string Context { get; set; } = string.Empty;
}

[Relationship(Label = "TAGGED_WITH")]
public record TaggedWith(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId)
{
    public TaggedWith() : this(string.Empty, string.Empty) { }
    public string TagName { get; set; } = string.Empty;
}

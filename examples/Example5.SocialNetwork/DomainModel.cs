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

[Node(Label = "User")]
public record User : Node
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime JoinedDate { get; set; }
    public string? Bio { get; set; } = string.Empty;
}

[Relationship(Label = "FOLLOWS")]
public record Follows : Relationship
{
    public DateTime Since { get; set; }

    public Follows() : base(string.Empty, string.Empty)
    {
    }

    public Follows(string startNodeId, string endNodeId, DateTime since) : base(startNodeId, endNodeId)
    {
        Since = since;
    }

    public Follows(string startNodeId, string endNodeId) : base(startNodeId, endNodeId)
    {
    }
}

[Relationship(Label = "LIKED_BY")]
public record LikedBy : Relationship
{
    public LikedBy() : base(string.Empty, string.Empty) { }

    public LikedBy(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }
}

[Relationship(Label = "POSTED")]
public record Posted : Relationship
{
    public Posted() : base(string.Empty, string.Empty) { }

    public Posted(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }
}

[Relationship(Label = "LIKES")]
public record Likes : Relationship
{
    public DateTime LikedAt { get; set; }

    public Likes() : base(string.Empty, string.Empty)
    {
    }

    public Likes(string startNodeId, string endNodeId, DateTime likedAt) : base(startNodeId, endNodeId)
    {
        LikedAt = likedAt;
    }

    public Likes(string startNodeId, string endNodeId) : base(startNodeId, endNodeId)
    {
    }
}

[Node(Label = "Comment")]
public record Comment : Node
{
    public string Content { get; set; } = string.Empty;
    public DateTime CommentedAt { get; set; }
}

[Relationship(Label = "REPLY_TO")]
public record ReplyTo : Relationship
{
    public ReplyTo() : base(string.Empty, string.Empty) { }

    public ReplyTo(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }
}

[Relationship(Label = "COMMENTED_ON")]
public record CommentedOn : Relationship
{
    public CommentedOn() : base(string.Empty, string.Empty) { }

    public CommentedOn(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }
}

[Relationship(Label = "WROTE")]
public record Wrote : Relationship
{
    public DateTime WrittenAt { get; set; }

    public Wrote() : base(string.Empty, string.Empty)
    {
    }

    public Wrote(string startNodeId, string endNodeId, DateTime writtenAt) : base(startNodeId, endNodeId)
    {
        WrittenAt = writtenAt;
    }

    public Wrote(string startNodeId, string endNodeId) : base(startNodeId, endNodeId)
    {
    }
}

[Relationship(Label = "AUTHORED_BY")]
public record Author : Relationship
{
    public DateTime PublishedDate { get; set; }
    public int Likes { get; set; }

    // Parameterless constructor required by IRelationship
    public Author() : base(string.Empty, string.Empty)
    {
    }

    // Constructor to initialize all properties
    public Author(string startNodeId, string endNodeId, DateTime publishedDate, int likes)
        : base(startNodeId, endNodeId)
    {
        PublishedDate = publishedDate;
        Likes = likes;
    }

    // Constructor for just start and end node IDs
    public Author(string startNodeId, string endNodeId)
        : base(startNodeId, endNodeId)
    {
    }
}

[Node(Label = "Post")]
public record Post : Node
{
    public DateTime PostedAt { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}
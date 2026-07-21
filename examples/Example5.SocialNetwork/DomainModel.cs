// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;

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
}

[Relationship(Label = "LIKED_BY")]
public record LikedBy : Relationship;

[Relationship(Label = "POSTED")]
public record Posted : Relationship;

[Relationship(Label = "LIKES")]
public record Likes : Relationship
{
    public DateTime LikedAt { get; set; }
}

[Node(Label = "Comment")]
public record Comment : Node
{
    public string Content { get; set; } = string.Empty;
    public DateTime CommentedAt { get; set; }
}

[Relationship(Label = "REPLY_TO")]
public record ReplyTo : Relationship;

[Relationship(Label = "COMMENTED_ON")]
public record CommentedOn : Relationship;

[Relationship(Label = "WROTE")]
public record Wrote : Relationship
{
    public DateTime WrittenAt { get; set; }
}

[Node(Label = "Post")]
public record Post : Node
{
    public DateTime PostedAt { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

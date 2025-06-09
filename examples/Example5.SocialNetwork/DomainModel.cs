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

[Node("User")]
public record User : Node
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime JoinedDate { get; set; }
    public string? Bio { get; set; } = string.Empty;
}

[Relationship("FOLLOWS")]
public record Follows(string sourceId, string targetId) : Relationship(sourceId, targetId)
{
    public DateTime Since { get; set; }
}

[Relationship("LIKED_BY")]
public record LikedBy(string sourceId, string targetId) : Relationship(sourceId, targetId);

[Relationship("POSTED")]
public record Posted(string sourceId, string targetId) : Relationship(sourceId, targetId);

[Relationship("LIKES")]
public record Likes(string sourceId, string targetId) : Relationship(sourceId, targetId)
{
    public DateTime LikedAt { get; set; }
}

[Node("Comment")]
public record Comment : Node
{
    public string Content { get; set; } = string.Empty;
    public DateTime CommentedAt { get; set; }
}

[Relationship("REPLY_TO")]
public record ReplyTo(string sourceId, string targetId) : Relationship(sourceId, targetId);

[Relationship("COMMENTED_ON")]
public record CommentedOn(string sourceId, string targetId) : Relationship(sourceId, targetId);

[Relationship("WROTE")]
public record Wrote(string sourceId, string targetId) : Relationship(sourceId, targetId)
{
    public DateTime WrittenAt { get; set; }
}

[Relationship("AUTHORED_BY")]
public record Author(string sourceId, string targetId) : Relationship(sourceId, targetId)
{
    public DateTime PublishedDate { get; set; }
    public int Likes { get; set; }
}

[Node("Post")]
public record Post : Node
{
    public required Author Author { get; init; }
    public DateTime PostedAt { get; set; }
    public string Content { get; set; } = string.Empty;
}
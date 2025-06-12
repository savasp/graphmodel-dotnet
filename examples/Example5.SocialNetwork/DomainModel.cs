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
public record Follows(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId)
{
    public DateTime Since { get; set; }
}

[Relationship(Label = "LIKED_BY")]
public record LikedBy(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId);

[Relationship(Label = "POSTED")]
public record Posted(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId);

[Relationship(Label = "LIKES")]
public record Likes(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId)
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
public record ReplyTo(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId);

[Relationship(Label = "COMMENTED_ON")]
public record CommentedOn(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId);

[Relationship(Label = "WROTE")]
public record Wrote(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId)
{
    public DateTime WrittenAt { get; set; }
}

[Relationship(Label = "AUTHORED_BY")]
public record Author(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId)
{
    public DateTime PublishedDate { get; set; }
    public int Likes { get; set; }
}

[Node(Label = "Post")]
public record Post : Node
{
    public required Author Author { get; init; }
    public DateTime PostedAt { get; set; }
    public string Content { get; set; } = string.Empty;
}
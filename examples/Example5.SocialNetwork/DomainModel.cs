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
public class User : Node
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime JoinedDate { get; set; }
    public string? Bio { get; set; } = string.Empty;
}

[Relationship("FOLLOWS")]
public class Follows : Relationship
{
    public DateTime Since { get; set; }
}

[Relationship("LIKED_BY")]
public class LikedBy : Relationship
{
}

[Relationship("POSTED")]
public class Posted : Relationship
{
}

[Relationship("LIKES")]
public class Likes : Relationship
{
    public DateTime LikedAt { get; set; }
}

[Node("Comment")]
public class Comment : Node
{
    public string Content { get; set; } = string.Empty;
    public DateTime CommentedAt { get; set; }
}

[Relationship("REPLY_TO")]
public class ReplyTo : Relationship
{
}

[Relationship("COMMENTED_ON")]
public class CommentedOn : Relationship
{
}

[Relationship("WROTE")]
public class Wrote : Relationship
{
    public DateTime WrittenAt { get; set; }
}

[Relationship("AUTHORED_BY")]
public class Author : Relationship
{
    public DateTime PublishedDate { get; set; }
    public int Likes { get; set; }
}

[Node("Post")]
public class Post : Node
{
    public Author Author { get; set; } = new Author();
    public DateTime PostedAt { get; set; }
    public string Content { get; set; } = string.Empty;
}
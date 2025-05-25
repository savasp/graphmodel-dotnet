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
public class Blog : Node
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

[Node("Content")]
public class Content : Node
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public List<ContainedIn> ContainedIn { get; set; } = new List<ContainedIn>();
    public List<TaggedWith> Tags { get; set; } = new List<TaggedWith>();
}

[Relationship("CONTAINS")]
public class ContainedIn : Relationship<Content, Blog>
{
    public ContainedIn() : base(isBidirectional: true)
    {
    }
}

[Node("Article")]
public class Article : Content
{
    public DateTime PublishedDate { get; set; }
    public int WordCount { get; set; }
    public List<Reference> References { get; set; } = new List<Reference>();
}

[Node("Video")]
public class Video : Content
{
    public int Duration { get; set; }
    public int Views { get; set; }
}

[Relationship("CONTAINS")]
public class Contains : Relationship<Blog, Article>
{
    public Contains() : base(isBidirectional: true)
    {
    }
}

[Relationship("REFERENCES")]
public class References : Relationship<Article, Video>
{
    public string Context { get; set; } = string.Empty;
}

[Node("Tag")]
public class Tag : Node
{
    public string Name { get; set; } = string.Empty;
    public Relationship<Tag, Content> TaggedContent { get; set; } = new Relationship<Tag, Content>();
}

[Relationship("REFERENCE")]
public class Reference : Relationship<Article, Video>
{
    public Reference() : base(isBidirectional: true)
    {
    }
    public string Context { get; set; } = string.Empty;
}

[Relationship("TAGGED_WITH")]
public class TaggedContent : Relationship<Content, Tag>
{
    public TaggedContent() : base(isBidirectional: true)
    {
    }
}

[Relationship("TAGGED_WITH")]
public class TaggedWith : Relationship<Content, Tag>
{
    public TaggedWith() : base(isBidirectional: true)
    {
    }
}

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

// ==== DOMAIN MODEL FOR FULL TEXT SEARCH DEMO ====

[Node(Label = "Author")]
public record Author : Node
{
    public string Name { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    
    [Property(IncludeInFullTextSearch = false)]
    public string PersonalNotes { get; set; } = string.Empty; // Excluded from search
}

[Node(Label = "Book")]
public record Book : Node
{
    public string Title { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int PublicationYear { get; set; }
    
    [Property(IncludeInFullTextSearch = false)]
    public decimal Price { get; set; } // Numeric property - not searched anyway
}

[Node(Label = "Publisher")]
public record Publisher : Node
{
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

[Relationship(Label = "WROTE")]
public record Wrote : Relationship
{
    public Wrote() : base(string.Empty, string.Empty) { }
    public Wrote(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }
    
    public DateTime WrittenDate { get; set; }
    public string WritingStyle { get; set; } = string.Empty;
}

[Relationship(Label = "PUBLISHED")]
public record Published : Relationship
{
    public Published() : base(string.Empty, string.Empty) { }
    public Published(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }
    
    public DateTime PublishedDate { get; set; }
    public string Edition { get; set; } = string.Empty;
    public string MarketingCampaign { get; set; } = string.Empty;
}

[Relationship(Label = "COLLABORATED")]
public record Collaborated : Relationship
{
    public Collaborated() : base(string.Empty, string.Empty) { }
    public Collaborated(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }
    
    public string ProjectType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;

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
    public DateTime WrittenDate { get; set; }
    public string WritingStyle { get; set; } = string.Empty;
}

[Relationship(Label = "PUBLISHED")]
public record Published : Relationship
{
    public DateTime PublishedDate { get; set; }
    public string Edition { get; set; } = string.Empty;
    public string MarketingCampaign { get; set; } = string.Empty;
}

[Relationship(Label = "COLLABORATED")]
public record Collaborated : Relationship
{
    public string ProjectType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

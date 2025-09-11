namespace SimpleMovieExample;

using Cvoya.Graph.Model;

record Movie : Node
{
    [Property(IsIndexed = true)]
    public string Title { get; set; } = string.Empty;

    public int ReleaseYear { get; set; }
}

record Person : Node
{
    [Property(IsIndexed = true)]
    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }
}

record CreditCard : Node
{
    public string Number { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;
}

record Paid : Relationship
{
    public Paid() : base(string.Empty, string.Empty) { }

    public Paid(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }

    public decimal Amount { get; set; }
    public DateTime Date { get; set; }

    [Property(IsIndexed = true)]
    public string MovieName { get; set; } = string.Empty;
}

record Watched : Relationship
{
    public Watched() : base(string.Empty, string.Empty) { }

    public Watched(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }

    public DateTime Date { get; set; }
}
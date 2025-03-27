// Models/Message.cs
namespace LuceneApi.Models;

public class Message
{
    public required string Id { get; init; }
    public required string Sender { get; init; }
    public required List<string> Recipients { get; init; } 
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public DateTime Timestamp { get; init; }
}

// Helper classes for search results (can be in the same file or separate)
public class SearchResult
{
    public required List<MessageHit> Hits { get; init; }
    public long TotalHits { get; init; } // Use long for potentially large hit counts
    public string? ErrorMessage { get; init; }
    public string? LuceneQuery { get; set; } // Add parsed query for debugging
}

public class MessageHit
{
    public float Score { get; init; }
    public required Message Message { get; init; }
}
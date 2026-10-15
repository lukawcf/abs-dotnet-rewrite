namespace Abs.Data.Entities;

/// <summary>Table "feeds" (RSS feeds). Only the columns Node puts into rssFeed in item lists are mapped.</summary>
public class Feed
{
    public required string Id { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? FeedUrl { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public bool? PreventIndexing { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

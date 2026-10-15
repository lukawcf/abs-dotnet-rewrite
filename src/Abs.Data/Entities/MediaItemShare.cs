namespace Abs.Data.Entities;

/// <summary>Table "mediaItemShares". Only existence per media item is needed (Node keeps the live state in memory).</summary>
public class MediaItemShare
{
    public required string Id { get; set; }
    public string? MediaItemId { get; set; }
    public string? MediaItemType { get; set; }
}

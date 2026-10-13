namespace Abs.Data.Entities;

public class PlaybackProgress
{
    public string? Id { get; set; }
    public string? UserId { get; set; }
    public string? MediaItemId { get; set; }
    public string? MediaItemType { get; set; }
    public double? Duration { get; set; }
    public double? CurrentTime { get; set; }
    public bool? IsFinished { get; set; }
    public bool? HideFromContinueListening { get; set; }
    public string? EbookLocation { get; set; }
    public double? EbookProgress { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? ExtraData { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

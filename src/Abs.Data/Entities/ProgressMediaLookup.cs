namespace Abs.Data.Entities;

// D-owned projections avoid extending the Items module's entities.
public class ProgressItemLookup
{
    public string? Id { get; set; }
    public string? MediaId { get; set; }
    public string? MediaType { get; set; }
}
public class ProgressBookLookup { public string? Id { get; set; } }
public class ProgressEpisodeLookup
{
    public string? Id { get; set; }
    public string? PodcastId { get; set; }
}

namespace Abs.Data.Entities;

/// <summary>Table "libraries". JSON columns stay as raw text.</summary>
public class Library
{
    public required string Id { get; set; }
    public string? Name { get; set; }
    public int? DisplayOrder { get; set; }
    public string? Icon { get; set; }
    public string? MediaType { get; set; }
    public string? Provider { get; set; }
    public DateTimeOffset? LastScan { get; set; }
    public string? LastScanVersion { get; set; }

    /// <summary>JSON text. Node passes it through to clients unchanged.</summary>
    public string? Settings { get; set; }

    /// <summary>JSON text.</summary>
    public string? ExtraData { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<LibraryFolder> Folders { get; set; } = [];
}

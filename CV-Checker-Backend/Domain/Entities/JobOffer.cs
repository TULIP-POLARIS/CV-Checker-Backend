namespace Domain.Entities;

public class JobOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = default!;
    public string? Company { get; set; }
    public string Description { get; set; } = default!;
    public string? Requirements { get; set; }
    public string? Location { get; set; }
    public Guid? SourceFileId { get; set; }
    public string TextContent { get; set; } = default!; // the text used for matching
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    //navigation 
    public User? OwnerUser { get; set; }
    //public FileAsset? SourceFile { get; set; }  // will see if we actually need it
    public List<CVComparison> Comparisons { get; set; } = new();
}


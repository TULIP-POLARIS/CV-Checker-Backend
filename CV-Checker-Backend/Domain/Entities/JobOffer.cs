namespace Domain.Entities;

public class JobOffer
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Company { get; set; }
    public string Description { get; set; } = default!;
    public string? Requirements { get; set; }
    public string? Location { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}


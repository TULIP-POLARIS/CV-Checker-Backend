namespace Domain.Entities;

public class CV
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public Guid? TemplateId { get; set; }
    public string? Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}


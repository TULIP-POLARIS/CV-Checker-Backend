namespace Domain.Entities;

public class Education
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string Degree { get; set; } = "";
    public string FieldOfStudy { get; set; } = "";
    public string Institution { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string? EndDate { get; set; }
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

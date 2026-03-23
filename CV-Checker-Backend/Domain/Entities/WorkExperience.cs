namespace Domain.Entities;

public class WorkExperience
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string JobTitle { get; set; } = "";
    public string Company { get; set; } = "";
    public string? Location { get; set; }
    public string StartDate { get; set; } = "";
    public string? EndDate { get; set; }
    public bool CurrentlyWorking { get; set; }
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

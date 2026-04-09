namespace Domain.Entities;

public class CvGenerated
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string JobTitle { get; set; } = "";
    public string JobDescription { get; set; } = "";
    public string CvUrl { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

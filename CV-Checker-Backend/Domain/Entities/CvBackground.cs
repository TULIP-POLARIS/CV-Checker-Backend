namespace Domain.Entities;

public class CvBackground
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string BackgroundText { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

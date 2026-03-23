namespace Domain.Entities;

public class Skill
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string Name { get; set; } = "";
    public string Level { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

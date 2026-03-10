namespace Domain.Entities;

public class CVComparison
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CVId { get; set; }
    public Guid JobOfferId { get; set; }
    public Guid UserId { get; set; }
    public int? MatchScore { get; set; }
    public string? Strengths { get; set; }
    public string? Weaknesses { get; set; }
    public string? Suggestions { get; set; }
    public string? AnalysisResult { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    //navigation
    public CV? CV { get; set; }
    public JobOffer? JobOffer { get; set; }
    public User? User { get; set; }
}



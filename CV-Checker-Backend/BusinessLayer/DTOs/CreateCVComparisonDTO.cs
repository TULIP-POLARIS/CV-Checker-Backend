namespace BusinessLogic.DTOs
{
    public class CreateCVComparisonDTO
    {
        public Guid CVId { get; set; }
        public Guid JobOfferId { get; set; }
        public Guid UserId { get; set; }
        public decimal? MatchScore { get; set; }
        public string? Strengths { get; set; }
        public string? Weaknesses { get; set; }
        public string? Suggestions { get; set; }
        public string? AnalysisResult { get; set; }
    }
}


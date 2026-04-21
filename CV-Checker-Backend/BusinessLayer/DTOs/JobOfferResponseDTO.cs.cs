namespace BusinessLogic.DTOs
{
    public class JobOfferResponseDTO
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; } = default!;
        public string? Company { get; set; }
        public string Description { get; set; } = default!;
        public string? Requirements { get; set; }
        public string? Location { get; set; }
        public Guid? SourceFileId { get; set; }
        public string TextContent { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}   
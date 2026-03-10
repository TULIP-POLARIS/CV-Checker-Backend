namespace BusinessLogic.DTOs
{
    public class CreateJobOfferDTO
    {
        public string Title { get; set; } = default!;
        public string? Company { get; set; }
        public string Description { get; set; } = default!;
        public string? Requirements { get; set; }
        public string? Location { get; set; }
    }

    public class UpdateJobOfferDTO
    {
        public string Title { get; set; } = default!;
        public string? Company { get; set; }
        public string Description { get; set; } = default!;
        public string? Requirements { get; set; }
        public string? Location { get; set; }
    }
}


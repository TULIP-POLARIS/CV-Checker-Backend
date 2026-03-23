namespace Domain.Entities;
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string? Name { get; set; }

        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }

        public string? PasswordHash { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    //navigation
    public List<CV> CVs { get; set; } = new();
    public List<JobOffer> JobOffers { get; set; } = new();
    public List<CVComparison> Comparisons { get; set; } = new();
}

namespace Domain.Entities;
    public class User
    {
        public Guid Id { get; set; }

        public string? Name { get; set; }

        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }

        public string? PasswordHash { get; set; }

        public DateTime CreatedAt { get; set; }
    }

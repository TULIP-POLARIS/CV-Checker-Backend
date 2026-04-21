namespace Domain.Entities;

// Personal profile data (roughly one row per user)
public class PersonalInfo
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Nationality { get; set; }
    public string? Address { get; set; }
    public string? CountryOfResidence { get; set; }
    public string? PhoneNumber { get; set; }

    public byte[]? ProfilePictureData { get; set; }
    public string? ProfilePictureContentType { get; set; }
    public string? ProfilePictureFileName { get; set; }
    public long? ProfilePictureFileSizeBytes { get; set; }
    public DateTime? ProfilePictureUpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
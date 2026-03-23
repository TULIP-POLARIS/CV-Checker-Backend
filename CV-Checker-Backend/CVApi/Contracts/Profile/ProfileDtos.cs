namespace CVApi.Contracts.Profile;

public class PersonalInfoResponse
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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PersonalInfoPutRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Nationality { get; set; }
    public string? Address { get; set; }
    public string? CountryOfResidence { get; set; }
    public string? PhoneNumber { get; set; }
}

public class EducationResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Degree { get; set; } = "";
    public string FieldOfStudy { get; set; } = "";
    public string Institution { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string? EndDate { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class EducationRequest
{
    public string Degree { get; set; } = "";
    public string FieldOfStudy { get; set; } = "";
    public string Institution { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string? EndDate { get; set; }
    public string? Description { get; set; }
}

public class WorkExperienceResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string JobTitle { get; set; } = "";
    public string Company { get; set; } = "";
    public string? Location { get; set; }
    public string StartDate { get; set; } = "";
    public string? EndDate { get; set; }
    public bool CurrentlyWorking { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WorkExperienceRequest
{
    public string JobTitle { get; set; } = "";
    public string Company { get; set; } = "";
    public string? Location { get; set; }
    public string StartDate { get; set; } = "";
    public string? EndDate { get; set; }
    public bool CurrentlyWorking { get; set; }
    public string? Description { get; set; }
}

public class SkillResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SkillRequest
{
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";
}

public class LanguageResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class LanguageRequest
{
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";
}

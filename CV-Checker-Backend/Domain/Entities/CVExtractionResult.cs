using System.Text.Json.Serialization;

namespace Domain.Entities
{
    public class CVExtractionResult
    {
        public string FullName { get; set; } = "";
        public string JobTitle { get; set; } = "";
        public string Location { get; set; } = "";

        [JsonPropertyName("Summary")]
        public string ProfessionalSummary { get; set; } = "";

        public List<string> Skills { get; set; } = new();
        public List<string> Languages { get; set; } = new();

        public List<WorkExperienceItem> WorkExperience { get; set; } = new();
        public List<EducationItem> Education { get; set; } = new();

        public List<SimpleItem> Certifications { get; set; } = new();
        public List<SimpleItem> Projects { get; set; } = new();
        public List<SimpleItem> Achievements { get; set; } = new();

        public string WorkAuthorization { get; set; } = "";
        public string Availability { get; set; } = "";

        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Linkedin { get; set; } = "";
        public string Github { get; set; } = "";

        public string RawExtractedText { get; set; } = "";
        public string Error { get; set; } = "";
    }

    public class WorkExperienceItem
    {
        public string Role { get; set; } = "";
        public string Company { get; set; } = "";
        public string StartDate { get; set; } = "";
        public string EndDate { get; set; } = "";
        public string Description { get; set; } = "";
        public string Raw { get; set; } = "";
    }

    public class EducationItem
    {
        public string Institution { get; set; } = "";
        public string Degree { get; set; } = "";
        public string StartDate { get; set; } = "";
        public string EndDate { get; set; } = "";
        public string Description { get; set; } = "";
        public string Raw { get; set; } = "";
    }

    public class SimpleItem
    {
        public string Name { get; set; } = "";
    }
}
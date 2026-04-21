using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.DTOs
{
    public class GeneratedCvDTO
    {
        public string FullName { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string? Nationality { get; set; }
        public string? Address { get; set; }

        public List<string> Skills { get; set; } = new();
        public List<string> Languages { get; set; } = new();

        public List<GeneratedEducationDTO> Education { get; set; } = new();
        public List<GeneratedWorkExperienceDTO> WorkExperience { get; set; } = new();
    }

    public class GeneratedEducationDTO
    {
        public string Degree { get; set; } = "";
        public string FieldOfStudy { get; set; } = "";
        public string Institution { get; set; } = "";
        public string StartDate { get; set; } = "";
        public string? EndDate { get; set; }
        public string? Description { get; set; }
    }

    public class GeneratedWorkExperienceDTO
    {
        public string JobTitle { get; set; } = "";
        public string Company { get; set; } = "";
        public string? Location { get; set; }
        public string StartDate { get; set; } = "";
        public string? EndDate { get; set; }
        public bool CurrentlyWorking { get; set; }
        public string? Description { get; set; }
    }
}

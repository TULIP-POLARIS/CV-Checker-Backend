using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class CVExtractionResult //Python returns structured data, and our project needs a typed C# object to return in Ok(...).
    {
            public string FullName { get; set; } = "Not found";
            public string JobTitle { get; set; } = "Not found";
            public string Location { get; set; } = "Not found";
            public string ProfessionalSummary { get; set; } = "Not found";
            public string Skills { get; set; } = "Not found";
            public string WorkExperience { get; set; } = "Not found";
            public string Education { get; set; } = "Not found";
            public string Certifications { get; set; } = "Not found";
            public string Projects { get; set; } = "Not found";
            public string Languages { get; set; } = "Not found";
            public string Achievements { get; set; } = "Not found";
            public string WorkAuthorization { get; set; } = "Not found";
            public string Availability { get; set; } = "Not found";
            public string Email { get; set; } = "Not found";
            public string Phone { get; set; } = "Not found";
            public string Linkedin { get; set; } = "Not found";
            public string Github { get; set; } = "Not found";
            public string Error { get; set; } = string.Empty;
        }
    }


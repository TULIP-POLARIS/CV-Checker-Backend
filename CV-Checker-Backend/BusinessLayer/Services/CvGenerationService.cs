using BusinessLogic.DTOs;
using BusinessLogic.Interface;
using DAL.Api;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BusinessLogic.Services
{
    public class CvGenerationService
    {
        private readonly ApiContext _context;
        private readonly ICVService _cvService;

        public CvGenerationService(
            ApiContext context,
            ICVService cvService)
        {
            _context = context;
            _cvService = cvService;
        }

        public async Task<GeneratedCvDTO> BuildGeneratedCvAsync(Guid userId)
        {
            var personalInfo = await _context.Set<PersonalInfo>()
                .FirstOrDefaultAsync(x => x.UserId == userId);

            var educations = await _context.Set<Education>()
                .Where(x => x.UserId == userId)
                .ToListAsync();

            var skills = await _context.Set<Skill>()
                .Where(x => x.UserId == userId)
                .ToListAsync();

            var languages = await _context.Set<Language>()
                .Where(x => x.UserId == userId)
                .ToListAsync();

            var workExperiences = await _context.Set<WorkExperience>()
                .Where(x => x.UserId == userId)
                .ToListAsync();

            CVExtractionResult? extraction = null;

            var userCvs = await _cvService.GetByUserIdAsync(userId);
            var latestCv = userCvs
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            if (latestCv != null && !string.IsNullOrWhiteSpace(latestCv.Content))
            {
                try
                {
                    extraction = JsonSerializer.Deserialize<CVExtractionResult>(
                        latestCv.Content,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                }
                catch (JsonException)
                {
                    extraction = null;
                }
            }

            var dto = new GeneratedCvDTO
            {
                FullName = BuildFullName(personalInfo, extraction),
                PhoneNumber = GetPhoneNumber(personalInfo, extraction),
                Nationality = GetNationality(personalInfo, extraction),
                Address = GetAddress(personalInfo, extraction),

                Skills = BuildSkills(skills, extraction),
                Languages = BuildLanguages(languages, extraction),

                Education = BuildEducation(educations, extraction),
                WorkExperience = BuildWorkExperience(workExperiences, extraction)
            };

            return dto;
        }

        private static string BuildFullName(PersonalInfo? personalInfo, CVExtractionResult? extraction)
        {
            var parts = new List<string>();

            if (personalInfo != null)
            {
                if (!string.IsNullOrWhiteSpace(personalInfo.FirstName))
                    parts.Add(personalInfo.FirstName);

                if (!string.IsNullOrWhiteSpace(personalInfo.LastName))
                    parts.Add(personalInfo.LastName);
            }

            var profileFullName = string.Join(" ", parts);

            if (!string.IsNullOrWhiteSpace(profileFullName))
                return profileFullName;

            if (extraction != null && HasText(extraction.FullName))
                return extraction.FullName;

            return string.Empty;
        }

        private static string? GetPhoneNumber(PersonalInfo? personalInfo, CVExtractionResult? extraction)
        {
            if (!string.IsNullOrWhiteSpace(personalInfo?.PhoneNumber))
                return personalInfo.PhoneNumber;

            if (extraction != null && HasText(extraction.Phone))
                return extraction.Phone;

            return null;
        }

        private static string? GetNationality(PersonalInfo? personalInfo, CVExtractionResult? extraction)
        {
            if (!string.IsNullOrWhiteSpace(personalInfo?.Nationality))
                return personalInfo.Nationality;

            if (extraction != null && HasText(extraction.WorkAuthorization))
                return extraction.WorkAuthorization;

            return null;
        }

        private static string? GetAddress(PersonalInfo? personalInfo, CVExtractionResult? extraction)
        {
            if (!string.IsNullOrWhiteSpace(personalInfo?.Address))
                return personalInfo.Address;

            if (extraction != null && HasText(extraction.Location))
                return extraction.Location;

            return null;
        }

        private static List<string> BuildSkills(List<Skill> skills, CVExtractionResult? extraction)
        {
            var result = skills
                .Select(x => x.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (result.Count > 0)
                return result;

            if (extraction?.Skills == null || extraction.Skills.Count == 0)
                return result;

            return extraction.Skills
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> BuildLanguages(List<Language> languages, CVExtractionResult? extraction)
        {
            var result = languages
                .Select(x => string.IsNullOrWhiteSpace(x.Level)
                    ? x.Name
                    : $"{x.Name} - {x.Level}")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (result.Count > 0)
                return result;

            if (extraction?.Languages == null || extraction.Languages.Count == 0)
                return result;

            return extraction.Languages
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<GeneratedEducationDTO> BuildEducation(List<Education> educations, CVExtractionResult? extraction)
        {
            var result = educations.Select(x => new GeneratedEducationDTO
            {
                Degree = x.Degree,
                FieldOfStudy = x.FieldOfStudy,
                Institution = x.Institution,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                Description = x.Description
            }).ToList();

            if (result.Count > 0)
                return result;

            if (extraction?.Education == null || extraction.Education.Count == 0)
                return result;

            result.AddRange(
                extraction.Education
                    .Where(e =>
                        HasText(e.Institution) ||
                        HasText(e.Degree) ||
                        HasText(e.Description) ||
                        HasText(e.Raw))
                    .Select(e => new GeneratedEducationDTO
                    {
                        Degree = HasText(e.Degree) ? e.Degree : "Extracted from CV",
                        FieldOfStudy = "",
                        Institution = HasText(e.Institution) ? e.Institution : "",
                        StartDate = HasText(e.StartDate) ? e.StartDate : "",
                        EndDate = HasText(e.EndDate) ? e.EndDate : null,
                        Description = HasText(e.Description) ? e.Description : e.Raw
                    }));

            return result;
        }

        private static List<GeneratedWorkExperienceDTO> BuildWorkExperience(List<WorkExperience> workExperiences, CVExtractionResult? extraction)
        {
            var result = workExperiences.Select(x => new GeneratedWorkExperienceDTO
            {
                JobTitle = x.JobTitle,
                Company = x.Company,
                Location = x.Location,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                CurrentlyWorking = x.CurrentlyWorking,
                Description = x.Description
            }).ToList();

            if (result.Count > 0)
                return result;

            if (extraction?.WorkExperience == null || extraction.WorkExperience.Count == 0)
                return result;

            result.AddRange(
                extraction.WorkExperience
                    .Where(w =>
                        HasText(w.Role) ||
                        HasText(w.Company) ||
                        HasText(w.Description) ||
                        HasText(w.Raw))
                    .Select(w => new GeneratedWorkExperienceDTO
                    {
                        JobTitle = HasText(w.Role)
                            ? w.Role
                            : (HasText(extraction.JobTitle) ? extraction.JobTitle : "Extracted from CV"),
                        Company = HasText(w.Company) ? w.Company : "",
                        Location = HasText(extraction.Location) ? extraction.Location : null,
                        StartDate = HasText(w.StartDate) ? w.StartDate : "",
                        EndDate = HasText(w.EndDate) ? w.EndDate : null,
                        CurrentlyWorking = string.Equals(w.EndDate, "Present", StringComparison.OrdinalIgnoreCase),
                        Description = HasText(w.Description) ? w.Description : w.Raw
                    }));

            return result;
        }

        private static bool HasText(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !value.Trim().Equals("Not found", StringComparison.OrdinalIgnoreCase);
        }
    }
}
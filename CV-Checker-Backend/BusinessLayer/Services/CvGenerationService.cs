using BusinessLogic.DTOs;
using BusinessLogic.Interface;
using BusinessLogic.Services;
using DAL;
using DAL.Api;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Services
{
    public class CvGenerationService
    {
        private readonly ApiContext _context;
        private readonly ICVService _cvService;
        private readonly CVExtractionRunner _cvExtractionRunner;

        public CvGenerationService(
            ApiContext context,
            ICVService cvService,
            CVExtractionRunner cvExtractionRunner)
        {
            _context = context;
            _cvService = cvService;
            _cvExtractionRunner = cvExtractionRunner;
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

            if (latestCv != null && !string.IsNullOrWhiteSpace(latestCv.FilePath))
            {
                extraction = await _cvExtractionRunner.ExtractFromFileAsync(latestCv.FilePath);
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

            if (extraction != null && !IsNotFound(extraction.FullName))
                return extraction.FullName;

            return string.Empty;
        }

        private static string? GetPhoneNumber(PersonalInfo? personalInfo, CVExtractionResult? extraction)
        {
            if (!string.IsNullOrWhiteSpace(personalInfo?.PhoneNumber))
                return personalInfo.PhoneNumber;

            if (extraction != null && !IsNotFound(extraction.Phone))
                return extraction.Phone;

            return null;
        }

        private static string? GetNationality(PersonalInfo? personalInfo, CVExtractionResult? extraction)
        {
            if (!string.IsNullOrWhiteSpace(personalInfo?.Nationality))
                return personalInfo.Nationality;

            if (extraction != null && !IsNotFound(extraction.WorkAuthorization))
                return extraction.WorkAuthorization;

            return null;
        }

        private static string? GetAddress(PersonalInfo? personalInfo, CVExtractionResult? extraction)
        {
            if (!string.IsNullOrWhiteSpace(personalInfo?.Address))
                return personalInfo.Address;

            if (extraction != null && !IsNotFound(extraction.Location))
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

            if (extraction == null || IsNotFound(extraction.Skills))
                return result;

            return SplitValues(extraction.Skills);
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

            if (extraction == null || IsNotFound(extraction.Languages))
                return result;

            return SplitValues(extraction.Languages);
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

            if (extraction == null || IsNotFound(extraction.Education))
                return result;

            result.Add(new GeneratedEducationDTO
            {
                Degree = "Extracted from CV",
                FieldOfStudy = "",
                Institution = "",
                StartDate = "",
                EndDate = null,
                Description = extraction.Education
            });

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

            if (extraction == null || IsNotFound(extraction.WorkExperience))
                return result;

            result.Add(new GeneratedWorkExperienceDTO
            {
                JobTitle = !IsNotFound(extraction.JobTitle) ? extraction.JobTitle : "Extracted from CV",
                Company = "",
                Location = !IsNotFound(extraction.Location) ? extraction.Location : null,
                StartDate = "",
                EndDate = null,
                CurrentlyWorking = false,
                Description = extraction.WorkExperience
            });

            return result;
        }

        private static List<string> SplitValues(string raw)
        {
            return raw
                .Split(new[] { ',', ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsNotFound(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ||
                   value.Trim().Equals("Not found", StringComparison.OrdinalIgnoreCase);
        }
    }
}
using BusinessLogic.DTOs;
using DAL;
using DAL.Api;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Services
{
    public class CVGenerationService
    {
        private readonly ApiContext _context;

        public CVGenerationService(ApiContext context)
        {
            _context = context;
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

            var dto = new GeneratedCvDTO
            {
                FullName = BuildFullName(personalInfo),
                PhoneNumber = personalInfo?.PhoneNumber,
                Nationality = personalInfo?.Nationality,
                Address = personalInfo?.Address,

                Skills = skills
                    .Select(x => x.Name)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                Languages = languages
                    .Select(x => string.IsNullOrWhiteSpace(x.Level)
                        ? x.Name
                        : $"{x.Name} - {x.Level}")
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                Education = educations.Select(x => new GeneratedEducationDTO
                {
                    Degree = x.Degree,
                    FieldOfStudy = x.FieldOfStudy,
                    Institution = x.Institution,
                    StartDate = x.StartDate,
                    EndDate = x.EndDate,
                    Description = x.Description
                }).ToList(),

                WorkExperience = workExperiences.Select(x => new GeneratedWorkExperienceDTO
                {
                    JobTitle = x.JobTitle,
                    Company = x.Company,
                    Location = x.Location,
                    StartDate = x.StartDate,
                    EndDate = x.EndDate,
                    CurrentlyWorking = x.CurrentlyWorking,
                    Description = x.Description
                }).ToList()
            };

            return dto;
        }

        private static string BuildFullName(PersonalInfo? personalInfo)
        {
            if (personalInfo == null)
                return string.Empty;

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(personalInfo.FirstName))
                parts.Add(personalInfo.FirstName);

            if (!string.IsNullOrWhiteSpace(personalInfo.LastName))
                parts.Add(personalInfo.LastName);

            return string.Join(" ", parts);
        }
    }
}
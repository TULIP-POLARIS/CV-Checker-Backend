using DAL;
using DAL.Api;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Services
{
    public class JobOfferReadinessService
    {
        private readonly ApiContext _context;

        public JobOfferReadinessService(ApiContext context)
        {
            _context = context;
        }

        public async Task<JobOfferReadinessResult> CheckUserReadinessAsync(Guid userId)
        {
            var hasPersonalInfo = await _context.Set<PersonalInfo>()
                .AnyAsync(x => x.UserId == userId);

            var hasEducation = await _context.Set<Education>()
                .AnyAsync(x => x.UserId == userId);

            var hasWorkExperience = await _context.Set<WorkExperience>()
                .AnyAsync(x => x.UserId == userId);

            var hasSkills = await _context.Set<Skill>()
                .AnyAsync(x => x.UserId == userId);

            var hasLanguages = await _context.Set<Language>()
                .AnyAsync(x => x.UserId == userId);

            var hasCV = await _context.Set<CV>()
                .AnyAsync(x => x.UserId == userId);

            var canProceed =
                hasCV ||
                (
                    hasPersonalInfo &&
                    hasSkills &&
                    (hasEducation || hasWorkExperience)
                );

            return new JobOfferReadinessResult
            {
                CanProceed = canProceed,
                Message = canProceed
                    ? "Profile is sufficient for job offer analysis."
                    : "Please fill in your profile or upload a CV before checking a job offer.",

                HasPersonalInfo = hasPersonalInfo,
                HasEducation = hasEducation,
                HasWorkExperience = hasWorkExperience,
                HasSkills = hasSkills,
                HasLanguages = hasLanguages,
                HasCV = hasCV
            };
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities;
using BusinessLogic.Interface;
using DAL.Interface;

namespace BusinessLogic.Services
{
    public class CVComparisonService : ICVComparisonService
    {
        private readonly ICVComparisonRepository _comparisonRepository;
        private readonly ICVRepository _cvRepository;
        private readonly IJobOfferRepository _jobOfferRepository;
        private readonly IUserRepository _userRepository;

        public CVComparisonService(
            ICVComparisonRepository comparisonRepository,
            ICVRepository cvRepository,
            IJobOfferRepository jobOfferRepository,
            IUserRepository userRepository)
        {
            _comparisonRepository = comparisonRepository;
            _cvRepository = cvRepository;
            _jobOfferRepository = jobOfferRepository;
            _userRepository = userRepository;
        }

        public Task<CVComparison?> GetByIdAsync(Guid id)
        {
            return _comparisonRepository.GetByIdAsync(id);
        }

        public Task<IEnumerable<CVComparison>> GetByUserIdAsync(Guid userId)
        {
            return _comparisonRepository.GetByUserIdAsync(userId);
        }

        public async Task<CVComparison> CreateCVComparisonAsync(CVComparison comparison)
        {
            if (comparison.CVId == Guid.Empty)
                throw new ArgumentException("CVId is required.");

            if (comparison.JobOfferId == Guid.Empty)
                throw new ArgumentException("JobOfferId is required.");

            if (comparison.UserId == Guid.Empty)
                throw new ArgumentException("UserId is required.");

            // Verify CV exists
            var cv = await _cvRepository.GetByIdAsync(comparison.CVId);
            if (cv == null)
                throw new ArgumentException("CV not found.");

            // Verify JobOffer exists
            var jobOffer = await _jobOfferRepository.GetByIdAsync(comparison.JobOfferId);
            if (jobOffer == null)
                throw new ArgumentException("JobOffer not found.");

            // Verify User exists
            var user = await _userRepository.GetByIdAsync(comparison.UserId);
            if (user == null)
                throw new ArgumentException("User not found.");

            // Ensure UserId matches CV's UserId
            if (cv.UserId != comparison.UserId)
                throw new ArgumentException("CV does not belong to the specified user.");

            if (comparison.CreatedAt == default)
                comparison.CreatedAt = DateTime.UtcNow;

            return await _comparisonRepository.CreateCVComparisonAsync(comparison);
        }
    }
}


using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities;
using BusinessLogic.Interface;
using DAL.Interface;

namespace BusinessLogic.Services
{
    public class JobOfferService : IJobOfferService
    {
        private readonly IJobOfferRepository _jobOfferRepository;

        public JobOfferService(IJobOfferRepository jobOfferRepository)
        {
            _jobOfferRepository = jobOfferRepository;
        }

        public Task<JobOffer?> GetByIdAsync(Guid id)
        {
            return _jobOfferRepository.GetByIdAsync(id);
        }

        public Task<IEnumerable<JobOffer>> GetAllAsync()
        {
            return _jobOfferRepository.GetAllAsync();
        }

        public async Task<JobOffer> CreateJobOfferAsync(JobOffer jobOffer)
        {
            if (string.IsNullOrWhiteSpace(jobOffer.Title))
                throw new ArgumentException("Title is required.");

            if (string.IsNullOrWhiteSpace(jobOffer.Description))
                throw new ArgumentException("Description is required.");

            if (jobOffer.CreatedAt == default)
                jobOffer.CreatedAt = DateTime.UtcNow;

            return await _jobOfferRepository.CreateJobOfferAsync(jobOffer);
        }

        public async Task<JobOffer> UpdateJobOfferAsync(JobOffer jobOffer)
        {
            if (jobOffer.Id == Guid.Empty)
                throw new ArgumentException("Invalid JobOffer.");

            if (string.IsNullOrWhiteSpace(jobOffer.Title))
                throw new ArgumentException("Title is required.");

            if (string.IsNullOrWhiteSpace(jobOffer.Description))
                throw new ArgumentException("Description is required.");

            return await _jobOfferRepository.UpdateJobOfferAsync(jobOffer);
        }

        public async Task<bool> DeleteJobOfferAsync(Guid id) // still need to see if this should be here
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Invalid JobOffer ID.");

            return await _jobOfferRepository.DeleteJobOfferAsync(id);
        }
    }
}


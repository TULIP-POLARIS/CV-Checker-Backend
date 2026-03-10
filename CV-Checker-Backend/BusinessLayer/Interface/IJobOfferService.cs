using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities;

namespace BusinessLogic.Interface
{
    public interface IJobOfferService
    {
        Task<JobOffer?> GetByIdAsync(Guid id);

        Task<IEnumerable<JobOffer>> GetAllAsync();

        Task<JobOffer> CreateJobOfferAsync(JobOffer jobOffer);

        Task<JobOffer> UpdateJobOfferAsync(JobOffer jobOffer);
        
        Task<bool> DeleteJobOfferAsync(Guid id);
    }
}


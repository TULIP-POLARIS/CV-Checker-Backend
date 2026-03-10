using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Entities;
using DAL.Api;
using DAL.Interface;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository
{
    public class JobOfferRepository : IJobOfferRepository
    {
        private readonly ApiContext _db;

        public JobOfferRepository(ApiContext db)
        {
            _db = db;
        }

        public async Task<JobOffer?> GetByIdAsync(Guid id)
        {
            return await _db.JobOffers.FirstOrDefaultAsync(jo => jo.Id == id);
        }

        public async Task<IEnumerable<JobOffer>> GetAllAsync()
        {
            return await _db.JobOffers
                .OrderByDescending(jo => jo.CreatedAt)
                .ToListAsync();
        }

        public async Task<JobOffer> CreateJobOfferAsync(JobOffer jobOffer)
        {
            if (jobOffer.CreatedAt == default)
                jobOffer.CreatedAt = DateTime.UtcNow;

            await _db.JobOffers.AddAsync(jobOffer);
            await _db.SaveChangesAsync();

            return jobOffer;
        }

        public async Task<JobOffer> UpdateJobOfferAsync(JobOffer jobOffer)
        {
            var existing = await _db.JobOffers.FirstOrDefaultAsync(jo => jo.Id == jobOffer.Id);
            if (existing == null)
                throw new InvalidOperationException("JobOffer not found.");

            existing.Title = jobOffer.Title;
            existing.Company = jobOffer.Company;
            existing.Description = jobOffer.Description;
            existing.Requirements = jobOffer.Requirements;
            existing.Location = jobOffer.Location;
            existing.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteJobOfferAsync(Guid id)
        {
            var jobOffer = await _db.JobOffers.FirstOrDefaultAsync(jo => jo.Id == id);
            if (jobOffer == null)
                return false;

            _db.JobOffers.Remove(jobOffer);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}


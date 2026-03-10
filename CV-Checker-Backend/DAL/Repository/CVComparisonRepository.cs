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
    public class CVComparisonRepository : ICVComparisonRepository
    {
        private readonly ApiContext _db;

        public CVComparisonRepository(ApiContext db)
        {
            _db = db;
        }

        public async Task<CVComparison?> GetByIdAsync(Guid id)
        {
            return await _db.CVComparisons.FirstOrDefaultAsync(cc => cc.Id == id);
        }

        public async Task<IEnumerable<CVComparison>> GetByUserIdAsync(Guid userId)
        {
            return await _db.CVComparisons
                .Where(cc => cc.UserId == userId)
                .OrderByDescending(cc => cc.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<CVComparison>> GetByCVIdAsync(Guid cvId)
        {
            return await _db.CVComparisons
                .Where(cc => cc.CVId == cvId)
                .OrderByDescending(cc => cc.CreatedAt)
                .ToListAsync();
        }

        public async Task<CVComparison> CreateCVComparisonAsync(CVComparison comparison)
        {
            if (comparison.CreatedAt == default)
                comparison.CreatedAt = DateTime.UtcNow;

            await _db.CVComparisons.AddAsync(comparison);
            await _db.SaveChangesAsync();

            return comparison;
        }
    }
}


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
    public class CVRepository : ICVRepository
    {
        private readonly ApiContext _db;

        public CVRepository(ApiContext db)
        {
            _db = db;
        }

        public async Task<CV?> GetByIdAsync(Guid id)
        {
            return await _db.CVs.FirstOrDefaultAsync(cv => cv.Id == id);
        }

        public async Task<IEnumerable<CV>> GetByUserIdAsync(Guid userId)
        {
            return await _db.CVs
                .Where(cv => cv.UserId == userId)
                .OrderByDescending(cv => cv.CreatedAt)
                .ToListAsync();
        }

        public async Task<CV> CreateCVAsync(CV cv)
        {
            if (cv.CreatedAt == default)
                cv.CreatedAt = DateTime.UtcNow;

            await _db.CVs.AddAsync(cv);
            await _db.SaveChangesAsync();

            return cv;
        }

        public async Task<CV> UpdateCVAsync(CV cv)
        {
            var existing = await _db.CVs.FirstOrDefaultAsync(c => c.Id == cv.Id);
            if (existing == null)
                throw new InvalidOperationException("CV not found.");

            existing.FileName = cv.FileName;
            existing.FilePath = cv.FilePath;
            existing.TemplateId = cv.TemplateId;
            existing.Content = cv.Content;
            existing.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteCVAsync(Guid id)
        {
            var cv = await _db.CVs.FirstOrDefaultAsync(c => c.Id == id);
            if (cv == null)
                return false;

            _db.CVs.Remove(cv);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}


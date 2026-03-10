using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities;

namespace DAL.Interface
{
    public interface ICVRepository
    {
        Task<CV?> GetByIdAsync(Guid id);
        Task<IEnumerable<CV>> GetByUserIdAsync(Guid userId);
        Task<CV> CreateCVAsync(CV cv);
        Task<CV> UpdateCVAsync(CV cv);
        Task<bool> DeleteCVAsync(Guid id);
    }
}


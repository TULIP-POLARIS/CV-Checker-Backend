using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities;

namespace BusinessLogic.Interface
{
    public interface ICVService
    {
        Task<CV?> GetByIdAsync(Guid id);

        Task<IEnumerable<CV>> GetByUserIdAsync(Guid userId);

        Task<CV> CreateCVAsync(CV cv);

        Task<CV> UpdateCVAsync(CV cv);
        
        Task<bool> DeleteCVAsync(Guid id);
    }
}


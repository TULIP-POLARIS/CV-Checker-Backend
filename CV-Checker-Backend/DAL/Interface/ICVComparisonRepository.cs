using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities;

namespace DAL.Interface
{
    public interface ICVComparisonRepository
    {
        Task<CVComparison?> GetByIdAsync(Guid id);
        Task<IEnumerable<CVComparison>> GetByUserIdAsync(Guid userId);
        Task<IEnumerable<CVComparison>> GetByCVIdAsync(Guid cvId);
        Task<CVComparison> CreateCVComparisonAsync(CVComparison comparison);
    }
}


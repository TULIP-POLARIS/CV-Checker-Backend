using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities;

namespace BusinessLogic.Interface
{
    public interface ICVComparisonService
    {
        Task<CVComparison?> GetByIdAsync(Guid id);

        Task<IEnumerable<CVComparison>> GetByUserIdAsync(Guid userId);
        
        Task<CVComparison> CreateCVComparisonAsync(CVComparison comparison);
    }
}


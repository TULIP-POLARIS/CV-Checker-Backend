using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities;
using BusinessLogic.Interface;
using DAL.Interface;

namespace BusinessLogic.Services
{
    public class CVService : ICVService
    {
        private readonly ICVRepository _cvRepository;
        private readonly IUserRepository _userRepository;

        public CVService(ICVRepository cvRepository, IUserRepository userRepository)
        {
            _cvRepository = cvRepository;
            _userRepository = userRepository;
        }

        public Task<CV?> GetByIdAsync(Guid id)
        {
            return _cvRepository.GetByIdAsync(id);
        }

        public Task<IEnumerable<CV>> GetByUserIdAsync(Guid userId)
        {
            return _cvRepository.GetByUserIdAsync(userId);
        }

        public async Task<CV> CreateCVAsync(CV cv)
        {
            if (cv.UserId == Guid.Empty)
                throw new ArgumentException("UserId is required.");

            // Verify user exists
            var user = await _userRepository.GetByIdAsync(cv.UserId);
            if (user == null)
                throw new ArgumentException("User not found.");

            if (cv.CreatedAt == default)
                cv.CreatedAt = DateTime.UtcNow;

            return await _cvRepository.CreateCVAsync(cv);
        }

        public async Task<CV> UpdateCVAsync(CV cv)
        {
            if (cv.Id == Guid.Empty)
                throw new ArgumentException("Invalid CV.");

            return await _cvRepository.UpdateCVAsync(cv);
        }

        public async Task<bool> DeleteCVAsync(Guid id)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Invalid CV ID.");

            return await _cvRepository.DeleteCVAsync(id);
        }
    }
}


using System;
using System.Threading.Tasks;
using Domain.Entities;
using BusinessLogic.Interface;
using DAL.Interface;

namespace BusinessLogic.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public Task<User?> GetByIdAsync(Guid id)
        {
            return _userRepository.GetByIdAsync(id);
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            return _userRepository.GetByEmailAsync(email);
        }

        public Task<User?> GetByPhoneAsync(string phoneNumber)
        {
            return _userRepository.GetByPhoneAsync(phoneNumber);
        }

        public async Task<User> CreateUserAsync(User user)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
                throw new ArgumentException("Email is required.");

            // i will leave createdat here for now (TODO: check if its better to put in repo)
            if (user.CreatedAt == default)
                user.CreatedAt = DateTime.UtcNow;


            return await _userRepository.CreateUserAsync(user);
        }

        public async Task<User> UpdateUserAsync(User user)
        {
            if (user.Id == Guid.Empty)
                throw new ArgumentException("Invalid user.");

            return await _userRepository.UpdateUserAsync(user);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using BusinessLogic.Entities.User;

namespace DAL.Repository
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByEmailAsync(string email);
        Task<Guid> CreateAsync(User user);
        Task UpdateAsync(User user);
        Task DeleteAsync(Guid id);
    }
}

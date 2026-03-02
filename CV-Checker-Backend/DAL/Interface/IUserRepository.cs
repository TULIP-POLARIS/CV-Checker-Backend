using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.BusinessLogic.Entities;

namespace DAL.Interface
{
    public class IUserRepository
    {
        Task<User> GetByIdAsync(int id);
        Task<User> GetByEmailAsync(string email);
        Task<User> GetByPhoneAsync(string phoneNumber);
        Task<User> CreateUserAsync(User user);
        Task<User> UpdateUserAsync(User user);
    }
}

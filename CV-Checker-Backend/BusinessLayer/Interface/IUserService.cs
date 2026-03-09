using Domain.Entities;
using System.Threading.Tasks;


namespace BusinessLogic.Interface
{
    public interface IUserService
    {
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByPhoneAsync(string phoneNumber);

        Task<User> CreateUserAsync(User user);
        Task<User> UpdateUserAsync(User user);
    }
}

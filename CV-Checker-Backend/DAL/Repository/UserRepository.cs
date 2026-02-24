using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repository
{
    public interface IUserService
    {
        Task<UserResponseDto> GetByIdAsync(Guid id);
        Task<UserResponseDto> CreateAsync(CreateUserRequestDto dto);
        Task<UserResponseDto> UpdateAsync(Guid id, UpdateUserRequestDto dto);
        Task DeleteAsync(Guid id);
    }
}

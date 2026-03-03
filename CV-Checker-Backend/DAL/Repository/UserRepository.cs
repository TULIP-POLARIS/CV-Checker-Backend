using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Domain.Entities;
using DAL.Api;
using DAL.Interface;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly ApiContext _db;

        public UserRepository(ApiContext db)
        {
            _db = db;
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> GetByPhoneAsync(string phoneNumber)
        {
            return await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
        }

        public async Task<User> CreateUserAsync(User user)
        {
            if (user.CreatedAt == default)
                user.CreatedAt = DateTime.UtcNow;

            await _db.Users.AddAsync(user);
            await _db.SaveChangesAsync();

            return user;
        }

        public async Task<User> UpdateUserAsync(User user)
        {
            var existing = await _db.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            if (existing == null)
                throw new InvalidOperationException("User not found.");

            existing.Name = user.Name;
            existing.Email = user.Email;
            existing.PhoneNumber = user.PhoneNumber;
            existing.PasswordHash = user.PasswordHash;

            await _db.SaveChangesAsync();
            return existing;
        }
    }
}


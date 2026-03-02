using BusinessLogic.Entities;
using Microsoft.EntityFrameworkCore;

namespace CVApi
{
    public class ApiContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public ApiContext(DbContextOptions<ApiContext> options) : base(options)
        {
        }
    }
}
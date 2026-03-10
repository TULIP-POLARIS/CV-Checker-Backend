using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DAL.Api
{
    public class ApiContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<CV> CVs { get; set; }
        public DbSet<JobOffer> JobOffers { get; set; }
        public DbSet<CVComparison> CVComparisons { get; set; }

        public ApiContext(DbContextOptions<ApiContext> options) : base(options)
        {
        }
    }
}
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

        public DbSet<PersonalInfo> PersonalInfos { get; set; }
        public DbSet<Education> Educations { get; set; }
        public DbSet<WorkExperience> WorkExperiences { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<Language> Languages { get; set; }
        public DbSet<CvBackground> CvBackgrounds { get; set; }
        public DbSet<CvGenerated> CvGenerations { get; set; }

        public ApiContext(DbContextOptions<ApiContext> options) : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CV>(entity =>
            {
                entity.Property(e => e.FileData)
                      .HasColumnType("varbinary(max)");
            });
        }
    }
}
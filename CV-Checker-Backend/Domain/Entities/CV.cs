using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace Domain.Entities
{
    public class CV
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        public string? FileName { get; set; }
        public Guid? TemplateId { get; set; }
        public string? FilePath { get; set; }
        public string? Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation ( Later, when we add EF Core in the DAL layer, EF can use these navigation properties to map relationships)
        public User? OwnerUser { get; set; }
        public List<CVComparison> Comparisons { get; set; } = new();
        //public FileAsset? SourceFile { get; set; }
       // public CVTemplate? Template { get; set; }

        public List<CVSection> Sections { get; set; } = new();
    }
}


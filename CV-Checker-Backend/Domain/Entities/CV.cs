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

        public Guid OwnerUserId { get; set; }
        public string Title { get; set; } = default!;

        public Guid? SourceFileId { get; set; }
        public Guid? TemplateId { get; set; }

        // Navigation ( Later, when we add EF Core in the DAL layer, EF can use these navigation properties to map relationships)
        public User? OwnerUser { get; set; }
        public FileAsset? SourceFile { get; set; }
        public CVTemplate? Template { get; set; }

        public List<CVSection> Sections { get; set; } = new();
    }
}


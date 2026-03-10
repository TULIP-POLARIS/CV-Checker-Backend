using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class CVSection
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid CVId { get; set; }
        public string SectionType { get; set; } = default!;
        public int Order { get; set; }

        // Lets just leave for now
        public string? Content { get; set; }

        // Navigation
        public CV? CV { get; set; }
    }
}

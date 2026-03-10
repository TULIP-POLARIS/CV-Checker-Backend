using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class MatchResult
    {
            public Guid Id { get; set; } = Guid.NewGuid();

            public Guid CVId { get; set; }
            public Guid JobDescriptionId { get; set; }

            public int ScorePercent { get; set; }

            // Navigation
            public CV? CV { get; set; }
            public JobDescription? JobDescription { get; set; }
    }
}

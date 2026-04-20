using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class JobOfferReadinessResult
    {
        public bool CanProceed { get; set; }
        public string Message { get; set; } = string.Empty;

        public bool HasPersonalInfo { get; set; }
        public bool HasEducation { get; set; }
        public bool HasWorkExperience { get; set; }
        public bool HasSkills { get; set; }
        public bool HasLanguages { get; set; }
        public bool HasCV { get; set; }
    }
}
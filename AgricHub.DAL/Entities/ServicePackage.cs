using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.DAL.Entities
{
    public class ServicePackage
    {
        public int Id { get; set; }
        public int ServiceId { get; set; }
        public Service Service { get; set; }
        public string PackageName { get; set; }
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }  // ADD THIS PROPERTY
        public string Description { get; set; }
        public bool IncludesOnsiteVisit { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

}

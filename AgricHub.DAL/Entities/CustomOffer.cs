using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.DAL.Entities
{
 

    public class CustomOffer
    {
        public Guid Id { get; set; }
        public Guid ChatSessionId { get; set; }
        public ChatSession ChatSession { get; set; }
        public int ServiceId { get; set; }
        public Service Service { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
        public bool IncludesOnsiteVisit { get; set; }
        public string Status { get; set; } // Pending, Accepted, Rejected
        public DateTime CreatedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
      
        public DateTime? ScheduledAt { get; set; } // Proposed consultation time
        public int DurationMinutes { get; set; } // Duration of the consultation
    }
}

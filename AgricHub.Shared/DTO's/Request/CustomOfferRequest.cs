using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Request
{


    public class CustomOfferRequest
    {
        public Guid ChatSessionId { get; set; }
        public int ServiceId { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
        public bool IncludesOnsiteVisit { get; set; }
        public DateTime ScheduledAt { get; set; }
        public int DurationMinutes { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Response
{
  
    public class CustomOfferResponse
    {
        public Guid Id { get; set; }
        public Guid ChatSessionId { get; set; }
        public int ServiceId { get; set; }
        public string ServiceName { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
        public bool IncludesOnsiteVisit { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
    }
}

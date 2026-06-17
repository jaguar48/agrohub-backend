using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Request
{
    public class ConsultationBookingRequest
    {
        public string ConsultantUserId { get; set; }  // ← CHANGED from int to string
        public int ServiceId { get; set; }
        public int ServicePackageId { get; set; }
        public DateTime ScheduledAt { get; set; }
        public string? Notes { get; set; }
    }
}

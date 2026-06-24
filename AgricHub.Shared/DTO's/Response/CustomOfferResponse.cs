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

        // ── ADDED: these existed on the CustomOffer entity but were never
        // exposed on the response, so the frontend had no way to ever show
        // duration or scheduled time for a custom offer. ───────────────────
        public int DurationMinutes { get; set; }
        public DateTime? ScheduledAt { get; set; }
    }
}
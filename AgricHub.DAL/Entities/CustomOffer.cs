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

        // ── Pitch fields — populated when a consultant responds to a customer's
        // CustomOfferRequest. Null for the old "consultant-initiated" offer flow. ──
        public Guid? OfferPostId { get; set; }
        public OfferPost? OfferPost { get; set; }

        /// <summary>The consultant's cover message / pitch — why they're the right fit.</summary>
        public string? PitchMessage { get; set; }

        /// <summary>Optional portfolio attachment (PDF/image) uploaded with the pitch.</summary>
        public string? PortfolioUrl { get; set; }
        public string? PortfolioFileName { get; set; }
        public int ConsultantId { get; set; } // who submitted this pitch (0 for legacy rows)
    }
}

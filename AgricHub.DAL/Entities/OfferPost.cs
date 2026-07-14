using System;
using AgricHub.DAL.Entities.Models;

namespace AgricHub.DAL.Entities
{
    /// <summary>
    /// A customer-posted request for a custom consultation. Consultants browse
    /// open requests (optionally filtered by category) and submit pitches
    /// (CustomOffer, extended with PitchMessage/PortfolioUrl) in response —
    /// same pattern as a Fiverr buyer request. Named OfferPost (not
    /// CustomOfferRequest) to avoid colliding with the existing
    /// Shared.DTO_s.Request.CustomOfferRequest DTO used by the older
    /// consultant-initiated offer flow.
    /// </summary>
    public class OfferPost
    {
        public Guid Id { get; set; }

        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        /// <summary>Optional — narrows which consultants see it in their feed. Null = any category.</summary>
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal? Budget { get; set; }              // customer's target budget, optional
        public DateTime? PreferredAt { get; set; }         // when they'd like the session, optional

        /// <summary>Open | Closed. Closes automatically once the customer accepts a pitch.</summary>
        public string Status { get; set; } = "Open";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ClosedAt { get; set; }
    }
}

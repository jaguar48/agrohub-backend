using System;

namespace AgricHub.Shared.DTO_s.Request
{
    /// <summary>Customer posts a request for a custom consultation — Fiverr-style buyer request.</summary>
    public class CreateOfferPostRequest
    {
        public int? CategoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal? Budget { get; set; }
        public DateTime? PreferredAt { get; set; }
    }

    /// <summary>Consultant submits a pitch in response to an open offer post.</summary>
    public class SubmitPitchRequest
    {
        public Guid OfferPostId { get; set; }
        public int ServiceId { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; } = string.Empty;   // what's included
        public string PitchMessage { get; set; } = string.Empty;  // cover message / why pick me
        public bool IncludesOnsiteVisit { get; set; }
        public DateTime ScheduledAt { get; set; }
        public int DurationMinutes { get; set; }
        // Portfolio file comes via multipart form, not this JSON body — see controller.
    }
}

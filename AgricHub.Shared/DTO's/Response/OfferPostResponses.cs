using System;
using System.Collections.Generic;

namespace AgricHub.Shared.DTO_s.Response
{
    public class OfferPostResponse
    {
        public Guid Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal? Budget { get; set; }
        public DateTime? PreferredAt { get; set; }
        public string Status { get; set; } = "Open";
        public DateTime CreatedAt { get; set; }
        public int PitchCount { get; set; }
    }

    /// <summary>A consultant's pitch on an offer post — reuses CustomOfferResponse shape,
    /// extended with the pitch-specific fields.</summary>
    public class PitchResponse
    {
        public Guid Id { get; set; }
        public Guid OfferPostId { get; set; }
        public int ConsultantId { get; set; }
        public string ConsultantName { get; set; } = string.Empty;
        public string? ConsultantAvatarUrl { get; set; }
        public double ConsultantRating { get; set; }
        public int ServiceId { get; set; }
        public string? ServiceName { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; } = string.Empty;
        public string PitchMessage { get; set; } = string.Empty;
        public string? PortfolioUrl { get; set; }
        public string? PortfolioFileName { get; set; }
        public bool IncludesOnsiteVisit { get; set; }
        public DateTime ScheduledAt { get; set; }
        public int DurationMinutes { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; }
        public Guid ChatSessionId { get; set; }
    }

    public class OfferPostDetailResponse : OfferPostResponse
    {
        public List<PitchResponse> Pitches { get; set; } = new();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace AgricHub.Shared.DTO_s.Response
{
    // AgricHub.Shared/DTO's/Response/ConsultationResponse.cs
    public class ConsultationResponse
    {
        public Guid Id { get; set; }
        public string CustomerUserId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerCountry { get; set; }      // ← add
        public string ConsultantUserId { get; set; }
        public string ConsultantName { get; set; }
        public int? ServiceId { get; set; }
        public string ServiceName { get; set; }
        public int? ServicePackageId { get; set; }
        public string PackageName { get; set; }
        public int? DurationMinutes { get; set; }         // ← add
        public string Notes { get; set; }
        public DateTime ScheduledAt { get; set; }
        public DateTime EndAt { get; set; }
        public string Status { get; set; }
        public string SendbirdChannelUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsCustomOffer { get; set; }
        public decimal? CustomPrice { get; set; }
        public DateTime? NoShowRequestedAt { get; set; }
        public int? NoShowGraceHours { get; set; }
        public bool NoShowProcessed { get; set; }
        public int? CustomDurationMinutes { get; set; }
        public decimal Price { get; set; }
        public decimal PendingAmount { get; set; }
        public DateTime? CustomerNoShowRequestedAt { get; set; }
        public int? CustomerNoShowGraceHours { get; set; }
        public bool CustomerNoShowProcessed { get; set; } = false;
        public string? CompletionSummary { get; set; }
        public string? CompletionFileUrl { get; set; }
        public DateTime? StartedAt { get; set; }

        public DateTime? CompletionSubmittedAt { get; set; }

        // Customer history
        public int CustomerTotalBookings { get; set; }      // ← add
        public int CustomerCompletedBookings { get; set; }  // ← add
        public int CustomerReviewCount { get; set; }        // ← add
        public double CustomerAverageRating { get; set; }   // ← add

        // Disputes
        public DateTime? DisputeRaisedAt { get; set; }
        public string? DisputeReason { get; set; }
        public string? DisputeStatus { get; set; }
        public DateTime? RescheduleRequestedAt { get; set; }
        public string? RescheduleRequestReason { get; set; }

    }
}
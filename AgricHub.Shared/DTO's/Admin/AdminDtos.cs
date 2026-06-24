using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s
{




    public record AdminStatsDto(
     int TotalUsers,
     int VerifiedConsultants,
     int PendingVerifications,
     int CompletedSessions30d,
     int TotalReviews,
     IReadOnlyList<RecentReviewSummaryDto> RecentReviews,
     IReadOnlyList<PendingVerifSummaryDto> PendingVerifs
 );


    public record ReportSummaryDto(
        string Id,
        string Reviewer,
        string Consultant,
        string Reason,
        DateTime ReportedAt,
        string Status
    );

    public record VerificationSummaryDto(
        string Name,
        string Biz,
        string Country,
        DateOnly Submitted
    );

    // ── Moderation ────────────────────────────────────────────────
    public record ReportDto(
        string Id,
        int ReviewId,
        string ReportingUser,
        string Reason,
        string Details,
        string Status,
        DateTime ReportedAt,
        string ConsultantName,
        string ConsultantBiz,
        string ReviewExcerpt
    );

    public record UpdateReportStatusRequest(
        string NewStatus,   // "Pending" | "Under Review" | "Resolved — Upheld" | "Resolved — Dismissed"
        string? Notes
    );

    // ── Verifications ─────────────────────────────────────────────


    public record VerificationDto(
      int Id,
      string FirstName,
      string LastName,
      string BusinessName,
      string Email,
      string? PhoneNumber,
      string? CountryId,
      string? StateId,
      bool IsVerified,
      string Status,
      string? SubmittedAt,
      string? RejectionNotes,
      List<string> Documents
  );

    public record UpdateVerificationStatusRequest(
        string NewStatus,   // "Approved" | "Rejected"
        string? Notes
    );

    // ── Users ─────────────────────────────────────────────────────
    public record AdminUserDto(
     string Id,
     string FirstName,
     string LastName,
     string Email,
     string? CountryId,
     IList<string> Roles,
     decimal WalletBalance,
     int TotalConsultations,
     int ActiveConsultations,
     int CompletedConsultations,
     int NoShowCount,
     DateTime? CreatedAt,
     bool IsSuspended,
     string? SuspensionReason
  );




    public record PagedResult<T>(
        IReadOnlyList<T> Items,
        int Total,
        int Page,
        int PageSize
    );

    // ── Consultants ───────────────────────────────────────────────
    public record AdminConsultantDto(
        int Id,
        string FirstName,
        string LastName,
        string? BusinessName,
        string? CountryId,
        string? StateId,
        bool IsVerified,
        int CompletedConsultations,
        double AverageRating,
        int TotalReviews
    );


    // ── Categories ────────────────────────────────────────────────
    public record CategoryDto(
        int Id,
        string Name,
        int ServiceCount
    );



    // ── Disputes (customer rejected a completed-work submission) ───
    public record DisputeDto(
        Guid Id,
        string CustomerName,
        string ConsultantName,
        string ServiceName,
        decimal Amount,
        string Reason,
        DateTime RaisedAt,
        string? CompletionSummary,
        string? CompletionFileUrl,
        string DisputeStatus,   // "Open" | "ResolvedReleased" | "ResolvedRefunded"
        string ConsultationStatus,
        string CustomerUserId,
        string ConsultantUserId
    );

    public record ResolveDisputeRequest(
        string Resolution,   // "Release" | "Refund"
        string? Notes
    );


    public record RecentReviewSummaryDto(
        int Id,
        string CustomerName,
        string ConsultantName,
        int Rating,
        string? Comment,
        DateTime CreatedAt
    );

    public record PendingVerifSummaryDto(
        int Id,
        string Name,
        string BusinessName,
        string? CountryId
    );

    // ── Reviews (Moderation) ──────────────────────────────────
    public record AdminReviewDto(
        int Id,
        Guid ConsultationId,
        string CustomerName,
        string ConsultantName,
        int Rating,
        string? Comment,
        DateTime CreatedAt
    );

    // ── Verifications ─────────────────────────────────────────


    public record UpdateVerificationRequest(bool Approve, string? Notes = null);

    // ── Users ─────────────────────────────────────────────────


    public record AdminUserPagedResult(
        IReadOnlyList<AdminUserDto> Items,
        int Total,
        int Page,
        int PageSize,
        int TotalCustomers,
        int TotalConsultants,
        int TotalAdmins
    );

    public record SuspendUserRequest(string Reason);

    // ── Consultants ───────────────────────────────────────────

    public record AdminConsultantPagedResult(
        IReadOnlyList<AdminConsultantDto> Items,
        int Total,
        int Page,
        int PageSize
    );

    // ── Categories ────────────────────────────────────────────

}
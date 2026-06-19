using AgricHub.DAL.Entities;
using AgricHub.Shared.DTO_s.Request;
using AgricHub.Shared.DTO_s.Response;
using Microsoft.AspNetCore.Http;

namespace AgricHub.BLL.Interfaces.IBusinessServices
{
    public interface IConsultationService
    {
        // ── Lifecycle ─────────────────────────────────────────────────────────
        Task<ConsultationResponse> BookConsultationAsync(ConsultationBookingRequest dto);
        Task<ConsultationResponse> ApproveConsultationAsync(Guid consultationId, string? notes = null);
        Task<ConsultationResponse> RejectConsultationAsync(Guid consultationId, string reason);
        Task<ConsultationResponse> StartConsultationAsync(Guid consultationId);
        Task<ConsultationResponse> SubmitCompletionAsync(Guid consultationId, string summary, IFormFile file);
        Task<ConsultationResponse> ApproveCompletionAsync(Guid consultationId);
        Task ProcessExpiredApprovalForConsultationAsync(Guid consultationId);
        Task ProcessExpiredApprovalsAsync();
        Task<ConsultationResponse> CancelConsultationAsync(Guid consultationId, string? reason = null);

        // ── No-show (customer reports consultant) ─────────────────────────────
        Task<ConsultationResponse> ReportConsultantNoShowAsync(Guid consultationId);
        Task RequestNoShowAsync(Guid consultationId, int graceHours);
        Task DismissNoShowRequestAsync(Guid consultationId);
        Task ProcessExpiredNoShowsAsync();
        Task ProcessExpiredNoShowForConsultationAsync(Guid consultationId);
        Task<int> GetActiveBookingsCountAsync(int consultantId);

        // ── No-show (consultant reports customer) ─────────────────────────────
        Task<ConsultationResponse> ReportCustomerNoShowAsync(Guid consultationId);
        Task RequestCustomerNoShowAsync(Guid consultationId, int graceHours);
        Task DismissCustomerNoShowRequestAsync(Guid consultationId);
        Task ProcessExpiredCustomerNoShowForConsultationAsync(Guid consultationId);
        Task ProcessExpiredCustomerNoShowsAsync();

        // ── Dispute ───────────────────────────────────────────────────────────
        Task<ConsultationResponse> DisputeCompletionAsync(Guid consultationId, string reason);
        Task<IEnumerable<ConsultationResponse>> GetDisputedConsultationsAsync(string? statusFilter = null);
        Task ResolveDisputeAsync(Guid consultationId, string resolution, string? notes);

        // ── Reschedule ────────────────────────────────────────────────────────
        Task RescheduleConsultationAsync(Guid consultationId, DateTime newScheduledAt);
        Task RequestRescheduleAsync(Guid consultationId, string reason);

        // ── Overdue sessions (sweep) ──────────────────────────────────────────
        Task ProcessOverdueInProgressSessionsAsync();

        // ── Queries ───────────────────────────────────────────────────────────
        Task<IEnumerable<ConsultationResponse>> GetMyConsultationsAsync();
        Task<IEnumerable<ConsultationResponse>> GetConsultantConsultationsAsync();
    }
}
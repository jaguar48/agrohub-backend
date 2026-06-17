using AgricHub.BLL.Interfaces.IBusinessServices;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace AgricHub.Presentation.Controllers.BusinessController
{
    [ApiController]
    [Route("api/agrichub/booking")]
    [Authorize]
    public class BookingController : ControllerBase
    {
        private readonly IConsultationService _consultationService;

        public BookingController(IConsultationService consultationService)
        {
            _consultationService = consultationService;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        [HttpPost("book")]
        [Authorize(Roles = "Customer")]
        [SwaggerOperation("Customer books a consultation. Payment deducted from wallet and held in escrow.")]
        public async Task<IActionResult> BookConsultation([FromBody] ConsultationBookingRequest dto)
        {
            try
            {
                var result = await _consultationService.BookConsultationAsync(dto);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{id}/approve")]
        [Authorize(Roles = "Consultant")]
        [SwaggerOperation("Consultant approves a pending consultation.")]
        public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveRequest? request)
        {
            try
            {
                var result = await _consultationService.ApproveConsultationAsync(id, request?.Notes);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{id}/reject")]
        [Authorize(Roles = "Consultant")]
        [SwaggerOperation("Consultant rejects a pending consultation. Customer receives full refund.")]
        public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRequest request)
        {
            try
            {
                var result = await _consultationService.RejectConsultationAsync(id, request.Reason);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{id}/start")]
        [Authorize(Roles = "Consultant")]
        [SwaggerOperation("Consultant starts an approved consultation.")]
        public async Task<IActionResult> Start(Guid id)
        {
            try
            {
                var result = await _consultationService.StartConsultationAsync(id);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        // ── Completion / approval (two-step) ────────────────────────────────────

        [HttpPost("{id}/submit-completion")]
        [Authorize(Roles = "Consultant")]
        [SwaggerOperation("Consultant submits a summary + proof file for customer review. Escrow stays held until the customer approves.")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubmitCompletion(Guid id, [FromForm] string summary, [FromForm] IFormFile file)
        {
            try
            {
                var result = await _consultationService.SubmitCompletionAsync(id, summary, file);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{id}/approve-completion")]
        [Authorize(Roles = "Customer")]
        [SwaggerOperation("Customer approves the submitted work. Escrow funds released to consultant wallet.")]
        public async Task<IActionResult> ApproveCompletion(Guid id)
        {
            try
            {
                var result = await _consultationService.ApproveCompletionAsync(id);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{id}/dispute-completion")]
        [Authorize(Roles = "Customer")]
        [SwaggerOperation("Customer rejects the submitted work and raises a dispute. Pauses the auto-release timer; sent to admin for review.")]
        public async Task<IActionResult> DisputeCompletion(Guid id, [FromBody] DisputeRequest dto)
        {
            try
            {
                var result = await _consultationService.DisputeCompletionAsync(id, dto.Reason);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{id}/process-expired-approval")]
        [Authorize(Roles = "Consultant")]
        [SwaggerOperation("Called by frontend when the customer's 3-day review window has expired — auto-releases escrow.")]
        public async Task<IActionResult> ProcessExpiredApproval(Guid id)
        {
            try { await _consultationService.ProcessExpiredApprovalForConsultationAsync(id); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{id}/cancel")]
        [SwaggerOperation("Customer or Consultant cancels a consultation. Full refund to customer.")]
        public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelRequest? request)
        {
            try
            {
                var result = await _consultationService.CancelConsultationAsync(id, request?.Reason);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        // ── No-show: customer reports consultant ──────────────────────────────

        [HttpPost("{id}/report-consultant-noshow")]
        [Authorize(Roles = "Customer")]
        [SwaggerOperation("Customer reports consultant no-show. Full refund to customer.")]
        public async Task<IActionResult> ReportConsultantNoShow(Guid id)
        {
            try
            {
                var result = await _consultationService.ReportConsultantNoShowAsync(id);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{id}/request-no-show")]
        [Authorize(Roles = "Customer")]
        [SwaggerOperation("Customer requests no-show with grace period. Consultant has time to respond before auto-refund.")]
        public async Task<IActionResult> RequestNoShow(Guid id, [FromBody] RequestNoShowDto dto)
        {
            try { await _consultationService.RequestNoShowAsync(id, dto.GraceHours); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{id}/dismiss-no-show")]
        [Authorize(Roles = "Consultant")]
        [SwaggerOperation("Consultant confirms presence — dismisses customer's no-show request.")]
        public async Task<IActionResult> DismissNoShow(Guid id)
        {
            try { await _consultationService.DismissNoShowRequestAsync(id); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{id}/process-expired-noshow")]
        [Authorize(Roles = "Customer")]
        [SwaggerOperation("Called by frontend when consultant no-show grace period has expired.")]
        public async Task<IActionResult> ProcessExpiredNoShow(Guid id)
        {
            try { await _consultationService.ProcessExpiredNoShowForConsultationAsync(id); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        // ── No-show: consultant reports customer ──────────────────────────────

        [HttpPost("{id}/report-customer-noshow")]
        [Authorize(Roles = "Consultant")]
        [SwaggerOperation("Consultant reports customer no-show immediately. 50% to consultant, 50% refunded to customer.")]
        public async Task<IActionResult> ReportCustomerNoShow(Guid id)
        {
            try
            {
                var result = await _consultationService.ReportCustomerNoShowAsync(id);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{id}/request-customer-noshow")]
        [Authorize(Roles = "Consultant")]
        [SwaggerOperation("Consultant requests customer no-show with grace period. Customer has time to join before 50/50 split.")]
        public async Task<IActionResult> RequestCustomerNoShow(Guid id, [FromBody] RequestNoShowDto dto)
        {
            try { await _consultationService.RequestCustomerNoShowAsync(id, dto.GraceHours); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{id}/dismiss-customer-noshow")]
        [Authorize(Roles = "Customer")]
        [SwaggerOperation("Customer confirms they are joining — dismisses consultant's no-show request.")]
        public async Task<IActionResult> DismissCustomerNoShow(Guid id)
        {
            try { await _consultationService.DismissCustomerNoShowRequestAsync(id); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{id}/process-expired-customer-noshow")]
        [Authorize(Roles = "Consultant")]
        [SwaggerOperation("Called by frontend when customer no-show grace period has expired.")]
        public async Task<IActionResult> ProcessExpiredCustomerNoShow(Guid id)
        {
            try { await _consultationService.ProcessExpiredCustomerNoShowForConsultationAsync(id); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        // ── Reschedule ────────────────────────────────────────────────────────

        [HttpPost("{id}/reschedule")]
        [Authorize]
        [SwaggerOperation("Customer or Consultant proposes a new session time.")]
        public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleDto dto)
        {
            try { await _consultationService.RescheduleConsultationAsync(id, dto.NewScheduledAt); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        // ── Queries ───────────────────────────────────────────────────────────

        [HttpGet("my-consultations")]
        [Authorize(Roles = "Customer")]
        [SwaggerOperation("Customer retrieves all their consultations with escrow status.")]
        public async Task<IActionResult> GetMyConsultations()
        {
            try
            {
                var result = await _consultationService.GetMyConsultationsAsync();
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpGet("consultant-consultations")]
        [Authorize(Roles = "Consultant")]
        [SwaggerOperation("Consultant retrieves all their consultations with pending payouts.")]
        public async Task<IActionResult> GetConsultantConsultations()
        {
            try
            {
                var result = await _consultationService.GetConsultantConsultationsAsync();
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }
        [HttpPost("{id}/request-reschedule")]
        public async Task<IActionResult> RequestReschedule(Guid id, [FromBody] RescheduleRequestDto dto)
        {
            try
            {
                await _consultationService.RequestRescheduleAsync(id, dto.Reason);
                return Ok(new { success = true, message = "Reschedule request sent to customer." });
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

    }

    // ── DTOs ──────────────────────────────────────────────────────────────────
    public class RescheduleRequestDto
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class RequestNoShowDto { public int GraceHours { get; set; } = 5; }
    public class DisputeRequest { public string Reason { get; set; } = string.Empty; }
    public class RescheduleDto { public DateTime NewScheduledAt { get; set; } }
    public class ApproveRequest { public string? Notes { get; set; } }
    public class RejectRequest { public string Reason { get; set; } }
    public class CancelRequest { public string? Reason { get; set; } }
}
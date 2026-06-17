// AgricHub.Presentation/Controllers/AdminController/AdminController.cs

using AgricHub.BLL.Interfaces;
using AgricHub.BLL.Interfaces.IAdminService;
using AgricHub.Shared.DTO_s;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgricHub.Presentation.Controllers.AdminController
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController(
        IAdminService adminService,
        IAdminFinancialsService financialsService,
        IPlatformSettingsService settingsService,
        IEmailService emailService) : ControllerBase
    {
        // ── Stats ──────────────────────────────────────────────────────────────

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
            => Ok(await adminService.GetStatsAsync());

        // ── Reviews ────────────────────────────────────────────────────────────

        [HttpGet("reviews")]
        public async Task<IActionResult> GetReviews([FromQuery] int? minRating = null)
            => Ok(await adminService.GetReviewsAsync(minRating));

        [HttpDelete("reviews/{id:int}")]
        public async Task<IActionResult> DeleteReview(int id)
        {
            try { await adminService.DeleteReviewAsync(id); return NoContent(); }
            catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
        }

        // ── Verifications ──────────────────────────────────────────────────────

        [HttpGet("verifications")]
        public async Task<IActionResult> GetVerifications([FromQuery] bool? verified = null)
            => Ok(await adminService.GetVerificationsAsync(verified));

        [HttpPatch("verifications/{id:int}")]
        public async Task<IActionResult> UpdateVerification(int id, [FromBody] UpdateVerificationRequest req)
        {
            try { await adminService.UpdateVerificationAsync(id, req); return NoContent(); }
            catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
        }

        // ── Users ──────────────────────────────────────────────────────────────

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] string? role = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? userId = null)
            => Ok(await adminService.GetUsersAsync(role, search, page, pageSize, userId));

        // ── Consultants ────────────────────────────────────────────────────────

        [HttpGet("consultants")]
        public async Task<IActionResult> GetConsultants(
            [FromQuery] bool? verifiedOnly = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
            => Ok(await adminService.GetConsultantsAsync(verifiedOnly, search, page, pageSize));

        // ── Categories ─────────────────────────────────────────────────────────

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
            => Ok(await adminService.GetCategoriesAsync());

        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest req)
        {
            var cat = await adminService.CreateCategoryAsync(req);
            return CreatedAtAction(nameof(GetCategories), new { id = cat.Id }, cat);
        }

        [HttpDelete("categories/{id:int}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try { await adminService.DeleteCategoryAsync(id); return NoContent(); }
            catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
        }

        // ── Disputes (consultation completion disputes) ───────────────────────

        /// <summary>Consultation disputes raised by customers. statusFilter: open | ResolvedReleased | ResolvedRefunded | all</summary>
        [HttpGet("disputes")]
        public async Task<IActionResult> GetDisputes([FromQuery] string? status = null)
            => Ok(await adminService.GetDisputesAsync(status));

        /// <summary>Resolve a dispute. Body: { resolution: "Release" | "Refund", notes? }</summary>
        [HttpPost("disputes/{id:guid}/resolve")]
        public async Task<IActionResult> ResolveDispute(Guid id, [FromBody] Shared.DTO_s.ResolveDisputeRequest req)
        {
            try { await adminService.ResolveDisputeAsync(id, req); return NoContent(); }
            catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
            catch (Exception e) { return BadRequest(new { message = e.Message }); }
        }

        // ── Financials ─────────────────────────────────────────────────────────

        [HttpGet("financials/overview")]
        public async Task<IActionResult> GetFinancialOverview()
        {
            try { return Ok(await financialsService.GetOverviewAsync()); }
            catch (Exception e) { return BadRequest(new { message = e.Message }); }
        }

        [HttpGet("financials/wallets")]
        public async Task<IActionResult> GetWallets(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try { return Ok(await financialsService.GetWalletsAsync(page, pageSize)); }
            catch (Exception e) { return BadRequest(new { message = e.Message }); }
        }

        [HttpPatch("financials/wallets/{id:int}")]
        public async Task<IActionResult> AdjustWallet(
            int id, [FromBody] AdjustWalletRequest req)
        {
            try { await financialsService.AdjustWalletAsync(id, req.Amount, req.Reason); return NoContent(); }
            catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
            catch (Exception e) { return BadRequest(new { message = e.Message }); }
        }

        [HttpGet("financials/transactions")]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
        {
            try { return Ok(await financialsService.GetTransactionsAsync(page, pageSize)); }
            catch (Exception e) { return BadRequest(new { message = e.Message }); }
        }

        [HttpPost("financials/payouts/{consultantId:int}")]
        public async Task<IActionResult> InitiatePayout(int consultantId)
        {
            try { await financialsService.InitiatePayoutAsync(consultantId); return NoContent(); }
            catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
            catch (Exception e) { return BadRequest(new { message = e.Message }); }
        }

        // ── Platform Settings ──────────────────────────────────────────────────

        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            try { return Ok(await settingsService.GetAllAsync()); }
            catch (Exception e) { return BadRequest(new { message = e.Message }); }
        }

        [HttpPatch("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] Dictionary<string, string> updates)
        {
            try { await settingsService.UpdateBulkAsync(updates); return NoContent(); }
            catch (Exception e) { return BadRequest(new { message = e.Message }); }
        }

        [HttpPost("settings/test-email")]
        public async Task<IActionResult> TestEmail()
        {
            try
            {
                var adminEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                              ?? "admin@agrichub.io";
                await emailService.SendVerificationEmailAsync(
                    adminEmail, "Admin", "https://agrichub.io/test");
                return Ok(new { message = "Test email sent successfully." });
            }
            catch (Exception e)
            {
                // Walk the full exception chain for the real cause
                var messages = new List<string>();
                var ex = e;
                while (ex != null)
                {
                    messages.Add(ex.Message);
                    ex = ex.InnerException;
                }
                return BadRequest(new { message = string.Join(" → ", messages) });
            }
        }
    }

    // ── Request DTOs (inline — move to Shared if preferred) ───────────────────
    public record AdjustWalletRequest(decimal Amount, string Reason);
}
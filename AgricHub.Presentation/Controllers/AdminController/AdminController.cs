// AgricHub.Presentation/Controllers/AdminController/AdminController.cs
using AgricHub.BLL.Interfaces;
using AgricHub.BLL.Interfaces.IAdminService;
using AgricHub.DAL.Entities;
using AgricHub.Shared.DTO_s;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace AgricHub.Presentation.Controllers.AdminController
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController(
        IAdminService adminService,
        IAdminFinancialsService financialsService,
        IPlatformSettingsService settingsService,
        IEmailService emailService,
        UserManager<ApplicationUser> userManager) : ControllerBase
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

        /// <summary>Suspend a user's account (consultant or customer). Body: { reason }</summary>
        [HttpPost("users/{userId}/suspend")]
        public async Task<IActionResult> SuspendUser(string userId, [FromBody] SuspendUserRequest req)
        {
            try { await adminService.SuspendUserAsync(userId, req.Reason); return NoContent(); }
            catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
            catch (InvalidOperationException e) { return BadRequest(new { message = e.Message }); }
        }

        /// <summary>Lift a suspension and restore normal account access.</summary>
        [HttpPost("users/{userId}/reinstate")]
        public async Task<IActionResult> ReinstateUser(string userId)
        {
            try { await adminService.ReinstateUserAsync(userId); return NoContent(); }
            catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
        }

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

        // ── My Account (self-service — the logged-in admin's OWN account only) ──
        // Deliberately scoped to self: reads the user ID from the caller's own
        // token, never a body-supplied target ID, so this can't be used to
        // change a DIFFERENT admin's email. Was previously only possible via
        // a raw SQL UPDATE against AspNetUsers — this replaces that with a
        // proper in-app form.
        [HttpGet("account")]
        public async Task<IActionResult> GetMyAccount()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var me = userId != null ? await userManager.FindByIdAsync(userId) : null;
            if (me == null) return NotFound(new { message = "Account not found." });

            return Ok(new { email = me.Email, userName = me.UserName, firstName = me.FirstName, lastName = me.LastName });
        }

        [HttpPatch("account/email")]
        public async Task<IActionResult> UpdateMyEmail([FromBody] UpdateAdminEmailRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.NewEmail) || !req.NewEmail.Contains('@'))
                return BadRequest(new { message = "Please provide a valid email address." });

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var me = userId != null ? await userManager.FindByIdAsync(userId) : null;
            if (me == null) return NotFound(new { message = "Account not found." });

            // Same normalization convention AdminSeeder itself uses (.ToUpper()),
            // not a different Identity-internal utility — keeps this consistent
            // with how the account was originally created.
            var normalized = req.NewEmail.Trim().ToUpper();

            var existing = await userManager.FindByEmailAsync(req.NewEmail.Trim());
            if (existing != null && existing.Id != me.Id)
                return BadRequest(new { message = "That email is already in use by another account." });

            me.Email = req.NewEmail.Trim();
            me.NormalizedEmail = normalized;
            me.EmailConfirmed = true; // admin-changed, no need to re-verify via link

            var result = await userManager.UpdateAsync(me);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join(", ", result.Errors.Select(e => e.Description)) });

            return Ok(new { message = "Email updated. Sign out and back in for it to take effect everywhere (including your login session).", email = me.Email });
        }

        [HttpPost("settings/test-email")]
        public async Task<IActionResult> TestEmail()
        {
            try
            {
                // Was reading User.FindFirst(ClaimTypes.Email) — that claim was
                // never resolving (JWT claim-type mapping issue), so this always
                // silently fell through to the hardcoded "admin@agrichub.io"
                // literal below, completely ignoring both the real logged-in
                // user AND any database changes to their email. Looking the
                // user up fresh by ID (from the token's NameIdentifier, which
                // reliably resolves) guarantees this always reflects the
                // actual current database value, no matter what the JWT
                // does or doesn't contain.
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var currentAdmin = userId != null ? await userManager.FindByIdAsync(userId) : null;

                var adminEmail = currentAdmin?.Email
                              ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                              ?? "admin@agrichub.io"; // last-resort fallback only if truly nothing else resolves

                // Was hardcoded to "https://agrichub.io/test" — a path that was
                // never a real route, so clicking the button in the test email
                // just landed on a broken page. This is only a connectivity
                // check (no real user/token exists for this send), so the link
                // can't point to an actual verification flow either way — but
                // it should at least go somewhere real instead of dead. Points
                // to your actual configured platform.url now.
                var platformUrl = await settingsService.GetAsync("platform.url");
                var testLink = string.IsNullOrWhiteSpace(platformUrl) ? "https://agrichub.io" : platformUrl.TrimEnd('/');

                await emailService.SendVerificationEmailAsync(
                    adminEmail, "Admin", testLink);
                return Ok(new { message = $"Test email sent to {adminEmail}." });
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
    public record UpdateAdminEmailRequest(string NewEmail);
}
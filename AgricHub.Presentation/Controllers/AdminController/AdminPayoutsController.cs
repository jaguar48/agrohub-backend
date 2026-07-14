using AgricHub.BLL.Interfaces.IWalletService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgricHub.Presentation.Controllers.AdminController
{
    /// <summary>
    /// Manual payout approval — every consultant payout request now lands here
    /// as "PendingApproval" instead of auto-transferring via Paystack the moment
    /// bank details are on file. An admin must explicitly approve (which fires
    /// the real Paystack transfer) or reject (which refunds the wallet).
    /// </summary>
    [ApiController]
    [Route("api/admin/payouts")]
    [Authorize(Roles = "Admin")]
    public class AdminPayoutsController(IWalletService walletService) : ControllerBase
    {
        [HttpGet("pending")]
        public async Task<IActionResult> GetPending()
        {
            try { return Ok(new { success = true, data = await walletService.GetPendingPayoutsAsync() }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{transactionId}/approve")]
        public async Task<IActionResult> Approve(int transactionId)
        {
            try { await walletService.ApprovePayoutAsync(transactionId); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{transactionId}/reject")]
        public async Task<IActionResult> Reject(int transactionId, [FromBody] string reason)
        {
            try { await walletService.RejectPayoutAsync(transactionId, reason); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }
    }
}

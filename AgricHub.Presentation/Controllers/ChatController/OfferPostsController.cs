using AgricHub.BLL.Interfaces.IChatServices;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace AgricHub.Presentation.Controllers.ChatController
{
    /// <summary>
    /// Fiverr-style custom offer flow: customer posts a request, consultants
    /// browse and pitch, customer picks one. Every pitch also appears in the
    /// customer↔consultant chat as a card (same visual pattern as the older
    /// single-consultant custom offer).
    /// </summary>
    [ApiController]
    [Route("api/offer-posts")]
    [Authorize]
    public class OfferPostsController(IOfferPostService svc) : ControllerBase
    {
        // ── Posts (customer) ────────────────────────────────────────────────

        [HttpPost]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CreatePost([FromBody] CreateOfferPostRequest request)
        {
            try { return Ok(new { success = true, data = await svc.CreatePostAsync(request) }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        /// <summary>Open requests — the consultant-facing browse feed.</summary>
        [HttpGet("open")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GetOpen([FromQuery] int? categoryId)
        {
            try { return Ok(new { success = true, data = await svc.GetOpenPostsAsync(categoryId) }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpGet("mine")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMine()
        {
            try { return Ok(new { success = true, data = await svc.GetMyPostsAsync() }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpGet("{postId}")]
        public async Task<IActionResult> GetDetail(Guid postId)
        {
            try { return Ok(new { success = true, data = await svc.GetPostDetailAsync(postId) }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("{postId}/close")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Close(Guid postId)
        {
            try { await svc.ClosePostAsync(postId); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        // ── Pitches (consultant) ────────────────────────────────────────────

        /// <summary>Submit a pitch. Multipart form — JSON fields + optional "portfolioFile".</summary>
        [HttpPost("pitches")]
        [Authorize(Roles = "Consultant")]
        [RequestSizeLimit(26_214_400)] // 25 MB
        public async Task<IActionResult> SubmitPitch([FromForm] SubmitPitchRequest request, IFormFile? portfolioFile)
        {
            try { return Ok(new { success = true, data = await svc.SubmitPitchAsync(request, portfolioFile) }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpGet("pitches/mine")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GetMyPitches()
        {
            try { return Ok(new { success = true, data = await svc.GetMyPitchesAsync() }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("pitches/{pitchId}/accept")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> AcceptPitch(Guid pitchId)
        {
            try { return Ok(new { success = true, data = await svc.AcceptPitchAsync(pitchId) }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }

        [HttpPost("pitches/{pitchId}/reject")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> RejectPitch(Guid pitchId, [FromBody] string reason)
        {
            try { return Ok(new { success = true, data = await svc.RejectPitchAsync(pitchId, reason) }); }
            catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
        }
    }
}

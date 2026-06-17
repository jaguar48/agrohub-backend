using AgricHub.BLL.Interfaces;
using AgricHub.BLL.Interfaces.IChatServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgricHub.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IRepository<Consultant> _consultantRepo;
        private readonly IRepository<Customer> _customerRepo;
        private readonly IUnitOfWork _unitOfWork;

        public ChatController(
            IChatService chatService,
            IUnitOfWork unitOfWork)
        {
            _chatService    = chatService;
            _unitOfWork     = unitOfWork;
            _consultantRepo = _unitOfWork.GetRepository<Consultant>();
            _customerRepo   = _unitOfWork.GetRepository<Customer>();
        }

        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        // ── Chat endpoints (unchanged) ─────────────────────────────────────

        [HttpPost("initiate")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> InitiateChat([FromBody] InitiateChatRequest request)
        {
            try
            {
                var channelUrl = await _chatService.InitiateChatAsync(request);
                return Ok(new { success = true, channelUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("custom-offer")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> CreateCustomOffer([FromBody] CustomOfferRequest request)
        {
            try
            {
                var offer = await _chatService.CreateCustomOfferAsync(request);
                return Ok(new { success = true, offer });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("custom-offer/{offerId}/accept")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> AcceptCustomOffer(Guid offerId)
        {
            try
            {
                var offer = await _chatService.AcceptCustomOfferAsync(offerId);
                return Ok(new { success = true, offer });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("custom-offer/{offerId}/reject")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> RejectCustomOffer(Guid offerId, [FromBody] string reason)
        {
            try
            {
                var offer = await _chatService.RejectCustomOfferAsync(offerId, reason);
                return Ok(new { success = true, offer });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("my-chats")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMyChats()
        {
            try
            {
                var chats = await _chatService.GetMyChatsAsync();
                return Ok(new { success = true, chats });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("consultant-chats")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GetConsultantChats()
        {
            try
            {
                var chats = await _chatService.GetConsultantChatsAsync();
                return Ok(new { success = true, chats });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ── Presence endpoints ─────────────────────────────────────────────

        /// <summary>Mark current user as online. Angular pings this every 30s.</summary>
        [HttpPost("presence/online")]
        [Authorize]
        public async Task<IActionResult> PresenceOnline()
        {
            await _UpdatePresence(GetUserId(), online: true);
            return Ok(new { success = true });
        }

        /// <summary>Mark current user as offline. Called on tab close / logout.</summary>
        [HttpPost("presence/offline")]
        [Authorize]
        public async Task<IActionResult> PresenceOffline()
        {
            await _UpdatePresence(GetUserId(), online: false);
            return Ok(new { success = true });
        }

        /// <summary>Get presence status of any user by their ASP.NET Identity userId.</summary>
        [HttpGet("presence/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetPresence(string userId)
        {
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId);
            if (consultant != null)
                return Ok(new { online = consultant.IsOnline, lastSeenAt = consultant.LastSeenAt });

            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId);
            if (customer != null)
                return Ok(new { online = customer.IsOnline, lastSeenAt = customer.LastSeenAt });

            return Ok(new { online = false, lastSeenAt = (DateTime?)null });
        }

        // ── Private helper ─────────────────────────────────────────────────

        private async Task _UpdatePresence(string userId, bool online)
        {
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId);
            if (consultant != null)
            {
                consultant.IsOnline   = online;
                consultant.LastSeenAt = DateTime.UtcNow;
                _consultantRepo.Update(consultant);
                await _unitOfWork.SaveChangesAsync();
                return;
            }

            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId);
            if (customer != null)
            {
                customer.IsOnline   = online;
                customer.LastSeenAt = DateTime.UtcNow;
                _customerRepo.Update(customer);
                await _unitOfWork.SaveChangesAsync();
            }
        }
    }
}
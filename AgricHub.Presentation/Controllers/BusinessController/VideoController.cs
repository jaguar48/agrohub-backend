using AgricHub.BLL.Interfaces.IChatServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AgricHub.Presentation.Controllers.BusinessController
{
    [ApiController]
    [Route("api/agrichub/video")]
    [Authorize]
    public class VideoController : ControllerBase
    {
        private readonly IDailyService _dailyService;
        private readonly IRepository<ChatSession> _chatSessionRepo;
        private readonly IRepository<Consultation> _consultationRepo;

        public VideoController(IDailyService dailyService, IUnitOfWork unitOfWork)
        {
            _dailyService     = dailyService;
            _chatSessionRepo  = unitOfWork.GetRepository<ChatSession>();
            _consultationRepo = unitOfWork.GetRepository<Consultation>();
        }

        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        [HttpPost("create-room")]
        [SwaggerOperation(
            "Creates (or reuses) a Daily.co video room tied to a chat session's active booking, " +
            "and returns a room URL + meeting token for use with Daily's Call Object SDK " +
            "(not the Prebuilt iframe). Either party may call this — the consultant always " +
            "receives owner controls (mute/kick), the customer always receives a standard " +
            "participant token, regardless of who started the call. Only available while the " +
            "underlying consultation is Approved or InProgress.")]
        public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
        {
            try
            {
                if (request.ChatSessionId == Guid.Empty)
                    return BadRequest(new { success = false, message = "chatSessionId is required." });

                var userId = GetUserId();

                var chatSession = await _chatSessionRepo.GetSingleByAsync(
                    cs => cs.Id == request.ChatSessionId,
                    include: q => q.Include(cs => cs.Customer).Include(cs => cs.Consultant))
                    ?? throw new KeyNotFoundException("Chat session not found.");

                var isConsultant = chatSession.Consultant?.UserId == userId;
                var isCustomer = chatSession.Customer?.UserId == userId;
                if (!isConsultant && !isCustomer)
                    return Forbid();

                // ── Only allow video calls for active (Approved/InProgress) bookings ──
                var candidates = await _consultationRepo.GetByAsync(c =>
                    c.CustomerId == chatSession.CustomerId &&
                    c.ConsultantId == chatSession.ConsultantId &&
                    (c.Status == "Approved" || c.Status == "InProgress"));

                var activeConsultation = candidates
                    .OrderByDescending(c => c.Status == "InProgress")
                    .ThenBy(c => c.ScheduledAt)
                    .FirstOrDefault();

                if (activeConsultation == null)
                    return BadRequest(new
                    {
                        success = false,
                        message = "Video calls are only available for confirmed, active bookings. " +
                                  "This conversation doesn't have an approved or in-progress session right now."
                    });

                var roomName = "agrichub-" + request.ChatSessionId.ToString("N");
                var roomUrl = await _dailyService.CreateRoomAsync(roomName);

                // ── The consultant ALWAYS gets owner controls, regardless of who
                // clicked "start call" first. ──────────────────────────────────────
                var userName = isConsultant
                    ? $"{chatSession.Consultant!.FirstName} {chatSession.Consultant.LastName}"
                    : $"{chatSession.Customer!.FirstName} {chatSession.Customer.LastName}";

                var token = await _dailyService.CreateMeetingTokenAsync(
                    roomName, isOwner: isConsultant, userName: userName);

                // ── CHANGED: url and token returned SEPARATELY (not concatenated)
                // for Daily's Call Object SDK, which takes them as distinct fields
                // in callObject.join({ url, token }) — matches Daily's own usage
                // pattern, no string-concatenation fragility. ─────────────────────
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        url = roomUrl,
                        token,
                        isOwner = isConsultant,
                        userName,
                    }
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }

    public class CreateRoomRequest
    {
        /// <summary>The chat session this call belongs to — used to verify there's an
        /// active booking and to derive a stable, reusable room name.</summary>
        public Guid ChatSessionId { get; set; }
    }
}
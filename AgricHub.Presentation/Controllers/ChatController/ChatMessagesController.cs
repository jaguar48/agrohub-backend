using AgricHub.BLL.Hubs;
using AgricHub.BLL.Interfaces;
using AgricHub.Contracts;
using AgricHub.DAL.Entities.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
namespace AgricHub.Presentation.Controllers.ChatController
{
    /// <summary>
    /// SignalR chat support endpoints: message history (replaces Sendbird's
    /// createPreviousMessageListQuery), presence lookup, and file messages.
    /// </summary>
    [ApiController]
    [Route("api/chat-messages")]
    [Authorize]
    public class ChatMessagesController(
        IUnitOfWork uow,
        IHubContext<ChatHub> hub,
        IStorageService storage) : ControllerBase
    {
        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        /// <summary>Last {limit} messages of a channel, oldest → newest.</summary>
        [HttpGet("{channelUrl}")]
        public async Task<IActionResult> GetHistory(string channelUrl, [FromQuery] int limit = 50)
        {
            // Authorisation: user must be a participant. New-format channels
            // ("Chat_*", "notif-*") check this via a cheap substring match;
            // legacy Sendbird-format channels fall back to a DB lookup against
            // whichever Consultation/ChatSession record actually owns this URL —
            // see ChatHub.IsParticipantAsync for why a plain .Contains(UserId)
            // check can never work for those (this endpoint used to have its
            // own duplicate of that broken check, causing the 403s on old chats).
            if (!await ChatHub.IsParticipantAsync(uow, UserId, channelUrl))
                return Forbid();
            var repo = uow.GetRepository<ChatMessage>();
            var items = await repo.GetAllAsync(
                m => m.ChannelUrl == channelUrl,
                orderBy: q => q.OrderByDescending(m => m.CreatedAt));
            var dto = items
                .Take(limit)
                .OrderBy(m => m.CreatedAt)
                .Select(ChatHub.ToDto);
            return Ok(dto);
        }
        /// <summary>Presence for one user (replaces Sendbird connectionStatus / lastSeenAt).</summary>
        [HttpGet("presence/{userId}")]
        public async Task<IActionResult> GetPresence(string userId)
        {
            var repo = uow.GetRepository<UserPresence>();
            var p = await repo.GetSingleByAsync(x => x.UserId == userId);
            return Ok(new
            {
                userId,
                isOnline = p?.IsOnline ?? false,
                lastSeenAt = p == null
                    ? (long?)null
                    : new DateTimeOffset(DateTime.SpecifyKind(p.LastSeenAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
            });
        }
        /// <summary>
        /// File message: uploads to storage (Cloudinary), persists a FILE message,
        /// and broadcasts it to the channel group.
        /// </summary>
        [HttpPost("file")]
        [RequestSizeLimit(26_214_400)] // 25 MB
        public async Task<IActionResult> SendFile([FromForm] IFormFile file, [FromForm] string channelUrl, [FromForm] string? senderName)
        {
            if (file == null || file.Length == 0) return BadRequest(new { message = "No file provided." });
            if (string.IsNullOrWhiteSpace(channelUrl) || !await ChatHub.IsParticipantAsync(uow, UserId, channelUrl))
                return Forbid();
            string url;
            await using (var stream = file.OpenReadStream())
                url = await storage.UploadAsync(stream, file.FileName, "agrichub/chat");
            var repo = uow.GetRepository<ChatMessage>();
            var msg = new ChatMessage
            {
                ChannelUrl  = channelUrl,
                SenderId    = UserId,
                SenderName  = senderName ?? UserId,
                MessageType = "file",
                Message     = file.FileName,
                FileUrl     = url,
                FileName    = file.FileName,
                FileMime    = file.ContentType,
                CreatedAt   = DateTime.UtcNow,
            };
            await repo.AddAsync(msg);
            await uow.SaveChangesAsync();
            await hub.Clients.Groups(ChatHub.ParticipantGroups(channelUrl)).SendAsync("messageReceived", ChatHub.ToDto(msg));
            return Ok(ChatHub.ToDto(msg));
        }
    }
}
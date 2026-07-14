//using AgricHub.BLL.Hubs;
//using AgricHub.BLL.Interfaces.ChatServices;
//using AgricHub.Contracts;
//using AgricHub.DAL.Entities.Models;
//using AgricHub.Shared.DTO_s.Request;
//using CloudinaryDotNet;
//using Microsoft.AspNetCore.SignalR;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Text.Json;
//using System.Threading.Tasks;

//namespace AgricHub.BLL.Implementations.ChatServices
//{

//    public class SignalRChatService : ISendbirdService
//    {
//        private readonly IUnitOfWork _uow;
//        private readonly IHubContext<ChatHub> _hub;
//        private readonly ILogger<SignalRChatService> _logger;

//        public SignalRChatService(
//            IUnitOfWork uow,
//            IHubContext<ChatHub> hub,
//            ILogger<SignalRChatService> logger)
//        {
//            _uow    = uow;
//            _hub    = hub;
//            _logger = logger;
//        }

//        // ── User management — no external service, so these are trivial ─────────
//        public Task<string> CreateSendbirdUserAsync() => Task.FromResult("ok");
//        public Task<string> CreateSendbirdUserAsync(string userId, string nickname) => Task.FromResult(userId);
//        public Task<string> EnsureSendbirdUserAsync(string userId, string nickname) => Task.FromResult(userId);

//        // ── Channels are just deterministic string keys now ─────────────────────
//        public Task<string> CreateGroupChannelAsync(string agropreneurUserId, string consultantUserId) =>
//            Task.FromResult($"Chat_{agropreneurUserId}_{consultantUserId}");

//        public async Task<string> GetExistingChannelAsync(string userId1, string userId2)
//        {
//            // A channel "exists" if any message was ever stored under either ordering
//            var repo = _uow.GetRepository<ChatMessage>();
//            var a = $"Chat_{userId1}_{userId2}";
//            var b = $"Chat_{userId2}_{userId1}";
//            if (await repo.AnyAsync(m => m.ChannelUrl == a)) return a;
//            if (await repo.AnyAsync(m => m.ChannelUrl == b)) return b;
//            return string.Empty;
//        }

//        public Task<string> CreateNotificationChannelAsync(string userId, string nickname) =>
//            Task.FromResult($"notif-{userId}");

//        // ── Display name resolution ──────────────────────────────────────────────
//        // The old Sendbird implementation resolved sender nicknames from Sendbird's
//        // own user directory (registered via EnsureSendbirdUserAsync/CreateSendbirdUserAsync
//        // with an actual nickname). This service has no such directory — so instead of
//        // ever echoing a raw ApplicationUser.Id (a GUID) back as a "name", we resolve it
//        // against our own Customer/Consultant tables. Falls back to the raw ID only if
//        // truly unresolvable, so nothing throws.
//        private async Task<string> ResolveDisplayNameAsync(string userId)
//        {
//            if (string.IsNullOrWhiteSpace(userId)) return "Someone";

//            var customer = await _uow.GetRepository<Customer>().GetSingleByAsync(c => c.UserId == userId);
//            if (customer != null)
//            {
//                var name = $"{customer.FirstName} {customer.LastName}".Trim();
//                if (!string.IsNullOrWhiteSpace(name)) return name;
//            }

//            var consultant = await _uow.GetRepository<Consultant>().GetSingleByAsync(c => c.UserId == userId);
//            if (consultant != null)
//            {
//                var name = $"{consultant.FirstName} {consultant.LastName}".Trim();
//                if (!string.IsNullOrWhiteSpace(name)) return name;
//            }

//            _logger.LogWarning("[SignalR] Could not resolve display name for userId={UserId} — falling back to raw ID.", userId);
//            return userId;
//        }

//        // ── Bell notification: persist + push to the user's personal group ──────
//        public async Task SendNotificationAsync(string userId, string message, string type, object? data = null)
//        {
//            if (string.IsNullOrWhiteSpace(userId)) return;
//            try
//            {
//                var repo = _uow.GetRepository<ChatMessage>();
//                var msg = new ChatMessage
//                {
//                    ChannelUrl  = $"notif-{userId}",
//                    SenderId    = null,
//                    SenderName  = "AgricHub",
//                    MessageType = "ADMM",
//                    Message     = message,
//                    CustomType  = type,
//                    Data        = JsonSerializer.Serialize(new { type, payload = data, timestamp = DateTime.UtcNow }),
//                    CreatedAt   = DateTime.UtcNow,
//                };
//                await repo.AddAsync(msg);
//                await _uow.SaveChangesAsync();

//                await _hub.Clients.Group($"user-{userId}").SendAsync("notification", new
//                {
//                    message,
//                    type,
//                    createdAt = new DateTimeOffset(msg.CreatedAt).ToUnixTimeMilliseconds(),
//                });
//            }
//            catch (Exception ex)
//            {
//                // Notifications must never break a business action
//                _logger.LogError(ex, "[SignalR] Notification failed for {UserId}, type={Type}", userId, type);
//            }
//        }

//        // ── Chat message from a user ─────────────────────────────────────────────
//        public async Task SendMessageAsync(string channelUrl, string senderUserId, string message,
//            bool isSystemMessage = false, object? data = null)
//        {
//            if (string.IsNullOrWhiteSpace(message))
//                throw new ArgumentException("Message cannot be empty", nameof(message));
//            if (string.IsNullOrWhiteSpace(channelUrl))
//            {
//                _logger.LogWarning("[SignalR] SendMessage: empty channelUrl — skipping.");
//                return;
//            }

//            var repo = _uow.GetRepository<ChatMessage>();
//            var msg = new ChatMessage
//            {
//                ChannelUrl  = channelUrl,
//                // System/booking-narrated messages have no real sender — store null so
//                // the frontend's "system" rendering path (customType) kicks in cleanly.
//                SenderId    = isSystemMessage ? null : senderUserId,
//                SenderName  = isSystemMessage ? "AgricHub" : await ResolveDisplayNameAsync(senderUserId),
//                MessageType = "MESG",
//                Message     = message,
//                CustomType  = isSystemMessage ? "system" : "user",
//                Data        = data != null ? JsonSerializer.Serialize(data) : null,
//                CreatedAt   = DateTime.UtcNow,
//            };
//            await repo.AddAsync(msg);
//            await _uow.SaveChangesAsync();
//            await _hub.Clients.Groups(ChatHub.ParticipantGroups(channelUrl)).SendAsync("messageReceived", ChatHub.ToDto(msg));
//        }

//        // ── Admin/system message in a chat channel ──────────────────────────────
//        public async Task SendAdminMessageAsync(string channelUrl, string message, object? data = null)
//        {
//            if (string.IsNullOrWhiteSpace(channelUrl))
//            {
//                _logger.LogWarning("[SignalR] SendAdminMessage: empty channelUrl — skipping.");
//                return;
//            }

//            var repo = _uow.GetRepository<ChatMessage>();
//            var msg = new ChatMessage
//            {
//                ChannelUrl  = channelUrl,
//                SenderId    = null,
//                SenderName  = "AgricHub",
//                MessageType = "ADMM",
//                Message     = message,
//                CustomType  = "system",
//                Data        = data != null ? JsonSerializer.Serialize(data) : null,
//                CreatedAt   = DateTime.UtcNow,
//            };
//            await repo.AddAsync(msg);
//            await _uow.SaveChangesAsync();
//            await _hub.Clients.Groups(ChatHub.ParticipantGroups(channelUrl)).SendAsync("messageReceived", ChatHub.ToDto(msg));
//        }

//        // ── Bell history straight from the DB — no timeouts, no cache needed ────
//        public async Task<List<NotificationHistoryItem>> GetNotificationHistoryAsync(string userId, int limit = 30)
//        {
//            var repo = _uow.GetRepository<ChatMessage>();
//            var items = await repo.GetAllAsync(
//                m => m.ChannelUrl == $"notif-{userId}",
//                orderBy: q => q.OrderByDescending(m => m.CreatedAt));

//            return items
//                .Take(limit)
//                .Select(m => new NotificationHistoryItem
//                {
//                    Message   = m.Message,
//                    Type      = m.CustomType ?? "info",
//                    CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(m.CreatedAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
//                })
//                .ToList();
//        }
//    }
//}


using AgricHub.BLL.Hubs;
using AgricHub.BLL.Interfaces;
using AgricHub.BLL.Interfaces.ChatServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AgricHub.BLL.Implementations.ChatServices
{
    /// <summary>
    /// DROP-IN replacement for SendbirdService. Implements the SAME ISendbirdService
    /// interface, so ConsultationService / ChatService / WalletService need ZERO changes.
    /// Messages persist to the ChatMessages table and broadcast through ChatHub.
    ///
    /// Swap in ServiceExtension.cs:
    ///   services.AddScoped&lt;ISendbirdService, SignalRChatService&gt;();
    /// Revert to Sendbird by flipping this one line back.
    /// </summary>
    public class SignalRChatService : ISendbirdService
    {
        private readonly IUnitOfWork _uow;
        private readonly IHubContext<ChatHub> _hub;
        private readonly ILogger<SignalRChatService> _logger;
        private readonly IPlatformSettingsService _settings;

        public SignalRChatService(
            IUnitOfWork uow,
            IHubContext<ChatHub> hub,
            ILogger<SignalRChatService> logger,
            IPlatformSettingsService settings)
        {
            _uow      = uow;
            _hub      = hub;
            _logger   = logger;
            _settings = settings;
        }

        // ── User management — no external service, so these are trivial ─────────
        public Task<string> CreateSendbirdUserAsync() => Task.FromResult("ok");
        public Task<string> CreateSendbirdUserAsync(string userId, string nickname) => Task.FromResult(userId);
        public Task<string> EnsureSendbirdUserAsync(string userId, string nickname) => Task.FromResult(userId);

        // ── Channels are just deterministic string keys now ─────────────────────
        public Task<string> CreateGroupChannelAsync(string agropreneurUserId, string consultantUserId) =>
            Task.FromResult($"Chat_{agropreneurUserId}_{consultantUserId}");

        public async Task<string> GetExistingChannelAsync(string userId1, string userId2)
        {
            // A channel "exists" if any message was ever stored under either ordering
            var repo = _uow.GetRepository<ChatMessage>();
            var a = $"Chat_{userId1}_{userId2}";
            var b = $"Chat_{userId2}_{userId1}";
            if (await repo.AnyAsync(m => m.ChannelUrl == a)) return a;
            if (await repo.AnyAsync(m => m.ChannelUrl == b)) return b;
            return string.Empty;
        }

        public Task<string> CreateNotificationChannelAsync(string userId, string nickname) =>
            Task.FromResult($"notif-{userId}");

        // ── Bell notification: persist + push to the user's personal group ──────
        public async Task SendNotificationAsync(string userId, string message, string type, object? data = null)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;

            // ── Global in-app notification kill switch (features.inAppNotifications) ──
            // Was seeded but never checked — every notification call across the whole
            // app (bookings, wallet, disputes, offers, pitches…) always persisted and
            // broadcast regardless of this toggle.
            var enabledRaw = await _settings.GetAsync("features.inAppNotifications");
            var enabled = enabledRaw == null || enabledRaw == "true";
            if (!enabled)
            {
                _logger.LogDebug("[SignalR] Notification skipped (features.inAppNotifications is off) — {Type} for {UserId}", type, userId);
                return;
            }

            try
            {
                var repo = _uow.GetRepository<ChatMessage>();
                var msg = new ChatMessage
                {
                    ChannelUrl  = $"notif-{userId}",
                    SenderId    = null,
                    SenderName  = "AgricHub",
                    MessageType = "ADMM",
                    Message     = message,
                    CustomType  = type,
                    Data        = JsonSerializer.Serialize(new { type, payload = data, timestamp = DateTime.UtcNow }),
                    CreatedAt   = DateTime.UtcNow,
                };
                await repo.AddAsync(msg);
                await _uow.SaveChangesAsync();

                await _hub.Clients.Group($"user-{userId}").SendAsync("notification", new
                {
                    message,
                    type,
                    createdAt = new DateTimeOffset(msg.CreatedAt).ToUnixTimeMilliseconds(),
                });
            }
            catch (Exception ex)
            {
                // Notifications must never break a business action
                _logger.LogError(ex, "[SignalR] Notification failed for {UserId}, type={Type}", userId, type);
            }
        }

        // ── Chat message from a user ─────────────────────────────────────────────
        public async Task SendMessageAsync(string channelUrl, string senderUserId, string message,
            bool isSystemMessage = false, object? data = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message cannot be empty", nameof(message));
            if (string.IsNullOrWhiteSpace(channelUrl))
            {
                _logger.LogWarning("[SignalR] SendMessage: empty channelUrl — skipping.");
                return;
            }

            var repo = _uow.GetRepository<ChatMessage>();
            var msg = new ChatMessage
            {
                ChannelUrl  = channelUrl,
                SenderId    = senderUserId,
                SenderName  = senderUserId,
                MessageType = "MESG",
                Message     = message,
                CustomType  = isSystemMessage ? "system" : "user",
                Data        = data != null ? JsonSerializer.Serialize(data) : null,
                CreatedAt   = DateTime.UtcNow,
            };
            await repo.AddAsync(msg);
            await _uow.SaveChangesAsync();
            await _hub.Clients.Groups(ChatHub.ParticipantGroups(channelUrl)).SendAsync("messageReceived", ChatHub.ToDto(msg));
        }

        // ── Admin/system message in a chat channel ──────────────────────────────
        public async Task SendAdminMessageAsync(string channelUrl, string message, object? data = null)
        {
            if (string.IsNullOrWhiteSpace(channelUrl))
            {
                _logger.LogWarning("[SignalR] SendAdminMessage: empty channelUrl — skipping.");
                return;
            }

            var repo = _uow.GetRepository<ChatMessage>();
            var msg = new ChatMessage
            {
                ChannelUrl  = channelUrl,
                SenderId    = null,
                SenderName  = "AgricHub",
                MessageType = "ADMM",
                CustomType  = "system",
                Message     = message,
                Data        = data != null ? JsonSerializer.Serialize(data) : null,
                CreatedAt   = DateTime.UtcNow,
            };
            await repo.AddAsync(msg);
            await _uow.SaveChangesAsync();
            await _hub.Clients.Groups(ChatHub.ParticipantGroups(channelUrl)).SendAsync("messageReceived", ChatHub.ToDto(msg));
        }

        // ── Bell history straight from the DB — no timeouts, no cache needed ────
        public async Task<List<NotificationHistoryItem>> GetNotificationHistoryAsync(string userId, int limit = 30)
        {
            var repo = _uow.GetRepository<ChatMessage>();
            var items = await repo.GetAllAsync(
                m => m.ChannelUrl == $"notif-{userId}",
                orderBy: q => q.OrderByDescending(m => m.CreatedAt));

            return items
                .Take(limit)
                .Select(m => new NotificationHistoryItem
                {
                    Message   = m.Message,
                    Type      = m.CustomType ?? "info",
                    CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(m.CreatedAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
                })
                .ToList();
        }
    }
}

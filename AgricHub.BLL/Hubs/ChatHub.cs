using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace AgricHub.BLL.Hubs
{
    /// <summary>
    /// The real-time core — replaces Sendbird's WebSocket connection.
    /// Clients join channel groups (same string keys as before) and receive:
    ///   messageReceived(message)                      — new message in a joined channel
    ///   notification(payload)                         — bell notification for this user
    ///   typing(channelUrl, userName, isTyping)
    ///   readReceipt(channelUrl, messageIds, readerId)
    ///   presence(userId, isOnline, lastSeenAt)
    /// </summary>
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IUnitOfWork _uow;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMemoryCache _cache;

        public ChatHub(IUnitOfWork uow, IServiceScopeFactory scopeFactory, IMemoryCache cache)
        {
            _uow = uow;
            _scopeFactory = scopeFactory;
            _cache = cache;
        }

        private string UserId => Context.UserIdentifier
            ?? throw new HubException("Unauthenticated hub connection.");

        // ── Connection lifecycle → presence ────────────────────────────────────
        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{UserId}");

            var repo = _uow.GetRepository<UserPresence>();
            var p = await repo.GetSingleByAsync(x => x.UserId == UserId, tracking: true);
            if (p == null)
            {
                await repo.AddAsync(new UserPresence
                {
                    UserId = UserId,
                    IsOnline = true,
                    ConnectionCount = 1,
                    LastSeenAt = DateTime.UtcNow
                });
            }
            else
            {
                p.ConnectionCount++;
                p.IsOnline   = true;
                p.LastSeenAt = DateTime.UtcNow;
                repo.Update(p);
            }
            await _uow.SaveChangesAsync();
            await Clients.Others.SendAsync("presence", UserId, true, DateTime.UtcNow);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var repo = _uow.GetRepository<UserPresence>();
            var p = await repo.GetSingleByAsync(x => x.UserId == UserId, tracking: true);
            if (p != null)
            {
                p.ConnectionCount = Math.Max(0, p.ConnectionCount - 1);
                p.IsOnline        = p.ConnectionCount > 0;
                p.LastSeenAt      = DateTime.UtcNow;
                repo.Update(p);
                await _uow.SaveChangesAsync();
                if (!p.IsOnline)
                    await Clients.Others.SendAsync("presence", UserId, false, p.LastSeenAt);
            }
            await base.OnDisconnectedAsync(ex);
        }

        // ── Participant check (shared, static) ─────────────────────────────────
        public static async Task<bool> IsParticipantAsync(IUnitOfWork uow, string userId, string channelUrl)
        {
            if (string.IsNullOrWhiteSpace(channelUrl) || string.IsNullOrWhiteSpace(userId)) return false;

            if (channelUrl.StartsWith("notif-"))
                return channelUrl[6..] == userId;

            if (channelUrl.StartsWith("Chat_") && channelUrl.Contains(userId))
                return true;

            var consultationRepo = uow.GetRepository<Consultation>();
            var consultation = await consultationRepo.GetSingleByAsync(c => c.SendbirdChannelUrl == channelUrl);
            if (consultation != null)
                return await IsCustomerOrConsultantUserAsync(uow, userId, consultation.CustomerId, consultation.ConsultantId);

            var chatSessionRepo = uow.GetRepository<ChatSession>();
            var session = await chatSessionRepo.GetSingleByAsync(s => s.SendbirdChannelUrl == channelUrl);
            if (session != null)
                return await IsCustomerOrConsultantUserAsync(uow, userId, session.CustomerId, session.ConsultantId);

            return false;
        }

        private static async Task<bool> IsCustomerOrConsultantUserAsync(IUnitOfWork uow, string userId, int customerId, int consultantId)
        {
            var customerRepo = uow.GetRepository<Customer>();
            var customer = await customerRepo.GetSingleByAsync(c => c.Id == customerId);
            if (customer?.UserId == userId) return true;

            var consultantRepo = uow.GetRepository<Consultant>();
            var consultant = await consultantRepo.GetSingleByAsync(c => c.Id == consultantId);
            return consultant?.UserId == userId;
        }

        // ── Server-side display name resolution ─────────────────────────────────
        // The client used to supply its own display name on every SendMessage call.
        // That meant a stale/wrong localStorage value silently corrupted the sender
        // name shown to the other party (e.g. showing "greenfield" — a business
        // name — instead of the person's actual name), and nothing stopped a client
        // from sending any name it wanted. Resolving against Customer/Consultant
        // server-side makes the sender name authoritative and unspoofable.
        public static async Task<string> ResolveDisplayNameAsync(IUnitOfWork uow, string userId, string fallback)
        {
            if (string.IsNullOrWhiteSpace(userId)) return fallback;

            var customer = await uow.GetRepository<Customer>().GetSingleByAsync(c => c.UserId == userId);
            if (customer != null)
            {
                var name = $"{customer.FirstName} {customer.LastName}".Trim();
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }

            var consultant = await uow.GetRepository<Consultant>().GetSingleByAsync(c => c.UserId == userId);
            if (consultant != null)
            {
                var name = $"{consultant.FirstName} {consultant.LastName}".Trim();
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }

            return fallback;
        }

        // ── Channel membership ──────────────────────────────────────────────────
        public async Task JoinChannel(string channelUrl)
        {
            if (!await IsParticipantAsync(_uow, UserId, channelUrl))
                throw new HubException("Not a participant of this channel.");
            await Groups.AddToGroupAsync(Context.ConnectionId, channelUrl);
        }

        public Task LeaveChannel(string channelUrl) =>
            Groups.RemoveFromGroupAsync(Context.ConnectionId, channelUrl);

        // ── Send a user message ─────────────────────────────────────────────────
        // NOTE: senderName from the client is now IGNORED as the source of truth —
        // only used as a last-resort fallback if server-side resolution fails
        // (e.g. the userId matches neither a Customer nor a Consultant record).
        public async Task SendMessage(string channelUrl, string message, string? data, string senderName, string? customType = null)
        {
            if (string.IsNullOrWhiteSpace(message)) throw new HubException("Message cannot be empty.");
            if (!await IsParticipantAsync(_uow, UserId, channelUrl)) throw new HubException("Not a participant of this channel.");

            var resolvedName = await ResolveDisplayNameAsync(_uow, UserId, senderName);

            var repo = _uow.GetRepository<ChatMessage>();
            var msg = new ChatMessage
            {
                ChannelUrl  = channelUrl,
                SenderId    = UserId,
                SenderName  = resolvedName,
                MessageType = "MESG",
                Message     = message,
                CustomType  = customType,
                Data        = data,
                CreatedAt   = DateTime.UtcNow,
            };
            await repo.AddAsync(msg);
            await _uow.SaveChangesAsync();

            await Clients.Groups(ParticipantGroups(channelUrl)).SendAsync("messageReceived", ToDto(msg));

            // Email the recipient ONLY if they're offline — a live back-and-forth
            // between two online users should never trigger email per message.
            // Fire-and-forget with its own scope, since this Hub instance's scope
            // may be gone before the email send completes.
            _ = NotifyOfflineRecipientAsync(channelUrl, UserId, resolvedName, message);
        }

        /// <summary>
        /// If exactly one side of this channel is offline, emails them that a
        /// new message arrived — with a per-(channel,recipient) cooldown so a
        /// burst of messages while they're away sends one email, not one per
        /// message. Never throws into the caller; email failures here should
        /// never break message sending itself.
        /// </summary>
        private async Task NotifyOfflineRecipientAsync(string channelUrl, string senderUserId, string senderName, string messageText)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var recipientUserId = await ResolveOtherParticipantAsync(uow, channelUrl, senderUserId);
                if (string.IsNullOrEmpty(recipientUserId)) return;

                var presenceRepo = uow.GetRepository<UserPresence>();
                var presence = await presenceRepo.GetSingleByAsync(p => p.UserId == recipientUserId);
                var isOnline = presence?.IsOnline == true;
                if (isOnline) return; // they'll see it live — no email needed

                // Cooldown — one email per (channel, recipient) per 20 minutes,
                // regardless of how many messages arrive in that window.
                var cooldownKey = $"offline_email_sent:{channelUrl}:{recipientUserId}";
                if (_cache.TryGetValue(cooldownKey, out _)) return;
                _cache.Set(cooldownKey, true, TimeSpan.FromMinutes(20));

                var (recipientEmail, recipientIsConsultant) = await ResolveEmailAndRoleAsync(uow, recipientUserId);
                var recipientName = await ResolveDisplayNameAsync(uow, recipientUserId, "there");
                if (string.IsNullOrEmpty(recipientEmail)) return;

                var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var preview = messageText.Length > 140 ? messageText[..140] + "…" : messageText;
                // Route-correct link — matches app.component.ts's NOTIFICATION_META
                // pattern (/customer/messages vs /consultant/messages), not a
                // single guessed path.
                var messagesLink = recipientIsConsultant
                    ? "https://agrichub.io/consultant/messages"
                    : "https://agrichub.io/customer/messages";
                await email.SendGenericNotificationAsync(
                    recipientEmail, recipientName,
                    $"New message from {senderName}",
                    $"{senderName} sent you a message",
                    $"<p style=\"font-style:italic;color:#555\">\"{preview}\"</p><p>You were offline when this arrived — log in to reply.</p>",
                    "Open messages", messagesLink);
            }
            catch
            {
                // Never let an email failure surface here — message sending
                // itself already succeeded and returned to the caller.
            }
        }

        /// <summary>Given a channel and the sender, resolves the OTHER participant's
        /// userId — works for "Chat_{a}_{b}" format directly (no DB hit needed),
        /// falls back to the same Consultation/ChatSession lookup IsParticipantAsync
        /// uses for legacy-format channels.</summary>
        private static async Task<string?> ResolveOtherParticipantAsync(IUnitOfWork uow, string channelUrl, string senderUserId)
        {
            if (channelUrl.StartsWith("Chat_"))
            {
                var rest = channelUrl[5..];
                if (rest.Length >= 73)
                {
                    var a = rest[..36];
                    var b = rest[37..73];
                    return a == senderUserId ? b : (b == senderUserId ? a : null);
                }
            }

            var consultationRepo = uow.GetRepository<Consultation>();
            var consultation = await consultationRepo.GetSingleByAsync(c => c.SendbirdChannelUrl == channelUrl);
            if (consultation != null)
                return await ResolveOtherFromCustomerConsultantAsync(uow, senderUserId, consultation.CustomerId, consultation.ConsultantId);

            var chatSessionRepo = uow.GetRepository<ChatSession>();
            var session = await chatSessionRepo.GetSingleByAsync(s => s.SendbirdChannelUrl == channelUrl);
            if (session != null)
                return await ResolveOtherFromCustomerConsultantAsync(uow, senderUserId, session.CustomerId, session.ConsultantId);

            return null;
        }

        private static async Task<string?> ResolveOtherFromCustomerConsultantAsync(IUnitOfWork uow, string senderUserId, int customerId, int consultantId)
        {
            var customer = await uow.GetRepository<Customer>().GetSingleByAsync(c => c.Id == customerId);
            var consultant = await uow.GetRepository<Consultant>().GetSingleByAsync(c => c.Id == consultantId);
            if (customer?.UserId == senderUserId) return consultant?.UserId;
            if (consultant?.UserId == senderUserId) return customer?.UserId;
            return null;
        }

        private static async Task<(string? email, bool isConsultant)> ResolveEmailAndRoleAsync(IUnitOfWork uow, string userId)
        {
            var customer = await uow.GetRepository<Customer>().GetSingleByAsync(c => c.UserId == userId);
            if (customer != null) return (customer.Email, false);
            var consultant = await uow.GetRepository<Consultant>().GetSingleByAsync(c => c.UserId == userId);
            return (consultant?.Email, true);
        }

        // ── Typing indicators (not persisted) ───────────────────────────────────
        public Task StartTyping(string channelUrl, string userName) =>
            Clients.OthersInGroup(channelUrl).SendAsync("typing", channelUrl, userName, true);

        public Task StopTyping(string channelUrl, string userName) =>
            Clients.OthersInGroup(channelUrl).SendAsync("typing", channelUrl, userName, false);

        // ── Read receipts ───────────────────────────────────────────────────────
        public async Task MarkRead(string channelUrl)
        {
            var repo = _uow.GetRepository<ChatMessage>();
            var unread = await repo.GetAllAsync(m =>
                m.ChannelUrl == channelUrl && m.SenderId != UserId && m.ReadAt == null);

            var ids = new List<long>();
            foreach (var m in unread)
            {
                m.ReadAt = DateTime.UtcNow;
                repo.Update(m);
                ids.Add(m.Id);
            }
            if (ids.Count > 0)
            {
                await _uow.SaveChangesAsync();
                await Clients.OthersInGroup(channelUrl).SendAsync("readReceipt", channelUrl, ids, UserId);
            }
        }

        public static string[] ParticipantGroups(string channelUrl)
        {
            if (channelUrl.StartsWith("notif-"))
                return new[] { $"user-{channelUrl[6..]}" };
            if (channelUrl.StartsWith("Chat_"))
            {
                var rest = channelUrl[5..];
                if (rest.Length >= 73)
                    return new[] { $"user-{rest[..36]}", $"user-{rest[37..73]}" };
            }
            return Array.Empty<string>();
        }

        public static object ToDto(ChatMessage m) => new
        {
            messageId = m.Id,
            channelUrl = m.ChannelUrl,
            message = m.Message,
            messageType = m.MessageType,
            customType = m.CustomType,
            data = m.Data,
            url = m.FileUrl,
            name = m.FileName,
            type = m.FileMime,
            createdAt = new DateTimeOffset(DateTime.SpecifyKind(m.CreatedAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
            readAt = m.ReadAt,
            sender = new { userId = m.SenderId ?? "system", nickname = m.SenderName ?? "System" },
        };
    }
}
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

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
        public ChatHub(IUnitOfWork uow) => _uow = uow;

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

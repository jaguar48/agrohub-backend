using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.DAL.Entities.Models
{
    public class ChatMessage
    {
        public long Id { get; set; }                  // identity PK — doubles as messageId
        public string ChannelUrl { get; set; } = string.Empty;  // group key, indexed
        public string? SenderId { get; set; }                  // AspNetUsers.Id; NULL = admin/system (ADMM)
        public string? SenderName { get; set; }                  // denormalised display name
        public string MessageType { get; set; } = "MESG";        // MESG | ADMM | FILE
        public string Message { get; set; } = string.Empty;
        public string? CustomType { get; set; }                  // notification type, "system", etc.
        public string? Data { get; set; }                  // JSON payload (offers, call invites)
        public string? FileUrl { get; set; }                  // Cloudinary URL for FILE messages
        public string? FileName { get; set; }
        public string? FileMime { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }                  // read receipt (1:1 chats)
    }

    // Lightweight presence tracking — replaces Sendbird's connectionStatus/lastSeenAt
    public class UserPresence
    {
        public string UserId { get; set; } = string.Empty;  // PK
        public bool IsOnline { get; set; }
        public int ConnectionCount { get; set; }                  // multiple tabs
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }
}

using AgricHub.Shared.DTO_s.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.BLL.Interfaces.ChatServices
{
    public interface ISendbirdService
    {
        Task<string> CreateSendbirdUserAsync();
        Task<string> CreateSendbirdUserAsync(string userId, string nickname);
        Task<string> CreateGroupChannelAsync(string agropreneurUserId, string consultantUserId);
        Task<string> GetExistingChannelAsync(string userId1, string userId2);
        Task SendMessageAsync(string channelUrl, string senderUserId, string message, bool isSystemMessage = false, object? data = null);
        Task<string> EnsureSendbirdUserAsync(string userId, string nickname);
        Task SendAdminMessageAsync(string channelUrl, string message, object? data = null);

        // ── Notification channel ──────────────────────────────────────────────
        Task<string> CreateNotificationChannelAsync(string userId, string nickname);
        Task SendNotificationAsync(string userId, string message, string type, object? data = null);
        Task<List<NotificationHistoryItem>> GetNotificationHistoryAsync(string userId, int limit = 30);
    }
}


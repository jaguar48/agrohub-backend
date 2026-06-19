// AgricHub.BLL/Implementations/ChatServices/SendbirdService.cs

using AgricHub.BLL.Interfaces.ChatServices;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Text;

namespace AgricHub.BLL.Implementations.ChatServices
{
    public class SendbirdChannel
    {
        public string channel_url { get; set; }
        public List<SendbirdMember> members { get; set; }
        public bool is_distinct { get; set; }
    }

    public class SendbirdChannelResponse
    {
        public List<SendbirdChannel> channels { get; set; }
    }

    public class SendbirdService : ISendbirdService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly HttpClient _httpClient;
        private readonly string _sendbirdAppId;
        private readonly string _sendbirdApiToken;

        public SendbirdService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _httpClient          = new HttpClient();
            _sendbirdAppId       = configuration["Sendbird:AppId"];
            _sendbirdApiToken    = configuration["Sendbird:ApiToken"];

            Console.WriteLine($"[Sendbird] Service initialized — AppId={(_sendbirdAppId ?? "NULL")}, ApiToken={(string.IsNullOrEmpty(_sendbirdApiToken) ? "MISSING" : "set (" + _sendbirdApiToken.Length + " chars)")}");
        }

        // ── User management ────────────────────────────────────────────────────

        public async Task<string> CreateSendbirdUserAsync()
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var username = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                throw new Exception("User context is missing.");
            return await CreateSendbirdUserAsync(userId, username);
        }

        public async Task<string> CreateSendbirdUserAsync(string userId, string nickname)
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api-{_sendbirdAppId}.sendbird.com/v3/users")
            {
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    user_id = userId,
                    nickname = nickname,
                    profile_url = "https://placehold.co/100x100.png"
                }), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Api-Token", _sendbirdApiToken);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                if (content.Contains("user_id already exists"))
                {
                    var getReq = new HttpRequestMessage(HttpMethod.Get,
                        $"https://api-{_sendbirdAppId}.sendbird.com/v3/users/{userId}");
                    getReq.Headers.Add("Api-Token", _sendbirdApiToken);
                    var getRes = await _httpClient.SendAsync(getReq);
                    return await getRes.Content.ReadAsStringAsync();
                }
                throw new Exception($"Failed to create Sendbird user: {content}");
            }

            return content;
        }

        public async Task<string> EnsureSendbirdUserAsync(string userId, string nickname)
        {
            var getReq = new HttpRequestMessage(HttpMethod.Get,
                $"https://api-{_sendbirdAppId}.sendbird.com/v3/users/{userId}");
            getReq.Headers.Add("Api-Token", _sendbirdApiToken);

            var getRes = await _httpClient.SendAsync(getReq);
            if (getRes.IsSuccessStatusCode)
                return await getRes.Content.ReadAsStringAsync();

            return await CreateSendbirdUserAsync(userId, nickname);
        }

        // ── Channels ────────────────────────────────────────────────────────────

        public async Task<string> CreateGroupChannelAsync(string agropreneurUserId, string consultantUserId)
        {
            var existing = await GetExistingChannelAsync(agropreneurUserId, consultantUserId);
            if (!string.IsNullOrEmpty(existing)) return existing;

            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api-{_sendbirdAppId}.sendbird.com/v3/group_channels")
            {
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    name = $"Chat_{agropreneurUserId}_{consultantUserId}",
                    user_ids = new[] { agropreneurUserId, consultantUserId },
                    is_distinct = true
                }), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Api-Token", _sendbirdApiToken);

            var response = await _httpClient.SendAsync(request);
            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Sendbird channel creation failed: {result}");

            return JsonConvert.DeserializeObject<SendbirdChannel>(result)!.channel_url;
        }

        public async Task<string> GetExistingChannelAsync(string userId1, string userId2)
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api-{_sendbirdAppId}.sendbird.com/v3/group_channels?user_id={userId1}&show_member=true");
            request.Headers.Add("Api-Token", _sendbirdApiToken);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to fetch channels: {content}");

            var channelResponse = JsonConvert.DeserializeObject<SendbirdChannelResponse>(content);
            foreach (var channel in channelResponse!.channels)
            {
                var memberIds = channel.members.Select(m => m.user_id).ToList();
                if (memberIds.Contains(userId1) && memberIds.Contains(userId2) && channel.is_distinct)
                    return channel.channel_url;
            }

            return null;
        }

        // ── Notification channel ───────────────────────────────────────────────

        public async Task<string> CreateNotificationChannelAsync(string userId, string nickname)
        {
            await EnsureSendbirdUserAsync(userId, nickname);

            var channelUrl = $"notif-{userId}";
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api-{_sendbirdAppId}.sendbird.com/v3/group_channels")
            {
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    channel_url = channelUrl,
                    name = "Notifications",
                    user_ids = new[] { userId },
                    is_distinct = false,
                    is_public = false,
                    custom_type = "notifications",
                }), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Api-Token", _sendbirdApiToken);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Sendbird] CreateNotificationChannel response ({(int)response.StatusCode}): {content}");

                // Channel already existing is NOT an error — expected for any user
                // who has received a notification before. Sendbird's actual message
                // is `"channel_url" violates unique constraint.` (code 400202), which
                // does NOT contain "already", so we check for both.
                var channelAlreadyExists =
                    content.Contains("already", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("400202");

                if (!channelAlreadyExists)
                    throw new Exception($"Failed to create notification channel: {content}");

                Console.WriteLine($"[Sendbird] Notification channel already exists (expected): {channelUrl}");
            }
            else
            {
                Console.WriteLine($"[Sendbird] Created notification channel: {channelUrl}");
            }

            return channelUrl;
        }

        public async Task SendNotificationAsync(string userId, string message, string type, object? data = null)
        {
            Console.WriteLine($"[Sendbird] >>> SendNotificationAsync CALLED — userId={userId}, type={type}");

            try
            {
                // Create channel on-demand for existing users who registered before this feature
                var channelUrl = await CreateNotificationChannelAsync(userId, userId);

                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://api-{_sendbirdAppId}.sendbird.com/v3/group_channels/{channelUrl}/messages")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(new
                    {
                        message_type = "ADMM",
                        message,
                        custom_type = type,
                        data = JsonConvert.SerializeObject(new
                        {
                            type,
                            payload = data,
                            timestamp = DateTime.UtcNow
                        })
                    }), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Api-Token", _sendbirdApiToken);

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    Console.WriteLine($"[Sendbird] ❌ Notification SEND FAILED ({(int)response.StatusCode}) for {userId}: {content}");
                else
                    Console.WriteLine($"[Sendbird] ✅ Notification SENT to {channelUrl}: \"{message}\"");
            }
            catch (Exception ex)
            {
                // Never crash a business action because of a notification
                Console.WriteLine($"[Sendbird] ❌ Notification ERROR for {userId}: {ex.GetType().Name} — {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"[Sendbird]    Inner: {ex.InnerException.Message}");
            }
        }

        // ── Messages ────────────────────────────────────────────────────────────

        public async Task SendMessageAsync(string channelUrl, string senderUserId, string message,
            bool isSystemMessage = false, object? data = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api-{_sendbirdAppId}.sendbird.com/v3/group_channels/{channelUrl}/messages")
            {
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    message_type = "MESG",
                    user_id = senderUserId,
                    message,
                    custom_type = isSystemMessage ? "system" : "user",
                    data = data != null ? JsonConvert.SerializeObject(data) : null
                }), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Api-Token", _sendbirdApiToken);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to send message: {content}");
        }

        public async Task SendAdminMessageAsync(string channelUrl, string message, object? data = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api-{_sendbirdAppId}.sendbird.com/v3/group_channels/{channelUrl}/messages")
            {
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    message_type = "ADMM",
                    message,
                    data = data != null ? JsonConvert.SerializeObject(data) : null
                }), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Api-Token", _sendbirdApiToken);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to send admin message: {content}");
        }

        // ── FIXED: Sendbird's "List messages" endpoint requires a message_ts
        // anchor + prev_limit/next_limit — it does NOT accept a plain "limit"
        // param. The old query (?message_type=ADMM&limit=30&reverse=true) was
        // being rejected/misread by Sendbird, so the bell always looked empty
        // even after a notification was sent successfully. ────────────────────
        public async Task<List<NotificationHistoryItem>> GetNotificationHistoryAsync(string userId, int limit = 30)
        {
            var channelUrl = $"notif-{userId}";
            var nowTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var url = $"https://api-{_sendbirdAppId}.sendbird.com/v3/group_channels/{channelUrl}/messages" +
                      $"?message_ts={nowTs}&prev_limit={limit}&next_limit=0&include=true&reverse=true&message_type=ADMM";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Api-Token", _sendbirdApiToken);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Sendbird] GetNotificationHistory response ({(int)response.StatusCode}) for {channelUrl}: {content}");

                // Channel doesn't exist yet (no notifications ever sent) — return empty list
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    return new List<NotificationHistoryItem>();

                throw new Exception($"Failed to fetch notification history: {content}");
            }

            Console.WriteLine($"[Sendbird] GetNotificationHistory OK for {channelUrl} — raw response: {content}");

            var data = JsonConvert.DeserializeObject<dynamic>(content);
            var result = new List<NotificationHistoryItem>();

            foreach (var m in data!.messages)
            {
                dynamic parsed = new { type = "info" };
                try { parsed = JsonConvert.DeserializeObject<dynamic>((string)(m.data ?? "{}")); } catch { }

                result.Add(new NotificationHistoryItem
                {
                    Message   = (string)(m.message ?? ""),
                    Type      = (string)(parsed.type ?? m.custom_type ?? "info"),
                    CreatedAt = (long)(m.created_at ?? 0),
                });
            }

            Console.WriteLine($"[Sendbird] GetNotificationHistory parsed {result.Count} item(s) for {channelUrl}.");

            return result;
        }

    }
}
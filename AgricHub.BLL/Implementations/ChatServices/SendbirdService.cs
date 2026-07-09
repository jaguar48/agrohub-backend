// AgricHub.BLL/Implementations/ChatServices/SendbirdService.cs
//
// KEY CHANGE: Channel URL cache — EnsureSendbirdUserAsync + CreateNotificationChannelAsync
// were being called on EVERY SendNotificationAsync invocation (2 HTTP round trips before
// the actual send). Under load or when Sendbird is slow, those 2 calls alone consumed
// the full 15s timeout before the notification even sent.
//
// Fix: static ConcurrentDictionary caches the notif-{userId} channel URL after the
// first successful creation/verification. Subsequent calls skip both preflight requests
// entirely and go straight to sending. Cache survives app-pool recycling restarts
// (just re-warms on first call after restart — one extra round trip, not a problem).

using AgricHub.BLL.Interfaces.ChatServices;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;

namespace AgricHub.BLL.Implementations.ChatServices
{
    public class SendbirdChannel
    {
        public string channel_url { get; set; } = string.Empty;
        public List<SendbirdMember> members { get; set; } = new();
        public bool is_distinct { get; set; }
    }

    public class SendbirdChannelResponse
    {
        public List<SendbirdChannel> channels { get; set; } = new();
    }

    public class SendbirdService : ISendbirdService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly HttpClient _httpClient;
        private readonly string _sendbirdAppId;
        private readonly string _sendbirdApiToken;
        private readonly ILogger<SendbirdService> _logger;

        // ── Notification channel cache ─────────────────────────────────────────
        // Keyed by userId. Once a notif-{userId} channel is confirmed to exist,
        // we never call EnsureSendbirdUserAsync or CreateNotificationChannelAsync
        // again for that user — saving 2 HTTP round trips on every notification.
        private static readonly ConcurrentDictionary<string, string> _notifChannelCache = new();

        public SendbirdService(
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            HttpClient httpClient,
            ILogger<SendbirdService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _httpClient          = httpClient;
            _sendbirdAppId       = configuration["Sendbird:AppId"]   ?? string.Empty;
            _sendbirdApiToken    = configuration["Sendbird:ApiToken"] ?? string.Empty;
            _logger              = logger;

            _httpClient.Timeout = TimeSpan.FromSeconds(15);

            _logger.LogInformation(
                "[Sendbird] Service initialized — AppId={AppId}, ApiToken={TokenStatus}",
                string.IsNullOrEmpty(_sendbirdAppId) ? "NULL" : _sendbirdAppId,
                string.IsNullOrEmpty(_sendbirdApiToken) ? "MISSING" : $"set ({_sendbirdApiToken.Length} chars)");
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private HttpRequestMessage BuildRequest(HttpMethod method, string url, object? body = null)
        {
            var req = new HttpRequestMessage(method, url);
            req.Headers.Add("Api-Token", _sendbirdApiToken);
            if (body != null)
                req.Content = new StringContent(
                    JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            return req;
        }

        private static bool IsChannelNotFound(int statusCode, string content) =>
            statusCode == 404 ||
            (statusCode == 400 && (
                content.Contains("400302", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("not found", StringComparison.OrdinalIgnoreCase)));

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
            var response = await _httpClient.SendAsync(BuildRequest(
                HttpMethod.Post,
                $"https://api-{_sendbirdAppId}.sendbird.com/v3/users",
                new { user_id = userId, nickname, profile_url = "https://placehold.co/100x100.png" }));

            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                if (content.Contains("user_id already exists"))
                {
                    var getRes = await _httpClient.SendAsync(BuildRequest(
                        HttpMethod.Get,
                        $"https://api-{_sendbirdAppId}.sendbird.com/v3/users/{userId}"));
                    return await getRes.Content.ReadAsStringAsync();
                }
                throw new Exception($"Failed to create Sendbird user: {content}");
            }
            return content;
        }

        public async Task<string> EnsureSendbirdUserAsync(string userId, string nickname)
        {
            var getRes = await _httpClient.SendAsync(BuildRequest(
                HttpMethod.Get,
                $"https://api-{_sendbirdAppId}.sendbird.com/v3/users/{userId}"));
            if (getRes.IsSuccessStatusCode) return await getRes.Content.ReadAsStringAsync();
            return await CreateSendbirdUserAsync(userId, nickname);
        }

        // ── Channels ────────────────────────────────────────────────────────────

        public async Task<string> CreateGroupChannelAsync(string agropreneurUserId, string consultantUserId)
        {
            var existing = await GetExistingChannelAsync(agropreneurUserId, consultantUserId);
            if (!string.IsNullOrEmpty(existing)) return existing;

            var response = await _httpClient.SendAsync(BuildRequest(
                HttpMethod.Post,
                $"https://api-{_sendbirdAppId}.sendbird.com/v3/group_channels",
                new
                {
                    name = $"Chat_{agropreneurUserId}_{consultantUserId}",
                    user_ids = new[] { agropreneurUserId, consultantUserId },
                    is_distinct = true
                }));

            var result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Sendbird channel creation failed: {result}");
            return JsonConvert.DeserializeObject<SendbirdChannel>(result)!.channel_url;
        }

        public async Task<string> GetExistingChannelAsync(string userId1, string userId2)
        {
            var response = await _httpClient.SendAsync(BuildRequest(
                HttpMethod.Get,
                $"https://api-{_sendbirdAppId}.sendbird.com/v3/group_channels?user_id={userId1}&show_member=true"));

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
            return string.Empty;
        }

        // ── Notification channel ───────────────────────────────────────────────

        public async Task<string> CreateNotificationChannelAsync(string userId, string nickname)
        {
            await EnsureSendbirdUserAsync(userId, nickname);
            var channelUrl = $"notif-{userId}";

            var response = await _httpClient.SendAsync(BuildRequest(
                HttpMethod.Post,
                $"https://api-{_sendbirdAppId}.sendbird.com/v3/group_channels",
                new
                {
                    channel_url = channelUrl,
                    name = "Notifications",
                    user_ids = new[] { userId },
                    is_distinct = false,
                    is_public = false,
                    custom_type = "notifications",
                }));

            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var alreadyExists =
                    content.Contains("already", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("400202");

                if (!alreadyExists)
                    throw new Exception($"Failed to create notification channel: {content}");

                _logger.LogDebug("[Sendbird] Notification channel already exists: {ChannelUrl}", channelUrl);
            }
            else
            {
                _logger.LogDebug("[Sendbird] Created notification channel: {ChannelUrl}", channelUrl);
            }

            // Cache it — both new and already-existing channels are valid
            _notifChannelCache[userId] = channelUrl;
            return channelUrl;
        }

        // ── FAST NOTIFICATION PATH ─────────────────────────────────────────────
        // Checks the cache first. If the channel URL is already known, skips
        // EnsureSendbirdUserAsync + CreateNotificationChannelAsync entirely
        // (saves 2 HTTP round trips = up to 2 × 15s potential timeout exposure).

        private async Task<string> GetOrCreateNotifChannelAsync(string userId)
        {
            // Fast path: channel already known
            if (_notifChannelCache.TryGetValue(userId, out var cached))
                return cached;

            // Slow path: first time for this user — create/verify the channel
            return await CreateNotificationChannelAsync(userId, userId);
        }

        public async Task SendNotificationAsync(string userId, string message, string type, object? data = null)
        {
            _logger.LogDebug("[Sendbird] SendNotificationAsync — userId={UserId}, type={Type}", userId, type);

            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("[Sendbird] SendNotificationAsync called with empty userId — skipping (type={Type})", type);
                return;
            }

            try
            {
                // GetOrCreateNotifChannelAsync uses cache — 0 extra HTTP calls after first notification
                var channelUrl = await GetOrCreateNotifChannelAsync(userId);

                var payload = new
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
                };

                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    try
                    {
                        var response = await _httpClient.SendAsync(BuildRequest(
                            HttpMethod.Post,
                            $"https://api-{_sendbirdAppId}.sendbird.com/v3/group_channels/{channelUrl}/messages",
                            payload));

                        var content = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogDebug("[Sendbird] Notification sent to {ChannelUrl}: {Message}", channelUrl, message);
                            return;
                        }

                        var statusCode = (int)response.StatusCode;
                        _logger.LogWarning("[Sendbird] Notification attempt {Attempt}/2 failed ({Status}) for {UserId}: {Content}",
                            attempt, statusCode, userId, content);

                        // If the channel was deleted/invalidated, evict cache and retry once
                        if (statusCode == 400 || statusCode == 404)
                        {
                            _notifChannelCache.TryRemove(userId, out _);
                            if (attempt == 1)
                            {
                                channelUrl = await GetOrCreateNotifChannelAsync(userId);
                                continue;
                            }
                        }

                        if (statusCode < 500 || attempt == 2) return;
                        await Task.Delay(400);
                    }
                    catch (TaskCanceledException) when (attempt < 2)
                    {
                        _logger.LogWarning("[Sendbird] Timeout on attempt {Attempt}/2 for {UserId}, retrying…", attempt, userId);
                        await Task.Delay(300);
                    }
                    catch (HttpRequestException ex) when (attempt < 2)
                    {
                        _logger.LogWarning("[Sendbird] Connection error attempt {Attempt}/2: {Message}", attempt, ex.Message);
                        await Task.Delay(300);
                    }
                }
            }
            catch (Exception ex)
            {
                // Never crash a business action due to a notification failure
                _logger.LogError(ex, "[Sendbird] Notification error for userId={UserId}, type={Type}", userId, type);
            }
        }

        // ── Messages ────────────────────────────────────────────────────────────

        public async Task SendMessageAsync(string channelUrl, string senderUserId, string message,
            bool isSystemMessage = false, object? data = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message cannot be empty", nameof(message));

            if (string.IsNullOrWhiteSpace(channelUrl))
            {
                _logger.LogWarning("[Sendbird] SendMessage: empty channelUrl — skipping.");
                return;
            }

            for (int attempt = 1; attempt <= 2; attempt++)
            {
                var response = await _httpClient.SendAsync(BuildRequest(
                    HttpMethod.Post,
                    $"https://api-{_sendbirdAppId}.sendbird.com/v3/group_channels/{channelUrl}/messages",
                    new
                    {
                        message_type = "MESG",
                        user_id = senderUserId,
                        message,
                        custom_type = isSystemMessage ? "system" : "user",
                        data = data != null ? JsonConvert.SerializeObject(data) : null
                    }));

                var content = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode) return;

                var status = (int)response.StatusCode;

                // Channel not found (stale URL after Sendbird account switch) —
                // evict and let ChatService.EnsureChannelAsync recreate it.
                // We can't recreate here because we don't have the two user IDs.
                if (IsChannelNotFound(status, content))
                {
                    _logger.LogWarning("[Sendbird] SendMessage: channel {Url} not found (stale). Swallowing — ChatService will recover on next call.", channelUrl);
                    return;
                }

                if (attempt == 2)
                    throw new Exception($"Failed to send message: {content}");

                await Task.Delay(300);
            }
        }

        public async Task SendAdminMessageAsync(string channelUrl, string message, object? data = null)
        {
            if (string.IsNullOrWhiteSpace(channelUrl))
            {
                _logger.LogWarning("[Sendbird] SendAdminMessage: empty channelUrl — skipping.");
                return;
            }

            for (int attempt = 1; attempt <= 2; attempt++)
            {
                var response = await _httpClient.SendAsync(BuildRequest(
                    HttpMethod.Post,
                    $"https://api-{_sendbirdAppId}.sendbird.com/v3/group_channels/{channelUrl}/messages",
                    new
                    {
                        message_type = "ADMM",
                        message,
                        data = data != null ? JsonConvert.SerializeObject(data) : null
                    }));

                var content = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode) return;

                var status = (int)response.StatusCode;

                if (IsChannelNotFound(status, content))
                {
                    _logger.LogWarning("[Sendbird] SendAdminMessage: channel {Url} not found (stale). Swallowing.", channelUrl);
                    return;
                }

                if (attempt == 2)
                    throw new Exception($"Failed to send admin message: {content}");

                await Task.Delay(300);
            }
        }

        // ── Notification history ───────────────────────────────────────────────

        public async Task<List<NotificationHistoryItem>> GetNotificationHistoryAsync(
            string userId, int limit = 30)
        {
            var channelUrl = $"notif-{userId}";
            var nowTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var url = $"https://api-{_sendbirdAppId}.sendbird.com/v3/group_channels/{channelUrl}/messages" +
                             $"?message_ts={nowTs}&prev_limit={limit}&next_limit=0&include=true&reverse=true&message_type=ADMM";

            var response = await _httpClient.SendAsync(BuildRequest(HttpMethod.Get, url));
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("[Sendbird] GetNotificationHistory {Status} for {ChannelUrl}",
                    (int)response.StatusCode, channelUrl);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    return new List<NotificationHistoryItem>();

                throw new Exception($"Failed to fetch notification history: {content}");
            }

            var data = JsonConvert.DeserializeObject<dynamic>(content);
            var result = new List<NotificationHistoryItem>();

            if (data?.messages == null) return result;

            foreach (var m in data.messages)
            {
                dynamic parsed = new { type = "info" };
                try { parsed = JsonConvert.DeserializeObject<dynamic>((string)(m.data ?? "{}")); } catch { }

                result.Add(new NotificationHistoryItem
                {
                    Message   = (string)(m.message ?? ""),
                    Type      = (string)(parsed.type ?? m.custom_type ?? "info"),
                    CreatedAt = (long)(m.created_at ?? 0L),
                });
            }

            _logger.LogDebug("[Sendbird] GetNotificationHistory: {Count} item(s) for {ChannelUrl}",
                result.Count, channelUrl);
            return result;
        }
    }
}
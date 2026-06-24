using AgricHub.BLL.Interfaces.IChatServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AgricHub.BLL.Implementations.ChatServices
{
    public class DailyService : IDailyService
    {
        private readonly HttpClient _http;
        private readonly ILogger<DailyService> _logger;
        private readonly string _apiKey;

        public DailyService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<DailyService> logger)
        {
            _http   = httpClientFactory.CreateClient();
            _logger = logger;
            _apiKey = config["Daily:ApiKey"]
                      ?? throw new InvalidOperationException("Daily:ApiKey is not configured.");
            _http.BaseAddress = new Uri("https://api.daily.co/v1/");
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<string> CreateRoomAsync(string roomName, int expirySeconds = 7200)
        {
            var payload = new
            {
                name = roomName,
                properties = new
                {
                    exp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expirySeconds,
                    enable_screenshare = true,
                    enable_chat = false,
                }
            };

            var response = await _http.PostAsJsonAsync("rooms", payload);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Daily.co room creation failed ({Status}): {Body}", response.StatusCode, body);

                // Daily.co returns 400 with "already exists" if the room name is reused —
                // fetch the existing room, but ONLY reuse it if it hasn't expired.
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest && body.Contains("already exists"))
                {
                    var existing = await _http.GetFromJsonAsync<DailyRoomResponse>($"rooms/{roomName}");
                    var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    // ── FIXED: the old fallback returned the stale room unconditionally,
                    // even if it had already expired — joiners would get a blank iframe
                    // since Daily.co rejects connections to expired rooms. ─────────────────
                    if (existing?.Url is { Length: > 0 } && existing.Config?.Exp is long exp && exp > nowUnix)
                    {
                        _logger.LogInformation("Reusing existing, still-valid Daily.co room '{Room}' (expires in {Seconds}s).",
                            roomName, exp - nowUnix);
                        return existing.Url;
                    }

                    _logger.LogWarning("Existing Daily.co room '{Room}' has expired or has no exp — creating a fresh room instead.", roomName);

                    // Expired (or unreadable exp) — create a fresh room with a unique name
                    // rather than handing back a dead link.
                    var freshName = $"{roomName}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                    return await CreateRoomAsync(freshName, expirySeconds);
                }

                throw new InvalidOperationException("Could not create video room. Please try again.");
            }

            var room = await response.Content.ReadFromJsonAsync<DailyRoomResponse>();
            if (string.IsNullOrEmpty(room?.Url))
                throw new InvalidOperationException("Daily.co did not return a room URL.");

            return room.Url;
        }

        /// <summary>
        /// Creates a meeting token for a participant. Use isOwner=true for the consultant
        /// so they can mute/kick participants and control the room; customers join without
        /// a token (or with isOwner=false) using just the room URL.
        /// </summary>
        public async Task<string> CreateMeetingTokenAsync(string roomName, bool isOwner = false, string? userName = null, int expirySeconds = 7200)
        {
            var payload = new
            {
                properties = new
                {
                    room_name = roomName,
                    is_owner = isOwner,
                    user_name = userName,
                    exp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expirySeconds,
                }
            };

            var response = await _http.PostAsJsonAsync("meeting-tokens", payload);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Daily.co meeting token creation failed ({Status}): {Body}", response.StatusCode, body);
                throw new InvalidOperationException("Could not create meeting token.");
            }

            var token = await response.Content.ReadFromJsonAsync<DailyTokenResponse>();
            if (string.IsNullOrEmpty(token?.Token))
                throw new InvalidOperationException("Daily.co did not return a meeting token.");

            return token.Token;
        }

        private class DailyRoomResponse
        {
            [JsonPropertyName("url")]
            public string? Url { get; set; }

            [JsonPropertyName("config")]
            public DailyRoomConfig? Config { get; set; }
        }

        private class DailyRoomConfig
        {
            [JsonPropertyName("exp")]
            public long? Exp { get; set; }
        }

        private class DailyTokenResponse
        {
            [JsonPropertyName("token")]
            public string? Token { get; set; }
        }
    }
}
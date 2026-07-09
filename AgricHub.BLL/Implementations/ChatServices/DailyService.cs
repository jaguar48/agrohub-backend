using AgricHub.BLL.Interfaces.IChatServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AgricHub.BLL.Implementations.ChatServices
{
    public class DailyService : IDailyService
    {
        private readonly HttpClient _http;
        private readonly ILogger<DailyService> _logger;

        public DailyService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<DailyService> logger)
        {
            _logger = logger;

            var apiKey = config["Daily:ApiKey"]
                ?? throw new InvalidOperationException("Daily:ApiKey is not configured.");

            // FIX 1: Use the named "daily" client registered in Program.cs so the
            // 15-second timeout and BaseAddress are already set — don't mutate here.
            // If you haven't added the named registration yet, add this to Program.cs:
            //   builder.Services.AddHttpClient("daily", c => {
            //       c.BaseAddress = new Uri("https://api.daily.co/v1/");
            //       c.Timeout = TimeSpan.FromSeconds(15);
            //   });
            _http = httpClientFactory.CreateClient("daily");
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
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

                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest
                    && body.Contains("already exists"))
                {
                    // FIX 2: GetFromJsonAsync throws on non-2xx — use a raw GET with
                    // explicit success check so a failed fetch doesn't crash the request.
                    DailyRoomResponse? existing = null;
                    try
                    {
                        var fetchRes = await _http.GetAsync($"rooms/{roomName}");
                        if (fetchRes.IsSuccessStatusCode)
                            existing = await fetchRes.Content.ReadFromJsonAsync<DailyRoomResponse>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not fetch existing Daily.co room '{Room}'.", roomName);
                    }

                    var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    if (existing?.Url is { Length: > 0 }
                        && existing.Config?.Exp is long exp
                        && exp > nowUnix)
                    {
                        _logger.LogInformation(
                            "Reusing existing, still-valid Daily.co room '{Room}' (expires in {Seconds}s).",
                            roomName, exp - nowUnix);
                        return existing.Url;
                    }

                    _logger.LogWarning(
                        "Existing Daily.co room '{Room}' has expired or is unreachable — creating a fresh room.",
                        roomName);

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
        /// Creates a meeting token for a participant. Consultants always get isOwner=true
        /// so they can mute/remove participants and control the room.
        /// </summary>
        public async Task<string> CreateMeetingTokenAsync(
            string roomName,
            bool isOwner = false,
            string? userName = null,
            int expirySeconds = 7200)
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
                _logger.LogError("Daily.co meeting token creation failed ({Status}): {Body}",
                    response.StatusCode, body);
                throw new InvalidOperationException("Could not create meeting token.");
            }

            var token = await response.Content.ReadFromJsonAsync<DailyTokenResponse>();
            if (string.IsNullOrEmpty(token?.Token))
                throw new InvalidOperationException("Daily.co did not return a meeting token.");

            return token.Token;
        }

        private class DailyRoomResponse
        {
            [JsonPropertyName("url")] public string? Url { get; set; }
            [JsonPropertyName("config")] public DailyRoomConfig? Config { get; set; }
        }

        private class DailyRoomConfig
        {
            [JsonPropertyName("exp")] public long? Exp { get; set; }
        }

        private class DailyTokenResponse
        {
            [JsonPropertyName("token")] public string? Token { get; set; }
        }
    }
}
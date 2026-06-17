using AgricHub.BLL.Interfaces.IChatServices;
using MailKit;
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
                // fetch and return the existing room's URL instead of failing.
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest && body.Contains("already exists"))
                {
                    var existing = await _http.GetFromJsonAsync<DailyRoomResponse>($"rooms/{roomName}");
                    if (existing?.Url is { Length: > 0 })
                        return existing.Url;
                }

                throw new InvalidOperationException("Could not create video room. Please try again.");
            }

            var room = await response.Content.ReadFromJsonAsync<DailyRoomResponse>();
            if (string.IsNullOrEmpty(room?.Url))
                throw new InvalidOperationException("Daily.co did not return a room URL.");

            return room.Url;
        }

        private class DailyRoomResponse
        {
            [JsonPropertyName("url")]
            public string? Url { get; set; }
        }
    }
}
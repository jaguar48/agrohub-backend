// AgricHub.Presentation/Controllers/NotificationsController.cs

using AgricHub.BLL.Interfaces.ChatServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace AgricHub.Presentation.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController(
        ISendbirdService sendbirdService,
        ILogger<NotificationsController> logger) : ControllerBase
    {
        // Per-process 60-second cache — stops rapid bell polls from hammering
        // Sendbird on every request. Keyed by userId.
        private static readonly ConcurrentDictionary<string, (DateTime At, object[] Items)> _cache = new();
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Serve from cache if still fresh
            if (_cache.TryGetValue(userId, out var cached) &&
                DateTime.UtcNow - cached.At < CacheTtl)
            {
                return Ok(cached.Items);
            }

            try
            {
                // Hard 8-second timeout on the Sendbird fetch.
                // Previously this call had NO timeout — it inherited the HttpClient
                // global timeout (100s) and blocked the entire response thread.
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

                var history = await sendbirdService
                    .GetNotificationHistoryAsync(userId)
                    .WaitAsync(cts.Token);

                var items = history
                    .Select(h => (object)new
                    {
                        message = h.Message,
                        type = h.Type,
                        createdAt = h.CreatedAt,    // Unix ms — matches frontend
                    })
                    .ToArray();

                _cache[userId] = (DateTime.UtcNow, items);
                return Ok(items);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("[Notifications] History fetch timed out for {UserId} — returning empty list.", userId);
                return Ok(Array.Empty<object>());   // 200 + empty: bell degrades gracefully
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Notifications] History fetch failed for {UserId}.", userId);
                return Ok(Array.Empty<object>());
            }
        }

        // Keep the test endpoint — useful for verifying the bell pipeline end-to-end
        [HttpPost("test-notification")]
        [Authorize]
        public async Task<IActionResult> TestNotification()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await sendbirdService.SendNotificationAsync(
                userId,
                "🔔 Test notification — if you see this in the bell, it works!",
                "booking_confirmed"
            );
            return Ok(new { sentTo = userId });
        }
    }
}
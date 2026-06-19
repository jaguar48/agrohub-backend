// AgricHub.Presentation/Controllers/NotificationsController.cs

using AgricHub.BLL.Implementations.ChatServices;
using AgricHub.BLL.Interfaces.ChatServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgricHub.Presentation.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController(ISendbirdService sendbirdService) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                var history = await sendbirdService.GetNotificationHistoryAsync(userId);
                // Always return 200 with the list (even if empty)
                // so the bell's error handler never fires for missing history
                return Ok(history);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Notifications] GetNotifications failed for {userId}: {e.Message}");
                // Return empty list — never 400/500 — bell degrades gracefully
                return Ok(new List<object>());
            }
        }

        // Temporary test — remove after confirming
        [HttpPost("test-notification")]
        [Authorize]
        public async Task<IActionResult> TestNotification()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await sendbirdService.SendNotificationAsync(
                userId,
                "🔔 Test notification — if you see this in the bell, it works!",
                "booking_confirmed"
            );
            return Ok(new { sentTo = userId });
        }
    }
}
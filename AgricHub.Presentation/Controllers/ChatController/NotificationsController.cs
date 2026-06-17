// AgricHub.Presentation/Controllers/ChatController/NotificationsController.cs

using AgricHub.BLL.Interfaces.ChatServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgricHub.Presentation.Controllers.ChatController
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
                return Ok(history);
            }
            catch (Exception e)
            {
                return BadRequest(new { message = e.Message });
            }
        }
    }
}
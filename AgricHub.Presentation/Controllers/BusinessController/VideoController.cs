using AgricHub.BLL.Interfaces.IChatServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Presentation.Controllers.BusinessController
{
  
    [ApiController]
    [Route("api/agrichub/video")]
    [Authorize]
    public class VideoController : ControllerBase
    {
        private readonly IDailyService _dailyService;

        public VideoController(IDailyService dailyService)
        {
            _dailyService = dailyService;
        }

        [HttpPost("create-room")]
        [SwaggerOperation("Creates (or reuses) a Daily.co video room for a chat session. Either party may call this.")]
        public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
        {
            try
            {
                // Sanitize: Daily.co room names allow letters, numbers, hyphens and underscores only.
                var safeName = "agrichub-" + new string(
                    (request.RoomName ?? Guid.NewGuid().ToString())
                        .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
                        .ToArray());

                var url = await _dailyService.CreateRoomAsync(safeName);
                return Ok(new { success = true, data = new { url } });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }

    public class CreateRoomRequest
    {
        /// <summary>A stable identifier (e.g. the chat session/consultation id) used to name the room.</summary>
        public string? RoomName { get; set; }
    }
}
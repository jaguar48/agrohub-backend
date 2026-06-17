

using AgricHub.BLL.Interfaces.IUserServices;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace AgricHub.Presentation.Controllers.UserController
{

    [ApiController]
    [Route("api/profile/customer")]
    [Authorize(Roles = "Customer")]
    public class CustomerProfileController : ControllerBase
    {
        private readonly ICustomerProfileService _profileService;
        private readonly ILogger<CustomerProfileController> _logger;

        public CustomerProfileController(
            ICustomerProfileService profileService,
            ILogger<CustomerProfileController> logger)
        {
            _profileService = profileService;
            _logger = logger;
        }

        /// <summary>
        /// Get current customer profile
        /// </summary>
        [HttpGet]
        [SwaggerOperation("Get customer profile")]
        [SwaggerResponse(200, "Profile retrieved successfully")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var profile = await _profileService.GetMyProfileAsync();
                return Ok(new { success = true, data = profile });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving profile");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Update customer profile
        /// </summary>
        [HttpPut]
        [SwaggerOperation("Update customer profile")]
        [SwaggerResponse(200, "Profile updated successfully")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateCustomerProfileRequest request)
        {
            try
            {
                await _profileService.UpdateProfileAsync(request);
                return Ok(new { success = true, message = "Profile updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Upload profile picture
        /// </summary>
        [HttpPost("avatar")]
        [SwaggerOperation("Upload profile picture")]
        [SwaggerResponse(200, "Avatar uploaded successfully")]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { success = false, message = "No file uploaded" });

                var avatarUrl = await _profileService.UploadAvatarAsync(file);
                return Ok(new { success = true, message = "Avatar uploaded successfully", data = new { avatarUrl } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading avatar");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Change password
        /// </summary>
        [HttpPost("change-password")]
        [SwaggerOperation("Change password")]
        [SwaggerResponse(200, "Password changed successfully")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                await _profileService.ChangePasswordAsync(request);
                return Ok(new { success = true, message = "Password changed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
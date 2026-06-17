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
    [Route("api/profile/consultant")]
    [Authorize(Roles = "Consultant")]
    public class ConsultantProfileController : ControllerBase
    {
        private readonly IConsultantProfileService _profileService;
        private readonly ILogger<ConsultantProfileController> _logger;

        public ConsultantProfileController(
            IConsultantProfileService profileService,
            ILogger<ConsultantProfileController> logger)
        {
            _profileService = profileService;
            _logger         = logger;
        }

        [HttpGet]
        [SwaggerOperation("Get consultant profile")]
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

        [HttpPut]
        [SwaggerOperation("Update consultant profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateConsultantProfileRequest request)
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
        /// Returns the bank list for the consultant's country.
        /// Empty list = country not supported by Paystack → frontend shows free-text input.
        /// </summary>
        [HttpGet("banks")]
        [SwaggerOperation("Get banks for consultant's country")]
        public async Task<IActionResult> GetBanks()
        {
            try
            {
                // Load profile to determine the consultant's country
                var profile = await _profileService.GetMyProfileAsync();
                var country = MapCountryToPaystack(profile.CountryId);

                var banks = await _profileService.GetBanksAsync(country);
                return Ok(new { success = true, data = banks });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving banks");
                // Never hard-fail — return empty list so frontend shows free-text fallback
                return Ok(new { success = true, data = Array.Empty<object>() });
            }
        }

        [HttpPost("verify-account")]
        [SwaggerOperation("Verify bank account")]
        public async Task<IActionResult> VerifyBankAccount([FromBody] VerifyAccountRequest request)
        {
            try
            {
                var details = await _profileService.VerifyBankAccountAsync(
                    request.AccountNumber, request.BankCode);
                return Ok(new { success = true, data = details });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying account");
                return BadRequest(new { success = false, message = "Invalid account details" });
            }
        }

        [HttpPut("bank-details")]
        [SwaggerOperation("Update bank details")]
        public async Task<IActionResult> UpdateBankDetails([FromBody] UpdateBankDetailsRequest request)
        {
            try
            {
                await _profileService.UpdateBankDetailsAsync(request);
                return Ok(new { success = true, message = "Bank details updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating bank details");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("avatar")]
        [SwaggerOperation("Upload profile picture")]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { success = false, message = "No file uploaded" });

                var avatarUrl = await _profileService.UploadAvatarAsync(file);
                return Ok(new { success = true, data = new { avatarUrl } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading avatar");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("change-password")]
        [SwaggerOperation("Change password")]
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

        // ── Maps consultant's stored country name/code to Paystack country slug ──
        private static string MapCountryToPaystack(string? country) =>
            country?.ToLower().Trim() switch
            {
                "nigeria"      or "ng" => "nigeria",
                "ghana"        or "gh" => "ghana",
                "south africa" or "za" => "south africa",
                "kenya"        or "ke" => "kenya",
                "egypt"        or "eg" => "egypt",
                "rwanda"       or "rw" => "rwanda",
                "ivory coast"  or "ci"
                    or "côte d'ivoire" => "ivory coast",
                _ => ""   // unsupported → empty list → frontend free-text
            };
    }

    public record VerifyAccountRequest(string AccountNumber, string BankCode);
}
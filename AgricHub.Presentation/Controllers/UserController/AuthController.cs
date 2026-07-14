// AgricHub.Presentation/Controllers/UserController/AuthController.cs

using AgricHub.BLL.Interfaces.IUserServices;
using AgricHub.DAL.Entities;
using AgricHub.Shared.DTO_s.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Net;

namespace AgricHub.Presentation.Controllers.UserController
{
    [ApiController]
    [Route("/api/agrichub/authentication")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authentication;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthController(IAuthService authentication, UserManager<ApplicationUser> userManager)
        {
            _authentication = authentication;
            _userManager    = userManager;
        }

        [HttpPost("login")]
        [SwaggerOperation(Summary = "Authenticate user and create token")]
        [SwaggerResponse((int)HttpStatusCode.OK, "Token created successfully.")]
        [SwaggerResponse((int)HttpStatusCode.BadRequest, "Invalid user credentials.")]
        public async Task<IActionResult> Authenticate([FromBody] UserAuthenticationResponse user)
        {
            var response = await _authentication.ValidateUser(user);
            if (!response.Success)
                return BadRequest(response);
            var token = await _authentication.CreateToken();
            return Ok(new { Token = token, Role = response.Role });
        }

        [HttpPost("google")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Sign in or register with Google")]
        [SwaggerResponse((int)HttpStatusCode.OK, "Google authentication successful.")]
        [SwaggerResponse((int)HttpStatusCode.BadRequest, "Invalid Google credential.")]
        public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthRequest request)
        {
            try
            {
                var response = await _authentication.GoogleAuth(request.Credential, request.Role);
                return Ok(response);
            }
            catch (InvalidOperationException e)
            {
                return BadRequest(new { message = e.Message });
            }
        }

        // Was completely missing — the frontend (verify-email.component.ts) has
        // been calling GET {base}/verify?email=X&token=Y since it was built, but
        // no matching endpoint ever existed. AuthService.VerifyUser was already
        // written and ready, just never wired to any route.
        [HttpGet("verify")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Confirm a user's email via the link sent at registration")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string email, [FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
                return BadRequest(new { success = false, message = "Missing email or token." });

            var user = await _authentication.VerifyUser(email, token);
            if (user == null)
                return BadRequest(new { success = false, message = "Invalid or expired verification link." });

            return Ok(new { success = true, message = "Email verified successfully." });
        }
    }

    public record GoogleAuthRequest(string Credential, string? Role = null);
}
using AgricHub.BLL.Interfaces.IUserServices;
using AgricHub.DAL.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Presentation.Controllers.UserController
{


    [ApiController]
    [Route("api/consultant/profile")]
    [Authorize(Roles = "Consultant")]
    public class ConsultantVerificationController(IConsultantVerificationService verifService) : ControllerBase
    {
        // GET /api/consultant/profile/verification-status
        [HttpGet("verification-status")]
        public async Task<IActionResult> GetStatus()
        {
            try { return Ok(await verifService.GetVerificationStatusAsync()); }
            catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
        }

        // POST /api/consultant/profile/verification
        [HttpPost("verification")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Submit(
            IFormFile businessReg,
            IFormFile credentials,
            IFormFile? governmentId)
        {
            if (businessReg is null || credentials is null)
                return BadRequest(new { message = "Business registration and credentials are required." });

            try
            {
                await verifService.SubmitVerificationAsync(new SubmitVerificationRequest
                {
                    BusinessReg  = businessReg,
                    Credentials  = credentials,
                    GovernmentId = governmentId
                });
                return Ok(new { message = "Verification submitted successfully." });
            }
            catch (InvalidOperationException e) { return Conflict(new { message = e.Message }); }
            catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
        }
    }
}
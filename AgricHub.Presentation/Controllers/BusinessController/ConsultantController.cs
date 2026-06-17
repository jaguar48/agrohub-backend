// AgricHub.Presentation/Controllers/BusinessController/ConsultantController.cs

using AgricHub.BLL.Interfaces.IUserServices;
using AgricHub.Presentation.Filters;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace AgricHub.Presentation.Controllers.BusinessController
{
    [ApiController]
    [Route("api/agrichub")]
    public class ConsultantController : ControllerBase
    {
        private readonly IConsultantService _consultantServices;

        public ConsultantController(IConsultantService consultantServices)
        {
            _consultantServices = consultantServices;
        }

        [HttpPost("register")]
        [SwaggerOperation("Registers a new consultant.")]
        [SwaggerResponse(200, "The consultant has been successfully registered.", typeof(ConsultantRegistrationRequest))]
        public async Task<IActionResult> RegisterConsultant([FromBody] ConsultantRegistrationRequest consultantRegistrationRequest)
        {
            var result = await _consultantServices.RegisterConsultant(consultantRegistrationRequest);
            return Ok(result);
        }

        // GET /api/agrichub/consultants?search=adaeze&countryId=NG
        [HttpGet("consultants")]
        [AllowAnonymous]
        [SwaggerOperation("Returns all verified consultants. Optionally filter by search term or country.")]
        public async Task<IActionResult> GetAllConsultants(
            [FromQuery] string? search = null,
            [FromQuery] string? countryId = null)
        {
            var result = await _consultantServices.GetAllConsultantsAsync(search, countryId);
            return Ok(result);
        }

        // GET /api/agrichub/consultants/101
        [HttpGet("consultants/{id:int}")]
        [AllowAnonymous]
        [SwaggerOperation("Returns full public profile of a consultant by ID.")]
        public async Task<IActionResult> GetConsultantById(int id)
        {
            try
            {
                var result = await _consultantServices.GetConsultantByIdAsync(id);
                return Ok(result);
            }
            catch (KeyNotFoundException e)
            {
                return NotFound(new { message = e.Message });
            }
        }
    }
}
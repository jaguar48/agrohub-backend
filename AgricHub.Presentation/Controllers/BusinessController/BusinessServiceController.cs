using AgricHub.BLL.Interfaces.IAgrichub_Services;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;

namespace AgricHub.Presentation.Controllers.BusinessController
{
    [ApiController]
    [Route("api/agrichub/service")]
    public class ServiceController : ControllerBase
    {
        private readonly IBusinessForService _businessForService;

        public ServiceController(IBusinessForService businessForService)
        {
            _businessForService = businessForService;
        }

        [Authorize(Roles = "Consultant")]  
        [HttpPost("add")]
        [SwaggerOperation("Adds a service to an existing business.")]
        public async Task<IActionResult> AddServiceToBusiness([FromForm] CreateServiceRequest serviceRequest)
        {
            try
            {
                var result = await _businessForService.AddServiceAsync(serviceRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [Authorize(Roles = "Consultant")] 
        [HttpPut("update/{serviceId}")]
        [SwaggerOperation("Updates an existing service.")]
        public async Task<IActionResult> UpdateService([FromRoute] int serviceId, [FromForm] CreateServiceRequest serviceRequest)
        {
            try
            {
                var result = await _businessForService.UpdateServiceAsync(serviceId, serviceRequest);
                return Ok(JsonConvert.DeserializeObject(result));
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("view/{serviceId}")]
        [SwaggerOperation("Retrieves details of a specific service.")]
        public async Task<IActionResult> ViewService([FromRoute] int serviceId)
        {
            try
            {
                var result = await _businessForService.ViewServiceAsync(serviceId);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("all")]
        [SwaggerOperation("Retrieves all available services.")]
        public async Task<IActionResult> GetAllServices()
        {
            try
            {
                var result = await _businessForService.ViewAllServicesAsync();
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [Authorize(Roles = "Consultant")]
        [HttpGet("my-services")]
        [SwaggerOperation("Retrieves all services owned by the logged-in business consultant.")]
        public async Task<IActionResult> GetMyBusinessServices()
        {
            try
            {
                var result = await _businessForService.ViewOwnBusinessServicesAsync();
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }


        [HttpDelete("delete/{serviceId}")]
        [SwaggerOperation("Deletes a specific service.")]
        [SwaggerResponse(200, "Service deleted successfully.")]
        [SwaggerResponse(400, "Bad request. Invalid input or unauthorized.")]
        public async Task<IActionResult> DeleteService([FromRoute] int serviceId)
        {
            try
            {
                var result = await _businessForService.DeleteServiceAsync(serviceId);
                return Ok(result); 
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }


    }
}

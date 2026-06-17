using AgricHub.BLL.Interfaces.IUserServices;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace AgricHub.Presentation.Controllers.BusinessController
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterCustomer([FromBody] CustomerRegistrationRequest request)
        {
            try
            {
                var resultJson = await _customerService.RegisterCustomer(request);
                return Ok(resultJson);
            }
            catch (Exception ex)
            {
                // You can log the error here if you want
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}

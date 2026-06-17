using AgricHub.BLL.Interfaces.IAgrichub_Services;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace AgricHub.Presentation.Controllers.BusinessController
{
    [ApiController]
    [Route("/api/agrichub/business")]
    public class BusinessController : ControllerBase
    {
        private readonly IBusiness_ConsultServices _business_ConsultServices;

        public BusinessController(IBusiness_ConsultServices businessServices)
        {
            _business_ConsultServices = businessServices;
        }

        [Authorize(Roles = "Consultant")]
        [HttpPost("createBusiness")]
        public async Task<IActionResult> CreateBusiness([FromForm] CreateBusinessRequest businessRequest)
        {
            try
            {
                var result = await _business_ConsultServices.AddBusiness(businessRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
        [Authorize(Roles = "Consultant")]
        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateBusiness(int id, [FromForm] UpdateBusinessRequest request)
        {
            try
            {
                await _business_ConsultServices.UpdateBusinessAsync(id, request);
                return Ok(new { success = true, message = "Business updated successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("createCategory")]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest categoryRequest)
        {
            try
            {
                var result = await _business_ConsultServices.AddCategory(categoryRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [Authorize(Roles = "Consultant")]
        [HttpGet("my-business")]           // ← new endpoint
        public async Task<IActionResult> GetMyBusiness()
        {
            try
            {
                var result = await _business_ConsultServices.GetMyBusinessAsync();
                if (result == null)
                    return Ok(new { success = false, data = (object)null, message = "No business found." });
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("categories")]            // ← fix for the 404
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _business_ConsultServices.GetCategoriesAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
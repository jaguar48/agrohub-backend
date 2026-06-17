using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Request;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgricHub.BLL.Interfaces.IAgrichub_Services
{
    public interface IBusiness_ConsultServices
    {
        Task<string> AddCategory(CreateCategoryRequest categoryRequest);
        Task<string> AddBusiness(CreateBusinessRequest businessRequest);
        Task<Business> GetMyBusinessAsync();
        Task<IEnumerable<Category>> GetCategoriesAsync();  // ← new
        Task UpdateBusinessAsync(int id, UpdateBusinessRequest request);
    }
}
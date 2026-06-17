using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.BLL.Interfaces
{
    public interface IPlatformSettingsService
    {
        Task<string?> GetAsync(string key);
        Task<Dictionary<string, string>> GetCategoryAsync(string category);
        Task<IEnumerable<object>> GetAllAsync();
        Task UpdateAsync(string key, string value);
        Task UpdateBulkAsync(Dictionary<string, string> updates);
    }
}



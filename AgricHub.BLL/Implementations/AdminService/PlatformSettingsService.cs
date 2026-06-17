using AgricHub.BLL.Interfaces;
using AgricHub.Contracts;
using AgricHub.DAL.Entities.Models;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.BLL.Implementations
{
    public class PlatformSettingsService : IPlatformSettingsService
    {
        private readonly IRepository<PlatformSetting> _repo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _cache;

        private const string CachePrefix = "setting:";

        public PlatformSettingsService(IUnitOfWork unitOfWork, IMemoryCache cache)
        {
            _unitOfWork = unitOfWork;
            _cache      = cache;
            _repo       = unitOfWork.GetRepository<PlatformSetting>();
        }

        public async Task<string?> GetAsync(string key)
        {
            var cacheKey = CachePrefix + key;
            if (_cache.TryGetValue(cacheKey, out string? cached)) return cached;

            var setting = await _repo.GetSingleByAsync(s => s.Key == key);
            var value = setting?.Value;
            _cache.Set(cacheKey, value, TimeSpan.FromMinutes(10));
            return value;
        }

        public async Task<Dictionary<string, string>> GetCategoryAsync(string category)
        {
            var settings = await _repo.GetByAsync(s => s.Category == category);
            return settings.ToDictionary(s => s.Key, s => s.Value ?? "");
        }

        public async Task<IEnumerable<object>> GetAllAsync()
        {
            var settings = await _repo.GetAllAsync(orderBy: q => q.OrderBy(s => s.Category).ThenBy(s => s.SortOrder));
            return settings.Select(s => new
            {
                s.Key,
                s.Category,
                s.Label,
                s.InputType,
                s.Group,
                s.SortOrder,
                Value = s.IsSecret && !string.IsNullOrEmpty(s.Value) ? "••••••••" : s.Value,
                IsSecret = s.IsSecret
            });
        }

        public async Task UpdateAsync(string key, string value)
        {
            var setting = await _repo.GetSingleByAsync(s => s.Key == key);
            if (setting == null) return;
            setting.Value     = value;
            setting.UpdatedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(setting);
            await _unitOfWork.SaveChangesAsync();
            _cache.Remove(CachePrefix + key);
        }

        public async Task UpdateBulkAsync(Dictionary<string, string> updates)
        {
            foreach (var (key, value) in updates)
            {
                var setting = await _repo.GetSingleByAsync(s => s.Key == key);
                if (setting == null) continue;
                setting.Value     = value;
                setting.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(setting);
                _cache.Remove(CachePrefix + key);
            }
            await _unitOfWork.SaveChangesAsync();
        }
    }
}

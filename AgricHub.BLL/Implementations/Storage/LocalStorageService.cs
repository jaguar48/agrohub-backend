using AgricHub.BLL.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.BLL.Implementations
{
    public class LocalStorageService : IStorageService
    {
        private readonly string _baseUrl;
        private readonly string _basePath;

        public LocalStorageService(IConfiguration config)
        {
            _baseUrl  = config["AppBaseUrl"] ?? "https://localhost:7212";
            _basePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources");
        }

        public async Task<string> UploadAsync(Stream stream, string fileName, string folder = "agrichub")
        {
            var ext = Path.GetExtension(fileName).ToLower();
            var subFolder = new[] { ".mp4", ".mov", ".avi", ".webm" }.Contains(ext) ? "Videos"
                          : new[] { ".pdf" }.Contains(ext) ? "Docs"
                          : "Images";

            var dir = Path.Combine(_basePath, subFolder);
            Directory.CreateDirectory(dir);

            var newName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(dir, newName);

            await using var fs = File.Create(fullPath);
            await stream.CopyToAsync(fs);

            return $"{_baseUrl}/Resources/{subFolder}/{newName}";
        }

        public Task DeleteAsync(string publicUrlOrId)
        {
            try
            {
                if (publicUrlOrId.StartsWith("http"))
                {
                    var uri = new Uri(publicUrlOrId);
                    var relative = uri.AbsolutePath.TrimStart('/');
                    var fullPath = Path.Combine(Directory.GetCurrentDirectory(), relative.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(fullPath)) File.Delete(fullPath);
                }
            }
            catch { /* ignore */ }
            return Task.CompletedTask;
        }
    }
}

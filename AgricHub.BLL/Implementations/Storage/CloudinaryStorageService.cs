// AgricHub.BLL/Implementations/CloudinaryStorageService.cs
// NuGet: CloudinaryDotNet

using AgricHub.BLL.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgricHub.BLL.Implementations
{
    public class CloudinaryStorageService : IStorageService
    {
        private readonly CloudinaryDotNet.Cloudinary _cloudinary;
        private readonly ILogger<CloudinaryStorageService> _logger;

        public CloudinaryStorageService(IConfiguration config, ILogger<CloudinaryStorageService> logger)
        {
            _logger = logger;
            var account = new CloudinaryDotNet.Account(
                config["Cloudinary:CloudName"],
                config["Cloudinary:ApiKey"],
                config["Cloudinary:ApiSecret"]);
            _cloudinary = new CloudinaryDotNet.Cloudinary(account) { Api = { Secure = true } };
        }

        public async Task<string> UploadAsync(Stream stream, string fileName, string folder = "agrichub")
        {
            var ext = Path.GetExtension(fileName).ToLower();
            var isVideo = new[] { ".mp4", ".mov", ".avi", ".webm" }.Contains(ext);
            var isRawFile = new[] { ".pdf", ".doc", ".docx" }.Contains(ext);
            var publicId = $"{folder}/{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}";

            if (isVideo)
            {
                var uploadParams = new VideoUploadParams
                {
                    File     = new FileDescription(fileName, stream),
                    PublicId = publicId,
                    Folder   = folder,
                };
                var result = await _cloudinary.UploadAsync(uploadParams);
                if (result.Error != null) throw new Exception($"Cloudinary video upload failed: {result.Error.Message}");
                _logger.LogInformation("[Storage] Video uploaded: {Url}", result.SecureUrl);
                return result.SecureUrl.ToString();
            }
            else if (isRawFile)
            {
                var uploadParams = new RawUploadParams
                {
                    File     = new FileDescription(fileName, stream),
                    PublicId = publicId,
                    Folder   = folder,
                };
                var result = await _cloudinary.UploadAsync(uploadParams);
                if (result.Error != null) throw new Exception($"Cloudinary upload failed: {result.Error.Message}");
                _logger.LogInformation("[Storage] File uploaded: {Url}", result.SecureUrl);
                return result.SecureUrl.ToString();
            }
            else
            {
                var uploadParams = new ImageUploadParams
                {
                    File            = new FileDescription(fileName, stream),
                    PublicId        = publicId,
                    Folder          = folder,
                    Transformation  = new Transformation().Quality("auto").FetchFormat("auto"),
                };
                var result = await _cloudinary.UploadAsync(uploadParams);
                if (result.Error != null) throw new Exception($"Cloudinary image upload failed: {result.Error.Message}");
                _logger.LogInformation("[Storage] Image uploaded: {Url}", result.SecureUrl);
                return result.SecureUrl.ToString();
            }
        }

        public async Task DeleteAsync(string publicUrlOrId)
        {
            try
            {
                // Extract public ID from URL if full URL passed
                var publicId = publicUrlOrId.Contains("cloudinary.com")
                    ? ExtractPublicId(publicUrlOrId)
                    : publicUrlOrId;

                await _cloudinary.DestroyAsync(new DeletionParams(publicId));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[Storage] Delete failed: {Msg}", ex.Message);
            }
        }

        private static string ExtractPublicId(string url)
        {
            // e.g. https://res.cloudinary.com/cloud/image/upload/v123/agrichub/file-abc.jpg
            // → agrichub/file-abc
            var uri = new Uri(url);
            var parts = uri.AbsolutePath.Split('/');
            var upload = Array.IndexOf(parts, "upload");
            if (upload < 0) return url;
            var relevant = parts.Skip(upload + 2); // skip version segment
            return string.Join("/", relevant).Replace(Path.GetExtension(url), "");
        }
    }
}
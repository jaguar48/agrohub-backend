using AgricHub.BLL.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgricHub.BLL.Interfaces
{
    /// <summary>
    /// Routes to Cloudinary or local disk storage based on the LIVE
    /// features.cloudStorage setting, checked on every call — not decided once
    /// at DI registration time like the old ServiceExtension.ConfigureServices
    /// conditional (`if (cloudName is set) AddScoped&lt;IStorageService, Cloudinary...&gt;
    /// else AddScoped&lt;IStorageService, Local...&gt;`), which meant flipping the
    /// admin toggle had zero effect until the app restarted.
    ///
    /// Both concrete implementations are registered directly in DI (not as
    /// IStorageService) and resolved here per-call via IServiceProvider, so this
    /// class needs no direct dependency on either's internals.
    ///
    /// Safety: if the toggle says "use Cloudinary" but the credentials aren't
    /// actually configured (CloudName missing/placeholder), falls back to local
    /// storage rather than throwing — an admin flipping a switch shouldn't be
    /// able to break file uploads app-wide by itself.
    /// </summary>
    public class StorageServiceRouter : IStorageService
    {
        private readonly IServiceProvider _provider;
        private readonly IPlatformSettingsService _settings;
        private readonly IConfiguration _config;
        private readonly ILogger<StorageServiceRouter> _logger;

        public StorageServiceRouter(
            IServiceProvider provider,
            IPlatformSettingsService settings,
            IConfiguration config,
            ILogger<StorageServiceRouter> logger)
        {
            _provider = provider;
            _settings = settings;
            _config   = config;
            _logger   = logger;
        }

        private async Task<IStorageService> ResolveAsync()
        {
            var toggleRaw = await _settings.GetAsync("features.cloudStorage");
            var wantsCloud = toggleRaw == "true";

            var cloudName = _config["Cloudinary:CloudName"];
            var cloudConfigured = !string.IsNullOrEmpty(cloudName) && cloudName != "your-cloud-name";

            if (wantsCloud && cloudConfigured)
                return _provider.GetRequiredService<CloudinaryStorageService>();

            if (wantsCloud && !cloudConfigured)
                _logger.LogWarning(
                    "[Storage] features.cloudStorage is on but Cloudinary credentials aren't configured — falling back to local storage.");

            return _provider.GetRequiredService<LocalStorageService>();
        }

        public async Task<string> UploadAsync(Stream stream, string fileName, string folder = "agrichub")
        {
            var svc = await ResolveAsync();
            return await svc.UploadAsync(stream, fileName, folder);
        }

        public async Task DeleteAsync(string publicUrlOrId)
        {
            var svc = await ResolveAsync();
            await svc.DeleteAsync(publicUrlOrId);
        }
    }
}

using AgricHub.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AgricHub.Presentation.Controllers.AdminController
{
    /// <summary>
    /// Unauthenticated read endpoint exposing ONLY the platform settings the
    /// frontend needs to render correctly (currency, feature toggles, booking
    /// display rules). Never exposes secrets (API keys, SMTP credentials etc.)
    /// — those stay behind AdminController.
    ///
    /// Why this exists: PlatformSettingsService/PlatformSettingsSeeder already
    /// store 30+ settings, and the admin UI can edit them — but nothing in the
    /// app actually reads them at runtime. This is the first piece: give the
    /// frontend a way to read the handful of settings it needs.
    /// </summary>
    [ApiController]
    [Route("api/public-settings")]
    public class PublicSettingsController(IPlatformSettingsService settings) : ControllerBase
    {
        // Explicit allow-list — never blindly expose everything, since some
        // categories (integrations, email) contain secrets even when IsSecret
        // masking exists; safest to enumerate exactly what's public.
        private static readonly string[] PublicKeys =
        {
            "platform.name",
            "platform.tagline",
            "platform.supportEmail",
            "platform.logoUrl",

            "finance.currency",
            "finance.currencySymbol",
            "finance.platformFeePercent",

            "booking.maxAdvanceDays",
            "booking.cancellationHours",
            "booking.autoConfirm",
            "booking.requiresVerification",

            "features.googleAuth",
            "features.maintenanceMode",
            "features.publicRegistration",
        };

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var result = new Dictionary<string, string>();
            foreach (var key in PublicKeys)
            {
                var value = await settings.GetAsync(key);
                if (value != null) result[key] = value;
            }
            return Ok(new { success = true, data = result });
        }
    }
}

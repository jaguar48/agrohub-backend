using AgricHub.BLL.Interfaces;

namespace AgricHub.API.Middleware
{
    /// <summary>
    /// Enforces two feature toggles that PlatformSettingsSeeder already stores
    /// but that nothing previously read at runtime:
    ///
    ///   features.maintenanceMode    — when "true", blocks all traffic except
    ///                                 admins (so an admin can still log in and
    ///                                 flip it back off) with a 503.
    ///   features.publicRegistration — when "false", blocks the two register
    ///                                 endpoints with a 403 instead of touching
    ///                                 ConsultantController/CustomerController
    ///                                 directly (keeps this fully self-contained,
    ///                                 zero risk to existing registration logic).
    ///
    /// Registered in Program.cs AFTER app.UseAuthentication() so User.IsInRole
    /// is available for the maintenance-mode admin bypass.
    /// </summary>
    public class PlatformSettingsGateMiddleware
    {
        private readonly RequestDelegate _next;
        public PlatformSettingsGateMiddleware(RequestDelegate next) => _next = next;

        private static readonly string[] RegisterPaths =
        {
            "/api/agrichub/register",   // ConsultantController — [Route("api/agrichub")]
            "/api/customer/register",   // CustomerController   — [Route("api/[controller]")]
        };

        public async Task InvokeAsync(HttpContext context, IPlatformSettingsService settings)
        {
            var path = context.Request.Path.Value ?? "";

            // ── Maintenance mode ────────────────────────────────────────────
            var maintenanceRaw = await settings.GetAsync("features.maintenanceMode");
            var maintenanceOn  = string.Equals(maintenanceRaw, "true", StringComparison.OrdinalIgnoreCase);

            if (maintenanceOn)
            {
                var isAdmin = context.User?.Identity?.IsAuthenticated == true
                              && context.User.IsInRole("Admin");

                // Always allow the admin's own login so they can get back in
                var isAdminLogin = path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase);

                if (!isAdmin && !isAdminLogin)
                {
                    context.Response.StatusCode = 503;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(
                        "{\"success\":false,\"message\":\"AgricHub is temporarily down for maintenance. Please check back shortly.\"}");
                    return;
                }
            }

            // ── Public registration toggle ──────────────────────────────────
            if (context.Request.Method == HttpMethods.Post &&
                RegisterPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                var regRaw = await settings.GetAsync("features.publicRegistration");
                // Default to allowed if the setting is somehow missing —
                // fail-open here so a missing row never locks out signups.
                var regAllowed = regRaw == null || string.Equals(regRaw, "true", StringComparison.OrdinalIgnoreCase);

                if (!regAllowed)
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(
                        "{\"success\":false,\"message\":\"New registrations are currently closed.\"}");
                    return;
                }
            }

            await _next(context);
        }
    }

    public static class PlatformSettingsGateMiddlewareExtensions
    {
        public static IApplicationBuilder UsePlatformSettingsGate(this IApplicationBuilder app) =>
            app.UseMiddleware<PlatformSettingsGateMiddleware>();
    }
}

namespace AgricHub.API.Extension
{
   
    /// <summary>
    /// Adds the Permissions-Policy header required by Chrome 94+ to allow
    /// display-capture (screen sharing) inside any iframe this page hosts —
    /// including the Daily.co video call iframe embedded in the chat UI.
    ///
    /// Without this header, Chrome blocks screen sharing inside the iframe
    /// regardless of the iframe's own allow="display-capture" attribute —
    /// both the parent page header AND the iframe attribute are required.
    /// This is the actual root cause of the "screen share shows blank" bug.
    /// </summary>
    public static class ScreenSharePermissionsMiddlewareExtensions
    {
        public static IApplicationBuilder UseScreenSharePermissions(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                context.Response.Headers.Append(
                    "Permissions-Policy",
                    "camera=*, microphone=*, display-capture=*, fullscreen=*"
                );
                await next();
            });
        }
    }
}
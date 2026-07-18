using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace AgricHub.Presentation.Controllers
{
    /// <summary>
    /// Detects a visitor's country from their IP address, server-side — works
    /// for BOTH logged-in users (dashboard, wallet) and anonymous landing-page
    /// visitors browsing consultants, since the latter have no account/profile
    /// to read a country from at all. This is why it can't just read the
    /// logged-in user's registered CountryId — that field doesn't exist until
    /// someone has actually registered.
    ///
    /// Routed through our own backend (not called directly from the browser)
    /// for the same reason as ExchangeRateController: avoids depending on the
    /// third party supporting CORS, and lets us cache per-IP briefly.
    /// </summary>
    [ApiController]
    [Route("api/geo")]
    public class GeoController(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<GeoController> logger) : ControllerBase
    {
        [HttpGet("currency")]
        public async Task<IActionResult> DetectCurrency()
        {
            var ip = GetClientIp();

            // No usable IP (e.g. local dev behind no proxy) — fall back to
            // Naira, since that's the primary market and what Paystack charges
            // regardless of what's displayed.
            if (string.IsNullOrWhiteSpace(ip) || ip == "::1" || ip == "127.0.0.1")
                return Ok(new { success = true, countryCode = "NG", currency = "NGN", source = "fallback-local" });

            var cacheKey = $"geo_currency_{ip}";
            if (cache.TryGetValue(cacheKey, out object? cached))
                return Ok(cached);

            try
            {
                var client = httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(4);
                // ip-api.com — free tier, no API key, server-to-server only
                // (their free tier explicitly disallows direct browser calls,
                // which is another reason this must be a backend call).
                var response = await client.GetFromJsonAsync<IpApiResponse>(
                    $"http://ip-api.com/json/{ip}?fields=status,countryCode");

                var countryCode = (response?.Status == "success" && !string.IsNullOrEmpty(response.CountryCode))
                    ? response.CountryCode
                    : "NG";

                var currency = countryCode == "NG" ? "NGN" : "USD";
                var result = new { success = true, countryCode, currency, source = "ip-lookup" };

                cache.Set(cacheKey, result, TimeSpan.FromHours(12));
                return Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Geo] IP lookup failed for {Ip} — defaulting to NGN.", ip);
                return Ok(new { success = true, countryCode = "NG", currency = "NGN", source = "fallback-error" });
            }
        }

        private string? GetClientIp()
        {
            // Behind a reverse proxy (common in production), the real client
            // IP is in X-Forwarded-For, not Connection.RemoteIpAddress.
            var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
                return forwarded.Split(',')[0].Trim();

            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        private class IpApiResponse
        {
            public string? Status { get; set; }
            public string? CountryCode { get; set; }
        }
    }
}

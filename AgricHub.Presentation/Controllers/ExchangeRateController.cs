using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Json;

namespace AgricHub.Presentation.Controllers
{
    /// <summary>
    /// Proxies the NGN/USD exchange rate through our own backend instead of
    /// letting the browser call a third-party API directly. Two reasons:
    ///   1. Avoids depending on that third party supporting CORS for browser
    ///      fetches — server-to-server calls have no such restriction.
    ///   2. One shared cache for ALL users instead of every browser hitting
    ///      the external API independently.
    /// DISPLAY-ONLY, same as the frontend service — never used for anything
    /// that touches actual money movement (Paystack always charges Naira).
    /// </summary>
    [ApiController]
    [Route("api/exchange-rate")]
    public class ExchangeRateController(IHttpClientFactory httpClientFactory, IMemoryCache cache) : ControllerBase
    {
        private const string CacheKey = "exchange_rate_ngn_per_usd";

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            if (cache.TryGetValue(CacheKey, out decimal cachedRate))
                return Ok(new { success = true, ngnPerUsd = cachedRate, cached = true });

            try
            {
                var client = httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(6);
                var response = await client.GetAsync("https://open.er-api.com/v6/latest/USD");

                if (!response.IsSuccessStatusCode)
                    return Ok(new { success = false, message = "Rate provider unavailable." });

                var json = await response.Content.ReadFromJsonAsync<ExchangeRateApiResponse>();
                var rate = json?.Rates?.GetValueOrDefault("NGN") ?? 0;

                if (rate <= 0)
                    return Ok(new { success = false, message = "Rate not available." });

                cache.Set(CacheKey, rate, TimeSpan.FromHours(6));
                return Ok(new { success = true, ngnPerUsd = rate, cached = false });
            }
            catch
            {
                return Ok(new { success = false, message = "Could not fetch exchange rate." });
            }
        }

        private class ExchangeRateApiResponse
        {
            public Dictionary<string, decimal>? Rates { get; set; }
        }
    }
}

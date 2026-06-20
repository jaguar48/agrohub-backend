using AgricHub.BLL.Interfaces.IPaystackService;
using AgricHub.Shared.DTO_s.Response;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgricHub.BLL.Implementations.PaystackService
{
    public class PaystackService : IPaystackService
    {
        private readonly HttpClient _httpClient;
        private readonly string _secretKey;
        private readonly ILogger<PaystackService> _logger;

        public PaystackService(
            IConfiguration configuration,
            HttpClient httpClient,
            ILogger<PaystackService> logger)
        {
            _secretKey  = configuration["Paystack:SecretKey"];
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://api.paystack.co/");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _secretKey);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            // Browser-like UA — reduces the chance of triggering Cloudflare's
            // bot challenge on api.paystack.co compared to a custom UA string.
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _logger = logger;
        }

        /// <summary>True if Paystack's response is actually a Cloudflare challenge page, not JSON.</summary>
        private static bool IsCloudflareBlock(string content) =>
            content.TrimStart().StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
            content.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Attention Required", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("cf-error", StringComparison.OrdinalIgnoreCase);

        public async Task<(string accessCode, string reference)> InitializeTransactionAsync(
            string email, decimal amount, string callbackUrl)
        {
            try
            {
                var request = new
                {
                    email,
                    amount = (int)(amount * 100),
                    currency = "USD",
                    callback_url = callbackUrl,
                    metadata = new
                    {
                        custom_fields = new[]
                        {
                            new { display_name = "Transaction Type",
                                  variable_name = "transaction_type",
                                  value = "Wallet Top-Up" }
                        }
                    }
                };

                var response = await _httpClient.PostAsJsonAsync("transaction/initialize", request);
                var content = await response.Content.ReadAsStringAsync();

                if (IsCloudflareBlock(content))
                {
                    _logger.LogError("Paystack InitializeTransactionAsync blocked by Cloudflare.");
                    throw new InvalidOperationException(
                        "Payment initialization is temporarily blocked by Paystack's network security. Please try again shortly.");
                }

                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Failed to initialize transaction: {content}");

                var result = JsonSerializer.Deserialize<PaystackInitializeResponse>(
                    content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Status != true || result.Data == null)
                    throw new InvalidOperationException($"Invalid Paystack response: {content}");

                return (result.Data.AccessCode, result.Data.Reference);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Paystack transaction");
                throw;
            }
        }

        public async Task<PaystackVerificationResponse> VerifyTransactionAsync(string reference)
        {
            try
            {
                var response = await _httpClient.GetAsync($"transaction/verify/{reference}");
                var content = await response.Content.ReadAsStringAsync();

                if (IsCloudflareBlock(content))
                {
                    _logger.LogError(
                        "Paystack VerifyTransactionAsync was blocked by Cloudflare for reference {Reference}. " +
                        "This is a network/IP-reputation issue, not an application bug — the server's outbound " +
                        "request to api.paystack.co is being challenged before it reaches Paystack's actual API.",
                        reference);
                    throw new InvalidOperationException(
                        "Payment verification is temporarily blocked by Paystack's network security (Cloudflare). " +
                        "This usually resolves on retry, or may require contacting Paystack support if it persists. " +
                        "Your payment was NOT lost — try verifying again in a moment.");
                }

                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Failed to verify transaction: {content}");

                var result = JsonSerializer.Deserialize<PaystackVerificationResponse>(
                    content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Status != true)
                    throw new InvalidOperationException("Transaction verification failed");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying Paystack transaction: {reference}");
                throw;
            }
        }

        public async Task InitiateConsultantPayoutAsync(
            string reference, string recipientCode, decimal amount)
        {
            try
            {
                var request = new
                {
                    source = "balance",
                    reason = $"Payout for consultation {reference}",
                    amount = (int)(amount * 100),
                    recipient = recipientCode
                };

                var response = await _httpClient.PostAsJsonAsync("transfer", request);
                var content = await response.Content.ReadAsStringAsync();

                if (IsCloudflareBlock(content))
                {
                    _logger.LogError("Paystack InitiateConsultantPayoutAsync blocked by Cloudflare for reference {Reference}.", reference);
                    throw new InvalidOperationException(
                        "Payout is temporarily blocked by Paystack's network security. Please try again shortly.");
                }

                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Failed to initiate payout: {content}");

                var result = JsonSerializer.Deserialize<PaystackTransferResponse>(
                    content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Status != true)
                    throw new InvalidOperationException("Transfer initiation failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error initiating payout: {reference}");
                throw;
            }
        }

        public async Task<string> CreateTransferRecipientAsync(
            string accountNumber, string accountName, string bankCode)
        {
            try
            {
                var request = new
                {
                    type = "nuban",
                    name = accountName,
                    account_number = accountNumber,
                    bank_code = bankCode,
                    currency = "NGN"
                };

                var response = await _httpClient.PostAsJsonAsync("transferrecipient", request);
                var content = await response.Content.ReadAsStringAsync();

                if (IsCloudflareBlock(content))
                {
                    _logger.LogError("Paystack CreateTransferRecipientAsync blocked by Cloudflare.");
                    throw new InvalidOperationException(
                        "Bank recipient creation is temporarily blocked by Paystack's network security. Please try again shortly.");
                }

                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Failed to create transfer recipient: {content}");

                var result = JsonSerializer.Deserialize<PaystackRecipientResponse>(
                    content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Status != true || result.Data == null)
                    throw new InvalidOperationException("Failed to create transfer recipient");

                return result.Data.RecipientCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating transfer recipient");
                throw;
            }
        }

        public async Task<List<BankInfo>> GetBanksAsync(string country = "nigeria")
        {
            try
            {
                if (string.IsNullOrEmpty(country)) return new List<BankInfo>();
                var response = await _httpClient.GetAsync($"bank?country={Uri.EscapeDataString(country)}&perPage=100");
                var content = await response.Content.ReadAsStringAsync();

                // Cloudflare/geo-block guard — HTML response means the API is unreachable
                if (IsCloudflareBlock(content))
                {
                    _logger.LogWarning("Paystack API returned HTML (Cloudflare block). Using fallback bank list.");
                    return FallbackNigerianBanks();
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Paystack banks unavailable ({response.StatusCode}). Using fallback.");
                    return FallbackNigerianBanks();
                }

                var result = JsonSerializer.Deserialize<PaystackBanksResponse>(
                    content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Status != true || result.Data == null || result.Data.Count == 0)
                {
                    _logger.LogWarning("Paystack returned empty bank list. Using fallback.");
                    return FallbackNigerianBanks();
                }

                return result.Data.Select(b => new BankInfo { Code = b.Code, Name = b.Name }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting banks — using fallback list.");
                return FallbackNigerianBanks();
            }
        }

        public async Task<BankAccountDetails> ResolveAccountNumberAsync(
            string accountNumber, string bankCode)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"bank/resolve?account_number={accountNumber}&bank_code={bankCode}");
                var content = await response.Content.ReadAsStringAsync();

                if (IsCloudflareBlock(content))
                    throw new InvalidOperationException(
                        "Bank verification is temporarily unavailable. Please enter your account name manually.");

                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException("Invalid bank account details");

                var result = JsonSerializer.Deserialize<PaystackResolveAccountResponse>(
                    content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Status != true || result.Data == null)
                    throw new InvalidOperationException("Invalid bank account details");

                return new BankAccountDetails
                {
                    AccountNumber = result.Data.AccountNumber,
                    AccountName   = result.Data.AccountName,
                    BankCode      = bankCode
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resolving account: {accountNumber}");
                throw;
            }
        }

        // ── Fallback bank list (shown when Paystack API is unreachable) ─────

        private static List<BankInfo> FallbackNigerianBanks() => new()
        {
            new() { Code = "044", Name = "Access Bank" },
            new() { Code = "063", Name = "Access Bank (Diamond)" },
            new() { Code = "035A", Name = "ALAT by Wema" },
            new() { Code = "401", Name = "ASO Savings and Loans" },
            new() { Code = "023", Name = "Citibank Nigeria" },
            new() { Code = "050", Name = "Ecobank Nigeria" },
            new() { Code = "562", Name = "Ekondo Microfinance Bank" },
            new() { Code = "070", Name = "Fidelity Bank" },
            new() { Code = "011", Name = "First Bank of Nigeria" },
            new() { Code = "214", Name = "First City Monument Bank (FCMB)" },
            new() { Code = "058", Name = "Guaranty Trust Bank (GTBank)" },
            new() { Code = "030", Name = "Heritage Bank" },
            new() { Code = "301", Name = "Jaiz Bank" },
            new() { Code = "082", Name = "Keystone Bank" },
            new() { Code = "606", Name = "Kuda Bank" },
            new() { Code = "526", Name = "Moniepoint MFB" },
            new() { Code = "076", Name = "Polaris Bank" },
            new() { Code = "101", Name = "Providus Bank" },
            new() { Code = "221", Name = "Stanbic IBTC Bank" },
            new() { Code = "068", Name = "Standard Chartered Bank" },
            new() { Code = "232", Name = "Sterling Bank" },
            new() { Code = "100", Name = "SunTrust Bank" },
            new() { Code = "032", Name = "Union Bank of Nigeria" },
            new() { Code = "033", Name = "United Bank for Africa (UBA)" },
            new() { Code = "215", Name = "Unity Bank" },
            new() { Code = "035", Name = "Wema Bank" },
            new() { Code = "057", Name = "Zenith Bank" },
        };
    }

    // ── Response models ───────────────────────────────────────────────────────

    public class PaystackInitializeResponse
    {
        public bool Status { get; set; }
        public string Message { get; set; }
        public PaystackInitializeData Data { get; set; }
    }
    public class PaystackInitializeData
    {
        [JsonPropertyName("authorization_url")] public string AuthorizationUrl { get; set; }
        [JsonPropertyName("access_code")] public string AccessCode { get; set; }
        [JsonPropertyName("reference")] public string Reference { get; set; }
    }
    public class PaystackVerificationResponse
    {
        public bool Status { get; set; }
        public string Message { get; set; }
        public PaystackVerificationData Data { get; set; }
    }
    public class PaystackVerificationData
    {
        public string Reference { get; set; }
        public string Status { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public DateTime TransactionDate { get; set; }
        public PaystackCustomer Customer { get; set; }
    }
    public class PaystackCustomer
    {
        public string Email { get; set; }
        public string CustomerCode { get; set; }
    }
    public class PaystackTransferResponse
    {
        public bool Status { get; set; }
        public string Message { get; set; }
        public PaystackTransferData Data { get; set; }
    }
    public class PaystackTransferData
    {
        public string TransferCode { get; set; }
        public string Reference { get; set; }
        public string Status { get; set; }
        public decimal Amount { get; set; }
    }
    public class PaystackRecipientResponse
    {
        public bool Status { get; set; }
        public string Message { get; set; }
        public PaystackRecipientData Data { get; set; }
    }
    public class PaystackRecipientData
    {
        [JsonPropertyName("recipient_code")] public string RecipientCode { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
    }
    public class PaystackBanksResponse
    {
        public bool Status { get; set; }
        public string Message { get; set; }
        public List<PaystackBank> Data { get; set; }
    }
    public class PaystackBank
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public string Longcode { get; set; }
        public string Gateway { get; set; }
    }
    public class PaystackResolveAccountResponse
    {
        public bool Status { get; set; }
        public string Message { get; set; }
        public PaystackAccountData Data { get; set; }
    }
    public class PaystackAccountData
    {
        [JsonPropertyName("account_number")] public string AccountNumber { get; set; }
        [JsonPropertyName("account_name")] public string AccountName { get; set; }
        [JsonPropertyName("bank_id")] public int BankId { get; set; }
    }
}
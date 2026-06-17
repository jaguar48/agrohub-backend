using AgricHub.BLL.Interfaces.IWalletService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace AgricHub.Presentation.Controllers.WalletController
{
    [ApiController]
    [Route("api/wallet")]
    [Authorize]
    public class WalletController : ControllerBase
    {
        private readonly IWalletService _walletService;
        private readonly ILogger<WalletController> _logger;

        public WalletController(IWalletService walletService, ILogger<WalletController> logger)
        {
            _walletService = walletService;
            _logger = logger;
        }

        /// <summary>
        /// Get wallet information (balance, user details, status)
        /// </summary>
        [HttpGet]
        [SwaggerOperation("Get wallet overview")]
        [SwaggerResponse(200, "Wallet retrieved successfully")]
        public async Task<IActionResult> GetWallet()
        {
            try
            {
                var wallet = await _walletService.GetMyWalletAsync();
                return Ok(new ApiResponse(true, "Wallet retrieved successfully.", wallet));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving wallet.");
                return BadRequest(new ApiResponse(false, ex.Message));
            }
        }

        /// <summary>
        /// Get wallet transaction history
        /// </summary>
        [HttpGet("transactions")]
        [SwaggerOperation("Get wallet transaction history")]
        [SwaggerResponse(200, "Transactions retrieved successfully")]
        public async Task<IActionResult> GetTransactions()
        {
            try
            {
                var transactions = await _walletService.GetMyTransactionsAsync();
                return Ok(new ApiResponse(true, "Transactions retrieved successfully.", transactions));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving transactions.");
                return BadRequest(new ApiResponse(false, ex.Message));
            }
        }

        /// <summary>
        /// Initiate wallet top-up (returns Paystack payment URL)
        /// </summary>
        [HttpPost("topup")]
        [Authorize(Roles = "Customer")]
        [SwaggerOperation("Initiate wallet top-up")]
        [SwaggerResponse(200, "Payment link generated successfully")]
        [SwaggerResponse(400, "Invalid amount or error occurred")]
        public async Task<IActionResult> TopUpWallet([FromBody] TopUpRequest request)
        {
            if (request.Amount <= 0)
                return BadRequest(new ApiResponse(false, "Amount must be greater than zero."));

            try
            {
                var result = await _walletService.TopUpWalletAsync(request.Amount);
                return Ok(new ApiResponse(true, "Payment link generated successfully.", result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during wallet top-up.");
                return BadRequest(new ApiResponse(false, ex.Message));
            }
        }

        /// <summary>
        /// Verify Paystack payment after top-up
        /// </summary>
        [HttpGet("verify")]
        [Authorize(Roles = "Customer")]
        [SwaggerOperation("Verify Paystack payment")]
        [SwaggerResponse(200, "Payment verified successfully")]
        [SwaggerResponse(400, "Invalid reference or verification failed")]
        public async Task<IActionResult> VerifyPayment([FromQuery] string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return BadRequest(new ApiResponse(false, "Transaction reference is required."));

            try
            {
                var result = await _walletService.VerifyPaymentAsync(reference);
                return Ok(new ApiResponse(true, "Payment verified successfully.", result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying payment with reference {Reference}", reference);
                return BadRequest(new ApiResponse(false, ex.Message));
            }
        }

        /// <summary>
        /// Request withdrawal/payout (Consultant only)
        /// </summary>
        [HttpPost("payout")]
        [Authorize(Roles = "Consultant")]
        [SwaggerOperation("Request payout to bank account")]
        [SwaggerResponse(200, "Payout initiated successfully")]
        [SwaggerResponse(400, "Insufficient balance or error occurred")]
        public async Task<IActionResult> RequestPayout([FromBody] PayoutRequest request)
        {
            if (request.Amount <= 0)
                return BadRequest(new ApiResponse(false, "Amount must be greater than zero."));

            try
            {
                await _walletService.RequestPayoutAsync(request.Amount);
                return Ok(new ApiResponse(true, $"Payout of ₦{request.Amount:N2} initiated successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting payout.");
                return BadRequest(new ApiResponse(false, ex.Message));
            }
        }
    }

    // --- Request DTOs ---
    public record TopUpRequest(decimal Amount);
    public record PayoutRequest(decimal Amount);

    // --- API Response Wrapper ---
    public class ApiResponse
    {
        public bool Success { get; }
        public string Message { get; }
        public object? Data { get; }

        public ApiResponse(bool success, string message, object? data = null)
        {
            Success = success;
            Message = message;
            Data = data;
        }
    }
}
using AgricHub.BLL.Interfaces.ChatServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgricHub.Presentation.Controllers.PaystackController
{
    [ApiController]
    [Route("api/paystack")]
    public class PaystackWebhookController : ControllerBase
    {
        private readonly IRepository<Wallet> _walletRepo;
        private readonly IRepository<Customer> _customerRepo;
        private readonly IRepository<WalletTransaction> _walletTransactionRepo;
        private readonly ISendbirdService _sendbirdService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;

        public PaystackWebhookController(
            IUnitOfWork unitOfWork,
            ISendbirdService sendbirdService,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _walletRepo = unitOfWork.GetRepository<Wallet>();
            _customerRepo = unitOfWork.GetRepository<Customer>();
            _walletTransactionRepo = unitOfWork.GetRepository<WalletTransaction>();
            _sendbirdService = sendbirdService;
            _configuration = configuration;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> HandleWebhook()
        {
            try
            {
                // Read request body
                using var reader = new StreamReader(Request.Body);
                var json = await reader.ReadToEndAsync();

                // Verify webhook signature
                var signature = Request.Headers["x-paystack-signature"].ToString();
                if (string.IsNullOrEmpty(signature))
                    return Unauthorized("Missing webhook signature.");

                var secretKey = _configuration["Paystack:SecretKey"];
                var computedSignature = ComputeSignature(json, secretKey);

                if (signature != computedSignature)
                    return Unauthorized("Invalid webhook signature.");

                // Deserialize with strongly-typed model
                var webhookEvent = JsonSerializer.Deserialize<PaystackWebhookEvent>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (webhookEvent == null || string.IsNullOrEmpty(webhookEvent.Event))
                    return BadRequest("Invalid webhook payload.");

                // Handle charge.success event
                if (webhookEvent.Event == "charge.success")
                {
                    await HandleChargeSuccessAsync(webhookEvent.Data);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                // Log the error (add logging if needed)
                return StatusCode(500, $"Error processing webhook: {ex.Message}");
            }
        }

        private async Task HandleChargeSuccessAsync(PaystackWebhookData data)
        {
            if (data == null || string.IsNullOrEmpty(data.Reference))
                throw new ArgumentException("Invalid webhook data");

            var amount = data.Amount / 100m; // Convert from kobo to naira

            // Find wallet transaction
            var walletTransaction = await _walletTransactionRepo.GetSingleByAsync(
                wt => wt.PaystackTransactionReference == data.Reference,
                include: q => q.Include(wt => wt.Customer)
            );

            if (walletTransaction == null)
                throw new KeyNotFoundException("No matching wallet transaction found.");

            // Prevent duplicate processing
            if (walletTransaction.Status == "Completed")
                return;

            // Get customer and wallet
            var customer = walletTransaction.Customer;
            if (customer == null)
                throw new KeyNotFoundException("Customer not found.");

            var wallet = await _walletRepo.GetSingleByAsync(w => w.CustomerId == customer.Id);
            if (wallet == null)
                throw new KeyNotFoundException("Wallet not found.");

            // Update wallet balance
            wallet.Balance += amount;
            wallet.LastUpdated = DateTime.UtcNow;
            _walletRepo.Update(wallet);

            // Update transaction status
            walletTransaction.Status = "Completed";
            walletTransaction.CompletedAt = DateTime.UtcNow;
            _walletTransactionRepo.Update(walletTransaction);

            // Save changes
            await _unitOfWork.SaveChangesAsync();

           
            if (!string.IsNullOrEmpty(customer.SendbirdChannelUrl))
            {
                try
                {
                    await _sendbirdService.SendAdminMessageAsync(
                        customer.SendbirdChannelUrl,
                        $"💳 Wallet topped up with ₦{amount:N2}. New balance: ₦{wallet.Balance:N2}."
                    );
                }
                catch
                {
                   
                }
            }
        }

        private string ComputeSignature(string payload, string secretKey)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }


    public class PaystackWebhookEvent
    {
        public string Event { get; set; }
        public PaystackWebhookData Data { get; set; }
    }

    public class PaystackWebhookData
    {
        public string Reference { get; set; }
        public string Status { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public DateTime TransactionDate { get; set; }
        public PaystackWebhookCustomer Customer { get; set; }
    }

    public class PaystackWebhookCustomer
    {
        public string Email { get; set; }
        public string CustomerCode { get; set; }
    }
}
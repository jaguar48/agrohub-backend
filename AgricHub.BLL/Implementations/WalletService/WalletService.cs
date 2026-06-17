// AgricHub.BLL/Implementations/WalletService/WalletService.cs

using AgricHub.BLL.Interfaces.ChatServices;
using AgricHub.BLL.Interfaces.IPaystackService;
using AgricHub.BLL.Interfaces.IWalletService;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgricHub.BLL.Implementations.WalletService
{
    public class WalletService : IWalletService
    {
        private readonly IRepository<Wallet> _walletRepo;
        private readonly IRepository<Customer> _customerRepo;
        private readonly IRepository<Consultant> _consultantRepo;
        private readonly IRepository<WalletTransaction> _walletTransactionRepo;
        private readonly IPaystackService _paystackService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISendbirdService _sendbirdService;

        public WalletService(
            IUnitOfWork unitOfWork,
            IPaystackService paystackService,
            IHttpContextAccessor httpContextAccessor,
            ISendbirdService sendbirdService)
        {
            _unitOfWork            = unitOfWork;
            _walletRepo            = unitOfWork.GetRepository<Wallet>();
            _customerRepo          = unitOfWork.GetRepository<Customer>();
            _consultantRepo        = unitOfWork.GetRepository<Consultant>();
            _walletTransactionRepo = unitOfWork.GetRepository<WalletTransaction>();
            _paystackService       = paystackService;
            _httpContextAccessor   = httpContextAccessor;
            _sendbirdService       = sendbirdService;
        }

        private string GetUserId()
        {
            var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("User is not authenticated.");
            return userId;
        }

        // ── Get wallet ─────────────────────────────────────────────────────────

        public async Task<WalletResponse> GetMyWalletAsync()
        {
            var userId = GetUserId();

            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId);
            if (customer != null)
            {
                var wallet = await _walletRepo.GetSingleByAsync(w => w.CustomerId == customer.Id)
                    ?? throw new KeyNotFoundException("Wallet not found.");
                return new WalletResponse
                {
                    UserId      = customer.UserId,
                    UserName    = $"{customer.FirstName} {customer.LastName}",
                    UserType    = "Customer",
                    Balance     = wallet.Balance,
                    IsActive    = wallet.IsActive,
                    LastUpdated = wallet.LastUpdated
                };
            }

            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId);
            if (consultant != null)
            {
                var wallet = await _walletRepo.GetSingleByAsync(w => w.ConsultantId == consultant.Id)
                    ?? throw new KeyNotFoundException("Wallet not found.");
                return new WalletResponse
                {
                    UserId      = consultant.UserId,
                    UserName    = $"{consultant.FirstName} {consultant.LastName}",
                    UserType    = "Consultant",
                    Balance     = wallet.Balance,
                    IsActive    = wallet.IsActive,
                    LastUpdated = wallet.LastUpdated
                };
            }

            throw new UnauthorizedAccessException("User not found.");
        }

        // ── Get transactions ───────────────────────────────────────────────────

        public async Task<IEnumerable<WalletTransactionResponse>> GetMyTransactionsAsync()
        {
            var userId = GetUserId();

            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId);
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId);

            IEnumerable<WalletTransaction> transactions;

            if (customer != null)
                transactions = await _walletTransactionRepo.GetAllAsync(wt => wt.CustomerId == customer.Id);
            else if (consultant != null)
                transactions = await _walletTransactionRepo.GetAllAsync(wt => wt.ConsultantId == consultant.Id);
            else
                throw new UnauthorizedAccessException("User not found.");

            return transactions.Select(t => new WalletTransactionResponse
            {
                Id                           = t.Id,
                Amount                       = t.Amount,
                TransactionType              = t.TransactionType,
                Status                       = t.Status,
                PaystackTransactionReference = t.PaystackTransactionReference,
                CreatedAt                    = t.CreatedAt,
                CompletedAt                  = t.CompletedAt
            }).OrderByDescending(t => t.CreatedAt);
        }

        // ── Top up ─────────────────────────────────────────────────────────────

        public async Task<WalletTopUpResponse> TopUpWalletAsync(decimal amount)
        {
            var userId = GetUserId();

            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Customer not found.");

            var wallet = await _walletRepo.GetSingleByAsync(w => w.CustomerId == customer.Id)
                ?? throw new InvalidOperationException("Wallet not found.");

            var callbackUrl = "http://localhost:4200/customer/wallet";

            var (accessCode, reference) = await _paystackService.InitializeTransactionAsync(
                customer.Email, amount, callbackUrl);

            await _walletTransactionRepo.AddAsync(new WalletTransaction
            {
                CustomerId                   = customer.Id,
                ConsultantId                 = null,
                Amount                       = amount,
                PaystackTransactionReference = reference,
                TransactionType              = "WalletTopUp",
                Status                       = "Pending",
                CreatedAt                    = DateTime.UtcNow,
                CompletedAt                  = null
            });
            await _unitOfWork.SaveChangesAsync();

            return new WalletTopUpResponse
            {
                AccessCode = accessCode,
                Reference  = reference,
                PaymentUrl = $"https://checkout.paystack.com/{accessCode}",
                Message    = "Complete wallet top-up using Paystack.",
                Amount     = amount,
                Balance    = wallet.Balance
            };
        }

        // ── Verify payment ─────────────────────────────────────────────────────

        public async Task<WalletTopUpResponse> VerifyPaymentAsync(string reference)
        {
            try
            {
                var userId = GetUserId();
                var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                    ?? throw new UnauthorizedAccessException("Customer not found.");

                var walletTransaction = await _walletTransactionRepo.GetSingleByAsync(
                    wt => wt.PaystackTransactionReference == reference &&
                          wt.CustomerId == customer.Id);

                // Already verified — return early
                if (walletTransaction?.Status == "Completed")
                {
                    var w = await _walletRepo.GetSingleByAsync(w => w.CustomerId == customer.Id);
                    return new WalletTopUpResponse
                    {
                        Reference = reference,
                        Message   = "Payment already verified and wallet updated.",
                        Amount    = walletTransaction.Amount,
                        Balance   = w?.Balance ?? 0
                    };
                }

                var verificationResult = await _paystackService.VerifyTransactionAsync(reference);
                if (verificationResult.Data.Status != "success")
                    throw new InvalidOperationException(
                        $"Payment verification failed. Status: {verificationResult.Data.Status}");

                await _unitOfWork.BeginTransactionAsync();

                var customerWallet = await _walletRepo.GetSingleByAsync(w => w.CustomerId == customer.Id)
                    ?? throw new InvalidOperationException("Wallet not found.");

                var amount = verificationResult.Data.Amount / 100m;

                customerWallet.Balance    += amount;
                customerWallet.LastUpdated = DateTime.UtcNow;
                _walletRepo.Update(customerWallet);

                if (walletTransaction != null)
                {
                    walletTransaction.Status      = "Completed";
                    walletTransaction.CompletedAt = DateTime.UtcNow;
                    _walletTransactionRepo.Update(walletTransaction);
                }
                else
                {
                    await _walletTransactionRepo.AddAsync(new WalletTransaction
                    {
                        CustomerId                   = customer.Id,
                        ConsultantId                 = null,
                        Amount                       = amount,
                        PaystackTransactionReference = reference,
                        TransactionType              = "WalletTopUp",
                        Status                       = "Completed",
                        CreatedAt                    = DateTime.UtcNow,
                        CompletedAt                  = DateTime.UtcNow
                    });
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                try
                {
                    await _sendbirdService.SendNotificationAsync(
                        customer.UserId,
                        $"💳 Wallet topped up · ₦{amount:N2} added · New balance: ₦{customerWallet.Balance:N2}",
                        "wallet_topup");
                }
                catch { /* notification failure shouldn't fail the payment */ }

                return new WalletTopUpResponse
                {
                    Reference = reference,
                    Message   = "Payment verified successfully!",
                    Amount    = amount,
                    Balance   = customerWallet.Balance
                };
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }

        // ── Request payout ─────────────────────────────────────────────────────

        public async Task RequestPayoutAsync(decimal amount)
        {
            try
            {
                var userId = GetUserId();
                var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                    ?? throw new UnauthorizedAccessException("Consultant not found.");

                var wallet = await _walletRepo.GetSingleByAsync(w => w.ConsultantId == consultant.Id);
                if (wallet == null || wallet.Balance < amount)
                    throw new InvalidOperationException("Insufficient wallet balance.");

                await _unitOfWork.BeginTransactionAsync();

                // Deduct from wallet
                wallet.Balance    -= amount;
                wallet.LastUpdated = DateTime.UtcNow;
                _walletRepo.Update(wallet);

                // Create transaction record
                var walletTransaction = new WalletTransaction
                {
                    ConsultantId    = consultant.Id,
                    CustomerId      = null,
                    Amount          = -amount,
                    TransactionType = "Payout",
                    Status          = "Pending",
                    CreatedAt       = DateTime.UtcNow
                };
                await _walletTransactionRepo.AddAsync(walletTransaction);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                // Case 1: No bank details — queue for manual payout
                if (string.IsNullOrEmpty(consultant.PaystackRecipientCode))
                {
                    walletTransaction.Status      = "PendingManual";
                    walletTransaction.CompletedAt = DateTime.UtcNow;
                    _walletTransactionRepo.Update(walletTransaction);
                    await _unitOfWork.SaveChangesAsync();

                    try
                    {
                        await _sendbirdService.SendNotificationAsync(
                            consultant.UserId,
                            $"💸 Payout of ₦{amount:N2} requested · Our team will process it within 2-3 business days",
                            "payout");
                    }
                    catch { }

                    return;
                }

                // Case 2: Has bank details — initiate Paystack transfer
                try
                {
                    await _paystackService.InitiateConsultantPayoutAsync(
                        Guid.NewGuid().ToString(),
                        consultant.PaystackRecipientCode,
                        amount);

                    walletTransaction.Status      = "Completed";
                    walletTransaction.CompletedAt = DateTime.UtcNow;
                    _walletTransactionRepo.Update(walletTransaction);
                    await _unitOfWork.SaveChangesAsync();
                }
                catch (Exception paystackEx)
                {
                    walletTransaction.Status      = "Failed";
                    walletTransaction.CompletedAt = DateTime.UtcNow;
                    _walletTransactionRepo.Update(walletTransaction);
                    await _unitOfWork.SaveChangesAsync();
                    throw new Exception(
                        $"Payout initiated but Paystack transfer failed: {paystackEx.Message}. Please contact support.",
                        paystackEx);
                }

                try
                {
                    await _sendbirdService.SendNotificationAsync(
                        consultant.UserId,
                        $"💸 Payout of ₦{amount:N2} sent to your bank · Wallet balance: ₦{wallet.Balance:N2}",
                        "payout");
                }
                catch { }
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }
    }
}
// AgricHub.BLL/Implementations/WalletService/WalletService.cs

using AgricHub.BLL.Interfaces;
using AgricHub.BLL.Interfaces.ChatServices;
using AgricHub.BLL.Interfaces.IPaystackService;
using AgricHub.BLL.Interfaces.IWalletService;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IPlatformSettingsService _settings;
        private readonly IServiceScopeFactory _scopeFactory;

        public WalletService(
            IUnitOfWork unitOfWork,
            IPaystackService paystackService,
            IHttpContextAccessor httpContextAccessor,
            ISendbirdService sendbirdService,
            IPlatformSettingsService settings,
            IServiceScopeFactory scopeFactory)
        {
            _unitOfWork            = unitOfWork;
            _walletRepo            = unitOfWork.GetRepository<Wallet>();
            _customerRepo          = unitOfWork.GetRepository<Customer>();
            _consultantRepo        = unitOfWork.GetRepository<Consultant>();
            _walletTransactionRepo = unitOfWork.GetRepository<WalletTransaction>();
            _paystackService       = paystackService;
            _httpContextAccessor   = httpContextAccessor;
            _sendbirdService       = sendbirdService;
            _settings              = settings;
            _scopeFactory          = scopeFactory;
        }

        /// <summary>
        /// Fire-and-forget email using a fresh DI scope (not the request-scoped
        /// _emailService directly) — this method's own DbContext/scope is disposed
        /// as soon as the request returns, which would tear down an in-flight
        /// background task if it captured that scope's services directly.
        /// </summary>
        private void FireEmail(string toEmail, string name, string subject, string headline, string bodyHtml,
            string? ctaText = null, string? ctaUrl = null)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await email.SendGenericNotificationAsync(toEmail, name, subject, headline, bodyHtml, ctaText, ctaUrl);
                }
                catch { /* never fail wallet action because of email */ }
            });
        }

        private void FireWalletTopUpEmail(string toEmail, string name, decimal amount, decimal newBalance)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await email.SendWalletTopUpAsync(toEmail, name, amount, newBalance);
                }
                catch { /* never fail wallet action because of email */ }
            });
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

            // Was hardcoded to localhost — now reads the real platform URL from
            // admin settings, falling back to localhost only if that setting is
            // somehow empty (e.g. fresh install before an admin sets it).
            var platformUrl = await _settings.GetAsync("platform.url");
            var callbackUrl = string.IsNullOrWhiteSpace(platformUrl)
                ? "http://localhost:4200/customer/wallet"
                : $"{platformUrl.TrimEnd('/')}/customer/wallet";

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

                FireWalletTopUpEmail(customer.Email, $"{customer.FirstName} {customer.LastName}", amount, customerWallet.Balance);

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

                // Enforce admin-configured minimum payout amount. Previously this
                // setting existed in PlatformSettingsSeeder but nothing checked it.
                var minPayoutRaw = await _settings.GetAsync("finance.minimumPayout");
                if (decimal.TryParse(minPayoutRaw, out var minPayout) && minPayout > 0 && amount < minPayout)
                    throw new InvalidOperationException(
                        $"Minimum payout amount is ₦{minPayout:N2}. Please request a larger amount or wait until your balance grows.");

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

                // Every payout now queues for admin approval regardless of whether
                // bank details are on file — previously, having a PaystackRecipientCode
                // set meant the transfer fired IMMEDIATELY and automatically with zero
                // admin involvement. Per decision: all payouts require manual admin
                // approval before any money actually moves. The approval action itself
                // (ApprovePayoutAsync below) is what triggers the real Paystack transfer.
                walletTransaction.Status      = "PendingApproval";
                _walletTransactionRepo.Update(walletTransaction);
                await _unitOfWork.SaveChangesAsync();

                try
                {
                    await _sendbirdService.SendNotificationAsync(
                        consultant.UserId,
                        $"💸 Payout of ₦{amount:N2} requested · Awaiting admin approval",
                        "payout");
                }
                catch { }

                FireEmail(consultant.Email, $"{consultant.FirstName} {consultant.LastName}",
                    "Payout requested",
                    "Your payout request is awaiting approval",
                    $"<p>Your payout request of <strong>₦{amount:N2}</strong> has been received and is awaiting admin approval. You'll be notified once it's processed.</p>",
                    "View wallet", "https://agrichub.io/consultant/wallet");
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }

        // ── Admin payout approval ────────────────────────────────────────────────
        // Was completely missing — "PendingManual" transactions had no way to ever
        // be acted on. This is what actually moves money: on approval, if the
        // consultant has bank details on file, fires the real Paystack transfer;
        // otherwise just marks it complete for the admin to handle the transfer
        // manually outside the system (e.g. direct bank transfer) and confirm here.

        public async Task<IEnumerable<object>> GetPendingPayoutsAsync()
        {
            var pending = await _walletTransactionRepo.GetAllAsync(
                t => t.TransactionType == "Payout" && t.Status == "PendingApproval",
                orderBy: q => q.OrderBy(t => t.CreatedAt));

            var result = new List<object>();
            foreach (var t in pending)
            {
                var consultant = await _consultantRepo.GetSingleByAsync(c => c.Id == t.ConsultantId);
                result.Add(new
                {
                    transactionId = t.Id,
                    consultantId = t.ConsultantId,
                    consultantName = consultant != null ? $"{consultant.FirstName} {consultant.LastName}" : "Unknown",
                    consultantEmail = consultant?.Email,
                    hasBankDetails = !string.IsNullOrEmpty(consultant?.PaystackRecipientCode),
                    amount = -t.Amount, // stored negative (a debit); show as positive for admin display
                    requestedAt = t.CreatedAt,
                });
            }
            return result;
        }

        public async Task ApprovePayoutAsync(int transactionId)
        {
            var t = await _walletTransactionRepo.GetSingleByAsync(x => x.Id == transactionId)
                ?? throw new KeyNotFoundException("Payout request not found.");
            if (t.Status != "PendingApproval")
                throw new InvalidOperationException("This payout has already been processed.");

            var consultant = await _consultantRepo.GetSingleByAsync(c => c.Id == t.ConsultantId)
                ?? throw new KeyNotFoundException("Consultant not found.");
            var amount = -t.Amount; // stored negative

            if (!string.IsNullOrEmpty(consultant.PaystackRecipientCode))
            {
                try
                {
                    await _paystackService.InitiateConsultantPayoutAsync(
                        Guid.NewGuid().ToString(), consultant.PaystackRecipientCode, amount);
                }
                catch (Exception paystackEx)
                {
                    t.Status = "Failed";
                    t.CompletedAt = DateTime.UtcNow;
                    _walletTransactionRepo.Update(t);
                    await _unitOfWork.SaveChangesAsync();
                    throw new Exception($"Paystack transfer failed: {paystackEx.Message}. Payout marked as Failed — the consultant's wallet was NOT re-credited automatically; refund manually if appropriate.", paystackEx);
                }
            }
            // No bank details on file: admin handles the transfer manually outside
            // the system and this just records that it was completed.

            t.Status = "Completed";
            t.CompletedAt = DateTime.UtcNow;
            _walletTransactionRepo.Update(t);
            await _unitOfWork.SaveChangesAsync();

            try
            {
                await _sendbirdService.SendNotificationAsync(consultant.UserId,
                    $"💸 Payout of ₦{amount:N2} approved and sent", "payout");
            }
            catch { }

            FireEmail(consultant.Email, $"{consultant.FirstName} {consultant.LastName}",
                "Payout sent", "Your payout is on its way",
                $"<p><strong>₦{amount:N2}</strong> has been approved and sent to your bank account.</p>",
                "View wallet", "https://agrichub.io/consultant/wallet");
        }

        public async Task RejectPayoutAsync(int transactionId, string reason)
        {
            var t = await _walletTransactionRepo.GetSingleByAsync(x => x.Id == transactionId)
                ?? throw new KeyNotFoundException("Payout request not found.");
            if (t.Status != "PendingApproval")
                throw new InvalidOperationException("This payout has already been processed.");

            var consultant = await _consultantRepo.GetSingleByAsync(c => c.Id == t.ConsultantId)
                ?? throw new KeyNotFoundException("Consultant not found.");
            var amount = -t.Amount;

            // Refund the wallet — money was deducted at request time, so a
            // rejection must give it back.
            var wallet = await _walletRepo.GetSingleByAsync(w => w.ConsultantId == consultant.Id);
            if (wallet != null)
            {
                wallet.Balance += amount;
                wallet.LastUpdated = DateTime.UtcNow;
                _walletRepo.Update(wallet);
            }

            t.Status = "Rejected";
            t.CompletedAt = DateTime.UtcNow;
            _walletTransactionRepo.Update(t);
            await _unitOfWork.SaveChangesAsync();

            try
            {
                await _sendbirdService.SendNotificationAsync(consultant.UserId,
                    $"❌ Payout of ₦{amount:N2} was declined · Refunded to wallet", "payout");
            }
            catch { }

            FireEmail(consultant.Email, $"{consultant.FirstName} {consultant.LastName}",
                "Payout declined", "Your payout request was declined",
                $"<p>Your payout request of <strong>₦{amount:N2}</strong> was declined and refunded to your wallet.</p>" +
                (!string.IsNullOrWhiteSpace(reason) ? $"<p><strong>Reason:</strong> {reason}</p>" : ""),
                "View wallet", "https://agrichub.io/consultant/wallet");
        }
    }
}
using AgricHub.BLL.Interfaces.IAdminService;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgricHub.BLL.Implementations.AdminService
{
    public class AdminFinancialsService : IAdminFinancialsService
    {
        private readonly IRepository<Wallet> _walletRepo;
        private readonly IRepository<WalletTransaction> _txRepo;
        private readonly IRepository<PendingTransaction> _pendingRepo;
        private readonly IRepository<Consultation> _consultationRepo;
        private readonly IRepository<Customer> _customerRepo;
        private readonly IRepository<Consultant> _consultantRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _config;

        public AdminFinancialsService(IUnitOfWork unitOfWork, IConfiguration config)
        {
            _unitOfWork       = unitOfWork;
            _config           = config;
            _walletRepo       = unitOfWork.GetRepository<Wallet>();
            _txRepo           = unitOfWork.GetRepository<WalletTransaction>();
            _pendingRepo      = unitOfWork.GetRepository<PendingTransaction>();
            _consultationRepo = unitOfWork.GetRepository<Consultation>();
            _customerRepo     = unitOfWork.GetRepository<Customer>();
            _consultantRepo   = unitOfWork.GetRepository<Consultant>();
        }

        public async Task<AdminFinancialOverviewDto> GetOverviewAsync()
        {
            var allTx = await _txRepo.GetAllAsync();

            var completed = allTx.Where(t => t.Status == "Completed").ToList();
            var totalRevenue = completed.Where(t => t.Amount > 0).Sum(t => t.Amount);
            var totalPayouts = completed.Where(t => t.TransactionType == "Payout").Sum(t => t.Amount);

            var pending = await _pendingRepo.GetAllAsync();
            var escrowHeld = pending.Where(p => p.Status == "Held").Sum(p => p.Amount);
            var pendingPayouts = pending.Where(p => p.Status == "PendingPayout").Sum(p => p.Amount);

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var monthTx = completed.Where(t => t.CreatedAt >= monthStart).ToList();
            var revenueMonth = monthTx.Where(t => t.Amount > 0).Sum(t => t.Amount);
            var payoutsMonth = monthTx.Where(t => t.TransactionType == "Payout").Sum(t => t.Amount);

            var total = allTx.Count();
            var success = completed.Count;
            var rate = total > 0 ? Math.Round((double)success / total * 100, 1) : 0;

            return new AdminFinancialOverviewDto(
                totalRevenue, escrowHeld, totalPayouts, pendingPayouts,
                total, rate, revenueMonth, payoutsMonth);
        }

        public async Task<PagedResult<AdminWalletDto>> GetWalletsAsync(int page, int pageSize)
        {
            var wallets = await _walletRepo.GetAllAsync();
            var customers = await _customerRepo.GetAllAsync();
            var consultants = await _consultantRepo.GetAllAsync();

            var custMap = customers.ToDictionary(c => c.Id, c => c.FirstName + " " + c.LastName);
            var consMap = consultants.ToDictionary(c => c.Id, c => c.FirstName + " " + c.LastName);

            var dtos = wallets.Select(w => new AdminWalletDto(
                w.Id, w.WalletNo, w.Balance, w.IsActive,
                w.CustomerId.HasValue && custMap.TryGetValue(w.CustomerId.Value, out var cn) ? cn
              : w.ConsultantId.HasValue && consMap.TryGetValue(w.ConsultantId.Value, out var co) ? co
              : "Unknown",
                w.CustomerId.HasValue ? "Customer" : "Consultant",
                w.CustomerId ?? w.ConsultantId ?? 0
            )).ToList();

            var total = dtos.Count;
            var paged = dtos.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return new PagedResult<AdminWalletDto>(paged, total, page, pageSize);
        }

        public async Task<PagedResult<AdminTransactionDto>> GetTransactionsAsync(int page, int pageSize)
        {
            var txs = await _txRepo.GetAllAsync(orderBy: q => q.OrderByDescending(t => t.CreatedAt));
            var customers = await _customerRepo.GetAllAsync();
            var consultants = await _consultantRepo.GetAllAsync();

            var custMap = customers.ToDictionary(c => c.Id, c => c.FirstName + " " + c.LastName);
            var consMap = consultants.ToDictionary(c => c.Id, c => c.FirstName + " " + c.LastName);

            var dtos = txs.Select(t => new AdminTransactionDto(
                t.Id.ToString(),
                t.Amount,
                t.TransactionType ?? "Transfer",
                t.Status ?? "Unknown",
                t.CreatedAt.ToString("O"),
                t.CustomerId.HasValue && custMap.TryGetValue(t.CustomerId.Value, out var cn) ? cn
              : t.ConsultantId.HasValue && consMap.TryGetValue(t.ConsultantId.Value, out var co) ? co
              : "Unknown",
                t.PaystackTransactionReference
            )).ToList();

            var total = dtos.Count;
            var paged = dtos.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return new PagedResult<AdminTransactionDto>(paged, total, page, pageSize);
        }

        public async Task AdjustWalletAsync(int walletId, decimal amount, string reason)
        {
            var wallet = await _walletRepo.GetByIdAsync(walletId)
                ?? throw new KeyNotFoundException($"Wallet {walletId} not found.");

            wallet.Balance    += amount;
            wallet.LastUpdated = DateTime.UtcNow;
            _walletRepo.Update(wallet);

            // Record the adjustment as a transaction
            await _txRepo.AddAsync(new WalletTransaction
            {
                CustomerId    = wallet.CustomerId,
                ConsultantId  = wallet.ConsultantId,
                Amount        = amount,
                TransactionType = "AdminAdjustment",
                Status        = "Completed",
                PaystackTransactionReference = reason,
                CreatedAt     = DateTime.UtcNow,
                CompletedAt   = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task InitiatePayoutAsync(int consultantId)
        {
            var consultant = await _consultantRepo.GetByIdAsync(consultantId)
                ?? throw new KeyNotFoundException($"Consultant {consultantId} not found.");

            var wallet = await _walletRepo.GetSingleByAsync(w => w.ConsultantId == consultantId)
                ?? throw new KeyNotFoundException("Consultant wallet not found.");

            if (wallet.Balance <= 0)
                throw new InvalidOperationException("No balance to pay out.");

            // Call Paystack transfer API
            var secretKey = _config["Paystack:SecretKey"];
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", secretKey);

            var payload = JsonSerializer.Serialize(new
            {
                source = "balance",
                amount = (int)(wallet.Balance * 100), // kobo
                recipient = consultant.PaystackRecipientCode,
                reason = $"AgricHub payout to {consultant.FirstName} {consultant.LastName}"
            });

            var response = await http.PostAsync("https://api.paystack.co/transfer",
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"Paystack transfer failed: {err}");
            }

            // Record payout transaction and zero wallet
            await _txRepo.AddAsync(new WalletTransaction
            {
                ConsultantId    = consultantId,
                Amount          = wallet.Balance,
                TransactionType = "Payout",
                Status          = "Completed",
                CreatedAt       = DateTime.UtcNow,
                CompletedAt     = DateTime.UtcNow
            });

            wallet.Balance    = 0;
            wallet.LastUpdated = DateTime.UtcNow;
            _walletRepo.Update(wallet);

            await _unitOfWork.SaveChangesAsync();
        }
    }
}


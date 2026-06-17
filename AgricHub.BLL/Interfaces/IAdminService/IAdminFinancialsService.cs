using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.BLL.Interfaces.IAdminService
{
    public interface IAdminFinancialsService
    {
        Task<AdminFinancialOverviewDto> GetOverviewAsync();
        Task<PagedResult<AdminWalletDto>> GetWalletsAsync(int page, int pageSize);
        Task<PagedResult<AdminTransactionDto>> GetTransactionsAsync(int page, int pageSize);
        Task AdjustWalletAsync(int walletId, decimal amount, string reason);
        Task InitiatePayoutAsync(int consultantId);
    }

    public record AdminFinancialOverviewDto(
        decimal TotalRevenue,
        decimal EscrowHeld,
        decimal TotalPayouts,
        decimal PendingPayouts,
        int TotalTransactions,
        double SuccessRate,
        decimal RevenueThisMonth,
        decimal PayoutsThisMonth
    );

    public record AdminWalletDto(
        int Id,
        string WalletNo,
        decimal Balance,
        bool IsActive,
        string OwnerName,
        string OwnerType,   // "Customer" | "Consultant"
        int OwnerId
    );

    public record AdminTransactionDto(
        string Id,
        decimal Amount,
        string Type,
        string Status,
        string CreatedAt,
        string OwnerName,
        string? Reference
    );

    public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
}

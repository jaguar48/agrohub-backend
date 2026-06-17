using AgricHub.Shared.DTO_s.Response;

namespace AgricHub.BLL.Interfaces.IWalletService
{
    public interface IWalletService
    {
        Task<WalletResponse> GetMyWalletAsync();
        Task<IEnumerable<WalletTransactionResponse>> GetMyTransactionsAsync();
        Task<WalletTopUpResponse> TopUpWalletAsync(decimal amount);
        Task<WalletTopUpResponse> VerifyPaymentAsync(string reference);
        Task RequestPayoutAsync(decimal amount);
    }
}
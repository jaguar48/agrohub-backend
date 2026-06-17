using AgricHub.BLL.Implementations.PaystackService;
using AgricHub.Shared.DTO_s.Response;

namespace AgricHub.BLL.Interfaces.IPaystackService
{
    public interface IPaystackService
    {
        Task<(string accessCode, string reference)> InitializeTransactionAsync(
            string email, decimal amount, string callbackUrl);

        Task<PaystackVerificationResponse> VerifyTransactionAsync(string reference);

        Task InitiateConsultantPayoutAsync(
            string reference, string recipientCode, decimal amount);

        Task<string> CreateTransferRecipientAsync(
            string accountNumber, string accountName, string bankCode);

        Task<List<BankInfo>> GetBanksAsync(string country = "nigeria");

        Task<BankAccountDetails> ResolveAccountNumberAsync(
            string accountNumber, string bankCode);
    }
}
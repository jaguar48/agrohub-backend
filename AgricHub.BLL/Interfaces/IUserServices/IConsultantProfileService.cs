using AgricHub.Shared.DTO_s.Request;
using AgricHub.Shared.DTO_s.Response;
using Microsoft.AspNetCore.Http;

namespace AgricHub.BLL.Interfaces.IUserServices
{
    public interface IConsultantProfileService
    {
        Task<ConsultantProfileResponse> GetMyProfileAsync();
        Task UpdateProfileAsync(UpdateConsultantProfileRequest request);
        Task UpdateBankDetailsAsync(UpdateBankDetailsRequest request);
        Task ChangePasswordAsync(ChangePasswordRequest request);
        Task<List<BankInfo>> GetBanksAsync(string country = "");
        Task<BankAccountDetails> VerifyBankAccountAsync(string accountNumber, string bankCode);
        Task<string> UploadAvatarAsync(IFormFile file);
    }
}
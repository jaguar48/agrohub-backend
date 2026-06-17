using AgricHub.DAL.Entities;
using AgricHub.Shared.DTO_s.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.BLL.Interfaces.IUserServices
{
    public interface IAuthService
    {
        Task<ServiceResponse<string>> ValidateUser(UserAuthenticationResponse response);
        Task<string> CreateToken();
        Task<ApplicationUser> VerifyUser(string email, string verificationToken);
        Task<bool> SendVerificationEmail(string email, string verificationToken);
        Task<bool> ResetPassword(string email, string token, string newPassword);
        Task<bool> SendPasswordResetEmail(string email, string resetToken);
       
        Task<AuthenticationResponse> GoogleAuth(string credential, string? role = null);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.BLL.Interfaces
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string toEmail, string name, string verificationUrl);
        Task SendVerificationApprovedAsync(string toEmail, string name);
        Task SendVerificationRejectedAsync(string toEmail, string name, string reason);
        Task SendBookingConfirmedAsync(string toEmail, string name, string serviceName, string consultantName, DateTime scheduledAt, decimal amount);
        Task SendBookingRequestAsync(string toEmail, string consultantName, string customerName, string serviceName, DateTime scheduledAt);
        Task SendWalletTopUpAsync(string toEmail, string name, decimal amount, decimal newBalance);
        Task SendPasswordResetAsync(string toEmail, string name, string resetUrl);
    }
}


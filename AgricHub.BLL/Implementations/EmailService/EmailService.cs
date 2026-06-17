// AgricHub.BLL/Implementations/EmailService.cs
// Uses MailKit for SMTP (supports port 465 implicit SSL + port 587 STARTTLS)
// NuGet: Install-Package MailKit

using AgricHub.BLL.Interfaces;
using MailKit.Security;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace AgricHub.BLL.Implementations
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly IPlatformSettingsService _settings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IConfiguration config,
            IPlatformSettingsService settings,
            ILogger<EmailService> logger)
        {
            _config   = config;
            _settings = settings;
            _logger   = logger;
        }

        // ── Read setting: DB first, secrets.json fallback ──────────────────────
        private async Task<string> GetAsync(string dbKey, string configKey)
        {
            var dbVal = await _settings.GetAsync(dbKey);
            if (!string.IsNullOrWhiteSpace(dbVal) &&
                !dbVal.StartsWith("YOUR_") &&
                dbVal != "••••••••")
                return dbVal;

            var cfgVal = _config[configKey] ?? "";
            return cfgVal.StartsWith("YOUR_") ? "" : cfgVal;
        }

        // ── Core send ──────────────────────────────────────────────────────────
        private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            var sgKey = await GetAsync("email.sendgridKey", "SendGrid:ApiKey");
            var senderName = await GetAsync("email.senderName", "EmailSettings:SenderName");
            var senderEmail = await GetAsync("email.senderEmail", "EmailSettings:SenderEmail");

            if (!string.IsNullOrWhiteSpace(sgKey))
            {
                try
                {
                    await SendViaSendGridAsync(toEmail, toName, subject, htmlBody, sgKey, senderEmail, senderName);
                    _logger.LogInformation("[Email] Sent via SendGrid to {Email}", toEmail);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[Email] SendGrid failed ({Msg}), falling back to SMTP.", ex.Message);
                }
            }

            await SendViaSmtpAsync(toEmail, toName, subject, htmlBody, senderEmail, senderName);
            _logger.LogInformation("[Email] Sent via SMTP to {Email}", toEmail);
        }

        private async Task SendViaSendGridAsync(
            string toEmail, string toName, string subject, string htmlBody,
            string apiKey, string fromEmail, string fromName)
        {
            var client = new SendGridClient(apiKey);
            var msg = MailHelper.CreateSingleEmail(
                new EmailAddress(fromEmail, fromName),
                new EmailAddress(toEmail, toName),
                subject, null, htmlBody);
            var response = await client.SendEmailAsync(msg);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"SendGrid returned {(int)response.StatusCode}");
        }

        private async Task SendViaSmtpAsync(
            string toEmail, string toName, string subject, string htmlBody,
            string fromEmail, string fromName)
        {
            var host = await GetAsync("email.smtpHost", "EmailSettings:SmtpHost");
            var portStr = await GetAsync("email.smtpPort", "EmailSettings:SmtpPort");
            var user = await GetAsync("email.smtpUser", "EmailSettings:SenderEmail");
            var pass = await GetAsync("email.smtpPassword", "EmailSettings:Password");

            var port = int.TryParse(portStr, out var p) ? p : 465;
            if (string.IsNullOrEmpty(user)) user = fromEmail;

            _logger.LogInformation("[SMTP] Connecting → {Host}:{Port} as {User}", host, port, user);

            // SecureSocketOptions: port 465 = SslOnConnect, port 587 = StartTls
            var socketOption = port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            using var client = new SmtpClient();
            client.Timeout = 15_000;

            try
            {
                await client.ConnectAsync(host, port, socketOption);
                _logger.LogInformation("[SMTP] Connected — authenticating…");

                await client.AuthenticateAsync(user, pass);
                _logger.LogInformation("[SMTP] Authenticated — sending…");

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(new MailboxAddress(toName, toEmail));
                message.Subject = subject;
                message.Body    = new TextPart("html") { Text = htmlBody };

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("[SMTP] Message sent to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError("[SMTP] Failed: {Error}", ex.ToString());
                throw new Exception($"SMTP error ({host}:{port}): {ex.Message}", ex);
            }
        }

        // ── Templates ──────────────────────────────────────────────────────────

        public Task SendVerificationEmailAsync(string toEmail, string name, string verificationUrl) =>
            SendAsync(toEmail, name, "Verify your AgricHub account", $@"
                <p>Hi {name},</p>
                <p>Please verify your email address:</p>
                <p><a href='{verificationUrl}' style='background:#2d6a4f;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600'>Verify my account</a></p>
                <p>— The AgricHub Team</p>");

        public Task SendVerificationApprovedAsync(string toEmail, string name) =>
            SendAsync(toEmail, name, "🎉 Your AgricHub verification has been approved", $@"
                <p>Hi {name},</p>
                <p>Your verification has been <strong>approved</strong>. Your profile now shows the <strong>Verified</strong> badge.</p>
                <p><a href='https://agrichub.io/consultant/overview' style='background:#2d6a4f;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600'>Go to my dashboard</a></p>
                <p>— The AgricHub Team</p>");

        public Task SendVerificationRejectedAsync(string toEmail, string name, string reason) =>
            SendAsync(toEmail, name, "AgricHub verification — action required", $@"
                <p>Hi {name},</p>
                <p>Your verification was <strong>not approved</strong>:</p>
                <blockquote style='border-left:4px solid #e63946;padding-left:14px;color:#555;margin:14px 0'>{reason}</blockquote>
                <p>Please resubmit with updated documents.</p>
                <p><a href='https://agrichub.io/consultant/profile' style='background:#2d6a4f;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600'>Resubmit</a></p>
                <p>— The AgricHub Team</p>");

        public Task SendBookingConfirmedAsync(string toEmail, string name, string serviceName,
            string consultantName, DateTime scheduledAt, decimal amount) =>
            SendAsync(toEmail, name, $"Booking confirmed — {serviceName}", $@"
                <p>Hi {name},</p>
                <p>Your consultation is confirmed:</p>
                <table style='border-collapse:collapse;max-width:400px'>
                  <tr><td style='padding:8px;color:#555'>Service</td><td style='padding:8px;font-weight:600'>{serviceName}</td></tr>
                  <tr><td style='padding:8px;color:#555'>Consultant</td><td style='padding:8px;font-weight:600'>{consultantName}</td></tr>
                  <tr><td style='padding:8px;color:#555'>Scheduled</td><td style='padding:8px;font-weight:600'>{scheduledAt:dddd, MMMM d yyyy 'at' h:mm tt}</td></tr>
                  <tr><td style='padding:8px;color:#555'>Amount held</td><td style='padding:8px;font-weight:600'>₦{amount:N2}</td></tr>
                </table>
                <p>— The AgricHub Team</p>");

        public Task SendBookingRequestAsync(string toEmail, string consultantName,
            string customerName, string serviceName, DateTime scheduledAt) =>
            SendAsync(toEmail, consultantName, $"New booking — {customerName}", $@"
                <p>Hi {consultantName},</p>
                <p><strong>{customerName}</strong> has booked a session with you:</p>
                <table style='border-collapse:collapse;max-width:400px'>
                  <tr><td style='padding:8px;color:#555'>Service</td><td style='padding:8px;font-weight:600'>{serviceName}</td></tr>
                  <tr><td style='padding:8px;color:#555'>Scheduled</td><td style='padding:8px;font-weight:600'>{scheduledAt:dddd, MMMM d yyyy 'at' h:mm tt}</td></tr>
                </table>
                <p><a href='https://agrichub.io/consultant/schedule' style='background:#2d6a4f;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600'>View schedule</a></p>
                <p>— The AgricHub Team</p>");

        public Task SendWalletTopUpAsync(string toEmail, string name, decimal amount, decimal newBalance) =>
            SendAsync(toEmail, name, "Wallet topped up", $@"
                <p>Hi {name},</p>
                <table style='border-collapse:collapse;max-width:400px'>
                  <tr><td style='padding:8px;color:#555'>Amount added</td><td style='padding:8px;font-weight:600;color:#2d6a4f'>+₦{amount:N2}</td></tr>
                  <tr><td style='padding:8px;color:#555'>New balance</td><td style='padding:8px;font-weight:600'>₦{newBalance:N2}</td></tr>
                </table>
                <p>— The AgricHub Team</p>");

        public Task SendPasswordResetAsync(string toEmail, string name, string resetUrl) =>
            SendAsync(toEmail, name, "Reset your AgricHub password", $@"
                <p>Hi {name},</p>
                <p>Click below to reset your password (expires in 24 hours):</p>
                <p><a href='{resetUrl}' style='background:#2d6a4f;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600'>Reset password</a></p>
                <p>— The AgricHub Team</p>");
    }
}
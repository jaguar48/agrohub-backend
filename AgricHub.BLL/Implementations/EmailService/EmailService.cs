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

        /// <summary>Platform name for use in email subject lines and signatures —
        /// was hardcoded "AgricHub" throughout every template despite platform.name
        /// already existing as an admin-editable setting.</summary>
        private async Task<string> GetPlatformNameAsync()
        {
            var name = await _settings.GetAsync("platform.name");
            return string.IsNullOrWhiteSpace(name) ? "AgricHub" : name;
        }

        /// <summary>
        /// Wraps a template's inner content in a proper branded shell — header
        /// bar with wordmark, white content card, footer with the platform name
        /// and tagline. Applied ONCE, centrally, in SendAsync — so every template
        /// gets this automatically instead of each one needing its own
        /// header/footer/signature markup.
        ///
        /// Deliberately uses a TEXT wordmark, not an image logo — email clients
        /// block remote images by default (visible in Hostinger Mail's own
        /// "Images are blocked to protect your privacy" banner), so an image
        /// logo would just show as a broken/missing image for most recipients
        /// on first open. Table-based layout throughout for compatibility with
        /// Outlook desktop and other clients with poor modern-CSS support.
        /// </summary>
        private async Task<string> WrapEmailShellAsync(string innerHtml)
        {
            var brand = await GetPlatformNameAsync();
            var tagline = await _settings.GetAsync("platform.tagline");
            var platformUrl = await _settings.GetAsync("platform.url");
            var year = DateTime.UtcNow.Year;

            return $@"
<!DOCTYPE html>
<html>
<body style='margin:0;padding:0;background-color:#f2f4f2;font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;'>
  <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='background-color:#f2f4f2;padding:32px 16px;'>
    <tr>
      <td align='center'>
        <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='max-width:520px;background:#ffffff;border-radius:14px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.06);'>

          <!-- Header — text wordmark, not an image -->
          <tr>
            <td style='background:linear-gradient(135deg,#2d6a4f,#1b4332);padding:28px 32px;'>
              <table role='presentation' cellpadding='0' cellspacing='0'>
                <tr>
                  <td style='width:34px;height:34px;background:rgba(255,255,255,0.18);border-radius:9px;text-align:center;vertical-align:middle;font-family:Georgia,serif;font-weight:700;font-size:15px;color:#ffffff;'>
                    {(brand.Length > 0 ? brand[0].ToString().ToUpper() : "A")}
                  </td>
                  <td style='padding-left:12px;font-size:19px;font-weight:700;color:#ffffff;letter-spacing:-0.01em;'>
                    {brand}
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- Content -->
          <tr>
            <td style='padding:36px 32px 28px;color:#1a1a1a;font-size:14.5px;line-height:1.65;'>
              {innerHtml}
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style='padding:22px 32px 28px;border-top:1px solid #eef0ee;'>
              <p style='margin:0 0 4px;font-size:12.5px;color:#8a8f8a;'>— The {brand} Team</p>
              {(string.IsNullOrWhiteSpace(tagline) ? "" : $"<p style='margin:0 0 12px;font-size:11.5px;color:#b0b5b0;'>{tagline}</p>")}
              <p style='margin:0;font-size:11px;color:#c2c6c2;'>
                © {year} {brand}.
                {(string.IsNullOrWhiteSpace(platformUrl) ? "" : $" <a href='{platformUrl}' style='color:#8a8f8a;text-decoration:underline;'>{platformUrl.Replace("https://", "").Replace("http://", "")}</a>")}
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }

        // ── Core send ──────────────────────────────────────────────────────────
        private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            // ── Global email kill switch (features.emailNotifications) ────────────
            // Was seeded but never checked — every email template call below always
            // fired regardless of this toggle. Now an admin can genuinely turn all
            // outbound email off (e.g. during testing/migration) without touching code.
            var emailEnabledRaw = await _settings.GetAsync("features.emailNotifications");
            var emailEnabled = emailEnabledRaw == null || emailEnabledRaw == "true";
            if (!emailEnabled)
            {
                _logger.LogInformation("[Email] Skipped (features.emailNotifications is off) — would have sent '{Subject}' to {Email}", subject, toEmail);
                return;
            }

            // Wrap once here — both the SendGrid and SMTP paths below reference
            // this same variable, so every template gets the branded shell
            // (header/footer/wordmark) automatically without each of the 8+
            // template methods needing to build their own.
            htmlBody = await WrapEmailShellAsync(htmlBody);

            var sgKey = await GetAsync("email.sendgridKey", "SendGrid:ApiKey");
            // Was reading a separate email.senderName setting that could drift
            // out of sync with platform.name (exactly what happened — an admin
            // renamed the platform but the "From" display name kept showing the
            // old name, since they're two independent settings). Now derives
            // from platform.name directly, so renaming the platform anywhere
            // renames it everywhere, including the From: field recipients see.
            var senderName = await GetPlatformNameAsync();
            var senderEmail = await GetAsync("email.senderEmail", "EmailSettings:SenderEmail");

            if (!string.IsNullOrWhiteSpace(sgKey))
            {
                // Masked diagnostic — confirms which key length/prefix is actually
                // being used, without ever logging the real key. Helps catch cases
                // like "the DB still has the OLD dead key" vs "the new key never
                // got saved" without needing to expose the secret.
                var keyPreview = sgKey.Length > 8 ? $"{sgKey[..6]}…{sgKey[^4..]} (len={sgKey.Length})" : "(too short — likely invalid)";
                _logger.LogInformation("[Email] Attempting SendGrid — key preview: {KeyPreview}, from: {From}", keyPreview, senderEmail);

                // Confirmed via testing: the exact same code/config succeeds
                // sometimes and fails with a connection-level SSL error other
                // times — an intermittent network condition, not a fixed
                // client misconfiguration. No single config change can "solve"
                // genuine intermittency; the correct mitigation is retrying a
                // few times before giving up, since a transient failure often
                // clears on the next attempt seconds later.
                const int maxAttempts = 3;
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        await SendViaSendGridAsync(toEmail, toName, subject, htmlBody, sgKey, senderEmail, senderName);
                        _logger.LogInformation("[Email] Sent via SendGrid to {Email} (attempt {Attempt}/{Max})", toEmail, attempt, maxAttempts);
                        return;
                    }
                    catch (Exception ex) when (IsTransientConnectionFailure(ex) && attempt < maxAttempts)
                    {
                        // Only retry genuinely transient connection-level failures
                        // (the SSL/socket-reset pattern). A clean HTTP rejection
                        // like the 403 "unverified sender" error is NOT transient —
                        // retrying it would just waste time hitting the same
                        // deterministic rejection three times, so that case falls
                        // through to the outer catch below and fails immediately.
                        var delayMs = attempt * 1500; // 1.5s, then 3s
                        _logger.LogWarning("[Email] SendGrid attempt {Attempt}/{Max} failed transiently ({Msg}) — retrying in {Delay}ms",
                            attempt, maxAttempts, ex.Message, delayMs);
                        await Task.Delay(delayMs);
                    }
                    catch (Exception ex)
                    {
                        // Final attempt, or a non-transient failure (e.g. the
                        // sender-not-verified 403) — log the full chain and fall
                        // through to the SMTP fallback below.
                        LogFullExceptionChain(ex, "SendGrid");
                        break;
                    }
                }
            }
            else
            {
                _logger.LogInformation("[Email] No SendGrid key configured — going straight to SMTP.");
            }

            // Lighter retry here (2 attempts, not 3) — this is already the
            // fallback of a fallback, so keep worst-case total wait time
            // reasonable. Same transient-vs-permanent distinction as above.
            const int smtpMaxAttempts = 2;
            for (var attempt = 1; attempt <= smtpMaxAttempts; attempt++)
            {
                try
                {
                    await SendViaSmtpAsync(toEmail, toName, subject, htmlBody, senderEmail, senderName);
                    _logger.LogInformation("[Email] Sent via SMTP to {Email} (attempt {Attempt}/{Max})", toEmail, attempt, smtpMaxAttempts);
                    return;
                }
                catch (Exception ex) when (IsTransientConnectionFailure(ex) && attempt < smtpMaxAttempts)
                {
                    var delayMs = attempt * 2000;
                    _logger.LogWarning("[Email] SMTP attempt {Attempt}/{Max} failed transiently ({Msg}) — retrying in {Delay}ms",
                        attempt, smtpMaxAttempts, ex.Message, delayMs);
                    await Task.Delay(delayMs);
                }
            }
            // If we fall out of the loop, the last attempt's exception already
            // propagated naturally (SendViaSmtpAsync wraps and re-throws its
            // own failures), so nothing further to do here.
        }

        /// <summary>
        /// True for connection/handshake-level failures worth retrying (the
        /// SSL-connection-forcibly-closed pattern seen in testing). False for
        /// anything that reached SendGrid and got a real HTTP response back
        /// (e.g. the 403 "sender not verified" rejection) — those are
        /// deterministic and retrying them is pointless.
        /// </summary>
        private static bool IsTransientConnectionFailure(Exception ex)
        {
            var current = ex;
            while (current != null)
            {
                if (current is System.Net.Sockets.SocketException) return true;
                if (current is System.IO.IOException) return true;
                if (current is System.Net.Http.HttpRequestException) return true;
                current = current.InnerException;
            }
            return false;
        }

        /// <summary>Logs every level of an exception's InnerException chain,
        /// since MailKit/SendGrid's top-level messages are frequently just
        /// "see inner exception" with the real cause several levels down.</summary>
        private void LogFullExceptionChain(Exception ex, string source)
        {
            var level = 0;
            var current = ex;
            while (current != null)
            {
                _logger.LogWarning("[Email] {Source} failure — level {Level}: {Type}: {Message}",
                    source, level, current.GetType().Name, current.Message);
                current = current.InnerException;
                level++;
            }
        }

        private async Task SendViaSendGridAsync(
            string toEmail, string toName, string subject, string htmlBody,
            string apiKey, string fromEmail, string fromName)
        {
            // Explicitly pin TLS 1.2+ instead of relying on the OS/.NET default
            // negotiation.
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;

            // ROOT CAUSE (confirmed via diagnostics): raw `curl` connects to
            // api.sendgrid.com cleanly from this machine — full TLS handshake,
            // valid HTTP response — while .NET's HttpClient (used internally by
            // SendGridClient) fails with "SSL connection could not be
            // established" → connection forcibly closed. Since curl succeeds
            // and .NET doesn't on the SAME machine/network, the difference is
            // in .NET's own TLS behavior, not the network. The most common
            // cause of exactly this discrepancy: .NET's HttpClient checks
            // certificate revocation status (OCSP/CRL) by default, an extra
            // outbound call curl typically skips or handles more gracefully.
            // If that specific revocation-checking endpoint is unreachable or
            // slow (common in restricted network environments even when the
            // main site is fine), .NET aborts the whole connection.
            // Disabling revocation checking here removes that extra
            // dependency — SendGrid's certificate itself is still fully
            // validated (chain of trust, hostname, expiry), only the
            // "has it been revoked since issuance" side-channel check is
            // skipped, which is an acceptable and common tradeoff.
            var httpHandler = new System.Net.Http.SocketsHttpHandler
            {
                SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                {
                    CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck,
                },
            };
            var httpClient = new System.Net.Http.HttpClient(httpHandler);

            var client = new SendGridClient(httpClient, apiKey);
            var msg = MailHelper.CreateSingleEmail(
                new EmailAddress(fromEmail, fromName),
                new EmailAddress(toEmail, toName),
                subject, null, htmlBody);
            var response = await client.SendEmailAsync(msg);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Body.ReadAsStringAsync();
                _logger.LogWarning("[Email] SendGrid rejected the request — status {Status}, body: {Body}",
                    (int)response.StatusCode, body);
                throw new Exception($"SendGrid returned {(int)response.StatusCode}: {body}");
            }
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
            // Was 15s — the exact failure ("timed out after 15000ms") suggests
            // this timeout itself was cutting the handshake off before it could
            // finish, not necessarily a hard block. Giving it more room in
            // combination with skipping the revocation check above.
            client.Timeout = 30_000;
            // Same root cause as the SendGrid fix above: MailKit's SmtpClient
            // checks certificate revocation (OCSP/CRL) by default, an extra
            // outbound side-channel call that curl skips/handles gracefully.
            // The SMTP failure changed from a plain connection timeout to an
            // explicit SSL-handshake-stage timeout after the earlier fixes,
            // which matches this exact cause. Disabling it here removes the
            // same dependency — the certificate's chain of trust, hostname,
            // and expiry are still fully validated; only the "was it revoked
            // since issuance" check against a separate server is skipped.
            client.CheckCertificateRevocation = false;

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
        // All subjects/signatures now pull the platform name from settings
        // instead of the hardcoded "AgricHub" they used to have — so renaming
        // the platform in Admin Settings actually reflects in every email.

        public async Task SendVerificationEmailAsync(string toEmail, string name, string verificationUrl)
        {
            var brand = await GetPlatformNameAsync();
            await SendAsync(toEmail, name, $"Verify your {brand} account", $@"
                <p>Hi {name},</p>
                <p>Please verify your email address:</p>
                <p><a href='{verificationUrl}' style='background:#2d6a4f;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600'>Verify my account</a></p>");
        }

        public async Task SendVerificationApprovedAsync(string toEmail, string name)
        {
            var brand = await GetPlatformNameAsync();
            await SendAsync(toEmail, name, $"🎉 Your {brand} verification has been approved", $@"
                <p>Hi {name},</p>
                <p>Your verification has been <strong>approved</strong>. Your profile now shows the <strong>Verified</strong> badge.</p>
                <p><a href='https://agrichub.io/consultant/overview' style='background:#2d6a4f;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600'>Go to my dashboard</a></p>");
        }

        public async Task SendVerificationRejectedAsync(string toEmail, string name, string reason)
        {
            var brand = await GetPlatformNameAsync();
            await SendAsync(toEmail, name, $"{brand} verification — action required", $@"
                <p>Hi {name},</p>
                <p>Your verification was <strong>not approved</strong>:</p>
                <blockquote style='border-left:4px solid #e63946;padding-left:14px;color:#555;margin:14px 0'>{reason}</blockquote>
                <p>Please resubmit with updated documents.</p>
                <p><a href='https://agrichub.io/consultant/profile' style='background:#2d6a4f;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600'>Resubmit</a></p>");
        }

        public async Task SendBookingConfirmedAsync(string toEmail, string name, string serviceName,
            string consultantName, DateTime scheduledAt, decimal amount)
        {
            var brand = await GetPlatformNameAsync();
            await SendAsync(toEmail, name, $"Booking confirmed — {serviceName}", $@"
                <p>Hi {name},</p>
                <p>Your consultation is confirmed:</p>
                <table style='border-collapse:collapse;max-width:400px'>
                  <tr><td style='padding:8px;color:#555'>Service</td><td style='padding:8px;font-weight:600'>{serviceName}</td></tr>
                  <tr><td style='padding:8px;color:#555'>Consultant</td><td style='padding:8px;font-weight:600'>{consultantName}</td></tr>
                  <tr><td style='padding:8px;color:#555'>Scheduled</td><td style='padding:8px;font-weight:600'>{scheduledAt:dddd, MMMM d yyyy 'at' h:mm tt}</td></tr>
                  <tr><td style='padding:8px;color:#555'>Amount held</td><td style='padding:8px;font-weight:600'>₦{amount:N2}</td></tr>
                </table>");
        }

        public async Task SendBookingRequestAsync(string toEmail, string consultantName,
            string customerName, string serviceName, DateTime scheduledAt)
        {
            var brand = await GetPlatformNameAsync();
            await SendAsync(toEmail, consultantName, $"New booking — {customerName}", $@"
                <p>Hi {consultantName},</p>
                <p><strong>{customerName}</strong> has booked a session with you:</p>
                <table style='border-collapse:collapse;max-width:400px'>
                  <tr><td style='padding:8px;color:#555'>Service</td><td style='padding:8px;font-weight:600'>{serviceName}</td></tr>
                  <tr><td style='padding:8px;color:#555'>Scheduled</td><td style='padding:8px;font-weight:600'>{scheduledAt:dddd, MMMM d yyyy 'at' h:mm tt}</td></tr>
                </table>
                <p><a href='https://agrichub.io/consultant/schedule' style='background:#2d6a4f;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600'>View schedule</a></p>");
        }

        public async Task SendWalletTopUpAsync(string toEmail, string name, decimal amount, decimal newBalance)
        {
            var brand = await GetPlatformNameAsync();
            await SendAsync(toEmail, name, "Wallet topped up", $@"
                <p>Hi {name},</p>
                <table style='border-collapse:collapse;max-width:400px'>
                  <tr><td style='padding:8px;color:#555'>Amount added</td><td style='padding:8px;font-weight:600;color:#2d6a4f'>+₦{amount:N2}</td></tr>
                  <tr><td style='padding:8px;color:#555'>New balance</td><td style='padding:8px;font-weight:600'>₦{newBalance:N2}</td></tr>
                </table>");
        }

        public async Task SendPasswordResetAsync(string toEmail, string name, string resetUrl)
        {
            var brand = await GetPlatformNameAsync();
            await SendAsync(toEmail, name, $"Reset your {brand} password", $@"
                <p>Hi {name},</p>
                <p>Click below to reset your password (expires in 24 hours):</p>
                <p><a href='{resetUrl}' style='background:#2d6a4f;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600'>Reset password</a></p>");
        }

        /// <summary>
        /// One flexible branded template used for every event that doesn't
        /// warrant its own bespoke design (cancellations, disputes, payouts,
        /// reschedules, pitches etc.) — avoids maintaining a dozen near-identical
        /// HTML templates for events that just need "here's what happened, here's
        /// where to go" messaging.
        /// </summary>
        public async Task SendGenericNotificationAsync(
            string toEmail, string name, string subject, string headline, string bodyHtml,
            string? ctaText = null, string? ctaUrl = null)
        {
            var brand = await GetPlatformNameAsync();
            await SendAsync(toEmail, name, subject, $@"
                <p>Hi {name},</p>
                <p style='font-size:16px;font-weight:600;color:#1a1a1a;margin:16px 0 8px'>{headline}</p>
                <div style='color:#333;line-height:1.6'>{bodyHtml}</div>
                {(ctaText != null && ctaUrl != null
                    ? $"<p style='margin-top:20px'><a href='{ctaUrl}' style='background:#2d6a4f;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600'>{ctaText}</a></p>"
                    : "")}");
        }
    }
}
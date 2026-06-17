// AgricHub.DAL/Seeders/PlatformSettingsSeeder.cs
// Seeds default settings on first run. Run from Program.cs.

using AgricHub.DAL.Context;
using AgricHub.DAL.Entities.Models;

namespace AgricHub.DAL.Seeders
{
    public static class PlatformSettingsSeeder
    {
        public static async Task SeedAsync(AgricHubDbContext db)
        {
            if (db.PlatformSettings.Any()) return;  // already seeded

            var settings = new List<PlatformSetting>
            {
                // ── General ───────────────────────────────────────────────────
                new() { Key="platform.name",        Value="AgricHub",                    Category="general", Label="Platform name",      InputType="text",   SortOrder=1 },
                new() { Key="platform.tagline",     Value="Connect with verified agricultural consultants", Category="general", Label="Tagline", InputType="text", SortOrder=2 },
                new() { Key="platform.url",         Value="https://agrichub.io",         Category="general", Label="Platform URL",       InputType="text",   SortOrder=3 },
                new() { Key="platform.supportEmail",Value="",                            Category="general", Label="Support email",      InputType="text",   SortOrder=4 },
                new() { Key="platform.logoUrl",     Value="",                            Category="general", Label="Logo URL",           InputType="text",   SortOrder=5 },

                // ── Email ─────────────────────────────────────────────────────
                new() { Key="email.provider",       Value="smtp",                        Category="email",   Label="Email provider",    InputType="select", Group="provider",   SortOrder=1 },
                new() { Key="email.senderName",     Value="AgricHub",                    Category="email",   Label="Sender name",       InputType="text",   Group="sender",     SortOrder=2 },
                new() { Key="email.senderEmail",    Value="",                            Category="email",   Label="Sender email",      InputType="text",   Group="sender",     SortOrder=3 },
                new() { Key="email.sendgridKey",    Value="",                            Category="email",   Label="SendGrid API key",  InputType="password",IsSecret=true, Group="sendgrid", SortOrder=4 },
                new() { Key="email.smtpHost",       Value="smtp.hostinger.com",          Category="email",   Label="SMTP host",         InputType="text",   Group="smtp",       SortOrder=5 },
                new() { Key="email.smtpPort",       Value="465",                         Category="email",   Label="SMTP port",         InputType="number", Group="smtp",       SortOrder=6 },
                new() { Key="email.smtpUser",       Value="",                            Category="email",   Label="SMTP username",     InputType="text",   Group="smtp",       SortOrder=7 },
                new() { Key="email.smtpPassword",   Value="",                            Category="email",   Label="SMTP password",     InputType="password",IsSecret=true, Group="smtp", SortOrder=8 },

                // ── Financials ────────────────────────────────────────────────
                new() { Key="finance.platformFeePercent",  Value="10",  Category="financials", Label="Platform fee (%)",            InputType="number", SortOrder=1 },
                new() { Key="finance.minimumPayout",       Value="5000",Category="financials", Label="Minimum payout (₦)",          InputType="number", SortOrder=2 },
                new() { Key="finance.payoutSchedule",      Value="manual",Category="financials",Label="Payout schedule",           InputType="select", SortOrder=3 },
                new() { Key="finance.currency",            Value="NGN", Category="financials", Label="Currency",                    InputType="text",   SortOrder=4 },
                new() { Key="finance.currencySymbol",      Value="₦",   Category="financials", Label="Currency symbol",             InputType="text",   SortOrder=5 },

                // ── Booking ───────────────────────────────────────────────────
                new() { Key="booking.maxAdvanceDays",      Value="60",  Category="booking", Label="Max advance booking (days)",     InputType="number", SortOrder=1 },
                new() { Key="booking.cancellationHours",   Value="24",  Category="booking", Label="Cancellation window (hours)",    InputType="number", SortOrder=2 },
                new() { Key="booking.noShowPenaltyPercent",Value="0",   Category="booking", Label="No-show penalty (%)",            InputType="number", SortOrder=3 },
                new() { Key="booking.autoConfirm",         Value="true",Category="booking", Label="Auto-confirm bookings",          InputType="toggle", SortOrder=4 },
                new() { Key="booking.requiresVerification",Value="true",Category="booking", Label="Require verified consultant",    InputType="toggle", SortOrder=5 },

                // ── Integrations ──────────────────────────────────────────────
                new() { Key="integrations.sendbirdAppId",   Value="",  Category="integrations", Label="Sendbird App ID",         InputType="text",    IsSecret=false,  Group="sendbird", SortOrder=1 },
                new() { Key="integrations.sendbirdApiToken",Value="",  Category="integrations", Label="Sendbird API Token",      InputType="password",IsSecret=true,   Group="sendbird", SortOrder=2 },
                new() { Key="integrations.paystackPublic",  Value="",  Category="integrations", Label="Paystack public key",     InputType="text",    IsSecret=false,  Group="paystack", SortOrder=3 },
                new() { Key="integrations.paystackSecret",  Value="",  Category="integrations", Label="Paystack secret key",     InputType="password",IsSecret=true,   Group="paystack", SortOrder=4 },
                new() { Key="integrations.cloudinaryCloud", Value="",  Category="integrations", Label="Cloudinary cloud name",   InputType="text",    IsSecret=false,  Group="cloudinary",SortOrder=5 },
                new() { Key="integrations.cloudinaryKey",   Value="",  Category="integrations", Label="Cloudinary API key",      InputType="text",    IsSecret=false,  Group="cloudinary",SortOrder=6 },
                new() { Key="integrations.cloudinarySecret",Value="",  Category="integrations", Label="Cloudinary API secret",   InputType="password",IsSecret=true,   Group="cloudinary",SortOrder=7 },
                new() { Key="integrations.googleClientId",  Value="",  Category="integrations", Label="Google OAuth Client ID",  InputType="text",    IsSecret=false,  Group="google",   SortOrder=8 },

                // ── Features ──────────────────────────────────────────────────
                new() { Key="features.googleAuth",          Value="true", Category="features", Label="Enable Google login",         InputType="toggle", SortOrder=1 },
                new() { Key="features.cloudStorage",        Value="false",Category="features", Label="Use Cloudinary storage",      InputType="toggle", SortOrder=2 },
                new() { Key="features.emailNotifications",  Value="true", Category="features", Label="Email notifications",         InputType="toggle", SortOrder=3 },
                new() { Key="features.inAppNotifications",  Value="true", Category="features", Label="In-app notifications",        InputType="toggle", SortOrder=4 },
                new() { Key="features.maintenanceMode",     Value="false",Category="features", Label="Maintenance mode",            InputType="toggle", SortOrder=5 },
                new() { Key="features.publicRegistration",  Value="true", Category="features", Label="Allow public registration",   InputType="toggle", SortOrder=6 },
            };

            db.PlatformSettings.AddRange(settings);
            await db.SaveChangesAsync();
        }
    }
}
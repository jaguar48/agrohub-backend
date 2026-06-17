// AgricHub.API/Extension/ServiceExtension.cs

using AgricHub.BLL.Helpers;
using AgricHub.BLL.Implementations;
using AgricHub.BLL.Implementations.AdminService;
using AgricHub.BLL.Implementations.AgrichubServices;
using AgricHub.BLL.Implementations.BusinessServices;
using AgricHub.BLL.Implementations.ChatServices;
using AgricHub.BLL.Implementations.PaystackService;
using AgricHub.BLL.Implementations.ReviewServices;
using AgricHub.BLL.Implementations.UserServices;
using AgricHub.BLL.Implementations.UserServices.UserServices;
using AgricHub.BLL.Implementations.WalletService;
using AgricHub.BLL.Interfaces;
using AgricHub.BLL.Interfaces.ChatServices;
using AgricHub.BLL.Interfaces.IAdminService;
using AgricHub.BLL.Interfaces.IAgrichub_Services;
using AgricHub.BLL.Interfaces.IBusinessServices;
using AgricHub.BLL.Interfaces.IChatServices;
using AgricHub.BLL.Interfaces.IPaystackService;
using AgricHub.BLL.Interfaces.IRatingServices;
using AgricHub.BLL.Interfaces.IUserServices;
using AgricHub.BLL.Interfaces.IWalletService;
using AgricHub.Contracts;
using AgricHub.DAL;
using AgricHub.DAL.Context;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace AgricHub.API.Extension
{
    public static class ServiceExtension
    {
        public static void ConfigureCors(this IServiceCollection services) =>
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", builder =>
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader());
            });

        public static void ConfigureEmail(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<EmailConfiguration>(options =>
                configuration.GetSection("EmailSettings").Bind(options));
            services.AddScoped<EmailConfiguration>();
        }

        public static void ConfigureIISIntegration(this IServiceCollection services) =>
            services.Configure<IISOptions>(options => { });

        public static void ConfigureSqlContext(this IServiceCollection services, IConfiguration configuration) =>
            services.AddDbContext<AgricHubDbContext>(opts =>
                opts.UseSqlServer(configuration.GetConnectionString("sqlConnection")));

        public static void ConfigureIdentity(this IServiceCollection services)
        {
            services.AddIdentity<ApplicationUser, IdentityRole>(o =>
            {
                o.Password.RequireDigit           = true;
                o.Password.RequireLowercase       = false;
                o.Password.RequireUppercase       = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredLength         = 10;
                o.User.RequireUniqueEmail         = true;
            })
            .AddEntityFrameworkStores<AgricHubDbContext>()
            .AddDefaultTokenProviders();
        }

        public static void ConfigureJWT(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtSettings = configuration.GetSection("JwtSettings");
            var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]);

            services.AddAuthentication(opt =>
            {
                opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                opt.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = jwtSettings["validIssuer"],
                    ValidAudience            = jwtSettings["validAudience"],
                    IssuerSigningKey         = new SymmetricSecurityKey(secretKey)
                };
            });
        }

        public static void ConfigureServices(this IServiceCollection services, IConfiguration configuration)
        {
            // ── Auth & User ────────────────────────────────────────────────────
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserServices, UserService>();
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IConsultantService, ConsultantService>();

            // ── Profile ────────────────────────────────────────────────────────
            services.AddScoped<ICustomerProfileService, CustomerProfileService>();
            services.AddScoped<IConsultantProfileService, ConsultantProfileService>();

            // ── Business & Services ────────────────────────────────────────────
            services.AddScoped<IBusinessForService, BusinessForService>();
            services.AddScoped<IConsultationService, ConsultationService>();
            services.AddScoped<IBusiness_ConsultServices, BusinessConsultService>();

            // ── Chat ───────────────────────────────────────────────────────────
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<ISendbirdService, SendbirdService>();

            // ── Video (Daily.co) ───────────────────────────────────────────────
            services.AddHttpClient();
            services.AddScoped<IDailyService, DailyService>();

            // ── Reviews ────────────────────────────────────────────────────────
            services.AddScoped<IReviewService, ReviewService>();

            // ── Wallet ─────────────────────────────────────────────────────────
            services.AddScoped<IWalletService, WalletService>();

            // ── Payments ───────────────────────────────────────────────────────
            services.AddHttpClient<IPaystackService, PaystackService>();

            // ── Admin ──────────────────────────────────────────────────────────
            services.AddScoped<IAdminService, AdminService>();
            services.AddScoped<IAdminFinancialsService, AdminFinancialsService>();

            // ── Verification ───────────────────────────────────────────────────
            services.AddScoped<IConsultantVerificationService, ConsultantVerificationService>();

            // ── Email (SendGrid primary → SMTP fallback) ───────────────────────
            services.AddScoped<IEmailService, EmailService>();

            // ── Storage (Cloudinary if configured → local fallback) ────────────
            var cloudName = configuration["Cloudinary:CloudName"];
            if (!string.IsNullOrEmpty(cloudName) && cloudName != "your-cloud-name")
                services.AddScoped<IStorageService, CloudinaryStorageService>();
            else
                services.AddScoped<IStorageService, LocalStorageService>();

            // ── Platform Settings (cached key-value config) ────────────────────
            services.AddMemoryCache();
            services.AddScoped<IPlatformSettingsService, PlatformSettingsService>();

            // ── Generic Repository ─────────────────────────────────────────────
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        }
    }
}
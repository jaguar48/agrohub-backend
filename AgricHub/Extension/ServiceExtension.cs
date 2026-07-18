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
                opts.UseSqlServer(configuration.GetConnectionString("sqlConnection"), sql =>
                    // Global default instead of SingleQuery — the app has many
                    // queries with 2+ collection .Include()s (consultant profile,
                    // dispute lists, consultation lists all Include Customer +
                    // Consultant together, sometimes alongside Service/Package
                    // too). SingleQuery joins everything into one giant result
                    // set, which multiplies rows for every extra collection
                    // (cartesian explosion) and gets slower as more Includes are
                    // added. SplitQuery issues one clean query per collection
                    // instead — this is what actually explains consultants/1004
                    // taking 766ms: that query loads Customer + Consultant
                    // collections together under the old SingleQuery default.
                    sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

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

                // SignalR sends the JWT as ?access_token= on the WebSocket handshake
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
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
            services.AddScoped<IOfferPostService, OfferPostService>();  // Fiverr-style buyer-request pitches

            // ── Sendbird — TYPED HttpClient (replaces AddScoped<ISendbirdService>)
            // Using IHttpClientFactory prevents socket exhaustion caused by
            // new HttpClient() per request (which was causing the 100-second hangs
            // on GetNotificationHistoryAsync under concurrent load).
            // ── SignalR replaces Sendbird (same interface, zero caller changes) ──
            // Revert to Sendbird: comment the next line, uncomment the AddHttpClient block.
            services.AddScoped<ISendbirdService, SignalRChatService>();
            services.AddSignalR();

            /* SENDBIRD (kept for 1-line revert)
            services.AddHttpClient<ISendbirdService, SendbirdService>(client =>
            {
                // Short global timeout — individual calls can be cancelled sooner
                // via CancellationToken (e.g. 8s in NotificationsController).
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));   // recycle connections every 5min
            */

            // ── Video (Daily.co) ───────────────────────────────────────────────
            // Was `services.AddHttpClient();` — a bare, UNNAMED registration.
            // DailyService.cs asks IHttpClientFactory for a client specifically
            // named "daily" (httpClientFactory.CreateClient("daily")), which was
            // never actually configured anywhere — only described in a comment
            // inside DailyService.cs itself, telling someone to add this exact
            // registration. Nobody did, so every call got a plain HttpClient with
            // no BaseAddress, throwing "must be an absolute URI or BaseAddress
            // must be set" on every relative-path call like GetAsync($"rooms/...").
            services.AddHttpClient("daily", client =>
            {
                client.BaseAddress = new Uri("https://api.daily.co/v1/");
                client.Timeout = TimeSpan.FromSeconds(15);
            });
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

            // ── Storage — routed live by features.cloudStorage, not fixed at
            // startup (see StorageServiceRouter for why). Both concrete
            // implementations register under their own type so the router can
            // resolve either at call time; IStorageService itself now always
            // points at the router. ──
            services.AddScoped<CloudinaryStorageService>();
            services.AddScoped<LocalStorageService>();
            services.AddScoped<IStorageService, StorageServiceRouter>();

            // ── Platform Settings (cached key-value config) ────────────────────
            services.AddMemoryCache();
            services.AddScoped<IPlatformSettingsService, PlatformSettingsService>();

            // ── Generic Repository ─────────────────────────────────────────────
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        }
    }
}
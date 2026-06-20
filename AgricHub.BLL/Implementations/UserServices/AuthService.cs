using AgricHub.BLL.Helpers;
using AgricHub.BLL.Interfaces;
using AgricHub.BLL.Interfaces.IUserServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Response;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Web;

namespace AgricHub.BLL.Implementations.UserServices
{
    public sealed class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private ApplicationUser? _user;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly IRepository<Consultant> _consultantRepo;
        private readonly IRepository<Customer> _customerRepo;
        private readonly IRepository<Wallet> _walletRepo;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            IEmailService emailService)
        {
            _userManager    = userManager;
            _unitOfWork     = unitOfWork;
            _emailService   = emailService;
            _configuration  = configuration;
            _consultantRepo = _unitOfWork.GetRepository<Consultant>();
            _customerRepo   = _unitOfWork.GetRepository<Customer>();
            _walletRepo     = _unitOfWork.GetRepository<Wallet>();
        }

        public async Task<bool> SendVerificationEmail(string email, string verificationToken)
        {
            var apiKey = "";
            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("");
            var to = new EmailAddress("");
            var subject = "Account Verification";
            var verificationUrl = $"{_configuration["AppBaseUrl"]}/marketplace/authentication/verify?email={HttpUtility.UrlEncode(email)}&verificationToken={verificationToken}";
            var msg = MailHelper.CreateSingleEmail(from, to, subject,
                $"Please click the following link to verify your account: {verificationUrl}",
                $"<p>Please click the following link to verify your account: <a href='{verificationUrl}'>{verificationUrl}</a></p>");
            var response = await client.SendEmailAsync(msg);
            return response.IsSuccessStatusCode;
        }

        public async Task<ApplicationUser> VerifyUser(string email, string verificationToken)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null && !user.EmailConfirmed && user.VerificationToken == verificationToken)
            {
                user.EmailConfirmed  = true;
                user.VerificationToken = null;
                await _userManager.UpdateAsync(user);
                return user;
            }
            return null;
        }

        public async Task<bool> SendPasswordResetEmail(string email, string resetToken)
        {
            try
            {
                var resetUrl = $"{_configuration["AppBaseUrl"]}/marketplace/authentication/reset-password?email={HttpUtility.UrlEncode(email)}&token={resetToken}";
                await _emailService.SendPasswordResetAsync(email, email, resetUrl);
                return true;
            }
            catch { return false; }
        }

        public async Task<bool> ResetPassword(string email, string token, string newPassword)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return false;
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            return result.Succeeded;
        }

        public async Task<ServiceResponse<string>> ValidateUser(UserAuthenticationResponse response)
        {
            _user = await _userManager.FindByNameAsync(response.UserName);
            var result = _user != null && await _userManager.CheckPasswordAsync(_user, response.Password);
            if (!result)
                return new ServiceResponse<string> { Success = false, Message = "Login failed. Wrong username or password." };

            var roles = await _userManager.GetRolesAsync(_user);
            var role = roles.FirstOrDefault() ?? "Customer";
            return new ServiceResponse<string> { Success = true, Message = "Login successful.", Role = role };
        }

        public async Task<string> CreateToken()
        {
            var signingCredentials = GetSigningCredentials();
            var claims = await GetClaims();
            var tokenOptions = GenerateTokenOptions(signingCredentials, claims);
            return new JwtSecurityTokenHandler().WriteToken(tokenOptions);
        }

        private SigningCredentials GetSigningCredentials()
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]);
            return new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
        }

        private async Task<List<Claim>> GetClaims()
        {
            var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub,        _user.Id.ToString()),
        new Claim(ClaimTypes.Name,                    _user.UserName),
        new Claim(ClaimTypes.Email,                   _user.Email ?? ""),
        new Claim(JwtRegisteredClaimNames.Jti,        Guid.NewGuid().ToString()),
        new Claim(ClaimTypes.NameIdentifier,          _user.Id.ToString()),
    };
            var roles = await _userManager.GetRolesAsync(_user);
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));
            return claims;
        }

        private JwtSecurityToken GenerateTokenOptions(SigningCredentials signingCredentials, List<Claim> claims)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            return new JwtSecurityToken(
                issuer: jwtSettings["validIssuer"],
                audience: jwtSettings["validAudience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(Convert.ToDouble(jwtSettings["expires"])),
                signingCredentials: signingCredentials
            );
        }

        public async Task<AuthenticationResponse> GoogleAuth(string credential, string? role = null)
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings()
            {
                Audience = new List<string>() { _configuration["Authentication:Google:ClientId"] }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(credential, settings);
            if (payload == null)
                throw new InvalidOperationException("Invalid Google authentication.");

            // ── Existing user — log straight in ──────────────────────────
            var user = await _userManager.FindByEmailAsync(payload.Email);
            if (user != null)
            {
                _user = user;
                var token = await GenerateToken();
                var roles = await _userManager.GetRolesAsync(user);
                var userType = roles.FirstOrDefault() ?? "Customer";
                return new AuthenticationResponse
                {
                    JwtToken   = token,
                    UserType   = userType,
                    FullName   = $"{user.FirstName} {user.LastName}",
                    TwoFactor  = false,
                    IsExisting = true
                };
            }

            // ── New user — ask for role if not provided ───────────────────
            if (string.IsNullOrWhiteSpace(role))
            {
                return new AuthenticationResponse
                {
                    NeedsRoleSelection = true,
                    FullName           = payload.Name,
                    IsExisting         = false
                };
            }

            // ── New user — create ApplicationUser ─────────────────────────
            var newUser = new ApplicationUser
            {
                Id                 = Guid.NewGuid().ToString(),
                Email              = payload.Email,
                UserName           = payload.Email,
                NormalizedEmail    = payload.Email.ToUpper(),
                NormalizedUserName = payload.Email.ToUpper(),
                FirstName          = payload.GivenName  ?? "",
                LastName           = payload.FamilyName ?? "",
                EmailConfirmed     = true,
                Address            = "",
                CountryId          = "",
                StateId            = "",
            };

            var result = await _userManager.CreateAsync(newUser);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

            var normalizedRole = role.Trim();
            await _userManager.AddToRoleAsync(newUser, normalizedRole);
            await _userManager.AddLoginAsync(newUser, new UserLoginInfo("GOOGLE", payload.Subject, "GOOGLE"));

            if (normalizedRole == "Consultant")
            {
                var consultant = new Consultant
                {
                    FirstName    = newUser.FirstName,
                    LastName     = newUser.LastName,
                    Email        = newUser.Email,
                    BusinessName = payload.Name ?? "",
                    UserId       = newUser.Id,
                    Address      = "",
                    CountryId    = "",
                    StateId      = "",
                };
                await _consultantRepo.AddAsync(consultant);
                await _unitOfWork.SaveChangesAsync(); // flush so consultant.Id is assigned

                await _walletRepo.AddAsync(new Wallet
                {
                    WalletNo     = WalletIdGenerator.GenerateWalletId(),
                    Balance      = 0,
                    IsActive     = true,
                    ConsultantId = consultant.Id,
                });
            }
            else // Customer
            {
                var customer = new Customer
                {
                    FirstName = newUser.FirstName,
                    LastName  = newUser.LastName,
                    Email     = newUser.Email,
                    UserId    = newUser.Id,
                    Address   = "",
                    CountryId = "",
                    StateId   = "",
                };
                await _customerRepo.AddAsync(customer);
                await _unitOfWork.SaveChangesAsync(); // flush so customer.Id is assigned

                await _walletRepo.AddAsync(new Wallet
                {
                    WalletNo   = WalletIdGenerator.GenerateWalletId(),
                    Balance    = 0,
                    IsActive   = true,
                    CustomerId = customer.Id,
                });
            }

            await _unitOfWork.SaveChangesAsync();

            _user = newUser;
            var jwtToken = await GenerateToken();

            return new AuthenticationResponse
            {
                JwtToken   = jwtToken,
                UserType   = normalizedRole,
                FullName   = $"{newUser.FirstName} {newUser.LastName}",
                TwoFactor  = false,
                IsExisting = false
            };
        }

        public async Task<JwtToken> GenerateToken()
        {
            var signingCredentials = GetSigningCredentials();
            var claims = await GetClaims();
            var tokenOptions = GenerateTokenOptions(signingCredentials, claims);
            return new JwtToken
            {
                Token   = new JwtSecurityTokenHandler().WriteToken(tokenOptions),
                Issued  = tokenOptions.ValidFrom,
                Expires = tokenOptions.ValidTo
            };
        }
    }
}
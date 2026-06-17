using AgricHub.BLL.Interfaces.IPaystackService;
using AgricHub.BLL.Interfaces.IUserServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Request;
using AgricHub.Shared.DTO_s.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace AgricHub.BLL.Implementations.UserServices
{
    public class ConsultantProfileService : IConsultantProfileService
    {
        private readonly IRepository<Consultant> _consultantRepo;
        private readonly IPaystackService _paystackService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ConsultantProfileService(
            IUnitOfWork unitOfWork,
            IPaystackService paystackService,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork          = unitOfWork;
            _paystackService     = paystackService;
            _userManager         = userManager;
            _httpContextAccessor = httpContextAccessor;
            _consultantRepo      = unitOfWork.GetRepository<Consultant>();
        }

        private string GetUserId()
        {
            var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("User is not authenticated.");
            return userId;
        }

        public async Task<ConsultantProfileResponse> GetMyProfileAsync()
        {
            var userId = GetUserId();
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new KeyNotFoundException("Consultant not found.");
            var user = await _userManager.FindByIdAsync(userId);

            return new ConsultantProfileResponse
            {
                Id                = consultant.Id,           // ← needed for reviews
                UserId            = consultant.UserId,
                FirstName         = consultant.FirstName,
                LastName          = consultant.LastName,
                Email             = consultant.Email,
                PhoneNumber       = consultant.PhoneNumber,
                BusinessName      = consultant.BusinessName,
                Address           = consultant.Address,
                Bio               = consultant.Bio,
                YearsOfExperience = consultant.YearsOfExperience,
                HourlyRate        = consultant.HourlyRate,
                CountryId         = consultant.CountryId,
                StateId           = consultant.StateId,
                AvatarUrl         = consultant.AvatarUrl,
                EmailConfirmed    = user?.EmailConfirmed ?? false,
                NoShowCount       = consultant.NoShowCount,
                BankName          = consultant.BankName,
                BankCode          = consultant.BankCode,
                AccountNumber     = consultant.AccountNumber,
                AccountName       = consultant.AccountName,
                HasBankDetails    = !string.IsNullOrEmpty(consultant.PaystackRecipientCode),
                CreatedAt         = consultant.CreatedAt,
                IsVerified        = consultant.IsVerified
            };
        }

        public async Task UpdateProfileAsync(UpdateConsultantProfileRequest request)
        {
            var userId = GetUserId();
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new KeyNotFoundException("Consultant not found.");

            consultant.FirstName         = request.FirstName;
            consultant.LastName          = request.LastName;
            consultant.PhoneNumber       = request.PhoneNumber;
            consultant.BusinessName      = request.BusinessName;
            consultant.Address           = request.Address;
            consultant.CountryId         = request.CountryId;
            consultant.StateId           = request.StateId;
            consultant.Bio               = request.Bio;               // ← was missing
            consultant.YearsOfExperience = request.YearsOfExperience; // ← was missing
            consultant.HourlyRate        = request.HourlyRate;        // ← was missing

            _consultantRepo.Update(consultant);

            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.FirstName   = request.FirstName;
                user.LastName    = request.LastName;
                user.PhoneNumber = request.PhoneNumber;
                await _userManager.UpdateAsync(user);
            }

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task UpdateBankDetailsAsync(UpdateBankDetailsRequest request)
        {
            var userId = GetUserId();
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new KeyNotFoundException("Consultant not found.");

            try
            {
                // For non-Paystack countries, bankCode may be empty — skip resolve/recipient creation
                if (!string.IsNullOrEmpty(request.BankCode))
                {
                    var accountDetails = await _paystackService.ResolveAccountNumberAsync(
                        request.AccountNumber, request.BankCode);
                    var recipientCode = await _paystackService.CreateTransferRecipientAsync(
                        request.AccountNumber, accountDetails.AccountName, request.BankCode);

                    consultant.AccountName          = accountDetails.AccountName;
                    consultant.PaystackRecipientCode = recipientCode;
                }

                consultant.BankName      = request.BankName;
                consultant.BankCode      = request.BankCode;
                consultant.AccountNumber = request.AccountNumber;

                _consultantRepo.Update(consultant);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update bank details: {ex.Message}");
            }
        }

        public async Task ChangePasswordAsync(ChangePasswordRequest request)
        {
            var userId = GetUserId();
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            var result = await _userManager.ChangePasswordAsync(
                user, request.CurrentPassword, request.NewPassword);

            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Password change failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        public async Task<List<BankInfo>> GetBanksAsync(string country = "")
            => await _paystackService.GetBanksAsync(country);

        public async Task<BankAccountDetails> VerifyBankAccountAsync(string accountNumber, string bankCode)
            => await _paystackService.ResolveAccountNumberAsync(accountNumber, bankCode);

        public async Task<string> UploadAvatarAsync(IFormFile file)
        {
            var userId = GetUserId();
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new KeyNotFoundException("Consultant not found.");

            if (file.Length > 5 * 1024 * 1024)
                throw new InvalidOperationException("File size must be less than 5MB");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                throw new InvalidOperationException("Only image files are allowed");

            var uploadsFolder = Path.Combine("wwwroot", "uploads", "avatars");
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{userId}_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            var avatarUrl = $"/uploads/avatars/{uniqueFileName}";
            consultant.AvatarUrl = avatarUrl;
            _consultantRepo.Update(consultant);
            await _unitOfWork.SaveChangesAsync();

            return avatarUrl;
        }
    }
}
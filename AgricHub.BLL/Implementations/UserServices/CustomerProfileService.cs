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
    public class CustomerProfileService : ICustomerProfileService
    {
        private readonly IRepository<Customer> _customerRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CustomerProfileService(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _customerRepo = unitOfWork.GetRepository<Customer>();
        }

        private string GetUserId()
        {
            var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("User is not authenticated.");
            return userId;
        }

        public async Task<CustomerProfileResponse> GetMyProfileAsync()
        {
            var userId = GetUserId();

            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new KeyNotFoundException("Customer not found.");

            var user = await _userManager.FindByIdAsync(userId);

            return new CustomerProfileResponse
            {
                UserId = customer.UserId,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber,
                Address = customer.Address,
                CountryId = customer.CountryId,
                StateId = customer.StateId,
                AvatarUrl = customer.AvatarUrl,
                EmailConfirmed = user?.EmailConfirmed ?? false,
                NoShowCount = customer.NoShowCount,
                CreatedAt = customer.CreatedAt
            };
        }

        public async Task UpdateProfileAsync(UpdateCustomerProfileRequest request)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new KeyNotFoundException("Customer not found.");

            customer.FirstName = request.FirstName;
            customer.LastName = request.LastName;
            customer.PhoneNumber = request.PhoneNumber;
            customer.Address = request.Address;
            customer.CountryId = request.CountryId;
            customer.StateId = request.StateId;

            _customerRepo.Update(customer);

            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.FirstName = request.FirstName;
                user.LastName = request.LastName;
                user.PhoneNumber = request.PhoneNumber;
                await _userManager.UpdateAsync(user);
            }

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task ChangePasswordAsync(ChangePasswordRequest request)
        {
            var userId = GetUserId();
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            var result = await _userManager.ChangePasswordAsync(
                user,
                request.CurrentPassword,
                request.NewPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Password change failed: {errors}");
            }
        }

        public async Task<string> UploadAvatarAsync(IFormFile file)
        {
            var userId = GetUserId();
            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new KeyNotFoundException("Customer not found.");

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
            {
                await file.CopyToAsync(stream);
            }

            var avatarUrl = $"/uploads/avatars/{uniqueFileName}";

            customer.AvatarUrl = avatarUrl;
            _customerRepo.Update(customer);
            await _unitOfWork.SaveChangesAsync();

            return avatarUrl;
        }
    }
}
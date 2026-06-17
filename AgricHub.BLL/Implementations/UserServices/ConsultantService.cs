// AgricHub.BLL/Implementations/UserServices/UserServices/ConsultantService.cs

using AgricHub.BLL.Helpers;
using AgricHub.BLL.Interfaces.IUserServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Request;
using AgricHub.Shared.DTO_s.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Claims;

namespace AgricHub.BLL.Implementations.UserServices.UserServices
{
    public sealed class ConsultantService : IConsultantService
    {
        private readonly IRepository<Consultant> _consultantRepo;
        private readonly IRepository<Wallet> _walletRepo;
        private readonly IRepository<Review> _reviewRepo;
        private readonly IRepository<Consultation> _consultationRepo;
        private readonly IRepository<Business> _businessRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserServices _userServices;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuthService _authService;

        public ConsultantService(
            IAuthService authService,
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            IUserServices userServices)
        {
            _unitOfWork       = unitOfWork;
            _userManager      = userManager;
            _authService      = authService;
            _userServices     = userServices;
            _consultantRepo   = _unitOfWork.GetRepository<Consultant>();
            _walletRepo       = _unitOfWork.GetRepository<Wallet>();
            _reviewRepo       = _unitOfWork.GetRepository<Review>();
            _consultationRepo = _unitOfWork.GetRepository<Consultation>();
            _businessRepo     = _unitOfWork.GetRepository<Business>();
        }

        public async Task<string> RegisterConsultant(ConsultantRegistrationRequest request)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var user = await _userServices.RegisterUser(new UserForRegistrationRequest
                {
                    FirstName = request.FirstName,
                    LastName  = request.LastName,
                    Email     = request.Email,
                    Password  = request.Password,
                    UserName  = request.UserName,
                    CountryId = request.CountryId,
                    StateId   = request.StateId,
                    Address   = request.Address
                });

                await _userManager.AddToRoleAsync(user, "Consultant");

                var consultant = new Consultant
                {
                    FirstName    = request.FirstName,
                    LastName     = request.LastName,
                    PhoneNumber  = request.PhoneNumber,
                    Email        = request.Email,
                    BusinessName = request.BusinessName,
                    CountryId    = request.CountryId,
                    StateId      = request.StateId,
                    Address      = request.Address,
                    UserId       = user.Id
                };

                await _consultantRepo.AddAsync(consultant);
                await CreateConsultantWalletAsync(consultant);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    message = "Registration successful! Please check your email for the verification link."
                });
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null) await _userManager.DeleteAsync(existingUser);
                return JsonConvert.SerializeObject(new { success = false, message = $"Registration failed: {ex.Message}" });
            }
        }

        public async Task<IEnumerable<PublicConsultantDto>> GetAllConsultantsAsync(
            string? search = null, string? countryId = null)
        {
            var consultants = await _consultantRepo.GetByAsync(predicate: c => c.IsVerified);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                consultants = consultants.Where(c =>
                    (c.FirstName + " " + c.LastName).ToLower().Contains(s) ||
                    (c.BusinessName ?? "").ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(countryId))
                consultants = consultants.Where(c => c.CountryId == countryId);

            var ids = consultants.Select(c => c.Id).ToList();

            // Reviews
            var reviews = await _reviewRepo.GetByAsync(r => ids.Contains(r.ConsultantId));
            var reviewMap = reviews
                .GroupBy(r => r.ConsultantId)
                .ToDictionary(g => g.Key, g => (Count: g.Count(), Avg: g.Average(r => (double)r.Rating)));

            // Completed consultations
            var completedConsultations = await _consultationRepo.GetByAsync(
                c => ids.Contains(c.ConsultantId) && c.Status == "Completed");
            var completedMap = completedConsultations
                .GroupBy(c => c.ConsultantId)
                .ToDictionary(g => g.Key, g => g.Count());

            // Businesses with Services → Packages + Category fully included
            var businesses = await _businessRepo.GetAllAsync(
                b => ids.Contains(b.ConsultantId),
                include: q => q
                    .Include(b => b.Services)
                        .ThenInclude(s => s.Packages)
                    .Include(b => b.Services)
                        .ThenInclude(s => s.Category)
            );

            // Group businesses by consultant
            var businessMap = businesses
                .GroupBy(b => b.ConsultantId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return consultants.Select(c =>
            {
                reviewMap.TryGetValue(c.Id, out var rv);
                completedMap.TryGetValue(c.Id, out var cc);
                businessMap.TryGetValue(c.Id, out var bizList);

                var businessDtos = (bizList ?? new()).Select(b => new PublicBusinessDto
                {
                    Id        = b.Id,
                    Name      = b.BusinessName,
                    ImagePath = b.ImagePath,
                    Services  = b.Services?.Select(s => new PublicServiceDto
                    {
                        Id           = s.Id,
                        ServiceName  = s.ServiceName,
                        Description  = s.Description,
                        Price        = s.Price,
                        CategoryName = s.Category?.Name,
                        ImagePath    = s.ImagePath,
                        MediaJson    = s.MediaJson,
                        Packages     = s.Packages?.Select(p => new PublicPackageDto
                        {
                            Id                  = p.Id,
                            PackageName         = p.PackageName,
                            Price               = p.Price,
                            DurationMinutes     = p.DurationMinutes,
                            Description         = p.Description,
                            IncludesOnsiteVisit = p.IncludesOnsiteVisit
                        }).ToList() ?? new()
                    }).ToList() ?? new()
                }).ToList();

                return new PublicConsultantDto
                {
                    Id                     = c.Id,
                    FirstName              = c.FirstName,
                    LastName               = c.LastName,
                    BusinessName           = c.BusinessName,
                    CountryId              = c.CountryId,
                    StateId                = c.StateId,
                    AvatarUrl              = c.AvatarUrl,
                    IsVerified             = c.IsVerified,
                    BusinessImagePath      = bizList?.FirstOrDefault()?.ImagePath,
                    ServiceCount           = bizList?.Sum(b => b.Services?.Count ?? 0) ?? 0,
                    AverageRating          = Math.Round(rv.Avg, 1),
                    TotalReviews           = rv.Count,
                    CompletedConsultations = cc,
                    YearsOfExperience      = c.YearsOfExperience,
                    HourlyRate             = c.HourlyRate,
                    Businesses             = businessDtos,   // ← nested data for card preview
                };
            }).OrderByDescending(c => c.AverageRating).ToList();
        }

        public async Task<PublicConsultantDetailDto> GetConsultantByIdAsync(int id)
        {
            var c = await _consultantRepo.GetSingleByAsync(predicate: c => c.Id == id)
                ?? throw new KeyNotFoundException($"Consultant {id} not found.");

            var businesses = await _businessRepo.GetAllAsync(
                b => b.ConsultantId == id,
                include: q => q
                    .Include(b => b.Services)
                        .ThenInclude(s => s.Packages)
                    .Include(b => b.Services)
                        .ThenInclude(s => s.Category)
            );

            var reviews = await _reviewRepo.GetByAsync(r => r.ConsultantId == id);
            var avgRating = reviews.Any() ? Math.Round(reviews.Average(r => (double)r.Rating), 1) : 0.0;

            var completed = await _consultationRepo.GetByAsync(
                con => con.ConsultantId == id && con.Status == "Completed");

            return new PublicConsultantDetailDto
            {
                Id                     = c.Id,
                FirstName              = c.FirstName,
                LastName               = c.LastName,
                BusinessName           = c.BusinessName,
                CountryId              = c.CountryId,
                StateId                = c.StateId,
                AvatarUrl              = c.AvatarUrl,
                UserId                 = c.UserId,
                Email                  = c.Email,
                PhoneNumber            = c.PhoneNumber,
                Bio                    = c.Bio,
                IsVerified             = c.IsVerified,
                AverageRating          = avgRating,
                TotalReviews           = reviews.Count(),
                CompletedConsultations = completed.Count(),
                YearsOfExperience      = c.YearsOfExperience,
                HourlyRate             = c.HourlyRate,
                Businesses = businesses.Select(b => new PublicBusinessDto
                {
                    Id        = b.Id,
                    Name      = b.BusinessName,
                    ImagePath = b.ImagePath,
                    Services  = b.Services?.Select(s => new PublicServiceDto
                    {
                        Id           = s.Id,
                        ServiceName  = s.ServiceName,
                        Description  = s.Description,
                        Price        = s.Price,
                        CategoryName = s.Category?.Name,
                        ImagePath    = s.ImagePath,
                        MediaJson    = s.MediaJson,
                        Packages     = s.Packages?.Select(p => new PublicPackageDto
                        {
                            Id                  = p.Id,
                            PackageName         = p.PackageName,
                            Price               = p.Price,
                            DurationMinutes     = p.DurationMinutes,
                            Description         = p.Description,
                            IncludesOnsiteVisit = p.IncludesOnsiteVisit
                        }).ToList() ?? new()
                    }).ToList() ?? new()
                }).ToList()
            };
        }

        private async Task CreateConsultantWalletAsync(Consultant consultant)
        {
            var wallet = new Wallet
            {
                WalletNo     = WalletIdGenerator.GenerateWalletId(),
                Balance      = 0,
                IsActive     = true,
                ConsultantId = consultant.Id,
            };
            await _walletRepo.AddAsync(wallet);
        }
    }
}
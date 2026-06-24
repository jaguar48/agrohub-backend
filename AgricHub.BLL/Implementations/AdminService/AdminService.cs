using AgricHub.BLL.Helpers;
using AgricHub.BLL.Interfaces.ChatServices;
// AgricHub.BLL/Implementations/AdminService/AdminService.cs

using AgricHub.BLL.Interfaces;
using AgricHub.BLL.Interfaces.IAdminService;
using AgricHub.BLL.Interfaces.IBusinessServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace AgricHub.BLL.Implementations.AdminService
{
    public class AdminService : IAdminService
    {
        private readonly IRepository<Consultant> _consultantRepo;
        private readonly IRepository<Customer> _customerRepo;
        private readonly IRepository<Wallet> _walletRepo;
        private readonly IRepository<Review> _reviewRepo;
        private readonly IRepository<Consultation> _consultationRepo;
        private readonly IRepository<Category> _categoryRepo;
        private readonly IRepository<Service> _serviceRepo;
        private readonly IRepository<BusinessVerification> _verifRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly ISendbirdService _sendbirdService;
        private readonly IConsultationService _consultationService;

        public AdminService(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            ISendbirdService sendbirdService,
            IConsultationService consultationService)
        {
            _unitOfWork          = unitOfWork;
            _userManager         = userManager;
            _emailService        = emailService;
            _sendbirdService     = sendbirdService;
            _consultationService = consultationService;
            _consultantRepo      = unitOfWork.GetRepository<Consultant>();
            _customerRepo        = unitOfWork.GetRepository<Customer>();
            _walletRepo          = unitOfWork.GetRepository<Wallet>();
            _reviewRepo          = unitOfWork.GetRepository<Review>();
            _consultationRepo    = unitOfWork.GetRepository<Consultation>();
            _categoryRepo        = unitOfWork.GetRepository<Category>();
            _serviceRepo         = unitOfWork.GetRepository<Service>();
            _verifRepo           = unitOfWork.GetRepository<BusinessVerification>();
        }

        // ── Overview ──────────────────────────────────────────────
        public async Task<AdminStatsDto> GetStatsAsync()
        {
            var totalUsers = _userManager.Users.Count();
            var verifiedConsultants = (int)await _consultantRepo.CountAsync(c => c.IsVerified);
            var pendingVerifs = (int)await _verifRepo.CountAsync(v => v.Status == "Pending");
            var completed30d = (int)await _consultationRepo.CountAsync(c =>
                c.Status == "Completed" &&
                c.CompletedAt != null &&
                c.CompletedAt >= DateTime.UtcNow.AddDays(-30));
            var totalReviews = (int)await _reviewRepo.CountAsync();

            var recentReviewEntities = await _reviewRepo.GetByAsync(
                predicate: null,
                orderBy: q => q.OrderByDescending(r => r.CreatedAt),
                skip: null, take: 5,
                include: q => q.Include(r => r.Customer).Include(r => r.Consultant)
            );
            var recentReviews = recentReviewEntities.Select(r => new RecentReviewSummaryDto(
                r.Id,
                r.Customer.FirstName + " " + r.Customer.LastName,
                r.Consultant.FirstName + " " + r.Consultant.LastName,
                r.Rating, r.Comment, r.CreatedAt
            )).ToList();

            var pendingVerifEntities = await _verifRepo.GetByAsync(
                predicate: v => v.Status == "Pending",
                orderBy: q => q.OrderBy(v => v.Id),
                skip: null, take: 5
            );
            var pendingVerifList = pendingVerifEntities.Select(v => new PendingVerifSummaryDto(
                v.Id, v.FirstName + " " + v.LastName, v.BusinessName, v.CountryId
            )).ToList();

            return new AdminStatsDto(
                totalUsers, verifiedConsultants, pendingVerifs,
                completed30d, totalReviews, recentReviews, pendingVerifList);
        }

        // ── Reviews ───────────────────────────────────────────────
        public async Task<IReadOnlyList<AdminReviewDto>> GetReviewsAsync(int? minRating = null)
        {
            var reviews = await _reviewRepo.GetByAsync(
                predicate: minRating.HasValue ? r => r.Rating <= minRating.Value : null,
                orderBy: q => q.OrderByDescending(r => r.CreatedAt),
                include: q => q.Include(r => r.Customer).Include(r => r.Consultant)
            );

            return reviews.Select(r => new AdminReviewDto(
                r.Id, r.ConsultationId,
                r.Customer.FirstName + " " + r.Customer.LastName,
                r.Consultant.FirstName + " " + r.Consultant.LastName,
                r.Rating, r.Comment, r.CreatedAt
            )).ToList();
        }

        public async Task DeleteReviewAsync(int reviewId)
        {
            var review = await _reviewRepo.GetByIdAsync(reviewId)
                ?? throw new KeyNotFoundException($"Review {reviewId} not found.");
            await _reviewRepo.DeleteAsync(review);
        }

        // ── Verifications ─────────────────────────────────────────
        public async Task<IReadOnlyList<VerificationDto>> GetVerificationsAsync(bool? verified = null)
        {
            var verifs = await _verifRepo.GetByAsync(
                predicate: verified.HasValue
                    ? (verified.Value ? v => v.Status == "Approved" : v => v.Status == "Pending")
                    : null,
                orderBy: q => q.OrderBy(v => v.Id)
            );

            return verifs.Select(v => new VerificationDto(
                v.Id, v.FirstName, v.LastName, v.BusinessName,
                v.Email, v.PhoneNumber, v.CountryId, v.StateId, v.IsVerified,
                v.Status, v.SubmittedAt.ToString("O"), v.RejectionNotes,
                v.DocumentPathsJson != null
                    ? JsonConvert.DeserializeObject<List<string>>(v.DocumentPathsJson) ?? new()
                    : new()
            )).ToList();
        }

        public async Task UpdateVerificationAsync(int verificationId, UpdateVerificationRequest req)
        {
            var verif = await _verifRepo.GetByIdAsync(verificationId)
                ?? throw new KeyNotFoundException($"Verification {verificationId} not found.");

            var fullName = $"{verif.FirstName} {verif.LastName}";

            if (req.Approve)
            {
                verif.Status     = "Approved";
                verif.IsVerified = true;

                // Flip IsVerified on the Consultant record
                if (verif.UserId != null)
                {
                    var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == verif.UserId);
                    if (consultant != null)
                    {
                        consultant.IsVerified = true;
                        await _consultantRepo.UpdateAsync(consultant);
                    }
                }

                // Send in-app notification
                try
                {
                    await _sendbirdService.SendNotificationAsync(verif.UserId,
                    "🎉 Your verification has been approved! Your Verified badge is now live.",
                    NotificationTypes.VerificationApproved);
                }
                catch { }

                // Send approval email
                if (!string.IsNullOrEmpty(verif.Email))
                {
                    try { await _emailService.SendVerificationApprovedAsync(verif.Email, fullName); }
                    catch { /* Don't fail if email fails */ }
                }
            }
            else
            {
                verif.Status         = "Rejected";
                verif.IsVerified     = false;
                verif.RejectionNotes = req.Notes;

                // Send in-app notification
                try
                {
                    await _sendbirdService.SendNotificationAsync(verif.UserId,
                    $"Your verification was not approved. Reason: {req.Notes ?? "See email for details."}",
                    NotificationTypes.VerificationRejected, new { reason = req.Notes });
                }
                catch { }

                // Send rejection email with reason
                if (!string.IsNullOrEmpty(verif.Email))
                {
                    try
                    {
                        await _emailService.SendVerificationRejectedAsync(
                            verif.Email, fullName, req.Notes ?? "No reason provided.");
                    }
                    catch { /* Don't fail if email fails */ }
                }
            }

            await _verifRepo.UpdateAsync(verif);
            await _unitOfWork.SaveChangesAsync();
        }

        // ── Users ─────────────────────────────────────────────────
        public async Task<AdminUserPagedResult> GetUsersAsync(
            string? role = null, string? search = null, int page = 1, int pageSize = 20, string? userId = null)
        {
            IList<ApplicationUser> roleUsers;
            if (!string.IsNullOrWhiteSpace(role) && role != "all")
                roleUsers = await _userManager.GetUsersInRoleAsync(role);
            else
                roleUsers = _userManager.Users.ToList();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                // Exact ID lookup — ignores role/search filters and returns just this one user (if any)
                roleUsers = roleUsers.Where(u => u.Id == userId).ToList();
            }
            else if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                roleUsers = roleUsers.Where(u =>
                    (u.FirstName + " " + u.LastName).ToLower().Contains(s) ||
                    (u.Email ?? "").ToLower().Contains(s) ||
                    (u.UserName ?? "").ToLower().Contains(s)).ToList();
            }

            var total = roleUsers.Count;
            var paged = roleUsers.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var dtos = new List<AdminUserDto>();
            foreach (var user in paged)
            {
                var roles = await _userManager.GetRolesAsync(user);

                decimal walletBalance = 0;
                int totalConsultations = 0;
                int activeConsultations = 0;
                int completedConsultations = 0;
                int noShowCount = 0;
                DateTime? createdAt = null;
                bool isSuspended = false;
                string? suspensionReason = null;

                if (roles.Contains("Customer"))
                {
                    var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == user.Id);
                    if (customer != null)
                    {
                        var wallet = await _walletRepo.GetSingleByAsync(w => w.CustomerId == customer.Id);
                        var consultations = await _consultationRepo.GetByAsync(c => c.CustomerId == customer.Id);

                        walletBalance          = wallet?.Balance ?? 0;
                        totalConsultations     = consultations.Count();
                        activeConsultations    = consultations.Count(c => c.Status is "Pending" or "Approved" or "InProgress");
                        completedConsultations = consultations.Count(c => c.Status == "Completed");
                        noShowCount            = customer.NoShowCount ?? 0;
                        createdAt              = customer.CreatedAt;
                        isSuspended             = customer.IsSuspended;
                        suspensionReason        = customer.SuspensionReason;
                    }
                }
                else if (roles.Contains("Consultant"))
                {
                    var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == user.Id);
                    if (consultant != null)
                    {
                        var wallet = await _walletRepo.GetSingleByAsync(w => w.ConsultantId == consultant.Id);
                        var consultations = await _consultationRepo.GetByAsync(c => c.ConsultantId == consultant.Id);

                        walletBalance          = wallet?.Balance ?? 0;
                        totalConsultations     = consultations.Count();
                        activeConsultations    = consultations.Count(c => c.Status is "Pending" or "Approved" or "InProgress");
                        completedConsultations = consultations.Count(c => c.Status == "Completed");
                        noShowCount            = consultant.NoShowCount ?? 0;
                        createdAt              = consultant.CreatedAt;
                        isSuspended             = consultant.IsSuspended;
                        suspensionReason        = consultant.SuspensionReason;
                    }
                }

                dtos.Add(new AdminUserDto(
                    user.Id, user.FirstName, user.LastName,
                    user.Email ?? "", user.CountryId, roles,
                    walletBalance, totalConsultations, activeConsultations, completedConsultations, noShowCount, createdAt,
                    isSuspended, suspensionReason));
            }

            var customers = (await _userManager.GetUsersInRoleAsync("Customer")).Count;
            var consultants = (await _userManager.GetUsersInRoleAsync("Consultant")).Count;
            var admins = (await _userManager.GetUsersInRoleAsync("Admin")).Count;

            return new AdminUserPagedResult(dtos, total, page, pageSize, customers, consultants, admins);
        }

        // ── Suspend / Reinstate ──────────────────────────────────────
        // Works for either role — looks up whichever record (Consultant or
        // Customer) is linked to this ApplicationUser.Id and flips the flag.

        public async Task SuspendUserAsync(string userId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("A reason is required to suspend an account.");

            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId);
            if (consultant != null)
            {
                consultant.IsSuspended      = true;
                consultant.SuspendedAt      = DateTime.UtcNow;
                consultant.SuspensionReason = reason.Trim();
                await _consultantRepo.UpdateAsync(consultant);
                await _unitOfWork.SaveChangesAsync();

                try
                {
                    await _sendbirdService.SendNotificationAsync(userId,
                        $"🚫 Your account has been suspended. Reason: {reason.Trim()} · Contact support if you believe this is a mistake.",
                        "account_suspended");
                }
                catch { }
                return;
            }

            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId);
            if (customer != null)
            {
                customer.IsSuspended      = true;
                customer.SuspendedAt      = DateTime.UtcNow;
                customer.SuspensionReason = reason.Trim();
                await _customerRepo.UpdateAsync(customer);
                await _unitOfWork.SaveChangesAsync();

                try
                {
                    await _sendbirdService.SendNotificationAsync(userId,
                        $"🚫 Your account has been suspended. Reason: {reason.Trim()} · Contact support if you believe this is a mistake.",
                        "account_suspended");
                }
                catch { }
                return;
            }

            throw new KeyNotFoundException($"No Consultant or Customer record found for user {userId}.");
        }

        public async Task ReinstateUserAsync(string userId)
        {
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId);
            if (consultant != null)
            {
                consultant.IsSuspended      = false;
                consultant.SuspendedAt      = null;
                consultant.SuspensionReason = null;
                await _consultantRepo.UpdateAsync(consultant);
                await _unitOfWork.SaveChangesAsync();

                try
                {
                    await _sendbirdService.SendNotificationAsync(userId,
                        "✅ Your account suspension has been lifted. You can now use AgricHub normally again.",
                        "account_reinstated");
                }
                catch { }
                return;
            }

            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId);
            if (customer != null)
            {
                customer.IsSuspended      = false;
                customer.SuspendedAt      = null;
                customer.SuspensionReason = null;
                await _customerRepo.UpdateAsync(customer);
                await _unitOfWork.SaveChangesAsync();

                try
                {
                    await _sendbirdService.SendNotificationAsync(userId,
                        "✅ Your account suspension has been lifted. You can now use AgricHub normally again.",
                        "account_reinstated");
                }
                catch { }
                return;
            }

            throw new KeyNotFoundException($"No Consultant or Customer record found for user {userId}.");
        }

        // ── Consultants ───────────────────────────────────────────
        public async Task<AdminConsultantPagedResult> GetConsultantsAsync(
            bool? verifiedOnly = null, string? search = null, int page = 1, int pageSize = 20)
        {
            var all = await _consultantRepo.GetByAsync(
                predicate: verifiedOnly.HasValue ? c => c.IsVerified == verifiedOnly.Value : null,
                include: q => q.Include(c => c.Consultations)
            );

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                all = all.Where(c =>
                    (c.FirstName + " " + c.LastName).ToLower().Contains(s) ||
                    (c.BusinessName ?? "").ToLower().Contains(s) ||
                    (c.CountryId ?? "").ToLower().Contains(s));
            }

            var list = all.ToList();
            var total = list.Count;
            var ids = list.Select(c => c.Id).ToList();

            var reviews = await _reviewRepo.GetByAsync(r => ids.Contains(r.ConsultantId));
            var reviewMap = reviews
                .GroupBy(r => r.ConsultantId)
                .ToDictionary(g => g.Key, g => (Count: g.Count(), Avg: g.Average(r => (double)r.Rating)));

            var paged = list
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c =>
                {
                    reviewMap.TryGetValue(c.Id, out var rv);
                    return new AdminConsultantDto(
                        c.Id, c.FirstName, c.LastName, c.BusinessName,
                        c.CountryId, c.StateId, c.IsVerified,
                        c.Consultations?.Count(x => x.Status == "Completed") ?? 0,
                        Math.Round(rv.Avg, 1), rv.Count);
                }).ToList();

            return new AdminConsultantPagedResult(paged, total, page, pageSize);
        }

        // ── Categories ────────────────────────────────────────────
        public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync()
        {
            var categories = await _categoryRepo.GetAllAsync();
            var services = await _serviceRepo.GetAllAsync();
            return categories.Select(cat => new CategoryDto(
                cat.Id, cat.Name, services.Count(s => s.CategoryId == cat.Id)
            )).OrderBy(c => c.Name).ToList();
        }

        public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest req)
        {
            var cat = new Category { Name = req.name };
            await _categoryRepo.AddAsync(cat);
            await _unitOfWork.SaveChangesAsync();
            return new CategoryDto(cat.Id, cat.Name, 0);
        }


        public async Task DeleteCategoryAsync(int categoryId)
        {
            var cat = await _categoryRepo.GetByIdAsync(categoryId)
                ?? throw new KeyNotFoundException($"Category {categoryId} not found.");
            await _categoryRepo.DeleteAsync(cat);
            await _unitOfWork.SaveChangesAsync();
        }

        // ── Disputes (consultation completion disputes) ────────────
        public async Task<IReadOnlyList<DisputeDto>> GetDisputesAsync(string? statusFilter = null)
        {
            var consultations = await _consultationService.GetDisputedConsultationsAsync(statusFilter);

            return consultations.Select(c => new DisputeDto(
                c.Id,
                c.CustomerName,
                c.ConsultantName,
                c.ServiceName ?? "",
                c.Price,
                c.DisputeReason ?? "",
                c.DisputeRaisedAt ?? DateTime.MinValue,
                c.CompletionSummary,
                c.CompletionFileUrl,
                c.DisputeStatus ?? "Open",
                c.Status,
                c.CustomerUserId,
                c.ConsultantUserId
            )).ToList();
        }

        public async Task ResolveDisputeAsync(Guid consultationId, Shared.DTO_s.ResolveDisputeRequest req)
            => await _consultationService.ResolveDisputeAsync(consultationId, req.Resolution, req.Notes);
    }
}
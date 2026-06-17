using AgricHub.Shared.DTO_s;
using AgricHub.Shared.DTO_s.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace AgricHub.BLL.Interfaces.IAdminService
{
    public interface IAdminService
    {
        // Overview
        Task<AdminStatsDto> GetStatsAsync();
        // Reviews (moderation)
        Task<IReadOnlyList<AdminReviewDto>> GetReviewsAsync(int? minRating = null);
        Task DeleteReviewAsync(int reviewId);
        // Verifications
        Task<IReadOnlyList<VerificationDto>> GetVerificationsAsync(bool? verified = null);
        Task UpdateVerificationAsync(int verificationId, UpdateVerificationRequest req);
        // Users
        Task<AdminUserPagedResult> GetUsersAsync(string? role = null, string? search = null, int page = 1, int pageSize = 20, string? userId = null);
        // Consultants
        Task<AdminConsultantPagedResult> GetConsultantsAsync(bool? verifiedOnly = null, string? search = null, int page = 1, int pageSize = 20);
        // Categories
        Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync();
        Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest req);
        Task DeleteCategoryAsync(int categoryId);
        // Disputes (consultation completion disputes — customer rejected a submission)
        Task<IReadOnlyList<DisputeDto>> GetDisputesAsync(string? statusFilter = null);
        Task ResolveDisputeAsync(Guid consultationId, Shared.DTO_s.ResolveDisputeRequest req);
    }
}
// AgricHub.BLL/Interfaces/IRatingServices/IReviewService.cs

using AgricHub.DAL.Entities;
using AgricHub.Shared.DTO_s.Request;
using AgricHub.Shared.DTO_s.Response;

namespace AgricHub.BLL.Interfaces.IRatingServices
{
    public interface IReviewService
    {
        Task<ReviewResponse> AddReviewAsync(CreateReviewRequest request);
        Task<IEnumerable<ReviewResponse>> GetReviewsForConsultantAsync(int consultantId);
        Task<double> GetAverageRatingForConsultantAsync(int consultantId);
        Task ReplyToReviewAsync(int reviewId, string reply);
    }
}
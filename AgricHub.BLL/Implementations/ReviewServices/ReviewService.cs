// AgricHub.BLL/Implementations/ReviewServices/ReviewService.cs

using AgricHub.BLL.Interfaces.IRatingServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Request;
using AgricHub.Shared.DTO_s.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgricHub.BLL.Implementations.ReviewServices
{
    public class ReviewService : IReviewService
    {
        private readonly IRepository<Review> _reviewRepo;
        private readonly IRepository<Consultation> _consultationRepo;
        private readonly IRepository<Customer> _customerRepo;
        private readonly IRepository<Consultant> _consultantRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ReviewService(IUnitOfWork unitOfWork, IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork          = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
            _reviewRepo          = _unitOfWork.GetRepository<Review>();
            _consultationRepo    = _unitOfWork.GetRepository<Consultation>();
            _customerRepo        = _unitOfWork.GetRepository<Customer>();
            _consultantRepo      = _unitOfWork.GetRepository<Consultant>();
        }

        public async Task<ReviewResponse> AddReviewAsync(CreateReviewRequest request)
        {
            var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("User not authenticated.");

            var customer = await _customerRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Customer not found.");

            var consultation = await _consultationRepo.GetSingleByAsync(
                c => c.Id == request.ConsultationId,
                include: q => q.Include(c => c.Service))
                ?? throw new KeyNotFoundException("Consultation not found.");

            if (consultation.CustomerId != customer.Id)
                throw new UnauthorizedAccessException("You can only review your own consultations.");

            if (consultation.Status != "Completed")
                throw new InvalidOperationException("Only completed consultations can be reviewed.");

            var existing = await _reviewRepo.GetSingleByAsync(r => r.ConsultationId == consultation.Id);
            if (existing != null)
                throw new InvalidOperationException("This consultation already has a review.");

            if (request.Rating < 1 || request.Rating > 5)
                throw new ArgumentException("Rating must be between 1 and 5.");

            var review = new Review
            {
                ConsultationId = consultation.Id,
                CustomerId     = consultation.CustomerId,
                ConsultantId   = consultation.ConsultantId,
                ServiceId      = consultation.ServiceId,
                Rating         = request.Rating,
                Comment        = request.Comment,
                CreatedAt      = DateTime.UtcNow
            };

            await _reviewRepo.AddAsync(review);
            await _unitOfWork.SaveChangesAsync();

            return new ReviewResponse
            {
                Id                = review.Id,
                ConsultationId    = review.ConsultationId.ToString(),
                CustomerId        = review.CustomerId,
                ConsultantId      = review.ConsultantId,
                ServiceId         = review.ServiceId,
                Rating            = review.Rating,
                Comment           = review.Comment,
                CreatedAt         = review.CreatedAt.ToString("O"),
                CustomerName      = $"{customer.FirstName} {customer.LastName}",
                ServiceName       = consultation.Service?.ServiceName,
                ConsultantReply   = null,
                ConsultantReplyAt = null
            };
        }

        public async Task<IEnumerable<ReviewResponse>> GetReviewsForConsultantAsync(int consultantId)
        {
            var reviews = await _reviewRepo.GetAllAsync(
                r => r.ConsultantId == consultantId,
                include: q => q.Include(r => r.Customer).Include(r => r.Service)
            );

            return reviews.Select(r => new ReviewResponse
            {
                Id                = r.Id,
                ConsultationId    = r.ConsultationId.ToString(),
                CustomerId        = r.CustomerId,
                ConsultantId      = r.ConsultantId,
                ServiceId         = r.ServiceId,
                Rating            = r.Rating,
                Comment           = r.Comment,
                CreatedAt         = r.CreatedAt.ToString("O"),
                CustomerName      = r.Customer != null
                    ? $"{r.Customer.FirstName} {r.Customer.LastName}"
                    : "Anonymous",
                ServiceName       = r.Service?.ServiceName,
                ConsultantReply   = r.ConsultantReply,
                ConsultantReplyAt = r.ConsultantReplyAt?.ToString("O")
            });
        }

        public async Task<double> GetAverageRatingForConsultantAsync(int consultantId)
        {
            var reviews = await _reviewRepo.GetAllAsync(r => r.ConsultantId == consultantId);
            return reviews.Any() ? Math.Round(reviews.Average(r => (double)r.Rating), 1) : 0.0;
        }

        public async Task ReplyToReviewAsync(int reviewId, string reply)
        {
            var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("User not authenticated.");

            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new UnauthorizedAccessException("Consultant not found.");

            var review = await _reviewRepo.GetSingleByAsync(r => r.Id == reviewId)
                ?? throw new KeyNotFoundException("Review not found.");

            if (review.ConsultantId != consultant.Id)
                throw new UnauthorizedAccessException("You can only reply to your own reviews.");

            if (string.IsNullOrWhiteSpace(reply))
                throw new ArgumentException("Reply cannot be empty.");

            review.ConsultantReply   = reply.Trim();
            review.ConsultantReplyAt = DateTime.UtcNow;
            _reviewRepo.Update(review);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
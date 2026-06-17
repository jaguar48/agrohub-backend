// AgricHub.BLL/Implementations/ConsultantVerificationService.cs

using AgricHub.BLL.Interfaces;
using AgricHub.BLL.Interfaces.IUserServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Security.Claims;

namespace AgricHub.BLL.Implementations
{
    public class ConsultantVerificationService : IConsultantVerificationService
    {
        private readonly IRepository<BusinessVerification> _verifRepo;
        private readonly IRepository<Consultant> _consultantRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHostEnvironment _env;

        public ConsultantVerificationService(
            IUnitOfWork unitOfWork,
            IHttpContextAccessor httpContextAccessor,
            IWebHostEnvironment env)
        {
            _unitOfWork          = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
            _env                 = env;
            _verifRepo           = unitOfWork.GetRepository<BusinessVerification>();
            _consultantRepo      = unitOfWork.GetRepository<Consultant>();
        }

        private string GetUserId() =>
            _httpContextAccessor.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User not authenticated.");

        public async Task<object> GetVerificationStatusAsync()
        {
            var userId = GetUserId();

            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new KeyNotFoundException("Consultant not found.");

            if (consultant.IsVerified)
                return new { status = "Approved" };

            var verifs = await _verifRepo.GetByAsync(v => v.UserId == userId);
            var latest = verifs.OrderByDescending(v => v.SubmittedAt).FirstOrDefault();

            if (latest is null)
                return new { status = "None" };

            return new
            {
                status = latest.Status,
                submittedAt = latest.SubmittedAt.ToString("O"),
                reviewNotes = latest.RejectionNotes   // ← matches Angular interface
            };
        }

        public async Task SubmitVerificationAsync(SubmitVerificationRequest req)
        {
            var userId = GetUserId();

            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new KeyNotFoundException("Consultant not found.");

            if (consultant.IsVerified)
                throw new InvalidOperationException("Your account is already verified.");

            var pending = await _verifRepo.GetSingleByAsync(
                v => v.UserId == userId && v.Status == "Pending");
            if (pending is not null)
                throw new InvalidOperationException("You already have a pending verification application.");

            var uploadDir = Path.Combine(
                _env.WebRootPath ?? "wwwroot", "verification-docs", consultant.Id.ToString());
            Directory.CreateDirectory(uploadDir);

            var savedPaths = new List<string>();
            savedPaths.Add(await SaveFile(req.BusinessReg, uploadDir, "business_reg", consultant.Id));
            savedPaths.Add(await SaveFile(req.Credentials, uploadDir, "credentials", consultant.Id));
            if (req.GovernmentId is not null)
                savedPaths.Add(await SaveFile(req.GovernmentId, uploadDir, "government_id", consultant.Id));

            var verif = new BusinessVerification
            {
                UserId            = userId,
                FirstName         = consultant.FirstName,
                LastName          = consultant.LastName,
                BusinessName      = consultant.BusinessName ?? "",
                Email             = consultant.Email,
                PhoneNumber       = consultant.PhoneNumber,
                CountryId         = consultant.CountryId,
                StateId           = consultant.StateId,
                Status            = "Pending",
                IsVerified        = false,
                SubmittedAt       = DateTime.UtcNow,
                DocumentPathsJson = JsonConvert.SerializeObject(savedPaths),
            };

            await _verifRepo.AddAsync(verif);
            await _unitOfWork.SaveChangesAsync();
        }

        private static async Task<string> SaveFile(
            IFormFile file, string dir, string label, int consultantId)
        {
            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{label}-{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(dir, fileName);
            await using var stream = File.Create(path);
            await file.CopyToAsync(stream);
            return $"/verification-docs/{consultantId}/{fileName}";
        }
    }
}

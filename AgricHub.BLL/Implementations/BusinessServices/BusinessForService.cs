// AgricHub.BLL/Implementations/AgrichubServices/BusinessForService.cs

using AgricHub.BLL.Interfaces;
using AgricHub.BLL.Interfaces.IAgrichub_Services;
using AgricHub.BLL.Interfaces.IUserServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Request;
using AgricHub.Shared.DTO_s.Response;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Claims;

namespace AgricHub.BLL.Implementations.AgrichubServices
{
    public class BusinessForService : IBusinessForService
    {
        private readonly IRepository<Service> _servicesRepo;
        private readonly IRepository<Business> _businessRepo;
        private readonly IRepository<Consultant> _consultantRepo;
        private readonly IRepository<ServicePackage> _servicePackageRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        private static readonly string[] ImageExts = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] VideoExts = { ".mp4", ".mov", ".avi", ".webm" };
        private static readonly string[] DocExts = { ".pdf" };

        public BusinessForService(
            IMapper mapper,
            IHttpContextAccessor httpContextAccessor,
            IAuthService authService,
            IUnitOfWork unitOfWork,
            IStorageService storageService)
        {
            _mapper              = mapper;
            _httpContextAccessor = httpContextAccessor;
            _unitOfWork          = unitOfWork;
            _storageService      = storageService;
            _businessRepo        = _unitOfWork.GetRepository<Business>();
            _consultantRepo      = _unitOfWork.GetRepository<Consultant>();
            _servicesRepo        = _unitOfWork.GetRepository<Service>();
            _servicePackageRepo  = _unitOfWork.GetRepository<ServicePackage>();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static bool IsAllowedMedia(string ext) =>
            ImageExts.Contains(ext) || VideoExts.Contains(ext) || DocExts.Contains(ext);

        /// <summary>Uploads a file to Cloudinary (or local fallback) and returns the public URL.</summary>
        private async Task<string> UploadFileAsync(IFormFile file, string folder = "agrichub/services")
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty.");

            if (file.Length > 50 * 1024 * 1024)
                throw new Exception("File size exceeds the 50 MB limit.");

            var ext = Path.GetExtension(file.FileName).ToLower();
            if (!IsAllowedMedia(ext))
                throw new Exception("Invalid file type. Allowed: JPG, PNG, WEBP, MP4, MOV, AVI, WEBM, PDF.");

            using var stream = file.OpenReadStream();
            return await _storageService.UploadAsync(stream, file.FileName, folder);
        }

        // ── AddService ─────────────────────────────────────────────────────────

        public async Task<string> AddServiceAsync(CreateServiceRequest serviceRequest)
        {
            if (!string.IsNullOrEmpty(serviceRequest.PackagesJson))
                serviceRequest.Packages = JsonConvert.DeserializeObject<List<ServicePackageRequest>>(serviceRequest.PackagesJson);

            if (serviceRequest.File == null || serviceRequest.File.Length == 0)
                throw new Exception("At least one image is required.");

            // Auth
            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? throw new Exception("User not found.");
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                             ?? throw new UnauthorizedAccessException("No consultant found for this user.");
            var business = await _businessRepo.GetSingleByAsync(
                                b => b.Id == serviceRequest.BusinessId && b.ConsultantId == consultant.Id)
                             ?? throw new UnauthorizedAccessException("Not authorised to add to this business.");
            var category = await _unitOfWork.GetRepository<Category>().GetByIdAsync(serviceRequest.CategoryId)
                             ?? throw new Exception("Invalid Category ID.");

            // Upload primary image → Cloudinary
            var primaryUrl = await UploadFileAsync(serviceRequest.File, "agrichub/services");

            // Upload additional media files (up to 2 more)
            var mediaUrls = new List<string>();
            if (serviceRequest.MediaFiles?.Any() == true)
            {
                foreach (var mf in serviceRequest.MediaFiles.Take(2))
                {
                    if (mf == null || mf.Length == 0) continue;
                    try { mediaUrls.Add(await UploadFileAsync(mf, "agrichub/services/media")); }
                    catch { /* skip invalid files */ }
                }
            }

            // Build service entity
            var service = _mapper.Map<Service>(serviceRequest);
            service.ImagePath   = primaryUrl;   // ← Cloudinary CDN URL
            service.MediaJson   = mediaUrls.Any() ? JsonConvert.SerializeObject(mediaUrls) : null;
            service.BusinessId  = business.Id;
            service.CategoryId  = category.Id;
            service.DateCreated = DateTime.UtcNow;

            // Packages
            if (serviceRequest.Packages?.Any() == true)
            {
                service.Packages = _mapper.Map<List<ServicePackage>>(serviceRequest.Packages);
                foreach (var pkg in service.Packages)
                    pkg.Description ??= "";
                foreach (var pkg in service.Packages)
                {
                    pkg.ServiceId = service.Id;
                    pkg.Service   = service;
                    pkg.CreatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                service.Packages.Add(new ServicePackage
                {
                    PackageName         = "Basic",
                    Price               = serviceRequest.Price,
                    DurationMinutes     = serviceRequest.DefaultDurationMinutes,
                    Description         = "Default package",
                    IncludesOnsiteVisit = false,
                    ServiceId           = service.Id,
                    CreatedAt           = DateTime.UtcNow
                });
            }

            await _servicesRepo.AddAsync(service);
            await _unitOfWork.SaveChangesAsync();

            return JsonConvert.SerializeObject(new { success = true, message = "Service created successfully." });
        }

        // ── UpdateService ──────────────────────────────────────────────────────

        public async Task<string> UpdateServiceAsync(int serviceId, CreateServiceRequest serviceRequest)
        {
            if (!string.IsNullOrEmpty(serviceRequest.PackagesJson))
                serviceRequest.Packages = JsonConvert.DeserializeObject<List<ServicePackageRequest>>(serviceRequest.PackagesJson);

            var service = await _servicesRepo.GetSingleByAsync(
                s => s.Id == serviceId,
                include: q => q.Include(s => s.Packages))
                ?? throw new Exception("Service not found.");

            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? throw new Exception("User not found.");
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                             ?? throw new UnauthorizedAccessException("Consultant not found.");
            var business = await _businessRepo.GetSingleByAsync(
                                b => b.Id == service.BusinessId && b.ConsultantId == consultant.Id)
                             ?? throw new UnauthorizedAccessException("Not authorised to update this service.");
            service.ServiceName = serviceRequest.ServiceName;
            service.Description = serviceRequest.Description;
            service.Price       = serviceRequest.Price;
            if (serviceRequest.CategoryId > 0)
            {
                var category = await _unitOfWork.GetRepository<Category>().GetByIdAsync(serviceRequest.CategoryId)
                                 ?? throw new Exception("Invalid Category ID.");
                service.CategoryId = category.Id;
            }

            // Replace primary image if a new one was provided
            if (serviceRequest.File?.Length > 0)
            {
                // Delete old image from Cloudinary
                if (!string.IsNullOrEmpty(service.ImagePath))
                    await _storageService.DeleteAsync(service.ImagePath);

                service.ImagePath = await UploadFileAsync(serviceRequest.File, "agrichub/services");
            }

            // Replace additional media if new ones provided
            if (serviceRequest.MediaFiles?.Any() == true)
            {
                var mediaUrls = new List<string>();
                foreach (var mf in serviceRequest.MediaFiles.Take(2))
                {
                    if (mf == null || mf.Length == 0) continue;
                    try { mediaUrls.Add(await UploadFileAsync(mf, "agrichub/services/media")); }
                    catch { /* skip */ }
                }
                service.MediaJson = mediaUrls.Any() ? JsonConvert.SerializeObject(mediaUrls) : null;
            }

            // Sync packages
            if (serviceRequest.Packages?.Any() == true)
            {
                var updatedIds = serviceRequest.Packages.Select(p => p.Id).Where(id => id > 0).ToList();
                foreach (var pkg in service.Packages.Where(p => !updatedIds.Contains(p.Id)).ToList())
                    _servicePackageRepo.Delete(pkg);

                foreach (var req in serviceRequest.Packages)
                {
                    var existing = service.Packages.FirstOrDefault(p => p.Id == req.Id);
                    if (existing != null)
                    {
                        existing.PackageName         = req.PackageName;
                        existing.Price               = req.Price;
                        existing.DurationMinutes     = req.DurationMinutes;
                        existing.Description         = req.Description ?? "";
                        existing.IncludesOnsiteVisit = req.IncludesOnsiteVisit;
                        existing.CreatedAt           = DateTime.UtcNow;
                        _servicePackageRepo.Update(existing);
                    }
                    else
                    {
                        service.Packages.Add(new ServicePackage
                        {
                            ServiceId           = service.Id,
                            PackageName         = req.PackageName,
                            Price               = req.Price,
                            DurationMinutes     = req.DurationMinutes,
                            Description         = req.Description ?? "",
                            IncludesOnsiteVisit = req.IncludesOnsiteVisit,
                            CreatedAt           = DateTime.UtcNow
                        });
                    }
                }
            }

            service.DateCreated = DateTime.UtcNow;
            _servicesRepo.Update(service);
            await _unitOfWork.SaveChangesAsync();

            return JsonConvert.SerializeObject(new { success = true, message = "Service updated successfully." });
        }

        // ── Read / Delete ──────────────────────────────────────────────────────

        public async Task<ViewServiceResponse> ViewServiceAsync(int serviceId)
        {
            var service = await _servicesRepo.GetSingleByAsync(
                s => s.Id == serviceId,
                include: q => q.Include(s => s.Business).Include(s => s.Category).Include(s => s.Packages))
                ?? throw new Exception("Service not found.");
            return _mapper.Map<ViewServiceResponse>(service);
        }

        public async Task<IEnumerable<ViewServiceResponse>> ViewAllServicesAsync()
        {
            var services = await _servicesRepo.GetAllAsync(
                include: q => q.Include(s => s.Business).Include(s => s.Category).Include(s => s.Packages));
            return _mapper.Map<IEnumerable<ViewServiceResponse>>(services);
        }

        public async Task<IEnumerable<ViewServiceResponse>> ViewOwnBusinessServicesAsync()
        {
            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? throw new UnauthorizedAccessException("User not found.");
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                             ?? throw new UnauthorizedAccessException("Consultant not found.");
            var business = await _businessRepo.GetSingleByAsync(b => b.ConsultantId == consultant.Id)
                             ?? throw new Exception("No business found for this user.");

            var services = await _servicesRepo.GetAllAsync(
                s => s.BusinessId == business.Id,
                include: q => q.Include(s => s.Business).Include(s => s.Category).Include(s => s.Packages));
            return _mapper.Map<IEnumerable<ViewServiceResponse>>(services);
        }

        public async Task<string> DeleteServiceAsync(int serviceId)
        {
            var service = await _servicesRepo.GetSingleByAsync(s => s.Id == serviceId)
                             ?? throw new Exception("Service not found.");
            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? throw new Exception("User not found.");
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                             ?? throw new UnauthorizedAccessException("Consultant not found.");
            var business = await _businessRepo.GetSingleByAsync(
                                b => b.Id == service.BusinessId && b.ConsultantId == consultant.Id)
                             ?? throw new UnauthorizedAccessException("Not authorised to delete this service.");

            // Delete files from Cloudinary
            if (!string.IsNullOrEmpty(service.ImagePath))
                await _storageService.DeleteAsync(service.ImagePath);

            if (!string.IsNullOrEmpty(service.MediaJson))
            {
                var paths = JsonConvert.DeserializeObject<List<string>>(service.MediaJson) ?? new();
                foreach (var path in paths)
                    await _storageService.DeleteAsync(path);
            }


            // Check consultations FIRST
            var consultationRepo = _unitOfWork.GetRepository<Consultation>();
            var hasConsultations = await consultationRepo.AnyAsync(
                c => c.ServiceId == serviceId &&
                     c.Status != "Completed" && c.Status != "Cancelled" &&
                     c.Status != "Rejected" && c.Status != "Missed");
            if (hasConsultations)
                throw new InvalidOperationException(
                    "Cannot delete a service that has active consultations...");

            // THEN delete files
            if (!string.IsNullOrEmpty(service.ImagePath))
                await _storageService.DeleteAsync(service.ImagePath);
            if (!string.IsNullOrEmpty(service.MediaJson))
            {
                var paths = JsonConvert.DeserializeObject<List<string>>(service.MediaJson) ?? new();
                foreach (var path in paths)
                    await _storageService.DeleteAsync(path);
            }

            _servicesRepo.Delete(service);
            await _unitOfWork.SaveChangesAsync();
            return JsonConvert.SerializeObject(new { success = true, message = "Service deleted successfully." });
        }
    }
}
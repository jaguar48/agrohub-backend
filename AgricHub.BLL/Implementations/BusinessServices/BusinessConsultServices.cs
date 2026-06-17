using AgricHub.BLL.Interfaces.IAgrichub_Services;
using AgricHub.BLL.Interfaces.IUserServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Request;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Security.Claims;

namespace AgricHub.BLL.Implementations.AgrichubServices
{
    public class BusinessConsultService : IBusiness_ConsultServices
    {
        private readonly IRepository<Consultant> _consultantRepo;
        private readonly IRepository<Category> _categoryRepo;
        private readonly IRepository<Business> _businessRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMapper _mapper;

        public BusinessConsultService(IMapper mapper, IHttpContextAccessor httpContextAccessor, IAuthService authService, IUnitOfWork unitOfWork)
        {
            _mapper = mapper;
            _httpContextAccessor = httpContextAccessor;
            _unitOfWork = unitOfWork;
            _consultantRepo = _unitOfWork.GetRepository<Consultant>();
            _businessRepo = _unitOfWork.GetRepository<Business>();
            _categoryRepo = _unitOfWork.GetRepository<Category>();
        }

        public async Task<Business> GetMyBusinessAsync()
        {
            var userId = _httpContextAccessor.HttpContext.User
                            .FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) throw new Exception("User not found.");

            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId);
            if (consultant == null) throw new Exception("Consultant not found.");

            return await _businessRepo.GetSingleByAsync(b => b.ConsultantId == consultant.Id);
        }

        public async Task<string> AddBusiness(CreateBusinessRequest businessRequest)
        {
            if (businessRequest.File == null || businessRequest.File.Length == 0)
                throw new Exception("Image file is required.");

            if (businessRequest.File.Length > 5 * 1024 * 1024)
                throw new Exception("File size exceeds the 5MB limit.");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var fileExtension = Path.GetExtension(businessRequest.File.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
                throw new Exception("Invalid file type. Only JPG, JPEG, and PNG are allowed.");

            var folderName = Path.Combine("Resources", "Images");
            var pathToSave = Path.Combine(Directory.GetCurrentDirectory(), folderName);
            if (!Directory.Exists(pathToSave))
                Directory.CreateDirectory(pathToSave);

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(businessRequest.File.FileName);
            var fullPath = Path.Combine(pathToSave, fileName);
            var dbPath = Path.Combine(folderName, fileName).Replace('\\', '/');

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await businessRequest.File.CopyToAsync(stream);
            }

            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) throw new Exception("User not found.");

            var consultant = await _consultantRepo.GetSingleByAsync(s => s.UserId == userId);
            if (consultant == null) throw new Exception("Consultant not found.");

            // Prevent duplicate business
            var existing = await _businessRepo.GetSingleByAsync(b => b.ConsultantId == consultant.Id);
            if (existing != null)
                throw new Exception("You already have a business. Please add services to your existing business.");

            var business = _mapper.Map<Business>(businessRequest);
            business.ImagePath = dbPath;
            business.ConsultantId = consultant.Id;
            business.DateCreated = DateTime.UtcNow;
            business.IsVerified   = true;   // ← add this

            await _businessRepo.AddAsync(business);
            await _unitOfWork.SaveChangesAsync();

            var result = new { success = true, message = "Business created successfully." };
            return JsonConvert.SerializeObject(result);
        }
        public async Task UpdateBusinessAsync(int id, UpdateBusinessRequest request)
        {
            var userId = _httpContextAccessor.HttpContext.User
                            .FindFirstValue(ClaimTypes.NameIdentifier);
            var consultant = await _consultantRepo.GetSingleByAsync(c => c.UserId == userId)
                ?? throw new Exception("Consultant not found.");

            var business = await _businessRepo.GetSingleByAsync(b => b.Id == id && b.ConsultantId == consultant.Id)
                ?? throw new Exception("Business not found.");

            business.BusinessName = request.BusinessName;
            business.Description  = request.Description;
            business.Address      = request.Address;

            if (request.File != null && request.File.Length > 0)
            {
                var folderName = Path.Combine("Resources", "Images");
                var pathToSave = Path.Combine(Directory.GetCurrentDirectory(), folderName);
                if (!Directory.Exists(pathToSave)) Directory.CreateDirectory(pathToSave);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(request.File.FileName);
                var fullPath = Path.Combine(pathToSave, fileName);
                using (var stream = new FileStream(fullPath, FileMode.Create))
                    await request.File.CopyToAsync(stream);

                business.ImagePath = Path.Combine(folderName, fileName).Replace('\\', '/');
            }

            _businessRepo.Update(business);
            await _unitOfWork.SaveChangesAsync();
        }
        public async Task<string> AddCategory(CreateCategoryRequest categoryRequest)
        {
            var category = _mapper.Map<Category>(categoryRequest);
            await _categoryRepo.AddAsync(category);
            await _unitOfWork.SaveChangesAsync();

            var result = new { success = true, message = "Category created successfully" };
            return JsonConvert.SerializeObject(result);
        }

        public async Task<IEnumerable<Category>> GetCategoriesAsync()
        {
            return await _categoryRepo.GetAllAsync();
        }
    }
}
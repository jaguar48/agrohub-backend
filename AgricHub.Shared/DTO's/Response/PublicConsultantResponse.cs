// AgricHub.Shared/DTO_s/Response/PublicConsultantResponse.cs
namespace AgricHub.Shared.DTO_s.Response
{
    // AgricHub.Shared/DTO_s/Response/PublicConsultantResponse.cs
   
        public class PublicConsultantDto
        {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string? BusinessName { get; set; }
            public string? CountryId { get; set; }
            public string? Bio { get; set; }
            public string? StateId { get; set; }
            public string? AvatarUrl { get; set; }
            public string? UserId { get; set; }
            public string? Email { get; set; }
            public string? PhoneNumber { get; set; }
            public string? BusinessImagePath { get; set; }
            public bool IsVerified { get; set; }
            public double AverageRating { get; set; }
            public int TotalReviews { get; set; }
            public int CompletedConsultations { get; set; }
            public int ServiceCount { get; set; }
            public int? YearsOfExperience { get; set; }
            public decimal? HourlyRate { get; set; }
            // Included on the browse list so cards can show specialty + service previews.
            // Was missing — caused Businesses to be silently dropped on the list endpoint.
            public List<PublicBusinessDto> Businesses { get; set; } = new();
        }

        // Detail adds nothing new now — Businesses already on base — kept for clarity
        public class PublicConsultantDetailDto : PublicConsultantDto { }

        public class PublicBusinessDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string? ImagePath { get; set; }
            public List<PublicServiceDto> Services { get; set; } = new();
        }

        public class PublicServiceDto
        {
            public int Id { get; set; }
            public string ServiceName { get; set; }
            public string? Description { get; set; }
            public decimal Price { get; set; }
            public string? CategoryName { get; set; }
            public string? ImagePath { get; set; }
            public string? MediaJson { get; set; }
            public List<PublicPackageDto> Packages { get; set; } = new();
        }

        public class PublicPackageDto
        {
            public int Id { get; set; }
            public string PackageName { get; set; }
            public decimal Price { get; set; }
            public int DurationMinutes { get; set; }
            public string? Description { get; set; }
            public bool IncludesOnsiteVisit { get; set; }
        }
    }
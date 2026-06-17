using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace AgricHub.Shared.DTO_s.Request
{
    public class CreateServiceRequest
    {
        public int BusinessId { get; set; }
        public int CategoryId { get; set; }
        public string ServiceName { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }

        public IFormFile? File { get; set; }
        public List<IFormFile>? MediaFiles { get; set; }  // ← additional media

        public string? PackagesJson { get; set; }

        [Range(1, 1440, ErrorMessage = "Duration must be between 1 and 1440 minutes (24 hours)")]
        public int DefaultDurationMinutes { get; set; } = 60;

        [NotMapped]
        [JsonIgnore]
        [BindNever]
        [ValidateNever]
        public List<ServicePackageRequest> Packages { get; set; }
    }

    public class ServicePackageRequest
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string PackageName { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }

        [Required]
        [Range(1, 1440, ErrorMessage = "Duration must be between 1 and 1440 minutes (24 hours)")]
        public int DurationMinutes { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        public bool IncludesOnsiteVisit { get; set; }
    }
}
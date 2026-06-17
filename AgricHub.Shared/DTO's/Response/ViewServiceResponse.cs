using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Response
{
    public class ViewServiceResponse
    {
        public int Id { get; set; }
        public string ServiceName { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string ImagePath { get; set; }

        public string? MediaJson { get; set; }   // ← ADD THIS
        public int BusinessId { get; set; }
        public int CategoryId { get; set; }       // ← ADD THIS
        public string BusinessName { get; set; }
        public DateTime DateCreated { get; set; }
        public List<ServicePackageResponse> Packages { get; set; } = new();
    }

    public class ServicePackageResponse
    {
        public int Id { get; set; }
        public string PackageName { get; set; }
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }  // ← ADD THIS
        public string Description { get; set; }
        public bool IncludesOnsiteVisit { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

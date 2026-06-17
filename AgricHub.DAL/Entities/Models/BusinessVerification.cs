using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.DAL.Entities.Models
{
 
    public class BusinessVerification
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string BusinessName { get; set; }
        public string Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? UserId { get; set; }
        public string? CountryId { get; set; }
        public string? StateId { get; set; }
        public string? Address { get; set; }

        // "Pending" | "Approved" | "Rejected"
        public string Status { get; set; } = "Pending";
        public bool IsVerified { get; set; } = false;
        public string? RejectionNotes { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        // Stored as JSON array: ["business_reg-xxx.pdf","credentials-xxx.pdf"]
        public string? DocumentPathsJson { get; set; }

        public ApplicationUser User { get; set; }
    }
}
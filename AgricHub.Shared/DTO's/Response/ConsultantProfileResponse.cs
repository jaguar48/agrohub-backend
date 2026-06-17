using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Response
{
    public class ConsultantProfileResponse
    {
        public int Id { get; set; }          // ← needed for reviews
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string BusinessName { get; set; }
        public string Address { get; set; }
        public string? Bio { get; set; }          // ← new
        public int? YearsOfExperience { get; set; }         // ← new
        public decimal? HourlyRate { get; set; }         // ← new
        public string? CountryId { get; set; }
        public string? CountryName { get; set; }
        public string? StateId { get; set; }
        public string? StateName { get; set; }
        public string? AvatarUrl { get; set; }
        public bool EmailConfirmed { get; set; }
        public int? NoShowCount { get; set; }
        public string? BankName { get; set; }
        public string? BankCode { get; set; }
        public string? AccountNumber { get; set; }
        public string? AccountName { get; set; }
        public bool HasBankDetails { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsVerified { get; set; }
    }
}

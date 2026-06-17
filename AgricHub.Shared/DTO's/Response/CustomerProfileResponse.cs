using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.Shared.DTO_s.Response
{
    public class CustomerProfileResponse
    {
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string? CountryId { get; set; }
        public string CountryName { get; set; }
        public string? StateId { get; set; }
        public string StateName { get; set; }
        public string AvatarUrl { get; set; }
        public bool EmailConfirmed { get; set; }
        public int? NoShowCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

